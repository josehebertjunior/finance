using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace FinanceApp.Api.Models;

public class ApplicationUser : IdentityUser
{
    // Exemplo: nome exibido no app
    public string? DisplayName { get; set; }

    public string? TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    [InverseProperty(nameof(Tenant.Users))]
    public Tenant? Tenant { get; set; }

    [InverseProperty(nameof(Tenant.CreatedBy))]
    public ICollection<Tenant> CreatedTenants { get; set; } = new List<Tenant>();

    public ICollection<TenantMembership> TenantMemberships { get; set; } = new List<TenantMembership>();
}
