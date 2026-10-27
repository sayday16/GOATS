using System.Net;
using GoatStore.Core.DTOs;
using LogWindowUI.Services;

namespace LogWindowUI.Tests
{
    [Collection(AuthSessionCollection.Name)]
    public class BearerTokenHandlerTests : AuthSessionTestBase
    {
        private readonly FakeHttpHandler _inner = new(() => new HttpResponseMessage(HttpStatusCode.OK));

        private Task SendAsync()
        {
            var handler = new BearerTokenHandler { InnerHandler = _inner };
            using var invoker = new HttpMessageInvoker(handler);
            return invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test/api/auth/me"), CancellationToken.None);
        }

        private static void StartSession(DateTime expiresAt) => AuthSession.Start(new AuthResponseDto
        {
            IsSuccess = true,
            AccessToken = "header.payload.signature",
            AccessTokenExpiresAt = expiresAt
        });

        [Fact]
        public async Task SendAsync_ActiveSession_AddsBearerHeader()
        {
            StartSession(DateTime.UtcNow.AddHours(1));

            await SendAsync();

            var authorization = _inner.LastRequest?.Headers.Authorization;
            Assert.Equal("Bearer", authorization?.Scheme);
            Assert.Equal("header.payload.signature", authorization?.Parameter);
        }

        [Fact]
        public async Task SendAsync_NoSession_NoHeader()
        {
            await SendAsync();

            Assert.Null(_inner.LastRequest?.Headers.Authorization);
        }

        [Fact]
        public async Task SendAsync_ExpiredSession_NoHeader()
        {
            StartSession(DateTime.UtcNow.AddMinutes(-1));

            await SendAsync();

            Assert.False(AuthSession.IsActive);
            Assert.Null(_inner.LastRequest?.Headers.Authorization);
        }
    }
}
