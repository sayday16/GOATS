using System.Collections.Concurrent;
using System.Net.Http.Json;
using GoatStore.API.Server.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GoatStore.API.Tests;

public class AuditLogTests : IClassFixture<TestApplicationFactory>
{
    private const string AuditCategory = "GoatStore.API.Server.Logging.AuditLog";

    private readonly TestApplicationFactory _factory;
    private readonly CapturingLoggerProvider _logs = new();

    public AuditLogTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    private sealed record Entry(LogLevel Level, string Category, string Message);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<Entry> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(string category, ConcurrentQueue<Entry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                entries.Enqueue(new Entry(logLevel, category, formatter(state, exception)));
        }
    }

    private HttpClient CreateClient() =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(_logs))).CreateClient();

    private IEnumerable<Entry> AuditEntries() => _logs.Entries.Where(e => e.Category == AuditCategory);

    private string AllMessages() => string.Join(Environment.NewLine, _logs.Entries.Select(e => e.Message));

    private async Task<string> LoginWithWrongPasswordAsync()
    {
        var client = CreateClient();
        var email = ApiTestHelpers.UniqueEmail("owner");
        await client.RegisterAndLoginAsync(email, ApiTestHelpers.OwnerRole, ApiTestHelpers.UniqueStoreName());
        await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "wrong-password-123" });
        return email;
    }

    private async Task<HttpClient> LoggedInOwnerAsync()
    {
        var client = CreateClient();
        var token = await client.RegisterAndLoginAsync(ApiTestHelpers.UniqueEmail("owner"), ApiTestHelpers.OwnerRole, ApiTestHelpers.UniqueStoreName());
        client.UseToken(token);
        return client;
    }

    [Fact]
    public async Task Login_WrongPassword_WritesWarning()
    {
        await LoginWithWrongPasswordAsync();

        Assert.Contains(AuditEntries(), e => e.Level == LogLevel.Warning && e.Message.StartsWith("Login failed"));
    }

    [Fact]
    public async Task Login_WrongPassword_DoesNotLogFullEmail()
    {
        var email = await LoginWithWrongPasswordAsync();

        Assert.DoesNotContain(email, AllMessages());
    }

    [Fact]
    public async Task Login_AnyResult_NeverLogsPassword()
    {
        await LoginWithWrongPasswordAsync();

        Assert.DoesNotContain("wrong-password-123", AllMessages());
    }

    [Fact]
    public async Task Login_Success_WritesInformation()
    {
        await LoggedInOwnerAsync();

        Assert.Contains(AuditEntries(), e => e.Level == LogLevel.Information && e.Message.StartsWith("Login succeeded"));
    }

    [Fact]
    public async Task Login_Success_NeverLogsAccessToken()
    {
        var client = CreateClient();
        var token = await client.RegisterAndLoginAsync(ApiTestHelpers.UniqueEmail("owner"), ApiTestHelpers.OwnerRole, ApiTestHelpers.UniqueStoreName());

        Assert.DoesNotContain(token, AllMessages());
    }

    [Fact]
    public async Task PostProduct_AsOwner_LogsCreation()
    {
        var client = await LoggedInOwnerAsync();

        await client.CreateProductAsync();

        Assert.Contains(AuditEntries(), e => e.Message.StartsWith("Product") && e.Message.Contains("created"));
    }

    [Fact]
    public async Task PostSale_Success_LogsReceiptNumber()
    {
        var client = await LoggedInOwnerAsync();
        var productId = await client.CreateProductAsync();

        await client.PostAsJsonAsync("/api/sales", new { Items = new[] { new { ProductId = productId, Quantity = 1 } }, PaymentMethod = 0 });

        Assert.Contains(AuditEntries(), e => e.Message.StartsWith("Sale") && e.Message.Contains("created") && e.Message.Contains("receipt 1,"));
    }

    [Fact]
    public async Task PostSale_ProductAlreadySold_LogsRejection()
    {
        var client = await LoggedInOwnerAsync();
        var productId = await client.CreateProductAsync();
        var request = new { Items = new[] { new { ProductId = productId, Quantity = 1 } }, PaymentMethod = 0 };
        await client.PostAsJsonAsync("/api/sales", request);

        await client.PostAsJsonAsync("/api/sales", request);

        Assert.Contains(AuditEntries(), e => e.Level == LogLevel.Warning && e.Message.StartsWith("Sale not"));
    }

    [Fact]
    public async Task AnyRequest_WritesRequestLog()
    {
        var client = await LoggedInOwnerAsync();

        await client.GetAsync("/api/products");

        Assert.Contains(_logs.Entries, e => e.Message.Contains("/api/products") && e.Message.Contains("responded 200"));
    }

    [Theory]
    [InlineData("owner@gmail.com", "o***@gmail.com")]
    [InlineData("  a@b.ua ", "a***@b.ua")]
    [InlineData("no-at-sign", "***")]
    [InlineData("", "(empty)")]
    [InlineData(null, "(empty)")]
    public void MaskEmail_HidesLocalPart(string? email, string expected)
    {
        Assert.Equal(expected, AuditLog.MaskEmail(email));
    }
}
