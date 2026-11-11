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
        public TaskCompletionSource<AuthResponseDto>? Pending { get; set; }
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

        private Task<AuthResponseDto> Respond()
        {
            if (Pending is not null) return Pending.Task;
            return Exception is null ? Task.FromResult(Response) : Task.FromException<AuthResponseDto>(Exception);
        }
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


    public sealed class FakeDialogService : IDialogService
    {
        public string? LastMessage { get; private set; }

        public void ShowInfo(string title, string message) => LastMessage = message;

        public void ShowWarning(string title, string message) => LastMessage = message;

        public void ShowError(string title, string message) => LastMessage = message;
    }

    public sealed class FakeProductApiClient : IProductApiClient
    {
        public List<ProductDto> Products { get; } = new();
        public Exception? GetAllException { get; set; }
        public ProductFilterDto? LastFilter { get; private set; }
        public Dictionary<Guid, string> FailingSales { get; } = new();
        public List<Guid> SoldIds { get; } = new();

        public Task<IReadOnlyList<ProductDto>> GetAllAsync(ProductFilterDto? filter = null, CancellationToken cancellationToken = default)
        {
            LastFilter = filter;
            if (GetAllException is not null)
                return Task.FromException<IReadOnlyList<ProductDto>>(GetAllException);

            IReadOnlyList<ProductDto> result = Products.ToList();
            return Task.FromResult(result);
        }

        public Task<ProductFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProductFilterOptionsDto());

        public Task<ProductResponseDto> GetAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProductResponseDto { IsSuccess = true });

        public Task<ProductResponseDto> CreateAsync(SaveProductRequestDto request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProductResponseDto { IsSuccess = true });

        public Task<ProductResponseDto> UpdateAsync(Guid productId, SaveProductRequestDto request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProductResponseDto { IsSuccess = true });

        public Task<ProductResponseDto> SellAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            if (FailingSales.TryGetValue(productId, out var message))
                return Task.FromResult(new ProductResponseDto { IsSuccess = false, Message = message });

            SoldIds.Add(productId);
            return Task.FromResult(new ProductResponseDto { IsSuccess = true, Message = "Товар продано." });
        }

        public Task<ProductResponseDto> DeleteAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProductResponseDto { IsSuccess = true });
    }
}