using System.Security.Claims;
using GoatStore.Core.DTOs;
using GoatStore.Core.Interfaces;

namespace GoatStore.API.Server.Auth
{
    public static class AuthEndpoints
    {
        public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
        {
            var auth = app.MapGroup("/api/auth");

            auth.MapPost("/register", async (RegisterRequestDto request, IAuthService authService, JwtTokenService tokens, CancellationToken cancellationToken) =>
                    ToHttpResult(tokens.WithAccessToken(await authService.RegisterAsync(request, cancellationToken))))
                .RequireRateLimiting(AuthSetup.AuthRateLimitPolicy);

            auth.MapPost("/login", async (LoginRequestDto request, IAuthService authService, JwtTokenService tokens, CancellationToken cancellationToken) =>
                    ToHttpResult(tokens.WithAccessToken(await authService.LoginAsync(request, cancellationToken))))
                .RequireRateLimiting(AuthSetup.AuthRateLimitPolicy);

            auth.MapGet("/me", async (ClaimsPrincipal user, IAuthService authService, CancellationToken cancellationToken) =>
                    Guid.TryParse(user.FindFirstValue(GoatClaims.UserId), out var userId)
                        ? ToHttpResult(await authService.GetUserAsync(userId, cancellationToken))
                        : Results.Unauthorized())
                .RequireAuthorization();

            return app;
        }

        private static IResult ToHttpResult(AuthResponseDto result) => result.Error switch
        {
            AuthError.None => Results.Ok(result),
            AuthError.Validation => Results.BadRequest(result),
            AuthError.Unauthorized => Results.Json(result, statusCode: StatusCodes.Status401Unauthorized),
            AuthError.Forbidden => Results.Json(result, statusCode: StatusCodes.Status403Forbidden),
            AuthError.Conflict => Results.Conflict(result),
            _ => Results.Json(result, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
