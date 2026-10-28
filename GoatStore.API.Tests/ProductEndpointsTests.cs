using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace GoatStore.API.Tests;

public class ProductEndpointsTests : IClassFixture<TestApplicationFactory>
{
    private readonly HttpClient _client;

    public ProductEndpointsTests(TestApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // Додано параметр storeName для зв'язування продавця з існуючим магазином
    private async Task<string> GetTokenAsync(string email, int role, string storeName)
    {
        var registerRequest = new
        {
            Email = email,
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            StoreName = storeName,
            StorePassword = "StorePassword123!",
            Role = role 
        };
        
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        var loginRequest = new
        {
            Email = email,
            Password = "Password123!"
        };
        
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();
        
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<JsonObject>();
        return loginResult!["accessToken"]!.ToString();
    }

    [Fact]
    public async Task PostProduct_AsSeller_ReturnsForbidden()
    {
        var storeName = $"Store_{Guid.NewGuid()}";
        
        // 1. Реєструємо Owner, щоб створити магазин у базі даних
        await GetTokenAsync("owner_creator@test.com", 0, storeName);

        // 2. Тепер реєструємо Seller у цей вже створений магазин
        var token = await GetTokenAsync("seller@test.com", 1, storeName);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Використовуємо правильну модель SaveProductRequestDto
        var productRequest = new { Title = "Test Product", PurchasePrice = 50m, SalePrice = 100m };
        var response = await _client.PostAsJsonAsync("/api/products", productRequest);

        // Переконуємося, що відмова сталася саме через роль (403), а не через валідацію моделі (400)
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostProduct_AsOwner_ReturnsCreatedOrOk()
    {
        var storeName = $"Store_{Guid.NewGuid()}";
        
        // Реєструємо Owner і створюємо новий магазин
        var token = await GetTokenAsync("owner@test.com", 0, storeName);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Використовуємо правильну модель SaveProductRequestDto
        var productRequest = new { Title = "Test Product", PurchasePrice = 50m, SalePrice = 100m };
        var response = await _client.PostAsJsonAsync("/api/products", productRequest);

        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created);
    }
}