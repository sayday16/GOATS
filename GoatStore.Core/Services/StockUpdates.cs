using GoatStore.DataAccess;
using GoatStore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoatStore.Core.Services
{
    internal static class StockUpdates
    {
        public static async Task<bool> TryTakeAsync(
            GoatStoreDbContext context, Guid storeId, Guid productId, int quantity, DateTime soldAt,
            CancellationToken cancellationToken)
        {
            var taken = await context.Products
                .Where(p => p.Id == productId && p.StoreId == storeId && p.Quantity - p.ReservedQuantity >= quantity)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.Quantity, p => p.Quantity - quantity),
                    cancellationToken);

            if (taken == 0)
                return false;

            await context.Products
                .Where(p => p.Id == productId && p.Quantity == 0 && p.Status == ProductStatus.InStock)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.Status, ProductStatus.Sold)
                    .SetProperty(p => p.SoldDate, soldAt),
                    cancellationToken);

            return true;
        }

        public static Task<int> RestoreAsync(
            GoatStoreDbContext context, Guid storeId, Guid productId, int quantity, CancellationToken cancellationToken) =>
            context.Products
                .Where(p => p.Id == productId && p.StoreId == storeId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.Quantity, p => p.Quantity + quantity)
                    .SetProperty(p => p.Status, ProductStatus.InStock)
                    .SetProperty(p => p.SoldDate, (DateTime?)null),
                    cancellationToken);
    }
}
