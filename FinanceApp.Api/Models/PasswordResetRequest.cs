using System;
using System.ComponentModel.DataAnnotations;

namespace FinanceApp.Api.Models;

public class PasswordResetRequest
{
    [Key]
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RequestToken { get; set; } = string.Empty;
    public string ResetToken { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool Used { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
