using System.Net;
using System.Net.Http.Json;
using Xunit;
using FluentAssertions;

namespace FinanceApp.Api.Tests;

public class ApiAuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ApiAuthTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_Then_Login_Refresh_Logout_Flow()
    {
        var client = _factory.CreateClient();
        var email = $"test+{Guid.NewGuid()}@local";
        var pwd = "Test123!";

        var regRes = await client.PostAsJsonAsync("/api/auth/register", new { email = email, password = pwd, displayName = "Tester" });
        regRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new { email = email, password = pwd });
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await loginRes.Content.ReadFromJsonAsync<LoginResult>();
        body.Should().NotBeNull();
        body!.accessToken.Should().NotBeNullOrEmpty();

        // attempt refresh without cookie should fail (no refresh cookie)
        var refreshRes = await client.PostAsync("/api/auth/refresh", null);
        refreshRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // logout should succeed (idempotent)
        var logoutRes = await client.PostAsync("/api/auth/logout", null);
        logoutRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    record LoginResult(string accessToken, int expiresIn);
}
