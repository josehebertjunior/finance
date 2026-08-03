using FinanceApp.Api.Models;
using FinanceApp.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace FinanceApp.Api.Endpoints;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin");
        group.RequireAuthorization("AdminOnly");

        group.MapPost("/invites", async ([FromBody] CreateInviteDto dto, AppIdentityDbContext idDb, UserManager<ApplicationUser> userManager, IOptions<AppSettings> appSettings, ClaimsPrincipal user) =>
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
                return Results.Ok(CreateInviteResponse(existingInvite, existingInvite.Tenant?.Name, appSettings.Value.FrontendUrl));
            }

            var tenant = await idDb.Tenants!.FirstOrDefaultAsync(t => t.Name == dto.TenantName);
            if (tenant == null)
            {
                tenant = new Tenant { Name = dto.TenantName, CreatedById = adminUser.Id };
                idDb.Tenants!.Add(tenant);
                await idDb.SaveChangesAsync();
            }

            await EnsureMembershipAsync(idDb, adminUser.Id, tenant.Id);
            if (string.IsNullOrWhiteSpace(adminUser.TenantId))
            {
                adminUser.TenantId = tenant.Id;
                await userManager.UpdateAsync(adminUser);
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
            return Results.Created($"/api/admin/invites/{invite.Id}", CreateInviteResponse(invite, tenant.Name, appSettings.Value.FrontendUrl));
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
            var tenants = await idDb.Tenants!.Include(t => t.Memberships).ToListAsync();
            return Results.Ok(tenants.Select(t => new { t.Id, t.Name, t.CreatedAt, UserCount = t.Memberships.Count }));
        });

        group.MapGet("/users", async (AppIdentityDbContext idDb, UserManager<ApplicationUser> userManager) =>
        {
            var users = await idDb.Users
                .Include(u => u.Tenant)
                .Include(u => u.TenantMemberships)
                .ThenInclude(membership => membership.Tenant)
                .ToListAsync();
            var userDtos = new List<object>();
            foreach (var user in users)
            {
                var roles = await userManager.GetRolesAsync(user);
                userDtos.Add(new
                {
                    user.Id,
                    user.Email,
                    user.DisplayName,
                    user.TenantId,
                    TenantName = user.Tenant?.Name,
                    Groups = user.TenantMemberships
                        .OrderBy(membership => membership.Tenant!.Name)
                        .Select(membership => new { membership.TenantId, Name = membership.Tenant!.Name }),
                    Roles = roles
                });
            }
            return Results.Ok(userDtos);
        });

        group.MapPost("/users/{id}/assign-tenant", async (string id, [FromBody] AssignTenantDto dto, AppIdentityDbContext idDb, UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null) return Results.NotFound();
            var tenant = await idDb.Tenants!.FindAsync(dto.TenantId);
            if (tenant == null) return Results.BadRequest(new { error = "Tenant not found." });
            await EnsureMembershipAsync(idDb, user.Id, tenant.Id);
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

    private static object CreateInviteResponse(InviteToken invite, string? tenantName, string frontendUrl)
    {
        var inviteUrl = $"{frontendUrl.TrimEnd('/')}/login?invite={Uri.EscapeDataString(invite.Token)}";
        return new
        {
            invite.Id,
            invite.Email,
            invite.ExpiresAt,
            invite.Token,
            TenantName = tenantName,
            InviteUrl = inviteUrl,
            Message = "Link de convite gerado. Copie e envie ao convidado."
        };
    }

    private static async Task EnsureMembershipAsync(AppIdentityDbContext idDb, string userId, string tenantId)
    {
        var exists = await idDb.TenantMemberships!
            .AnyAsync(membership => membership.UserId == userId && membership.TenantId == tenantId);
        if (!exists)
        {
            idDb.TenantMemberships!.Add(new TenantMembership { UserId = userId, TenantId = tenantId });
            await idDb.SaveChangesAsync();
        }
    }

    public record CreateInviteDto(string Email, string TenantName);
    public record AssignTenantDto(string TenantId);
    public record RoleUpdateDto(string Role);
}
