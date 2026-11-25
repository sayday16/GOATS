using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace GoatStore.API.Tests;

public class SaleEndpointsTests : IClassFixture<TestApplicationFactory>
{
    private readonly HttpClient _client;

    public SaleEndpointsTests(TestApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string OwnerToken, string SellerToken, string ProductId)> SetUpStoreWithProductAsync()
    {
        var storeName = ApiTestHelpers.UniqueStoreName();
        var ownerToken = await _client.RegisterAndLoginAsync(ApiTestHelpers.UniqueEmail("owner"), ApiTestHelpers.OwnerRole, storeName);
        _client.UseToken(ownerToken);
        var productId = await _client.CreateProductAsync();
        var sellerToken = await _client.RegisterAndLoginAsync(ApiTestHelpers.UniqueEmail("seller"), ApiTestHelpers.SellerRole, storeName);
        return (ownerToken, sellerToken, productId);
    }

    private Task<HttpResponseMessage> SellAsync(string productId, decimal discount = 0m) =>
        _client.PostAsJsonAsync("/api/sales", new
        {
            Items = new[] { new { ProductId = productId, Quantity = 1 } },
            PaymentMethod = 0,
            Discount = discount
        });

    private async Task<JsonObject> SellAsSellerAsync()
    {
        var (_, sellerToken, productId) = await SetUpStoreWithProductAsync();
        _client.UseToken(sellerToken);
        var response = await SellAsync(productId);
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!;
    }

    [Fact]
    public async Task PostSale_WithoutToken_ReturnsUnauthorized()
    {
        _client.UseToken(null);

        var response = await SellAsync(Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostSale_AsSeller_ReturnsCreated()
    {
        var (_, sellerToken, productId) = await SetUpStoreWithProductAsync();
        _client.UseToken(sellerToken);

        var response = await SellAsync(productId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task PostSale_FirstSale_ReturnsNumberOne()
    {
        var body = await SellAsSellerAsync();

        Assert.Equal(1, (int?)body["sale"]?["number"]);
    }

    [Fact]
    public async Task PostSale_SameProductTwice_ReturnsConflict()
    {
        var (_, sellerToken, productId) = await SetUpStoreWithProductAsync();
        _client.UseToken(sellerToken);
        await SellAsync(productId);

        var response = await SellAsync(productId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostSale_UnknownProduct_ReturnsNotFound()
    {
        var (_, sellerToken, _) = await SetUpStoreWithProductAsync();
        _client.UseToken(sellerToken);

        var response = await SellAsync(Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostSale_NoItems_ReturnsBadRequest()
    {
        var (_, sellerToken, _) = await SetUpStoreWithProductAsync();
        _client.UseToken(sellerToken);

        var response = await _client.PostAsJsonAsync("/api/sales", new { Items = Array.Empty<object>(), PaymentMethod = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostSale_DiscountAboveTotal_ReturnsBadRequest()
    {
        var (_, sellerToken, productId) = await SetUpStoreWithProductAsync();
        _client.UseToken(sellerToken);

        var response = await SellAsync(productId, discount: 1000m);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostSale_ProductOfAnotherStore_ReturnsNotFound()
    {
        var (_, _, foreignProductId) = await SetUpStoreWithProductAsync();
        var otherToken = await _client.RegisterAndLoginAsync(ApiTestHelpers.UniqueEmail("owner"), ApiTestHelpers.OwnerRole, ApiTestHelpers.UniqueStoreName());
        _client.UseToken(otherToken);

        var response = await SellAsync(foreignProductId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSale_ExistingNumber_ReturnsOk()
    {
        var (_, sellerToken, productId) = await SetUpStoreWithProductAsync();
        _client.UseToken(sellerToken);
        await SellAsync(productId);

        var response = await _client.GetAsync("/api/sales/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSale_UnknownNumber_ReturnsNotFound()
    {
        var (_, sellerToken, _) = await SetUpStoreWithProductAsync();
        _client.UseToken(sellerToken);

        var response = await _client.GetAsync("/api/sales/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostReturn_SoldItem_ReturnsOk()
    {
        var body = await SellAsSellerAsync();
        var itemId = (string?)body["sale"]?["items"]?[0]?["id"];

        var response = await _client.PostAsJsonAsync("/api/sales/1/returns", new { Items = new[] { new { SaleItemId = itemId, Quantity = 1 } } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostReturn_SameItemTwice_ReturnsConflict()
    {
        var body = await SellAsSellerAsync();
        var itemId = (string?)body["sale"]?["items"]?[0]?["id"];
        var request = new { Items = new[] { new { SaleItemId = itemId, Quantity = 1 } } };
        await _client.PostAsJsonAsync("/api/sales/1/returns", request);

        var response = await _client.PostAsJsonAsync("/api/sales/1/returns", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetReportSummary_WithoutToken_ReturnsUnauthorized()
    {
        _client.UseToken(null);

        var response = await _client.GetAsync("/api/reports/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetReportSummary_AsSeller_ReturnsForbidden()
    {
        var (_, sellerToken, _) = await SetUpStoreWithProductAsync();
        _client.UseToken(sellerToken);

        var response = await _client.GetAsync("/api/reports/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetReportSummary_AsOwner_ReturnsOk()
    {
        var (ownerToken, _, _) = await SetUpStoreWithProductAsync();
        _client.UseToken(ownerToken);

        var response = await _client.GetAsync("/api/reports/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetReportSummary_AfterSale_ShowsMonthRevenue()
    {
        var (ownerToken, sellerToken, productId) = await SetUpStoreWithProductAsync();
        _client.UseToken(sellerToken);
        await SellAsync(productId);
        _client.UseToken(ownerToken);

        var summary = await _client.GetFromJsonAsync<JsonObject>("/api/reports/summary");

        Assert.Equal(100m, (decimal?)summary?["monthRevenue"]);
    }
}
