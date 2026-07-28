using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FinanceApp.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceApp.Api.Tests;

public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public ApiTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<FinanceDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                var connection = new SqliteConnection("DataSource=:memory:");
                connection.Open();

                services.AddDbContext<FinanceDbContext>(options => options.UseSqlite(connection));

                var serviceProvider = services.BuildServiceProvider();
                using var scope = serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
                db.Database.EnsureCreated();
            });
        });
    }

    [Fact]
    public async Task GetCategories_ReturnsOk_AndEmptyArray()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/categories");

        response.EnsureSuccessStatusCode();
        var categories = await response.Content.ReadFromJsonAsync<Category[]>();

        Assert.NotNull(categories);
        Assert.Empty(categories);
    }

    [Fact]
    public async Task PostCategory_CreatesCategory()
    {
        var client = factory.CreateClient();
        var newCategory = new Category { Name = "Transporte", ColorHex = "#123456" };

        var response = await client.PostAsJsonAsync("/api/categories", newCategory);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<Category>();
        Assert.NotNull(created);
        Assert.Equal("Transporte", created.Name);
        Assert.Equal("#123456", created.ColorHex);
        Assert.True(created.Id > 0);
    }

    [Fact]
    public async Task PostTransaction_WithNegativeAmount_ReturnsBadRequest()
    {
        var client = factory.CreateClient();
        var transaction = new Transaction
        {
            Description = "Test",
            Amount = -10,
            Type = TransactionType.Expense,
            Date = DateTime.UtcNow,
            ReferenceMonth = DateTime.UtcNow,
            InstallmentTotal = 1
        };

        var response = await client.PostAsJsonAsync("/api/transactions", transaction);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
