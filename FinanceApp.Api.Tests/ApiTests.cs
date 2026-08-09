using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FinanceApp.Api.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceApp.Api.Tests;

/// <summary>
/// Integration tests for the business endpoints. The factory replaces the
/// application database with SQLite in-memory, so these requests never touch
/// the developer database.
/// </summary>
[Collection("API integration")]
public class ApiTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Categories_CanBeCreatedUpdatedAndListed()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await CreateAccessTokenAsync(client));
        var category = new Category { Name = "Transporte", ColorHex = "#123456" };

        var createResponse = await client.PostAsJsonAsync("/api/categories", category);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<Category>();
        created.Should().NotBeNull();
        created!.Id.Should().BeGreaterThan(0);

        created.Name = "Mobilidade";
        var updateResponse = await client.PutAsJsonAsync($"/api/categories/{created.Id}", created);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = await client.GetFromJsonAsync<Category[]>("/api/categories");
        categories.Should().ContainSingle(c => c.Id == created.Id && c.Name == "Mobilidade");
    }

    [Fact]
    public async Task Category_WithBlankName_IsRejected()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/categories", new Category { Name = "  " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Transactions_RequireAnAuthenticatedUser()
    {
        var client = _factory.CreateClient();
        var transaction = new Transaction
        {
            Description = "Conta de luz",
            Amount = 120m,
            Type = TransactionType.Expense,
            Date = DateTime.UtcNow,
            ReferenceMonth = DateTime.UtcNow,
            InstallmentTotal = 1
        };

        var getResponse = await client.GetAsync("/api/transactions?year=2026&month=7");
        var postResponse = await client.PostAsJsonAsync("/api/transactions", transaction);

        getResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WhatsAppMessage_IsImportedThenConfirmedAsATransaction()
    {
        var client = _factory.CreateClient();
        var session = await CreateTenantSessionAsync(client);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            var paymentMethod = new PaymentMethod { Name = "Nubank", IsCreditCard = true, TenantId = session.TenantId };
            db.PaymentMethods.Add(paymentMethod);
            await db.SaveChangesAsync();
            db.WhatsAppSenders.Add(new WhatsAppSender
            {
                PhoneNumber = "5511999999999",
                TenantId = session.TenantId,
                OwnerId = session.UserId,
                DisplayName = "Pessoa de teste"
            });
            await db.SaveChangesAsync();
        }

        const string payload = """{"entry":[{"changes":[{"value":{"messages":[{"id":"wamid.test-1","from":"5511999999999","timestamp":"1785619200","type":"text","text":{"body":"89,90 almoço Nubank"}}]}}]}]}""";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("test-app-secret"));
        var signature = "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var webhookRequest = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/whatsapp/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        webhookRequest.Headers.TryAddWithoutValidation("X-Hub-Signature-256", signature);
        (await client.SendAsync(webhookRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.AccessToken);
        var inbox = await client.GetFromJsonAsync<WhatsAppInboxItem[]>("/api/whatsapp/inbox?status=Pending");
        inbox.Should().ContainSingle();
        inbox![0].SuggestedAmount.Should().Be(89.90m);
        inbox[0].SuggestedPaymentMethodId.Should().NotBeNull();

        var confirmation = await client.PostAsync($"/api/whatsapp/inbox/{inbox[0].Id}/confirm", JsonContent.Create(new { }));
        confirmation.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmationBody = await confirmation.Content.ReadFromJsonAsync<ConfirmationResult>();
        confirmationBody!.transactionId.Should().BeGreaterThan(0);
        var transactions = await client.GetFromJsonAsync<Transaction[]>("/api/transactions?year=2026&month=8");
        transactions.Should().ContainSingle(transaction => transaction.Description == "almoço" && transaction.Amount == 89.90m && transaction.TenantId == session.TenantId);
    }

    private async Task<string> CreateAccessTokenAsync(HttpClient client)
    {
        return (await CreateTenantSessionAsync(client)).AccessToken;
    }

    private async Task<(string AccessToken, string UserId, string TenantId)> CreateTenantSessionAsync(HttpClient client)
    {
        var email = $"categories-{Guid.NewGuid()}@local";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var user = new ApplicationUser { UserName = email, Email = email };
        (await users.CreateAsync(user, "Test123!")).Succeeded.Should().BeTrue();
        var tenant = new Tenant { Name = $"Tenant-{Guid.NewGuid()}", CreatedById = user.Id };
        db.Tenants!.Add(tenant);
        await db.SaveChangesAsync();
        user.TenantId = tenant.Id;
        (await users.UpdateAsync(user)).Succeeded.Should().BeTrue();
        db.TenantMemberships!.Add(new TenantMembership { UserId = user.Id, TenantId = tenant.Id });
        await db.SaveChangesAsync();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test123!" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await login.Content.ReadFromJsonAsync<LoginResult>();
        return (session!.accessToken, user.Id, tenant.Id);
    }

    private record LoginResult(string accessToken);
    private record ConfirmationResult(int transactionId);
}
