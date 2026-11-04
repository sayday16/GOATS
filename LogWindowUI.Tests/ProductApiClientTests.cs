using System.Net;
using System.Net.Http.Json;
using GoatStore.Core.DTOs;
using GoatStore.DataAccess.Entities;
using LogWindowUI.Services;

namespace LogWindowUI.Tests
{
    public class ProductApiClientTests
    {
        private static readonly Guid ProductId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        private static FakeHttpHandler Returns(HttpStatusCode status, object? body = null) =>
            new(() => new HttpResponseMessage(status) { Content = body is null ? new StringContent("") : JsonContent.Create(body) });

        private static ProductApiClient CreateClient(FakeHttpHandler handler) => new(handler.CreateClient());

        private static string? SentRequest(FakeHttpHandler handler) =>
            handler.LastRequest is { } request ? $"{request.Method} {request.RequestUri?.PathAndQuery}" : null;

        private static SaveProductRequestDto Request() => new()
        {
            Title = "Худі Oversize",
            Brand = "Nike",
            Size = "M",
            Category = "Одяг",
            PurchasePrice = 400m,
            SalePrice = 1200m
        };

        [Fact]
        public async Task GetAllAsync_WithFilter_SendsFilterInQueryString()
        {
            var handler = Returns(HttpStatusCode.OK, Array.Empty<ProductDto>());

            await CreateClient(handler).GetAllAsync(new ProductFilterDto { Search = " худі ", Category = "Одяг", Status = ProductStatus.InStock });

            Assert.Equal("GET /api/products?search=%D1%85%D1%83%D0%B4%D1%96&category=%D0%9E%D0%B4%D1%8F%D0%B3&status=InStock", SentRequest(handler));
        }

        [Fact]
        public async Task GetAllAsync_WithoutFilter_SendsNoQueryString()
        {
            var handler = Returns(HttpStatusCode.OK, Array.Empty<ProductDto>());

            await CreateClient(handler).GetAllAsync();

            Assert.Equal("GET /api/products", SentRequest(handler));
        }

        [Fact]
        public async Task GetAllAsync_ReturnsProductsFromServer()
        {
            var handler = Returns(HttpStatusCode.OK, new[] { new ProductDto { Title = "Худі Oversize" } });

            var products = await CreateClient(handler).GetAllAsync();

            Assert.Equal(new[] { "Худі Oversize" }, products.Select(p => p.Title));
        }

        [Fact]
        public async Task GetAllAsync_ServerError_Throws()
        {
            var handler = Returns(HttpStatusCode.InternalServerError);

            await Assert.ThrowsAsync<HttpRequestException>(() => CreateClient(handler).GetAllAsync());
        }

        [Fact]
        public async Task GetFilterOptionsAsync_ReturnsCategoriesFromServer()
        {
            var handler = Returns(HttpStatusCode.OK, new ProductFilterOptionsDto { Categories = ["Взуття", "Одяг"] });

            var options = await CreateClient(handler).GetFilterOptionsAsync();

            Assert.Equal(new[] { "Взуття", "Одяг" }, options.Categories);
        }

        [Fact]
        public async Task CreateAsync_PostsToProducts()
        {
            var handler = Returns(HttpStatusCode.Created, new ProductResponseDto { IsSuccess = true, Message = "Товар додано." });

            await CreateClient(handler).CreateAsync(Request());

            Assert.Equal("POST /api/products", SentRequest(handler));
        }

        [Fact]
        public async Task CreateAsync_ConflictWithMessage_ReturnsServerMessage()
        {
            var handler = Returns(HttpStatusCode.Conflict, new ProductResponseDto { Message = "Товар із таким артикулом уже є в магазині." });

            var result = await CreateClient(handler).CreateAsync(Request());

            Assert.Equal("Товар із таким артикулом уже є в магазині.", result.Message);
        }

        [Fact]
        public async Task UpdateAsync_PutsToProduct()
        {
            var handler = Returns(HttpStatusCode.OK, new ProductResponseDto { IsSuccess = true, Message = "Товар оновлено." });

            await CreateClient(handler).UpdateAsync(ProductId, Request());

            Assert.Equal($"PUT /api/products/{ProductId}", SentRequest(handler));
        }

        [Fact]
        public async Task SellAsync_PostsToSellEndpoint()
        {
            var handler = Returns(HttpStatusCode.OK, new ProductResponseDto { IsSuccess = true, Message = "Товар продано." });

            await CreateClient(handler).SellAsync(ProductId);

            Assert.Equal($"POST /api/products/{ProductId}/sell", SentRequest(handler));
        }

        [Fact]
        public async Task SellAsync_AlreadySold_ReturnsServerMessage()
        {
            var handler = Returns(HttpStatusCode.Conflict, new ProductResponseDto { Message = "Цей товар уже продано." });

            var result = await CreateClient(handler).SellAsync(ProductId);

            Assert.Equal("Цей товар уже продано.", result.Message);
        }

        [Fact]
        public async Task DeleteAsync_ForbiddenWithoutBody_ReturnsRightsMessage()
        {
            var handler = Returns(HttpStatusCode.Forbidden);

            var result = await CreateClient(handler).DeleteAsync(ProductId);

            Assert.Equal("Недостатньо прав для цієї дії.", result.Message);
        }

        [Fact]
        public async Task GetAsync_ServerErrorWithoutMessage_IsNotSuccessful()
        {
            var handler = Returns(HttpStatusCode.InternalServerError);

            var result = await CreateClient(handler).GetAsync(ProductId);

            Assert.False(result.IsSuccess);
        }
    }
}
