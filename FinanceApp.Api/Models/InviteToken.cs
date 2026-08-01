using System;

namespace FinanceApp.Api.Models;

public class InviteToken
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool Used { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedById { get; set; }
    public string CreatedById { get; set; } = string.Empty;
    public ApplicationUser? CreatedBy { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Tenant? Tenant { get; set; }
}
