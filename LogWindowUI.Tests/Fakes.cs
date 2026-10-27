using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using GoatStore.Core.DTOs;
using GoatStore.Core.Interfaces;
using LogWindowUI.Services;

namespace LogWindowUI.Tests
{
    public sealed class FakeAuthService : IAuthService
    {
        public AuthResponseDto Response { get; set; } = new() { IsSuccess = false, Message = "Помилка." };
        public Exception? Exception { get; set; }
        public LoginRequestDto? LastLogin { get; private set; }
        public RegisterRequestDto? LastRegister { get; private set; }

        public Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
        {
            LastLogin = request;
            return Respond();
        }

        public Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
        {
            LastRegister = request;
            return Respond();
        }

        public Task<AuthResponseDto> GetUserAsync(Guid userId, CancellationToken cancellationToken = default) => Respond();

        public void Dispose()
        {
        }

        private Task<AuthResponseDto> Respond() =>
            Exception is null ? Task.FromResult(Response) : Task.FromException<AuthResponseDto>(Exception);
    }

    public sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _respond;

        public FakeHttpHandler(Func<HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        public static FakeHttpHandler Json(HttpStatusCode status, AuthResponseDto body) =>
            new(() => new HttpResponseMessage(status) { Content = JsonContent.Create(body) });

        public static FakeHttpHandler Text(HttpStatusCode status, string body) =>
            new(() => new HttpResponseMessage(status) { Content = new StringContent(body) });

        public static FakeHttpHandler Throws(Exception exception) =>
            new(() => throw exception);

        public HttpClient CreateClient() => new(this) { BaseAddress = new Uri("http://test/") };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_respond());
        }
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class AuthSessionCollection
    {
        public const string Name = "AuthSession";
    }

    public abstract class AuthSessionTestBase : IDisposable
    {
        protected AuthSessionTestBase() => AuthSession.Clear();

        public void Dispose() => AuthSession.Clear();
    }
}
