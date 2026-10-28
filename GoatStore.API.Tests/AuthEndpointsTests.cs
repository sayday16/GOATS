using System.Net.Http.Headers;
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
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithInvalidToken_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid_token_string");

        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

  [Fact]
    public async Task RegisterLoginAndGetMe_ReturnsSuccessAndCorrectData()
    {
        var registerRequest = new
        {
            Email = "owner@test.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!", 
            StoreName = "TestStore",
            StorePassword = "StorePassword123!", 
            Role = 0 
        };
        
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        
        if (!registerResponse.IsSuccessStatusCode)
        {
            var errorMessage = await registerResponse.Content.ReadAsStringAsync();
            throw new Exception($"Помилка реєстрації: {registerResponse.StatusCode} - {errorMessage}");
        }

        var loginRequest = new
        {
            Email = "owner@test.com",
            Password = "Password123!"
        };
        
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<JsonObject>();
       
        var token = loginResult!["accessToken"]!.ToString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var meResponse = await _client.GetAsync("/api/auth/me");
        meResponse.EnsureSuccessStatusCode();

        var meResult = await meResponse.Content.ReadFromJsonAsync<JsonObject>();
        
        Assert.Equal("owner@test.com", meResult!["email"]!.ToString());
    }
}