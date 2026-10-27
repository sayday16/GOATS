using System.Security.Claims;
using GoatStore.API.Server.Auth;
using GoatStore.Core.DTOs;
using GoatStore.Core.Interfaces;
using GoatStore.DataAccess.Entities;

namespace GoatStore.API.Server.Products
{
    public static class ProductEndpoints
    {
        public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
        {
            var products = app.MapGroup("/api/products").RequireAuthorization();

            products.MapGet("/", async (ProductStatus? status, ClaimsPrincipal user, IProductService productService, CancellationToken cancellationToken) =>
                TryGetStoreId(user, out var storeId)
                    ? Results.Ok(await productService.GetAllAsync(storeId, status, cancellationToken))
                    : Results.Unauthorized());

            products.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IProductService productService, CancellationToken cancellationToken) =>
                TryGetStoreId(user, out var storeId)
                    ? ToHttpResult(await productService.GetAsync(storeId, id, cancellationToken))
                    : Results.Unauthorized());

            products.MapPost("/{id:guid}/sell", async (Guid id, ClaimsPrincipal user, IProductService productService, CancellationToken cancellationToken) =>
                TryGetStoreId(user, out var storeId)
                    ? ToHttpResult(await productService.SellAsync(storeId, id, cancellationToken))
                    : Results.Unauthorized());

            products.MapPost("/", async (SaveProductRequestDto request, ClaimsPrincipal user, IProductService productService, CancellationToken cancellationToken) =>
                    TryGetStoreId(user, out var storeId)
                        ? ToCreatedResult(await productService.CreateAsync(storeId, request, cancellationToken))
                        : Results.Unauthorized())
                .RequireAuthorization(AuthSetup.AdminOnlyPolicy);

            products.MapPut("/{id:guid}", async (Guid id, SaveProductRequestDto request, ClaimsPrincipal user, IProductService productService, CancellationToken cancellationToken) =>
                    TryGetStoreId(user, out var storeId)
                        ? ToHttpResult(await productService.UpdateAsync(storeId, id, request, cancellationToken))
                        : Results.Unauthorized())
                .RequireAuthorization(AuthSetup.AdminOnlyPolicy);

            products.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, IProductService productService, CancellationToken cancellationToken) =>
                    TryGetStoreId(user, out var storeId)
                        ? ToHttpResult(await productService.DeleteAsync(storeId, id, cancellationToken))
                        : Results.Unauthorized())
                .RequireAuthorization(AuthSetup.AdminOnlyPolicy);

            return app;
        }

        private static bool TryGetStoreId(ClaimsPrincipal user, out Guid storeId) =>
            Guid.TryParse(user.FindFirstValue(GoatClaims.StoreId), out storeId);

        private static IResult ToCreatedResult(ProductResponseDto result) =>
            result.IsSuccess && result.Product is { } product
                ? Results.Created($"/api/products/{product.Id}", result)
                : ToHttpResult(result);

        private static IResult ToHttpResult(ProductResponseDto result) => result.Error switch
        {
            ProductError.None => Results.Ok(result),
            ProductError.Validation => Results.BadRequest(result),
            ProductError.NotFound => Results.NotFound(result),
            ProductError.Conflict => Results.Conflict(result),
            _ => Results.Json(result, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
