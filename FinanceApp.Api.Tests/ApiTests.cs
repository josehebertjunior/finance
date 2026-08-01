using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FinanceApp.Api.Models;
using FluentAssertions;
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
}
