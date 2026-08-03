using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Api.Models;

public class AppIdentityDbContext : IdentityDbContext<ApplicationUser>
{
    public AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options) : base(options)
    {
    }

    public DbSet<RefreshToken>? RefreshTokens { get; set; }
    public DbSet<Tenant>? Tenants { get; set; }
    public DbSet<TenantMembership>? TenantMemberships { get; set; }
    public DbSet<InviteToken>? InviteTokens { get; set; }
    public DbSet<PasswordResetRequest>? PasswordResetRequests { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>()
            .HasOne(u => u.Tenant)
            .WithMany(t => t.Users)
            .HasForeignKey(u => u.TenantId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Tenant>()
            .HasOne(t => t.CreatedBy)
            .WithMany(u => u.CreatedTenants)
            .HasForeignKey(t => t.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<TenantMembership>()
            .HasKey(membership => new { membership.UserId, membership.TenantId });

        builder.Entity<TenantMembership>()
            .HasOne(membership => membership.User)
            .WithMany(user => user.TenantMemberships)
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TenantMembership>()
            .HasOne(membership => membership.Tenant)
            .WithMany(tenant => tenant.Memberships)
            .HasForeignKey(membership => membership.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
