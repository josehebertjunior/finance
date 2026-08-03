using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace FinanceApp.Api.Services;

/// <summary>Envia e-mails pela API HTTPS do Resend, compatível com hospedagens que bloqueiam SMTP.</summary>
public class ResendEmailSender : IAppEmailSender
{
    private readonly HttpClient _http;
    private readonly ResendSettings _settings;
    private readonly AppSettings _app;

    public ResendEmailSender(HttpClient http, IOptions<ResendSettings> settings, IOptions<AppSettings> app)
    {
        _http = http;
        _settings = settings.Value;
        _app = app.Value;
    }

    public Task SendInviteAsync(string email, string token)
    {
        var inviteUrl = $"{_app.FrontendUrl.TrimEnd('/')}/login?invite={Uri.EscapeDataString(token)}";
        return SendAsync(email, "Convite para Finanças", $"<p>Você foi convidado para usar o sistema de finanças.</p><p><a href=\"{inviteUrl}\">Finalizar cadastro</a></p><p>O convite expira em 1 hora.</p>");
    }

    public Task SendPasswordResetAsync(string email, string code, string resetLink)
        => SendAsync(email, "Recuperação de senha", $"<p>Use este código em até 15 minutos:</p><p><strong>{code}</strong></p><p><a href=\"{resetLink}\">Redefinir senha</a></p>");

    private async Task SendAsync(string recipient, string subject, string html)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(_settings.FromEmail))
            throw new InvalidOperationException("Resend não foi configurado. Defina Resend__ApiKey e Resend__FromEmail.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        request.Content = JsonContent.Create(new
        {
            from = $"{_settings.FromName} <{_settings.FromEmail}>",
            to = new[] { recipient },
            subject,
            html
        });
        using var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
