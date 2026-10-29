using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.IdentityModel.Tokens;

namespace GoatStore.API.Tests;

public static class ApiTestHelpers
{
    public const int OwnerRole = 0;
    public const int SellerRole = 1;
    public const string Password = "Password123!";
    public const string StorePassword = "StorePassword123!";

    // База спільна для всіх тестів класу, тому кожен тест бере свої унікальні email і назву магазину
    public static string UniqueEmail(string prefix) => $"{prefix}_{Guid.NewGuid():N}@test.com";

    public static string UniqueStoreName() => $"Store_{Guid.NewGuid():N}";

    public static async Task<string> RegisterAndLoginAsync(this HttpClient client, string email, int role, string storeName)
    {
        var registerRequest = new
        {
            Email = email,
            Password,
            ConfirmPassword = Password,
            StoreName = storeName,
            StorePassword,
            Role = role
        };

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", registerRequest);
        if (!registerResponse.IsSuccessStatusCode)
        {
            var errorMessage = await registerResponse.Content.ReadAsStringAsync();
            throw new Exception($"Помилка реєстрації: {registerResponse.StatusCode} - {errorMessage}");
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password });
        loginResponse.EnsureSuccessStatusCode();

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<JsonObject>();
        return loginResult!["accessToken"]!.ToString();
    }

    public static void UseToken(this HttpClient client, string? token) =>
        client.DefaultRequestHeaders.Authorization = token is null ? null : new AuthenticationHeaderValue("Bearer", token);

    public static async Task<string> CreateProductAsync(this HttpClient client, string title = "Test Product")
    {
        var response = await client.PostAsJsonAsync("/api/products", new { Title = title, PurchasePrice = 50m, SalePrice = 100m });
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonObject>();
        return result!["product"]!["id"]!.ToString();
    }

    // Токен, підписаний вручну: для перевірки, що API відхиляє прострочені й підроблені токени
    public static string CreateToken(string signingKey, DateTime expiresAt)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestApplicationFactory.Issuer,
            audience: TestApplicationFactory.Audience,
            claims:
            [
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("email", "forged@test.com"),
                new Claim("role", "Admin"),
                new Claim("store_id", Guid.NewGuid().ToString())
            ],
            notBefore: expiresAt.AddHours(-1),
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
