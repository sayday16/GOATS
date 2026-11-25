using System.Net;
using LogWindowUI.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace LogWindowUI.Tests
{
    [Collection(AuthSessionCollection.Name)]
    public sealed class AppLoggingTests : AuthSessionTestBase, IDisposable
    {
        private sealed class CapturingSink : ILogEventSink
        {
            public List<LogEvent> Events { get; } = new();

            public void Emit(LogEvent logEvent) => Events.Add(logEvent);
        }

        private readonly CapturingSink _sink = new();
        private readonly ILogger _previous = Log.Logger;

        public AppLoggingTests()
        {
            Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(_sink).CreateLogger();
        }

        public new void Dispose()
        {
            Log.Logger = _previous;
            base.Dispose();
        }

        private static async Task SendAsync(FakeHttpHandler inner)
        {
            var handler = new BearerTokenHandler { InnerHandler = inner };
            using var invoker = new HttpMessageInvoker(handler);
            try
            {
                await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test/api/products?search=secret"), CancellationToken.None);
            }
            catch (HttpRequestException)
            {
            }
        }

        private LogEvent SingleEvent() => _sink.Events.Single();

        [Fact]
        public async Task SendAsync_SuccessfulResponse_LogsInformation()
        {
            await SendAsync(new FakeHttpHandler(() => new HttpResponseMessage(HttpStatusCode.OK)));

            Assert.Equal(LogEventLevel.Information, SingleEvent().Level);
        }

        [Fact]
        public async Task SendAsync_ErrorResponse_LogsWarning()
        {
            await SendAsync(new FakeHttpHandler(() => new HttpResponseMessage(HttpStatusCode.Conflict)));

            Assert.Equal(LogEventLevel.Warning, SingleEvent().Level);
        }

        [Fact]
        public async Task SendAsync_ErrorResponse_LogsStatusCode()
        {
            await SendAsync(new FakeHttpHandler(() => new HttpResponseMessage(HttpStatusCode.Conflict)));

            Assert.Equal("409", SingleEvent().Properties["Status"].ToString());
        }

        [Fact]
        public async Task SendAsync_ServerUnreachable_LogsError()
        {
            await SendAsync(FakeHttpHandler.Throws(new HttpRequestException("Connection refused")));

            Assert.Equal(LogEventLevel.Error, SingleEvent().Level);
        }

        [Fact]
        public async Task SendAsync_AnyRequest_DoesNotLogQueryString()
        {
            await SendAsync(new FakeHttpHandler(() => new HttpResponseMessage(HttpStatusCode.OK)));

            Assert.DoesNotContain("secret", SingleEvent().RenderMessage());
        }

        [Fact]
        public async Task SendAsync_ActiveSession_DoesNotLogToken()
        {
            AuthSession.Start(new GoatStore.Core.DTOs.AuthResponseDto
            {
                IsSuccess = true,
                AccessToken = "header.payload.signature",
                AccessTokenExpiresAt = DateTime.UtcNow.AddHours(1)
            });

            await SendAsync(new FakeHttpHandler(() => new HttpResponseMessage(HttpStatusCode.OK)));

            Assert.DoesNotContain("signature", SingleEvent().RenderMessage());
        }

        [Fact]
        public void UnexpectedErrorMessage_PointsToLogDirectory()
        {
            Assert.Contains(AppLog.LogDirectory, AppLog.UnexpectedErrorMessage);
        }
    }
}
