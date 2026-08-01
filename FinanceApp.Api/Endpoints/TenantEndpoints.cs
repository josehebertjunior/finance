using FinanceApp.Api.Models;
using FinanceApp.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FinanceApp.Api.Endpoints;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin");
        group.RequireAuthorization("AdminOnly");

        group.MapPost("/invites", async ([FromBody] CreateInviteDto dto, AppIdentityDbContext idDb, UserManager<ApplicationUser> userManager, IAppEmailSender emailSender, ClaimsPrincipal user) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.TenantName))
                return Results.BadRequest(new { error = "Email and tenant name are required." });

            var adminUser = await userManager.GetUserAsync(user);
            if (adminUser == null) return Results.Unauthorized();

            var existingInvite = await idDb.InviteTokens!.FirstOrDefaultAsync(i => i.Email == dto.Email && !i.Used && i.ExpiresAt > DateTime.UtcNow);
            if (existingInvite != null)
            {
                existingInvite.ExpiresAt = DateTime.UtcNow.AddHours(1);
                await idDb.SaveChangesAsync();
                await emailSender.SendInviteAsync(existingInvite.Email, existingInvite.Token);
                return Results.Ok(new { message = "Invite resent." });
            }

            var tenant = await idDb.Tenants!.FirstOrDefaultAsync(t => t.Name == dto.TenantName);
            if (tenant == null)
            {
                tenant = new Tenant { Name = dto.TenantName, CreatedById = adminUser.Id };
                idDb.Tenants.Add(tenant);
                await idDb.SaveChangesAsync();
            }

            var invite = new InviteToken
            {
                Email = dto.Email,
                Token = Guid.NewGuid().ToString("N"),
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                CreatedById = adminUser.Id,
                TenantId = tenant.Id,
                Used = false
            };
            idDb.InviteTokens!.Add(invite);
            await idDb.SaveChangesAsync();
            await emailSender.SendInviteAsync(invite.Email, invite.Token);
            return Results.Created($"/api/admin/invites/{invite.Id}", new { invite.Id, invite.Email, invite.ExpiresAt, invite.Token, tenant = tenant.Name });
        });

        group.MapGet("/invites", async (AppIdentityDbContext idDb) =>
        {
            var invites = await idDb.InviteTokens!.Include(i => i.Tenant).ToListAsync();
            return Results.Ok(invites.Select(i => new
            {
                i.Id,
                i.Email,
                i.Token,
                i.ExpiresAt,
                i.Used,
                i.UsedAt,
                TenantName = i.Tenant?.Name,
                i.CreatedById
            }));
        });

        group.MapGet("/tenants", async (AppIdentityDbContext idDb) =>
        {
            var tenants = await idDb.Tenants!.Include(t => t.Users).ToListAsync();
            return Results.Ok(tenants.Select(t => new { t.Id, t.Name, t.CreatedAt, UserCount = t.Users?.Count ?? 0 }));
        });

        group.MapGet("/users", async (AppIdentityDbContext idDb, UserManager<ApplicationUser> userManager) =>
        {
            var users = await idDb.Users.Include(u => u.Tenant).ToListAsync();
            var userDtos = await Task.WhenAll(users.Select(async u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.TenantId,
                TenantName = u.Tenant?.Name,
                Roles = await userManager.GetRolesAsync(u)
            }));
            return Results.Ok(userDtos);
        });

        group.MapPost("/users/{id}/assign-tenant", async (string id, [FromBody] AssignTenantDto dto, AppIdentityDbContext idDb, UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null) return Results.NotFound();
            var tenant = await idDb.Tenants!.FindAsync(dto.TenantId);
            if (tenant == null) return Results.BadRequest(new { error = "Tenant not found." });
            user.TenantId = tenant.Id;
            await userManager.UpdateAsync(user);
            return Results.Ok();
        });

        group.MapPost("/users/{id}/roles", async (string id, [FromBody] RoleUpdateDto dto, UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(dto.Role)) return Results.BadRequest(new { error = "Role is required." });
            if (!new[] { "Admin", "User" }.Contains(dto.Role)) return Results.BadRequest(new { error = "Role must be Admin or User." });
            if (await userManager.IsInRoleAsync(user, dto.Role)) return Results.Ok(new { message = "Role already assigned." });
            var result = await userManager.AddToRoleAsync(user, dto.Role);
            return result.Succeeded ? Results.Ok() : Results.BadRequest(result.Errors);
        });

        group.MapDelete("/users/{id}/roles/{role}", async (string id, string role, UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(role)) return Results.BadRequest(new { error = "Role is required." });
            if (!await userManager.IsInRoleAsync(user, role)) return Results.Ok(new { message = "Role not found on user." });
            var result = await userManager.RemoveFromRoleAsync(user, role);
            return result.Succeeded ? Results.Ok() : Results.BadRequest(result.Errors);
        });
    }

    public record CreateInviteDto(string Email, string TenantName);
    public record AssignTenantDto(string TenantId);
    public record RoleUpdateDto(string Role);
}
