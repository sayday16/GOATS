using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using GoatStore.API.Server.Errors;
using GoatStore.Core.DTOs;
using GoatStore.Core.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace GoatStore.API.Tests;

public class ErrorHandlingTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public ErrorHandlingTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    private sealed class TransientDbException() : DbException("connection lost")
    {
        public override bool IsTransient => true;
    }

    private sealed class ThrowingProductService(Exception exception) : IProductService
    {
        public Task<IReadOnlyList<ProductDto>> GetAllAsync(Guid storeId, ProductFilterDto? filter = null, CancellationToken cancellationToken = default) => throw exception;
        public Task<ProductFilterOptionsDto> GetFilterOptionsAsync(Guid storeId, CancellationToken cancellationToken = default) => throw exception;
        public Task<ProductResponseDto> GetAsync(Guid storeId, Guid productId, CancellationToken cancellationToken = default) => throw exception;
        public Task<ProductResponseDto> CreateAsync(Guid storeId, SaveProductRequestDto request, CancellationToken cancellationToken = default) => throw exception;
        public Task<ProductResponseDto> UpdateAsync(Guid storeId, Guid productId, SaveProductRequestDto request, CancellationToken cancellationToken = default) => throw exception;
        public Task<ProductResponseDto> SellAsync(Guid storeId, Guid productId, CancellationToken cancellationToken = default) => throw exception;
        public Task<ProductResponseDto> DeleteAsync(Guid storeId, Guid productId, CancellationToken cancellationToken = default) => throw exception;
    }

    private async Task<HttpResponseMessage> GetProductsWhenServiceThrowsAsync(Exception exception)
    {
        var client = _factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
                services.AddScoped<IProductService>(_ => new ThrowingProductService(exception))))
            .CreateClient();

        var token = await client.RegisterAndLoginAsync(ApiTestHelpers.UniqueEmail("owner"), ApiTestHelpers.OwnerRole, ApiTestHelpers.UniqueStoreName());
        client.UseToken(token);
        return await client.GetAsync("/api/products");
    }

    private static async Task<string?> MessageOf(HttpResponseMessage response) =>
        (string?)(await response.Content.ReadFromJsonAsync<JsonObject>())?["message"];

    [Fact]
    public async Task GetProducts_DatabaseUnavailable_ReturnsServiceUnavailable()
    {
        var response = await GetProductsWhenServiceThrowsAsync(new NpgsqlException("Connection refused"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task GetProducts_DatabaseUnavailable_ExplainsItInMessage()
    {
        var response = await GetProductsWhenServiceThrowsAsync(new NpgsqlException("Connection refused"));

        Assert.Equal(ApiExceptionHandler.DatabaseUnavailableMessage, await MessageOf(response));
    }

    [Fact]
    public async Task GetProducts_UnexpectedError_ReturnsInternalServerError()
    {
        var response = await GetProductsWhenServiceThrowsAsync(new InvalidOperationException("bug"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task GetProducts_UnexpectedError_ReturnsReadableMessage()
    {
        var response = await GetProductsWhenServiceThrowsAsync(new InvalidOperationException("bug"));

        Assert.Equal(ApiExceptionHandler.UnexpectedErrorMessage, await MessageOf(response));
    }

    [Fact]
    public async Task GetProducts_UnexpectedError_DoesNotLeakExceptionText()
    {
        var response = await GetProductsWhenServiceThrowsAsync(new InvalidOperationException("secret internal detail"));

        Assert.DoesNotContain("secret internal detail", await response.Content.ReadAsStringAsync());
    }

    public static TheoryData<Exception> UnavailableExceptions => new()
    {
        new NpgsqlException("Connection refused"),
        new PostgresException("connection failure", "FATAL", "FATAL", "08006"),
        new PostgresException("too many connections", "FATAL", "FATAL", "53300"),
        new PostgresException("cannot connect now", "FATAL", "FATAL", "57P03"),
        new TimeoutException(),
        new TransientDbException(),
        new InvalidOperationException("wrapper", new NpgsqlException("Connection refused"))
    };

    public static TheoryData<Exception> OtherExceptions => new()
    {
        new PostgresException("duplicate key", "ERROR", "ERROR", "23505"),
        new InvalidOperationException("bug"),
        new ArgumentException("bad argument")
    };

    [Theory]
    [MemberData(nameof(UnavailableExceptions))]
    public void IsDatabaseUnavailable_ConnectionProblem_ReturnsTrue(Exception exception)
    {
        Assert.True(ApiExceptionHandler.IsDatabaseUnavailable(exception));
    }

    [Theory]
    [MemberData(nameof(OtherExceptions))]
    public void IsDatabaseUnavailable_OtherError_ReturnsFalse(Exception exception)
    {
        Assert.False(ApiExceptionHandler.IsDatabaseUnavailable(exception));
    }
}
