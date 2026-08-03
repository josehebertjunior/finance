namespace FinanceApp.Api.Models;

public class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TenantId { get; set; }
}
