using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FinanceApp.Api.Services;

public class SmtpEmailSender : IAppEmailSender
{
    private readonly MailSettings _settings;

    public SmtpEmailSender(IOptions<MailSettings> options)
    {
        _settings = options.Value;
    }

    public async Task SendInviteAsync(string email, string token)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Convite para Finanças";
        var inviteUrl = $"http://localhost:4200/login?invite={Uri.EscapeDataString(token)}";
        message.Body = new TextPart("html")
        {
            Text = $"<p>Você foi convidado para usar o sistema de finanças.</p><p>Clique no link abaixo para finalizar o registro:</p><p><a href=\"{inviteUrl}\">{inviteUrl}</a></p><p>O convite expira em 1 hora.</p>"
        };

        await SendAsync(message);
    }

    public async Task SendPasswordResetAsync(string email, string code, string resetLink)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Recuperação de senha";
        message.Body = new TextPart("html")
        {
            Text = $"<p>Recebemos uma solicitação de redefinição de senha.</p><p>Use o código abaixo em até 15 minutos:</p><p><strong>{code}</strong></p><p>Clique no link para continuar:</p><p><a href=\"{resetLink}\">{resetLink}</a></p>"
        };

        await SendAsync(message);
    }

    private async Task SendAsync(MimeMessage message)
    {
        if (string.IsNullOrWhiteSpace(_settings.Host) || string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password))
        {
            Console.WriteLine("[EmailSender] SMTP não configurado. Mensagem de email: \n" + message.HtmlBody);
            return;
        }

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(_settings.Host, _settings.Port, _settings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(_settings.Username, _settings.Password);
        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);
    }
}
