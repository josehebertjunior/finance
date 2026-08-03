namespace FinanceApp.Api.Models;

public class TenantMembership
{
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? User { get; set; }
    public Tenant? Tenant { get; set; }
}
