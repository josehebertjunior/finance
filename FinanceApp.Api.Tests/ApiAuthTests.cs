using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FinanceApp.Api.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceApp.Api.Tests;

/// <summary>
/// Covers the invite-only registration and the refresh-token/CSRF lifecycle.
/// </summary>
[Collection("API integration")]
public class ApiAuthTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ApiAuthTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_WithoutAValidInvite_IsRejected()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"test-{Guid.NewGuid()}@local",
            password = "Test123!",
            displayName = "Tester",
            inviteCode = "not-a-valid-invite"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_Login_RefreshAndLogout_ProtectTheSession()
    {
        var email = $"test-{Guid.NewGuid()}@local";
        var inviteCode = await CreateInviteAsync(email);
        var client = _factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Test123!",
            displayName = "Tester",
            inviteCode
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test123!" });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        login.Should().NotBeNull();
        login!.accessToken.Should().NotBeNullOrWhiteSpace();

        var transactionRequest = new HttpRequestMessage(HttpMethod.Post, "/api/transactions")
        {
            Content = JsonContent.Create(new
            {
                description = "Despesa autenticada",
                amount = 10m,
                type = 1,
                date = DateTime.UtcNow,
                referenceMonth = DateTime.UtcNow,
                installmentTotal = 1
            })
        };
        transactionRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login.accessToken);
        var transactionResponse = await client.SendAsync(transactionRequest);
        transactionResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstSession = SessionCookies(loginResponse);
        var missingCsrfResponse = await client.PostAsync("/api/auth/refresh", null);
        missingCsrfResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var refreshResponse = await SendSessionRequest(client, "/api/auth/refresh", firstSession);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshedSession = SessionCookies(refreshResponse);

        var reusedTokenResponse = await SendSessionRequest(client, "/api/auth/refresh", firstSession);
        reusedTokenResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var logoutResponse = await SendSessionRequest(client, "/api/auth/logout", refreshedSession);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterLogoutResponse = await SendSessionRequest(client, "/api/auth/refresh", refreshedSession);
        afterLogoutResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<string> CreateInviteAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var adminEmail = $"admin-{Guid.NewGuid()}@local";
        var admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail };
        (await users.CreateAsync(admin, "Admin123!")).Succeeded.Should().BeTrue();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid()}", CreatedById = admin.Id };
        db.Tenants!.Add(tenant);
        await db.SaveChangesAsync();

        var invite = new InviteToken
        {
            Email = email,
            Token = Guid.NewGuid().ToString("N"),
            TenantId = tenant.Id,
            CreatedById = admin.Id,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        db.InviteTokens!.Add(invite);
        await db.SaveChangesAsync();
        return invite.Token;
    }

    private static async Task<HttpResponseMessage> SendSessionRequest(HttpClient client, string path, Session session)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Cookie", session.CookieHeader);
        request.Headers.Add("X-CSRF-Token", session.CsrfToken);
        return await client.SendAsync(request);
    }

    private static Session SessionCookies(HttpResponseMessage response)
    {
        var cookies = response.Headers.GetValues("Set-Cookie")
            .Select(value => value.Split(';', 2)[0])
            .ToArray();
        var csrf = cookies.Single(cookie => cookie.StartsWith("csrfToken=", StringComparison.Ordinal));
        return new Session(string.Join("; ", cookies), Uri.UnescapeDataString(csrf["csrfToken=".Length..]));
    }

    private record LoginResult(string accessToken, int expiresIn);
    private record Session(string CookieHeader, string CsrfToken);
}
