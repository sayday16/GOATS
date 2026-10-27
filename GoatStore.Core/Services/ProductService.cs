using GoatStore.Core.DTOs;
using GoatStore.Core.Interfaces;
using GoatStore.DataAccess;
using GoatStore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoatStore.Core.Services
{
    public class ProductService : IProductService
    {
        public const int MaxTitleLength = 200;
        public const decimal MaxPrice = 10_000_000m;

        private readonly GoatStoreDbContext _context;
        private readonly TimeProvider _timeProvider;

        public ProductService(GoatStoreDbContext context, TimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<IReadOnlyList<ProductDto>> GetAllAsync(
            Guid storeId, ProductStatus? status = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Products
                .AsNoTracking()
                .Where(p => p.StoreId == storeId);

            if (status is { } filter)
                query = query.Where(p => p.Status == filter);

            return await query
                .OrderBy(p => p.Title)
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    PurchasePrice = p.PurchasePrice,
                    SalePrice = p.SalePrice,
                    Status = p.Status,
                    SoldDate = p.SoldDate
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<ProductResponseDto> GetAsync(
            Guid storeId, Guid productId, CancellationToken cancellationToken = default)
        {
            var product = await FindAsync(storeId, productId, tracking: false, cancellationToken);

            return product is null ? NotFound() : Success(product, "OK");
        }

        public async Task<ProductResponseDto> CreateAsync(
            Guid storeId, SaveProductRequestDto request, CancellationToken cancellationToken = default)
        {
            var title = (request.Title ?? string.Empty).Trim();

            var validationError = Validate(request, title);
            if (validationError is not null)
                return Fail(ProductError.Validation, validationError);

            var product = new Product
            {
                Title = title,
                PurchasePrice = request.PurchasePrice,
                SalePrice = request.SalePrice,
                Status = ProductStatus.InStock,
                StoreId = storeId
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync(cancellationToken);

            return Success(product, "Товар додано.");
        }

        public async Task<ProductResponseDto> UpdateAsync(
            Guid storeId, Guid productId, SaveProductRequestDto request, CancellationToken cancellationToken = default)
        {
            var title = (request.Title ?? string.Empty).Trim();

            var validationError = Validate(request, title);
            if (validationError is not null)
                return Fail(ProductError.Validation, validationError);

            var product = await FindAsync(storeId, productId, tracking: true, cancellationToken);
            if (product is null)
                return NotFound();

            if (product.Status == ProductStatus.Sold)
                return Fail(ProductError.Conflict, "Проданий товар не можна змінювати.");

            product.Title = title;
            product.PurchasePrice = request.PurchasePrice;
            product.SalePrice = request.SalePrice;

            await _context.SaveChangesAsync(cancellationToken);

            return Success(product, "Товар оновлено.");
        }

        public async Task<ProductResponseDto> SellAsync(
            Guid storeId, Guid productId, CancellationToken cancellationToken = default)
        {
            var soldAt = _timeProvider.GetUtcNow().UtcDateTime;

            // Умовний UPDATE в одному запиті: з двох одночасних продажів пройде лише один.
            var updated = await _context.Products
                .Where(p => p.Id == productId && p.StoreId == storeId && p.Status == ProductStatus.InStock)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.Status, ProductStatus.Sold)
                    .SetProperty(p => p.SoldDate, soldAt),
                    cancellationToken);

            var product = await FindAsync(storeId, productId, tracking: false, cancellationToken);
            if (product is null)
                return NotFound();

            return updated == 0
                ? Fail(ProductError.Conflict, "Цей товар уже продано.")
                : Success(product, "Товар продано.");
        }

        public async Task<ProductResponseDto> DeleteAsync(
            Guid storeId, Guid productId, CancellationToken cancellationToken = default)
        {
            var product = await FindAsync(storeId, productId, tracking: true, cancellationToken);
            if (product is null)
                return NotFound();

            if (product.Status == ProductStatus.Sold)
                return Fail(ProductError.Conflict, "Проданий товар не можна видалити: він потрібен для звітів.");

            _context.Products.Remove(product);
            await _context.SaveChangesAsync(cancellationToken);

            return Success(product, "Товар видалено.");
        }

        private Task<Product?> FindAsync(Guid storeId, Guid productId, bool tracking, CancellationToken cancellationToken)
        {
            var query = tracking ? _context.Products : _context.Products.AsNoTracking();

            return query.FirstOrDefaultAsync(p => p.Id == productId && p.StoreId == storeId, cancellationToken);
        }

        private static string? Validate(SaveProductRequestDto request, string title)
        {
            if (title.Length == 0)
                return "Вкажіть назву товару.";

            if (title.Length > MaxTitleLength)
                return $"Назва товару занадто довга (максимум {MaxTitleLength} символів).";

            if (request.PurchasePrice < 0 || request.SalePrice < 0)
                return "Ціна не може бути від'ємною.";

            if (request.PurchasePrice > MaxPrice || request.SalePrice > MaxPrice)
                return $"Ціна занадто велика (максимум {MaxPrice:N0}).";

            return null;
        }

        private static ProductDto ToDto(Product product) => new()
        {
            Id = product.Id,
            Title = product.Title,
            PurchasePrice = product.PurchasePrice,
            SalePrice = product.SalePrice,
            Status = product.Status,
            SoldDate = product.SoldDate
        };

        private static ProductResponseDto NotFound() =>
            Fail(ProductError.NotFound, "Товар не знайдено.");

        private static ProductResponseDto Fail(ProductError error, string message) => new()
        {
            IsSuccess = false,
            Error = error,
            Message = message
        };

        private static ProductResponseDto Success(Product product, string message) => new()
        {
            IsSuccess = true,
            Message = message,
            Product = ToDto(product)
        };
    }
}
