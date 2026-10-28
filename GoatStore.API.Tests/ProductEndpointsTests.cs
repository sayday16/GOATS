using System.Net;
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

    [Fact]
    public async Task GetProducts_WithoutToken_ReturnsUnauthorized()
    {
        _client.UseToken(null);

        var response = await _client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostProduct_AsSeller_ReturnsForbidden()
    {
        var storeName = ApiTestHelpers.UniqueStoreName();

        // Спершу власник створює магазин, потім продавець приєднується до нього
        await _client.RegisterAndLoginAsync(ApiTestHelpers.UniqueEmail("owner"), ApiTestHelpers.OwnerRole, storeName);
        var token = await _client.RegisterAndLoginAsync(ApiTestHelpers.UniqueEmail("seller"), ApiTestHelpers.SellerRole, storeName);
        _client.UseToken(token);

        var productRequest = new { Title = "Test Product", PurchasePrice = 50m, SalePrice = 100m };
        var response = await _client.PostAsJsonAsync("/api/products", productRequest);

        // Саме 403 (роль), а не 400 (валідація)
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostProduct_AsOwner_ReturnsCreated()
    {
        var token = await _client.RegisterAndLoginAsync(ApiTestHelpers.UniqueEmail("owner"), ApiTestHelpers.OwnerRole, ApiTestHelpers.UniqueStoreName());
        _client.UseToken(token);

        var productRequest = new { Title = "Test Product", PurchasePrice = 50m, SalePrice = 100m };
        var response = await _client.PostAsJsonAsync("/api/products", productRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task PostProduct_InvalidData_ReturnsBadRequest()
    {
        var token = await _client.RegisterAndLoginAsync(ApiTestHelpers.UniqueEmail("owner"), ApiTestHelpers.OwnerRole, ApiTestHelpers.UniqueStoreName());
        _client.UseToken(token);

        var response = await _client.PostAsJsonAsync("/api/products", new { Title = "", PurchasePrice = 50m, SalePrice = 100m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutProduct_AsOwner_UpdatesProduct()
    {
        var token = await _client.RegisterAndLoginAsync(ApiTestHelpers.UniqueEmail("owner"), ApiTestHelpers.OwnerRole, ApiTestHelpers.UniqueStoreName());
        _client.UseToken(token);
        var productId = await _client.CreateProductAsync();

        var response = await _client.PutAsJsonAsync($"/api/products/{productId}", new { Title = "Updated", PurchasePrice = 60m, SalePrice = 150m });
        var product = await _client.GetFromJsonAsync<JsonObject>($"/api/products/{productId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Updated", product!["product"]!["title"]!.ToString());
    }

    [Fact]
    public async Task DeleteProduct_AsOwner_RemovesProduct()
    {
        var token = await _client.RegisterAndLoginAsync(ApiTestHelpers.UniqueEmail("owner"), ApiTestHelpers.OwnerRole, ApiTestHelpers.UniqueStoreName());
        _client.UseToken(token);
        var productId = await _client.CreateProductAsync();

        var response = await _client.DeleteAsync($"/api/products/{productId}");
        var afterDelete = await _client.GetAsync($"/api/products/{productId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task GetProducts_AsOwner_ReturnsOwnProducts()
    {
        var token = await _client.RegisterAndLoginAsync(ApiTestHelpers.UniqueEmail("owner"), ApiTestHelpers.OwnerRole, ApiTestHelpers.UniqueStoreName());
        _client.UseToken(token);
        await _client.CreateProductAsync("Levi's 501");

        var products = await _client.GetFromJsonAsync<JsonArray>("/api/products");

        Assert.Equal("Levi's 501", Assert.Single(products!)!["title"]!.ToString());
    }

    [Fact]
    public async Task GetProduct_FromOtherStore_ReturnsNotFound()
    {
        var ownerA = await _client.RegisterAndLoginAsync(ApiTestHelpers.UniqueEmail("ownerA"), ApiTestHelpers.OwnerRole, ApiTestHelpers.UniqueStoreName());
        var ownerB = await _client.RegisterAndLoginAsync(ApiTestHelpers.UniqueEmail("ownerB"), ApiTestHelpers.OwnerRole, ApiTestHelpers.UniqueStoreName());

        _client.UseToken(ownerA);
        var productId = await _client.CreateProductAsync();

        _client.UseToken(ownerB);
        var response = await _client.GetAsync($"/api/products/{productId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SellProduct_Twice_ReturnsConflict()
    {
        var token = await _client.RegisterAndLoginAsync(ApiTestHelpers.UniqueEmail("owner"), ApiTestHelpers.OwnerRole, ApiTestHelpers.UniqueStoreName());
        _client.UseToken(token);
        var productId = await _client.CreateProductAsync();

        var firstSale = await _client.PostAsync($"/api/products/{productId}/sell", null);
        var secondSale = await _client.PostAsync($"/api/products/{productId}/sell", null);

        Assert.Equal(HttpStatusCode.OK, firstSale.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondSale.StatusCode);
    }
}
