using System.Security.Claims;
using GoatStore.API.Server.Auth;
using GoatStore.API.Server.Logging;
using GoatStore.Core.DTOs;
using GoatStore.Core.Interfaces;

namespace GoatStore.API.Server.Sales
{
    public static class SaleEndpoints
    {
        public static IEndpointRouteBuilder MapSaleEndpoints(this IEndpointRouteBuilder app)
        {
            var sales = app.MapGroup("/api/sales").RequireAuthorization();

            sales.MapPost("/", async (CreateSaleRequestDto request, ClaimsPrincipal user, ISaleService saleService, AuditLog audit, CancellationToken cancellationToken) =>
            {
                if (!TryGetIds(user, out var storeId, out var userId))
                    return Results.Unauthorized();

                var result = await saleService.CreateAsync(storeId, userId, request, cancellationToken);
                audit.Sale("created", user, null, result);
                return ToCreatedResult(result);
            });

            sales.MapGet("/{number:int}", async (int number, ClaimsPrincipal user, ISaleService saleService, CancellationToken cancellationToken) =>
                TryGetIds(user, out var storeId, out _)
                    ? ToHttpResult(await saleService.GetByNumberAsync(storeId, number, cancellationToken))
                    : Results.Unauthorized());

            sales.MapPost("/{number:int}/returns", async (int number, ReturnSaleRequestDto request, ClaimsPrincipal user, ISaleService saleService, AuditLog audit, CancellationToken cancellationToken) =>
            {
                if (!TryGetIds(user, out var storeId, out _))
                    return Results.Unauthorized();

                var result = await saleService.ReturnAsync(storeId, number, request, cancellationToken);
                audit.Sale("returned", user, number, result);
                return ToHttpResult(result);
            });

            return app;
        }

        private static bool TryGetIds(ClaimsPrincipal user, out Guid storeId, out Guid userId)
        {
            userId = Guid.Empty;
            return Guid.TryParse(user.FindFirstValue(GoatClaims.StoreId), out storeId)
                && Guid.TryParse(user.FindFirstValue(GoatClaims.UserId), out userId);
        }

        private static IResult ToCreatedResult(SaleResponseDto result) =>
            result.IsSuccess && result.Sale is { } sale
                ? Results.Created($"/api/sales/{sale.Number}", result)
                : ToHttpResult(result);

        private static IResult ToHttpResult(SaleResponseDto result) => result.Error switch
        {
            SaleError.None => Results.Ok(result),
            SaleError.Validation => Results.BadRequest(result),
            SaleError.NotFound => Results.NotFound(result),
            SaleError.Conflict => Results.Conflict(result),
            _ => Results.Json(result, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
