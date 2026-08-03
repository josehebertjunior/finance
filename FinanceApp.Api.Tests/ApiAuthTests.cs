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

        var fixedMonth = new DateTime(2026, 8, 1);
        var fixedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/transactions")
        {
            Content = JsonContent.Create(new
            {
                description = "Conta fixa de teste",
                amount = 120m,
                type = 1,
                date = fixedMonth,
                referenceMonth = fixedMonth,
                isFixed = true,
                installmentTotal = 1
            })
        };
        fixedRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login.accessToken);
        (await client.SendAsync(fixedRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        var nextMonthRequest = new HttpRequestMessage(HttpMethod.Get, "/api/transactions?year=2026&month=9");
        nextMonthRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login.accessToken);
        var nextMonthResponse = await client.SendAsync(nextMonthRequest);
        var nextMonthTransactions = await nextMonthResponse.Content.ReadFromJsonAsync<Transaction[]>();
        nextMonthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        nextMonthTransactions.Should().ContainSingle(t => t.Description == "Conta fixa de teste" && t.IsFixed);

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

    [Fact]
    public async Task TenantMember_CanReadTransactionsCreatedByTheTenantOwner()
    {
        var memberEmail = $"member-{Guid.NewGuid()}@local";
        var invitation = await CreateInviteWithOwnerAsync(memberEmail);
        var client = _factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = memberEmail,
            password = "Test123!",
            displayName = "Member",
            inviteCode = invitation.Token
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var ownerLogin = await LoginAsync(client, invitation.OwnerEmail);
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/transactions")
        {
            Content = JsonContent.Create(new
            {
                description = "Despesa compartilhada",
                amount = 90m,
                type = 1,
                date = new DateTime(2026, 8, 1),
                referenceMonth = new DateTime(2026, 8, 1),
                installmentTotal = 1
            })
        };
        createRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerLogin.accessToken);
        (await client.SendAsync(createRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        var memberLogin = await LoginAsync(client, memberEmail, "Test123!");
        var transactionsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/transactions?year=2026&month=8");
        transactionsRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", memberLogin.accessToken);
        var transactionsResponse = await client.SendAsync(transactionsRequest);
        var transactions = await transactionsResponse.Content.ReadFromJsonAsync<Transaction[]>();

        transactionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        transactions.Should().Contain(transaction => transaction.Description == "Despesa compartilhada");
    }

    [Fact]
    public async Task TenantMember_CannotReadTransactionsFromAnotherTenant()
    {
        var firstMemberEmail = $"member-a-{Guid.NewGuid()}@local";
        var firstInvitation = await CreateInviteWithOwnerAsync(firstMemberEmail);
        var secondMemberEmail = $"member-b-{Guid.NewGuid()}@local";
        var secondInvitation = await CreateInviteWithOwnerAsync(secondMemberEmail);
        var client = _factory.CreateClient();

        var firstRegistration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = firstMemberEmail, password = "Test123!", displayName = "Member", inviteCode = firstInvitation.Token
        });
        firstRegistration.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondRegistration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = secondMemberEmail, password = "Test123!", displayName = "Member", inviteCode = secondInvitation.Token
        });
        secondRegistration.StatusCode.Should().Be(HttpStatusCode.Created);

        var firstOwnerLogin = await LoginAsync(client, firstInvitation.OwnerEmail);
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/transactions")
        {
            Content = JsonContent.Create(new
            {
                description = "Somente do primeiro grupo", amount = 90m, type = 1,
                date = new DateTime(2026, 8, 1), referenceMonth = new DateTime(2026, 8, 1), installmentTotal = 1
            })
        };
        createRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", firstOwnerLogin.accessToken);
        (await client.SendAsync(createRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        var secondMemberLogin = await LoginAsync(client, secondMemberEmail, "Test123!");
        var transactionsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/transactions?year=2026&month=8");
        transactionsRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", secondMemberLogin.accessToken);
        var transactions = await (await client.SendAsync(transactionsRequest)).Content.ReadFromJsonAsync<Transaction[]>();

        transactions.Should().NotContain(transaction => transaction.Description == "Somente do primeiro grupo");
    }

    [Fact]
    public async Task AdminUsers_ListsMultipleUsersWithoutConcurrentContextAccess()
    {
        var memberEmail = $"member-{Guid.NewGuid()}@local";
        var invitation = await CreateInviteWithOwnerAsync(memberEmail, ownerIsAdmin: true);
        var client = _factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = memberEmail,
            password = "Test123!",
            displayName = "Member",
            inviteCode = invitation.Token
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var ownerLogin = await LoginAsync(client, invitation.OwnerEmail);
        var usersRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/users");
        usersRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerLogin.accessToken);
        var usersResponse = await client.SendAsync(usersRequest);

        usersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await usersResponse.Content.ReadFromJsonAsync<ApplicationUser[]>();
        users.Should().NotBeNull();
        users!.Should().Contain(user => user.Email == memberEmail);
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

    private async Task<(string Token, string OwnerEmail)> CreateInviteWithOwnerAsync(string email, bool ownerIsAdmin = false)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var ownerEmail = $"owner-{Guid.NewGuid()}@local";
        var owner = new ApplicationUser { UserName = ownerEmail, Email = ownerEmail };
        (await users.CreateAsync(owner, "Admin123!")).Succeeded.Should().BeTrue();
        if (ownerIsAdmin) (await users.AddToRoleAsync(owner, "Admin")).Succeeded.Should().BeTrue();

        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid()}", CreatedById = owner.Id };
        db.Tenants!.Add(tenant);
        await db.SaveChangesAsync();
        owner.TenantId = tenant.Id;
        (await users.UpdateAsync(owner)).Succeeded.Should().BeTrue();
        db.TenantMemberships!.Add(new TenantMembership { UserId = owner.Id, TenantId = tenant.Id });

        var invite = new InviteToken
        {
            Email = email,
            Token = Guid.NewGuid().ToString("N"),
            TenantId = tenant.Id,
            CreatedById = owner.Id,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        db.InviteTokens!.Add(invite);
        await db.SaveChangesAsync();
        return (invite.Token, ownerEmail);
    }

    private static async Task<LoginResult> LoginAsync(HttpClient client, string email, string password = "Admin123!")
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await response.Content.ReadFromJsonAsync<LoginResult>();
        login.Should().NotBeNull();
        return login!;
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
