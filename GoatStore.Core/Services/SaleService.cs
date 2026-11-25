using GoatStore.Core.DTOs;
using GoatStore.Core.Interfaces;
using GoatStore.DataAccess;
using GoatStore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoatStore.Core.Services
{
    internal static class SaleMath
    {
        public static decimal Subtotal(Sale sale) => sale.Items.Sum(i => i.UnitPrice * i.Quantity);

        public static decimal Total(Sale sale) => Subtotal(sale) - sale.Discount;

        public static decimal DiscountRatio(Sale sale)
        {
            var subtotal = Subtotal(sale);
            return subtotal == 0 ? 0 : (subtotal - sale.Discount) / subtotal;
        }

        public static decimal Revenue(Sale sale) =>
            Math.Round(sale.Items.Sum(i => i.UnitPrice * (i.Quantity - i.ReturnedQuantity)) * DiscountRatio(sale), 2);

        public static decimal Refunded(Sale sale) =>
            Math.Round(sale.Items.Sum(i => i.UnitPrice * i.ReturnedQuantity) * DiscountRatio(sale), 2);

        public static decimal Cost(Sale sale) =>
            sale.Items.Sum(i => i.PurchasePrice * (i.Quantity - i.ReturnedQuantity));
    }

    public class SaleService : ISaleService
    {
        private const int MaxNumberAttempts = 3;

        private readonly GoatStoreDbContext _context;
        private readonly TimeProvider _timeProvider;

        public SaleService(GoatStoreDbContext context, TimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<SaleResponseDto> CreateAsync(
            Guid storeId, Guid userId, CreateSaleRequestDto request, CancellationToken cancellationToken = default)
        {
            var lines = MergeLines(request.Items.Select(i => (i.ProductId, i.Quantity)));
            if (lines.Count == 0)
                return Fail(SaleError.Validation, "Чек порожній.");

            if (lines.Any(l => l.Quantity < 1))
                return Fail(SaleError.Validation, "Кількість має бути не меншою за 1.");

            if (!Enum.IsDefined(request.PaymentMethod))
                return Fail(SaleError.Validation, "Оберіть спосіб оплати: картка або готівка.");

            if (request.Discount < 0)
                return Fail(SaleError.Validation, "Знижка не може бути від'ємною.");

            var ids = lines.Select(l => l.ProductId).ToList();
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => p.StoreId == storeId && ids.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            foreach (var line in lines)
            {
                if (!products.TryGetValue(line.ProductId, out var product))
                    return Fail(SaleError.NotFound, "Товар не знайдено.");

                if (product.IsUnique && line.Quantity > 1)
                    return Fail(SaleError.Validation, $"Товар «{product.Title}» унікальний: його можна продати лише в одному екземплярі.");
            }

            var subtotal = lines.Sum(l => products[l.ProductId].SalePrice * l.Quantity);
            if (request.Discount > subtotal)
                return Fail(SaleError.Validation, "Знижка не може перевищувати суму чека.");

            for (var attempt = 1; attempt <= MaxNumberAttempts; attempt++)
            {
                var result = await TryCreateAsync(storeId, userId, request, lines, products, attempt < MaxNumberAttempts, cancellationToken);
                if (result is not null)
                    return result;
            }

            return Fail(SaleError.Conflict, "Не вдалося створити чек. Спробуйте ще раз.");
        }

        public async Task<SaleResponseDto> GetByNumberAsync(
            Guid storeId, int number, CancellationToken cancellationToken = default)
        {
            var sale = await LoadAsync(storeId, number, cancellationToken);

            return sale is null ? NotFound() : Success(sale, "OK");
        }

        public async Task<SaleResponseDto> ReturnAsync(
            Guid storeId, int number, ReturnSaleRequestDto request, CancellationToken cancellationToken = default)
        {
            var lines = MergeLines(request.Items.Select(i => (i.SaleItemId, i.Quantity)));
            if (lines.Count == 0)
                return Fail(SaleError.Validation, "Оберіть товари для повернення.");

            if (lines.Any(l => l.Quantity < 1))
                return Fail(SaleError.Validation, "Кількість має бути не меншою за 1.");

            var sale = await LoadAsync(storeId, number, cancellationToken);
            if (sale is null)
                return NotFound();

            var items = sale.Items.ToDictionary(i => i.Id);
            if (lines.Any(l => !items.ContainsKey(l.ProductId)))
                return Fail(SaleError.Validation, "Цього товару немає в чеку.");

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            foreach (var line in lines)
            {
                var item = items[line.ProductId];
                var returned = await _context.SaleItems
                    .Where(i => i.Id == item.Id && i.Quantity - i.ReturnedQuantity >= line.Quantity)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(i => i.ReturnedQuantity, i => i.ReturnedQuantity + line.Quantity),
                        cancellationToken);

                if (returned == 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Fail(SaleError.Conflict, $"Товар «{item.Product?.Title}» уже повернуто.");
                }

                await StockUpdates.RestoreAsync(_context, storeId, item.ProductId, line.Quantity, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            _context.ChangeTracker.Clear();
            var updated = await LoadAsync(storeId, number, cancellationToken);

            return Success(updated!, "Повернення оформлено.");
        }

        private async Task<SaleResponseDto?> TryCreateAsync(
            Guid storeId, Guid userId, CreateSaleRequestDto request, List<(Guid ProductId, int Quantity)> lines,
            Dictionary<Guid, Product> products, bool canRetry, CancellationToken cancellationToken)
        {
            var soldAt = _timeProvider.GetUtcNow().UtcDateTime;

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            foreach (var line in lines)
            {
                var taken = await StockUpdates.TryTakeAsync(_context, storeId, line.ProductId, line.Quantity, soldAt, cancellationToken);
                if (!taken)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    var title = products[line.ProductId].Title;
                    return Fail(SaleError.Conflict, products[line.ProductId].IsUnique
                        ? $"Товар «{title}» уже продано."
                        : $"Недостатньо товару «{title}» на складі.");
                }
            }

            var lastNumber = await _context.Sales
                .Where(s => s.StoreId == storeId)
                .MaxAsync(s => (int?)s.Number, cancellationToken) ?? 0;

            var sale = new Sale
            {
                StoreId = storeId,
                SoldByUserId = userId,
                Number = lastNumber + 1,
                PaymentMethod = request.PaymentMethod,
                Discount = request.Discount,
                CreatedAt = soldAt,
                Items = lines.Select(l => new SaleItem
                {
                    ProductId = l.ProductId,
                    Quantity = l.Quantity,
                    UnitPrice = products[l.ProductId].SalePrice,
                    PurchasePrice = products[l.ProductId].PurchasePrice
                }).ToList()
            };

            _context.Sales.Add(sale);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException) when (canRetry)
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                return null;
            }

            _context.ChangeTracker.Clear();
            var saved = await LoadAsync(storeId, sale.Number, cancellationToken);

            return Success(saved!, "Чек сформовано.");
        }

        private Task<Sale?> LoadAsync(Guid storeId, int number, CancellationToken cancellationToken) =>
            _context.Sales
                .AsNoTracking()
                .Include(s => s.Items).ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(s => s.StoreId == storeId && s.Number == number, cancellationToken);

        private static List<(Guid ProductId, int Quantity)> MergeLines(IEnumerable<(Guid Id, int Quantity)> lines) =>
            lines.GroupBy(l => l.Id)
                .Select(g => (g.Key, g.Sum(l => l.Quantity)))
                .ToList();

        private static SaleDto ToDto(Sale sale) => new()
        {
            Id = sale.Id,
            Number = sale.Number,
            Channel = sale.Channel,
            PaymentMethod = sale.PaymentMethod,
            Subtotal = SaleMath.Subtotal(sale),
            Discount = sale.Discount,
            Total = SaleMath.Total(sale),
            RefundedTotal = SaleMath.Refunded(sale),
            CreatedAt = sale.CreatedAt,
            Items = sale.Items.OrderBy(i => i.Product?.Title).ThenBy(i => i.Id).Select(i => new SaleItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                Title = i.Product?.Title ?? string.Empty,
                Sku = i.Product?.Sku,
                Quantity = i.Quantity,
                ReturnedQuantity = i.ReturnedQuantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        };

        private static SaleResponseDto NotFound() =>
            Fail(SaleError.NotFound, "Чек не знайдено.");

        private static SaleResponseDto Fail(SaleError error, string message) => new()
        {
            IsSuccess = false,
            Error = error,
            Message = message
        };

        private static SaleResponseDto Success(Sale sale, string message) => new()
        {
            IsSuccess = true,
            Message = message,
            Sale = ToDto(sale)
        };
    }
}
