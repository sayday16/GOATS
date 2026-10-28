using GoatStore.API.Server.Auth;
using GoatStore.Core.DTOs;
using GoatStore.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GoatStore.API.Tests;

public class WarmUpAuthTests
{
    private sealed class FakeAuthService(Exception? exception = null) : IAuthService
    {
        public LoginRequestDto? LastLogin { get; private set; }

        public Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
        {
            LastLogin = request;
            return exception is null
                ? Task.FromResult(new AuthResponseDto())
                : Task.FromException<AuthResponseDto>(exception);
        }

        public Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AuthResponseDto> GetUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Dispose()
        {
        }
    }

    private static WebApplication BuildApp(FakeAuthService authService)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IAuthService>(authService);
        return builder.Build();
    }

    [Fact]
    public async Task WarmUpAuthAsync_MakesTrialLogin()
    {
        var authService = new FakeAuthService();
        await using var app = BuildApp(authService);

        await app.WarmUpAuthAsync();

        Assert.Equal("warmup@goatstore.local", authService.LastLogin?.Email);
    }

    [Fact]
    public async Task WarmUpAuthAsync_DatabaseUnavailable_DoesNotThrow()
    {
        await using var app = BuildApp(new FakeAuthService(new InvalidOperationException("DB is down")));

        var exception = await Record.ExceptionAsync(() => app.WarmUpAuthAsync());

        Assert.Null(exception);
    }
}
