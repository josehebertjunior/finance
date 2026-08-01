namespace FinanceApp.Api.Services;

public interface IAppEmailSender
{
    Task SendInviteAsync(string email, string token);
    Task SendPasswordResetAsync(string email, string code, string resetLink);
}
