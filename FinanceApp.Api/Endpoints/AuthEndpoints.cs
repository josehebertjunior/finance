using FinanceApp.Api.Models;
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

        group.MapPost("/register", async ([FromBody] RegisterDto dto, UserManager<ApplicationUser> um) =>
        {
            var user = new ApplicationUser { UserName = dto.Email, Email = dto.Email, DisplayName = dto.DisplayName };
            var result = await um.CreateAsync(user, dto.Password);
            if (!result.Succeeded) return Results.BadRequest(result.Errors);
            return Results.Created($"/api/users/{user.Id}", new { user.Id, user.Email });
        });

        var loginEndpoint = group.MapPost("/login", async (LoginDto dto, UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm, AppIdentityDbContext idDb, IConfiguration cfg, HttpResponse response, HttpRequest request, ILoggerFactory lf) =>
        {
            var user = await um.FindByEmailAsync(dto.Email);
            if (user == null) return Results.Unauthorized();

            var pw = await um.CheckPasswordAsync(user, dto.Password);
            if (!pw)
            {
                // optionally increment access failed count for lockout
                await um.AccessFailedAsync(user);
                return Results.Unauthorized();
            }

            // reset access failed count on successful login
            await um.ResetAccessFailedCountAsync(user);

            var accessToken = GenerateJwt(user, cfg);
            var ip = request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            var refresh = GenerateRefreshToken(ip, user.Id);
            idDb.RefreshTokens!.Add(refresh);
            await idDb.SaveChangesAsync();
            // set HttpOnly refresh token cookie aligned with refresh expiry
            response.Cookies.Append("refreshToken", refresh.Token, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.None, Expires = refresh.Expires, Path = "/" });

            // set CSRF cookie (double-submit pattern) - accessible to JS
            var csrf = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            response.Cookies.Append("csrfToken", csrf, new CookieOptions { HttpOnly = false, Secure = true, SameSite = SameSiteMode.None, Expires = DateTime.UtcNow.AddMinutes(15), Path = "/" });

            lf.CreateLogger("AuthEndpoints").LogInformation("User {Email} logged in", dto.Email);
            return Results.Ok(new { accessToken, expiresIn = 900 });
        });

        group.MapPost("/refresh", async (HttpRequest request, AppIdentityDbContext idDb, UserManager<ApplicationUser> um, IConfiguration cfg, HttpResponse response, ILoggerFactory lf) =>
        {
            // CSRF double-submit protection
            if (!request.Cookies.TryGetValue("csrfToken", out var csrfCookie) || !request.Headers.TryGetValue("X-CSRF-Token", out var csrfHeader) || csrfCookie != csrfHeader)
            {
                return Results.Unauthorized();
            }

            if (!request.Cookies.TryGetValue("refreshToken", out var token)) return Results.Unauthorized();
            var dbToken = await idDb.RefreshTokens!.Include(r => r.User).FirstOrDefaultAsync(r => r.Token == token);
            if (dbToken == null || !dbToken.IsActive) return Results.Unauthorized();

            var user = dbToken.User!;

            // rotate
            var ip = request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            var newRefresh = GenerateRefreshToken(ip, user.Id);
            dbToken.Revoked = true;
            dbToken.RevokedAt = DateTime.UtcNow;
            dbToken.RevokedByIp = ip;
            dbToken.ReplacedByToken = newRefresh.Token;

            idDb.RefreshTokens!.Add(newRefresh);
            await idDb.SaveChangesAsync();

            // rotate cookies
            response.Cookies.Append("refreshToken", newRefresh.Token, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.None, Expires = newRefresh.Expires, Path = "/" });
            var csrf = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            response.Cookies.Append("csrfToken", csrf, new CookieOptions { HttpOnly = false, Secure = true, SameSite = SameSiteMode.None, Expires = DateTime.UtcNow.AddMinutes(15), Path = "/" });
            var accessToken = GenerateJwt(user, cfg);
            lf.CreateLogger("AuthEndpoints").LogInformation("Refresh token rotated for user {UserId}", user.Id);
            return Results.Ok(new { accessToken, expiresIn = 900 });
        });

        group.MapPost("/logout", async (HttpRequest req, AppIdentityDbContext idDb, HttpResponse res, ILoggerFactory lf) =>
        {
            // CSRF check
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

        // require named login rate limiter
        loginEndpoint.RequireRateLimiting("Login");
    }

    static string GenerateJwt(ApplicationUser user, IConfiguration cfg)
    {
        var key = cfg["Jwt:Key"] ?? "dev-key-please-change";
        var issuer = cfg["Jwt:Issuer"] ?? "finance-api";
        var audience = cfg["Jwt:Audience"] ?? "finance-client";

        var claims = new[] {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty)
        };

        var keyBytes = Encoding.UTF8.GetBytes(key);
        // Ensure key is at least 256 bits for HS256. If not, derive a SHA-256 of the value (development fallback).
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

    public record RegisterDto(string Email, string Password, string? DisplayName);
    public record LoginDto(string Email, string Password);
}
