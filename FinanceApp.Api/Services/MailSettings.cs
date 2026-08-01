namespace FinanceApp.Api.Services;

public class MailSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromName { get; set; } = "FinanceApp";
    public string FromEmail { get; set; } = "no-reply@financeapp.local";
}
