using Microsoft.AspNetCore.Identity;

namespace FinanceApp.Api.Models;

public class ApplicationUser : IdentityUser
{
    // Exemplo: nome exibido no app
    public string? DisplayName { get; set; }
}
