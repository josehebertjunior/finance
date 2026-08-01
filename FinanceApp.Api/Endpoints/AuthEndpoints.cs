using FinanceApp.Api.Models;
using FinanceApp.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace FinanceApp.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", async ([FromBody] RegisterDto dto, UserManager<ApplicationUser> um, AppIdentityDbContext idDb) =>
        {
            if (string.IsNullOrWhiteSpace(dto.InviteCode))
                return Results.BadRequest(new { error = "É necessário um código de convite válido para registrar." });

            var invite = await idDb.InviteTokens!.Include(i => i.Tenant).FirstOrDefaultAsync(i => i.Token == dto.InviteCode && !i.Used && i.ExpiresAt > DateTime.UtcNow);
            if (invite == null)
                return Results.BadRequest(new { error = "Código de convite inválido ou expirado." });

            var existing = await um.FindByEmailAsync(dto.Email);
            if (existing != null)
                return Results.BadRequest(new { error = "Já existe uma conta com este email." });

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                DisplayName = dto.DisplayName,
                TenantId = invite.TenantId
            };
            var result = await um.CreateAsync(user, dto.Password);
            if (!result.Succeeded) return Results.BadRequest(result.Errors);

            invite.Used = true;
            invite.UsedAt = DateTime.UtcNow;
            invite.UsedById = user.Id;
            await idDb.SaveChangesAsync();

            return Results.Created($"/api/users/{user.Id}", new { user.Id, user.Email, user.TenantId });
        });

        group.MapPost("/forgot-password", async (ForgotPasswordDto dto, UserManager<ApplicationUser> um, AppIdentityDbContext idDb, IAppEmailSender emailSender) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
            {
                return Results.BadRequest(new { error = "Email é obrigatório." });
            }

            var user = await um.FindByEmailAsync(dto.Email);
            if (user != null)
            {
                var requestToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
                var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString("D6");
                var identityToken = await um.GeneratePasswordResetTokenAsync(user);

                var resetRequest = new PasswordResetRequest
                {
                    UserId = user.Id,
                    Email = user.Email ?? dto.Email,
                    RequestToken = requestToken,
                    ResetToken = identityToken,
                    Code = code,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                    Used = false,
                    CreatedAt = DateTime.UtcNow
                };

                idDb.PasswordResetRequests!.Add(resetRequest);
                await idDb.SaveChangesAsync();

                var resetLink = $"http://localhost:4200/login?resetToken={Uri.EscapeDataString(requestToken)}";
                await emailSender.SendPasswordResetAsync(user.Email!, code, resetLink);
            }

            return Results.Ok(new { message = "Se o email existir, você receberá instruções para redefinir a senha." });
        });

        group.MapPost("/reset-password", async (ResetPasswordDto dto, UserManager<ApplicationUser> um, AppIdentityDbContext idDb) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Token) || string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                return Results.BadRequest(new { error = "Token, código e nova senha são obrigatórios." });
            }

            var request = await idDb.PasswordResetRequests!.FirstOrDefaultAsync(r => r.RequestToken == dto.Token && !r.Used && r.ExpiresAt > DateTime.UtcNow);
            if (request == null || request.Code != dto.Code)
            {
                return Results.BadRequest(new { error = "Token ou código inválido." });
            }

            var user = await um.FindByIdAsync(request.UserId);
            if (user == null)
            {
                return Results.BadRequest(new { error = "Usuário não encontrado." });
            }

            var resetResult = await um.ResetPasswordAsync(user, request.ResetToken, dto.NewPassword);
            if (!resetResult.Succeeded)
            {
                return Results.BadRequest(resetResult.Errors);
            }

            request.Used = true;
            await idDb.SaveChangesAsync();

            return Results.Ok(new { message = "Senha redefinida com sucesso." });
        });

        var loginEndpoint = group.MapPost("/login", async (LoginDto dto, UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm, AppIdentityDbContext idDb, IConfiguration cfg, HttpResponse response, HttpRequest request, ILoggerFactory lf) =>
        {
            var user = await um.FindByEmailAsync(dto.Email);
            if (user == null) return Results.Unauthorized();

            var pw = await um.CheckPasswordAsync(user, dto.Password);
            if (!pw)
            {
                await um.AccessFailedAsync(user);
                return Results.Unauthorized();
            }

            await um.ResetAccessFailedCountAsync(user);

            var accessToken = await GenerateJwtAsync(user, cfg, um);
            var ip = request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            var refresh = GenerateRefreshToken(ip, user.Id);
            idDb.RefreshTokens!.Add(refresh);
            await idDb.SaveChangesAsync();
            response.Cookies.Append("refreshToken", refresh.Token, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.None, Expires = refresh.Expires, Path = "/" });

            var csrf = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            response.Cookies.Append("csrfToken", csrf, new CookieOptions { HttpOnly = false, Secure = true, SameSite = SameSiteMode.None, Expires = DateTime.UtcNow.AddMinutes(15), Path = "/" });

            lf.CreateLogger("AuthEndpoints").LogInformation("User {Email} logged in", dto.Email);
            return Results.Ok(new { accessToken, expiresIn = 900 });
        });

        group.MapPost("/refresh", async (HttpRequest request, AppIdentityDbContext idDb, UserManager<ApplicationUser> um, IConfiguration cfg, HttpResponse response, ILoggerFactory lf) =>
        {
            if (!request.Cookies.TryGetValue("csrfToken", out var csrfCookie) || !request.Headers.TryGetValue("X-CSRF-Token", out var csrfHeader) || csrfCookie != csrfHeader)
            {
                return Results.Unauthorized();
            }

            if (!request.Cookies.TryGetValue("refreshToken", out var token)) return Results.Unauthorized();
            var dbToken = await idDb.RefreshTokens!.Include(r => r.User).FirstOrDefaultAsync(r => r.Token == token);
            if (dbToken == null || !dbToken.IsActive) return Results.Unauthorized();

            var user = dbToken.User!;

            var ip = request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            var newRefresh = GenerateRefreshToken(ip, user.Id);
            dbToken.Revoked = true;
            dbToken.RevokedAt = DateTime.UtcNow;
            dbToken.RevokedByIp = ip;
            dbToken.ReplacedByToken = newRefresh.Token;

            idDb.RefreshTokens!.Add(newRefresh);
            await idDb.SaveChangesAsync();

            response.Cookies.Append("refreshToken", newRefresh.Token, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.None, Expires = newRefresh.Expires, Path = "/" });
            var csrf = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            response.Cookies.Append("csrfToken", csrf, new CookieOptions { HttpOnly = false, Secure = true, SameSite = SameSiteMode.None, Expires = DateTime.UtcNow.AddMinutes(15), Path = "/" });
            var accessToken = await GenerateJwtAsync(user, cfg, um);
            lf.CreateLogger("AuthEndpoints").LogInformation("Refresh token rotated for user {UserId}", user.Id);
            return Results.Ok(new { accessToken, expiresIn = 900 });
        });

        group.MapPost("/logout", async (HttpRequest req, AppIdentityDbContext idDb, HttpResponse res, ILoggerFactory lf) =>
        {
            if (!req.Cookies.TryGetValue("csrfToken", out var csrfCookie) || !req.Headers.TryGetValue("X-CSRF-Token", out var csrfHeader) || csrfCookie != csrfHeader)
            {
                return Results.Unauthorized();
            }

            if (req.Cookies.TryGetValue("refreshToken", out var token))
            {
                var rt = await idDb.RefreshTokens!.FirstOrDefaultAsync(r => r.Token == token);
                if (rt != null)
                {
                    rt.Revoked = true;
                    rt.RevokedAt = DateTime.UtcNow;
                    rt.RevokedByIp = req.HttpContext.Connection.RemoteIpAddress?.ToString();
                    await idDb.SaveChangesAsync();
                }
                res.Cookies.Delete("refreshToken", new CookieOptions { Path = "/" });
                res.Cookies.Delete("csrfToken", new CookieOptions { Path = "/" });
            }
            lf.CreateLogger("AuthEndpoints").LogInformation("Logout executed");
            return Results.Ok();
        });

        loginEndpoint.RequireRateLimiting("Login");
    }

    static async Task<string> GenerateJwtAsync(ApplicationUser user, IConfiguration cfg, UserManager<ApplicationUser> um)
    {
        var key = cfg["Jwt:Key"] ?? "dev-key-please-change";
        var issuer = cfg["Jwt:Issuer"] ?? "finance-api";
        var audience = cfg["Jwt:Audience"] ?? "finance-client";
        var roles = await um.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty)
        };

        if (!string.IsNullOrWhiteSpace(user.TenantId))
        {
            claims.Add(new Claim("tenant", user.TenantId));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var keyBytes = Encoding.UTF8.GetBytes(key);
        if (keyBytes.Length < 32)
        {
            using var sha = SHA256.Create();
            keyBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
        }
        var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, expires: DateTime.UtcNow.AddMinutes(15), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    static RefreshToken GenerateRefreshToken(string ip, string userId)
    {
        var random = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return new RefreshToken
        {
            Token = random,
            Expires = DateTime.UtcNow.AddDays(14),
            Created = DateTime.UtcNow,
            CreatedByIp = ip,
            UserId = userId,
            Revoked = false
        };
    }

    public record RegisterDto(string Email, string Password, string? DisplayName, string InviteCode);
    public record LoginDto(string Email, string Password);
    public record ForgotPasswordDto(string Email);
    public record ResetPasswordDto(string Token, string Code, string NewPassword);
}
