namespace FinanceApp.Api.Models;

using System.ComponentModel.DataAnnotations.Schema;

public class Tenant
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string CreatedById { get; set; } = string.Empty;

    [ForeignKey(nameof(CreatedById))]
    [InverseProperty(nameof(ApplicationUser.CreatedTenants))]
    public ApplicationUser? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [InverseProperty(nameof(ApplicationUser.Tenant))]
    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();

    public ICollection<InviteToken> Invites { get; set; } = new List<InviteToken>();
}
