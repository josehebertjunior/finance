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

public class ResendSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string FromName { get; set; } = "Finanças Pessoais";
    public string FromEmail { get; set; } = string.Empty;
}

public class AppSettings
{
    public string FrontendUrl { get; set; } = "http://localhost:4200";
}
