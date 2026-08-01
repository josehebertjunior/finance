using FinanceApp.Api.Models;

namespace FinanceApp.Api.Services;

public class ConsoleEmailSender : IAppEmailSender
{
    public Task SendInviteAsync(string email, string token)
    {
        Console.WriteLine($"[EmailSender] Invite generated for {email}: token={token}");
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string code, string resetLink)
    {
        Console.WriteLine($"[EmailSender] Password reset for {email}: code={code}, link={resetLink}");
        return Task.CompletedTask;
    }
}
