using System.Net;
using GoatStore.Core.DTOs;
using GoatStore.DataAccess.Entities;
using LogWindowUI.Services;

namespace LogWindowUI.Tests
{
    [Collection(AuthSessionCollection.Name)]
    public class AuthApiClientTests : AuthSessionTestBase
    {
        private static readonly LoginRequestDto Login = new() { Email = "owner@test.com", Password = "password123" };

        private static AuthResponseDto SuccessBody() => new()
        {
            IsSuccess = true,
            Message = "Вхід виконано успішно!",
            UserId = Guid.NewGuid(),
            Email = "owner@test.com",
            Role = UserRole.Admin,
            AccessToken = "header.payload.signature",
            AccessTokenExpiresAt = DateTime.UtcNow.AddHours(8)
        };

        [Fact]
        public async Task LoginAsync_Success_StartsSession()
        {
            var handler = FakeHttpHandler.Json(HttpStatusCode.OK, SuccessBody());

            var result = await new AuthApiClient(handler.CreateClient()).LoginAsync(Login);

            Assert.True(result.IsSuccess);
            Assert.True(AuthSession.IsActive);
            Assert.Equal("header.payload.signature", AuthSession.AccessToken);
            Assert.Equal("owner@test.com", AuthSession.User?.Email);
        }

        [Fact]
        public async Task LoginAsync_ErrorWithBody_ReturnsServerMessage()
        {
            var handler = FakeHttpHandler.Json(HttpStatusCode.Unauthorized,
                new AuthResponseDto { IsSuccess = false, Message = "Невірний Email або пароль." });

            var result = await new AuthApiClient(handler.CreateClient()).LoginAsync(Login);

            Assert.False(result.IsSuccess);
            Assert.Equal("Невірний Email або пароль.", result.Message);
        }

        [Fact]
        public async Task LoginAsync_Failure_DoesNotStartSession()
        {
            var handler = FakeHttpHandler.Json(HttpStatusCode.Unauthorized,
                new AuthResponseDto { IsSuccess = false, Message = "Невірний Email або пароль." });

            await new AuthApiClient(handler.CreateClient()).LoginAsync(Login);

            Assert.False(AuthSession.IsActive);
            Assert.Null(AuthSession.AccessToken);
        }

        [Theory]
        [InlineData(HttpStatusCode.TooManyRequests, "Забагато спроб. Спробуйте ще раз за хвилину.")]
        [InlineData(HttpStatusCode.Unauthorized, "Сесія завершилась. Увійдіть знову.")]
        [InlineData(HttpStatusCode.InternalServerError, "Сервер повернув неочікувану відповідь (500).")]
        public async Task LoginAsync_ResponseWithoutJson_ReturnsMessageByStatus(HttpStatusCode status, string expectedMessage)
        {
            var handler = FakeHttpHandler.Text(status, "<html>error</html>");

            var result = await new AuthApiClient(handler.CreateClient()).LoginAsync(Login);

            Assert.False(result.IsSuccess);
            Assert.Equal(expectedMessage, result.Message);
            Assert.False(AuthSession.IsActive);
        }

        [Fact]
        public async Task LoginAsync_ServerUnreachable_Throws()
        {
            var handler = FakeHttpHandler.Throws(new HttpRequestException("Connection refused"));

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                new AuthApiClient(handler.CreateClient()).LoginAsync(Login));
        }

        [Fact]
        public async Task LoginAsync_PostsToLoginEndpoint()
        {
            var handler = FakeHttpHandler.Json(HttpStatusCode.OK, SuccessBody());

            await new AuthApiClient(handler.CreateClient()).LoginAsync(Login);

            Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
            Assert.Equal("/api/auth/login", handler.LastRequest?.RequestUri?.AbsolutePath);
        }

        [Fact]
        public async Task RegisterAsync_PostsToRegisterEndpoint_AndDoesNotStartSession()
        {
            var handler = FakeHttpHandler.Json(HttpStatusCode.OK, SuccessBody());

            await new AuthApiClient(handler.CreateClient()).RegisterAsync(new RegisterRequestDto());

            Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
            Assert.Equal("/api/auth/register", handler.LastRequest?.RequestUri?.AbsolutePath);
            Assert.False(AuthSession.IsActive);
        }

        [Fact]
        public async Task GetUserAsync_GetsMeEndpoint()
        {
            var handler = FakeHttpHandler.Json(HttpStatusCode.OK, SuccessBody());

            var result = await new AuthApiClient(handler.CreateClient()).GetUserAsync(Guid.NewGuid());

            Assert.True(result.IsSuccess);
            Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
            Assert.Equal("/api/auth/me", handler.LastRequest?.RequestUri?.AbsolutePath);
        }
    }
}
