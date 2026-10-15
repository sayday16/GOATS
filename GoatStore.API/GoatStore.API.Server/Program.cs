using System.Threading.RateLimiting;
using GoatStore.Core.DTOs;
using GoatStore.Core.Interfaces;
using GoatStore.Core.Services;
using GoatStore.DataAccess;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();

builder.Services.AddDbContext<GoatStoreDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        await context.HttpContext.Response.WriteAsJsonAsync(
            new AuthResponseDto { IsSuccess = false, Message = "Забагато спроб. Спробуйте ще раз за хвилину." },
            cancellationToken);
    };

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Environment.IsDevelopment() ? 100 : 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseRateLimiter();

var authGroup = app.MapGroup("/api/auth").RequireRateLimiting("auth");

authGroup.MapPost("/register", async (RegisterRequestDto request, IAuthService authService, CancellationToken cancellationToken) =>
    ToHttpResult(await authService.RegisterAsync(request, cancellationToken)));

authGroup.MapPost("/login", async (LoginRequestDto request, IAuthService authService, CancellationToken cancellationToken) =>
    ToHttpResult(await authService.LoginAsync(request, cancellationToken)));

app.MapDefaultEndpoints();
app.UseFileServer();

app.Run();

static IResult ToHttpResult(AuthResponseDto result) => result.Error switch
{
    AuthError.None => Results.Ok(result),
    AuthError.Validation => Results.BadRequest(result),
    AuthError.Unauthorized => Results.Json(result, statusCode: StatusCodes.Status401Unauthorized),
    AuthError.Forbidden => Results.Json(result, statusCode: StatusCodes.Status403Forbidden),
    AuthError.Conflict => Results.Conflict(result),
    _ => Results.Json(result, statusCode: StatusCodes.Status500InternalServerError)
};