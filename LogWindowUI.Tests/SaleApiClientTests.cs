using System.Net;
using System.Net.Http.Json;
using GoatStore.Core.DTOs;
using LogWindowUI.Services;

namespace LogWindowUI.Tests
{
    public class SaleApiClientTests
    {
        private static FakeHttpHandler Returns(HttpStatusCode status, object? body = null) =>
            new(() => new HttpResponseMessage(status) { Content = body is null ? new StringContent("") : JsonContent.Create(body) });

        private static SaleApiClient CreateClient(FakeHttpHandler handler) => new(handler.CreateClient());

        private static string? SentRequest(FakeHttpHandler handler) =>
            handler.LastRequest is { } request ? $"{request.Method} {request.RequestUri?.PathAndQuery}" : null;

        private static CreateSaleRequestDto Request() => new()
        {
            Items = [new CreateSaleItemDto { ProductId = Guid.NewGuid() }]
        };

        [Fact]
        public async Task CreateAsync_SendsPostToSalesEndpoint()
        {
            var handler = Returns(HttpStatusCode.Created, new SaleResponseDto { IsSuccess = true });

            await CreateClient(handler).CreateAsync(Request());

            Assert.Equal("POST /api/sales", SentRequest(handler));
        }

        [Fact]
        public async Task CreateAsync_ReturnsSaleNumberFromServer()
        {
            var handler = Returns(HttpStatusCode.Created, new SaleResponseDto { IsSuccess = true, Sale = new SaleDto { Number = 7 } });

            var result = await CreateClient(handler).CreateAsync(Request());

            Assert.Equal(7, result.Sale?.Number);
        }

        [Fact]
        public async Task CreateAsync_ConflictWithMessage_ReturnsServerMessage()
        {
            var handler = Returns(HttpStatusCode.Conflict, new SaleResponseDto { Message = "Товар «Куртка» уже продано." });

            var result = await CreateClient(handler).CreateAsync(Request());

            Assert.Equal("Товар «Куртка» уже продано.", result.Message);
        }

        [Fact]
        public async Task CreateAsync_ConflictWithMessage_IsNotSuccess()
        {
            var handler = Returns(HttpStatusCode.Conflict, new SaleResponseDto { Message = "Товар «Куртка» уже продано." });

            var result = await CreateClient(handler).CreateAsync(Request());

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task CreateAsync_ServerErrorWithoutBody_ShowsStatusCode()
        {
            var result = await CreateClient(Returns(HttpStatusCode.InternalServerError)).CreateAsync(Request());

            Assert.Equal("Сервер повернув неочікувану відповідь (500).", result.Message);
        }

        [Fact]
        public async Task CreateAsync_Unauthorized_AsksToSignInAgain()
        {
            var result = await CreateClient(Returns(HttpStatusCode.Unauthorized)).CreateAsync(Request());

            Assert.Equal("Сесія завершилась. Увійдіть знову.", result.Message);
        }

        [Fact]
        public async Task GetByNumberAsync_SendsGetWithNumber()
        {
            var handler = Returns(HttpStatusCode.OK, new SaleResponseDto { IsSuccess = true });

            await CreateClient(handler).GetByNumberAsync(12);

            Assert.Equal("GET /api/sales/12", SentRequest(handler));
        }

        [Fact]
        public async Task GetByNumberAsync_NotFoundWithoutBody_ReturnsSaleNotFoundMessage()
        {
            var result = await CreateClient(Returns(HttpStatusCode.NotFound)).GetByNumberAsync(12);

            Assert.Equal("Чек не знайдено.", result.Message);
        }

        [Fact]
        public async Task ReturnAsync_SendsPostToReturnsEndpoint()
        {
            var handler = Returns(HttpStatusCode.OK, new SaleResponseDto { IsSuccess = true });

            await CreateClient(handler).ReturnAsync(5, new ReturnSaleRequestDto { Items = [new ReturnItemDto { SaleItemId = Guid.NewGuid() }] });

            Assert.Equal("POST /api/sales/5/returns", SentRequest(handler));
        }
    }

    public class ReportApiClientTests
    {
        [Fact]
        public async Task GetSummaryAsync_SendsGetToSummaryEndpoint()
        {
            var handler = new FakeHttpHandler(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new ReportSummaryDto()) });

            await new ReportApiClient(handler.CreateClient()).GetSummaryAsync();

            Assert.Equal("/api/reports/summary", handler.LastRequest?.RequestUri?.PathAndQuery);
        }

        [Fact]
        public async Task GetSummaryAsync_ReturnsRevenueFromServer()
        {
            var handler = new FakeHttpHandler(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new ReportSummaryDto { MonthRevenue = 1500m }) });

            var summary = await new ReportApiClient(handler.CreateClient()).GetSummaryAsync();

            Assert.Equal(1500m, summary.MonthRevenue);
        }

        [Fact]
        public async Task GetSummaryAsync_Forbidden_ThrowsHttpRequestException()
        {
            var handler = new FakeHttpHandler(() => new HttpResponseMessage(HttpStatusCode.Forbidden));

            await Assert.ThrowsAsync<HttpRequestException>(() => new ReportApiClient(handler.CreateClient()).GetSummaryAsync());
        }
    }
}
