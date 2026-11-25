using System.Security.Claims;
using GoatStore.API.Server.Auth;
using GoatStore.API.Server.Logging;
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

            products.MapGet("/", async (string? search, string? category, string? brand, ProductStatus? status,
                    ClaimsPrincipal user, IProductService productService, CancellationToken cancellationToken) =>
                TryGetStoreId(user, out var storeId)
                    ? Results.Ok(await productService.GetAllAsync(storeId,
                        new ProductFilterDto { Search = search, Category = category, Brand = brand, Status = status },
                        cancellationToken))
                    : Results.Unauthorized());

            products.MapGet("/filters", async (ClaimsPrincipal user, IProductService productService, CancellationToken cancellationToken) =>
                TryGetStoreId(user, out var storeId)
                    ? Results.Ok(await productService.GetFilterOptionsAsync(storeId, cancellationToken))
                    : Results.Unauthorized());

            products.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IProductService productService, CancellationToken cancellationToken) =>
                TryGetStoreId(user, out var storeId)
                    ? ToHttpResult(await productService.GetAsync(storeId, id, cancellationToken))
                    : Results.Unauthorized());

            products.MapPost("/{id:guid}/sell", async (Guid id, ClaimsPrincipal user, IProductService productService, AuditLog audit, CancellationToken cancellationToken) =>
            {
                if (!TryGetStoreId(user, out var storeId))
                    return Results.Unauthorized();

                var result = await productService.SellAsync(storeId, id, cancellationToken);
                audit.Product("sold", user, id, result);
                return ToHttpResult(result);
            });

            products.MapPost("/", async (SaveProductRequestDto request, ClaimsPrincipal user, IProductService productService, AuditLog audit, CancellationToken cancellationToken) =>
                {
                    if (!TryGetStoreId(user, out var storeId))
                        return Results.Unauthorized();

                    var result = await productService.CreateAsync(storeId, request, cancellationToken);
                    audit.Product("created", user, null, result);
                    return ToCreatedResult(result);
                })
                .RequireAuthorization(AuthSetup.AdminOnlyPolicy);

            products.MapPut("/{id:guid}", async (Guid id, SaveProductRequestDto request, ClaimsPrincipal user, IProductService productService, AuditLog audit, CancellationToken cancellationToken) =>
                {
                    if (!TryGetStoreId(user, out var storeId))
                        return Results.Unauthorized();

                    var result = await productService.UpdateAsync(storeId, id, request, cancellationToken);
                    audit.Product("updated", user, id, result);
                    return ToHttpResult(result);
                })
                .RequireAuthorization(AuthSetup.AdminOnlyPolicy);

            products.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, IProductService productService, AuditLog audit, CancellationToken cancellationToken) =>
                {
                    if (!TryGetStoreId(user, out var storeId))
                        return Results.Unauthorized();

                    var result = await productService.DeleteAsync(storeId, id, cancellationToken);
                    audit.Product("deleted", user, id, result);
                    return ToHttpResult(result);
                })
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
