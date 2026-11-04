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
        public const int MaxBrandLength = 100;
        public const int MaxSizeLength = 30;
        public const int MaxCategoryLength = 100;
        public const int MaxSkuLength = 50;
        public const decimal MaxPrice = 10_000_000m;

        private readonly GoatStoreDbContext _context;
        private readonly TimeProvider _timeProvider;

        public ProductService(GoatStoreDbContext context, TimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<IReadOnlyList<ProductDto>> GetAllAsync(
            Guid storeId, ProductFilterDto? filter = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Products
                .AsNoTracking()
                .Where(p => p.StoreId == storeId);

            if (filter?.Status is { } status)
                query = query.Where(p => p.Status == status);

            if (!string.IsNullOrWhiteSpace(filter?.Category))
            {
                var category = filter.Category.Trim().ToLower();
                query = query.Where(p => p.Category.ToLower() == category);
            }

            if (!string.IsNullOrWhiteSpace(filter?.Brand))
            {
                var brand = filter.Brand.Trim().ToLower();
                query = query.Where(p => p.Brand.ToLower() == brand);
            }

            if (!string.IsNullOrWhiteSpace(filter?.Search))
            {
                var term = filter.Search.Trim().ToLower();
                query = query.Where(p =>
                    p.Title.ToLower().Contains(term) ||
                    p.Brand.ToLower().Contains(term) ||
                    (p.Sku != null && p.Sku.ToLower().Contains(term)));
            }

            var products = await query
                .OrderBy(p => p.Title)
                .ToListAsync(cancellationToken);

            return products.Select(ToDto).ToList();
        }

        public async Task<ProductFilterOptionsDto> GetFilterOptionsAsync(
            Guid storeId, CancellationToken cancellationToken = default)
        {
            var products = _context.Products.AsNoTracking().Where(p => p.StoreId == storeId);

            return new ProductFilterOptionsDto
            {
                Categories = await products.Select(p => p.Category).Distinct().OrderBy(c => c).ToListAsync(cancellationToken),
                Brands = await products.Select(p => p.Brand).Distinct().OrderBy(b => b).ToListAsync(cancellationToken)
            };
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
            var fields = Normalize(request);

            var validationError = Validate(request, fields);
            if (validationError is not null)
                return Fail(ProductError.Validation, validationError);

            if (await SkuTakenAsync(storeId, fields.Sku, exceptProductId: null, cancellationToken))
                return SkuConflict();

            var product = new Product
            {
                StoreId = storeId,
                Status = ProductStatus.InStock,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };
            Apply(product, request, fields);

            _context.Products.Add(product);
            await _context.SaveChangesAsync(cancellationToken);

            return Success(product, "Товар додано.");
        }

        public async Task<ProductResponseDto> UpdateAsync(
            Guid storeId, Guid productId, SaveProductRequestDto request, CancellationToken cancellationToken = default)
        {
            var fields = Normalize(request);

            var validationError = Validate(request, fields);
            if (validationError is not null)
                return Fail(ProductError.Validation, validationError);

            var product = await FindAsync(storeId, productId, tracking: true, cancellationToken);
            if (product is null)
                return NotFound();

            if (product.Status == ProductStatus.Sold)
                return Fail(ProductError.Conflict, "Проданий товар не можна змінювати.");

            if (await SkuTakenAsync(storeId, fields.Sku, exceptProductId: productId, cancellationToken))
                return SkuConflict();

            Apply(product, request, fields);

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

        private sealed record ProductFields(string Title, string Brand, string Size, string Category, string? Sku);

        private static ProductFields Normalize(SaveProductRequestDto request)
        {
            var sku = request.Sku?.Trim();

            return new ProductFields(
                (request.Title ?? string.Empty).Trim(),
                (request.Brand ?? string.Empty).Trim(),
                (request.Size ?? string.Empty).Trim(),
                (request.Category ?? string.Empty).Trim(),
                string.IsNullOrEmpty(sku) ? null : sku);
        }

        private static void Apply(Product product, SaveProductRequestDto request, ProductFields fields)
        {
            product.Title = fields.Title;
            product.Brand = fields.Brand;
            product.Size = fields.Size;
            product.Category = fields.Category;
            product.Sku = fields.Sku;
            product.Condition = request.Condition;
            product.PurchasePrice = request.PurchasePrice;
            product.SalePrice = request.SalePrice;
        }

        private Task<bool> SkuTakenAsync(Guid storeId, string? sku, Guid? exceptProductId, CancellationToken cancellationToken)
        {
            if (sku is null)
                return Task.FromResult(false);

            var skuKey = sku.ToLower();
            return _context.Products.AnyAsync(
                p => p.StoreId == storeId && p.Id != exceptProductId && p.Sku != null && p.Sku.ToLower() == skuKey,
                cancellationToken);
        }

        private static ProductResponseDto SkuConflict() =>
            Fail(ProductError.Conflict, "Товар із таким артикулом уже є в магазині.");

        private static string? Validate(SaveProductRequestDto request, ProductFields fields)
        {
            if (fields.Title.Length == 0)
                return "Вкажіть назву товару.";

            if (fields.Title.Length > MaxTitleLength)
                return $"Назва товару занадто довга (максимум {MaxTitleLength} символів).";

            if (fields.Brand.Length == 0)
                return "Вкажіть бренд.";

            if (fields.Brand.Length > MaxBrandLength)
                return $"Назва бренду занадто довга (максимум {MaxBrandLength} символів).";

            if (fields.Size.Length == 0)
                return "Вкажіть розмір.";

            if (fields.Size.Length > MaxSizeLength)
                return $"Розмір занадто довгий (максимум {MaxSizeLength} символів).";

            if (fields.Category.Length == 0)
                return "Вкажіть категорію.";

            if (fields.Category.Length > MaxCategoryLength)
                return $"Назва категорії занадто довга (максимум {MaxCategoryLength} символів).";

            if (fields.Sku?.Length > MaxSkuLength)
                return $"Артикул занадто довгий (максимум {MaxSkuLength} символів).";

            if (!Enum.IsDefined(request.Condition))
                return "Оберіть стан товару: новий або вживаний.";

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
            Brand = product.Brand,
            Size = product.Size,
            Category = product.Category,
            Condition = product.Condition,
            Sku = product.Sku,
            PurchasePrice = product.PurchasePrice,
            SalePrice = product.SalePrice,
            Status = product.Status,
            CreatedAt = product.CreatedAt,
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
