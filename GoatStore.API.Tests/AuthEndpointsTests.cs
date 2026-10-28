using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace GoatStore.API.Tests;

public class AuthEndpointsTests : IClassFixture<TestApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(TestApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetMe_WithoutToken_ReturnsUnauthorized()
    {
        _client.UseToken(null);

        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithInvalidToken_ReturnsUnauthorized()
    {
        _client.UseToken("invalid_token_string");

        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithExpiredToken_ReturnsUnauthorized()
    {
        _client.UseToken(ApiTestHelpers.CreateToken(TestApplicationFactory.SigningKey, DateTime.UtcNow.AddMinutes(-10)));

        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithTokenSignedByOtherKey_ReturnsUnauthorized()
    {
        _client.UseToken(ApiTestHelpers.CreateToken("AnotherKeyThatIsAlsoLongEnoughButNotOurs456!", DateTime.UtcNow.AddHours(1)));

        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RegisterLoginAndGetMe_ReturnsSuccessAndCorrectData()
    {
        var email = ApiTestHelpers.UniqueEmail("owner");
        var token = await _client.RegisterAndLoginAsync(email, ApiTestHelpers.OwnerRole, ApiTestHelpers.UniqueStoreName());
        _client.UseToken(token);

        var meResponse = await _client.GetAsync("/api/auth/me");
        meResponse.EnsureSuccessStatusCode();

        var meResult = await meResponse.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(email, meResult!["email"]!.ToString());
    }
}
