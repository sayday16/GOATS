using GoatStore.Core.DTOs;
using GoatStore.Core.Services;
using GoatStore.DataAccess.Entities;

namespace GoatStore.Core.Tests
{
    public sealed class ProductServiceTests : IDisposable
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

        private readonly TestDb _db = new();

        public void Dispose() => _db.Dispose();

        private ProductService CreateService() =>
            new(_db.CreateContext(), new FixedTimeProvider(Now));

        private static SaveProductRequestDto ValidRequest(string title = "Levi's 501") => new()
        {
            Title = title,
            PurchasePrice = 400m,
            SalePrice = 1200m
        };

        [Fact]
        public async Task CreateAsync_ValidRequest_SavesInStockProductToStore()
        {
            var store = _db.AddStore();

            var result = await CreateService().CreateAsync(store.Id, ValidRequest("  Levi's 501  "));

            Assert.True(result.IsSuccess);
            Assert.Equal(ProductError.None, result.Error);
            Assert.NotNull(result.Product);
            Assert.Equal("Levi's 501", result.Product.Title);
            Assert.Equal(ProductStatus.InStock, result.Product.Status);

            using var context = _db.CreateContext();
            var saved = Assert.Single(context.Products);
            Assert.Equal(store.Id, saved.StoreId);
            Assert.Equal(1200m, saved.SalePrice);
        }

        [Theory]
        [InlineData("", 100, 200)]
        [InlineData("   ", 100, 200)]
        [InlineData("Jacket", -1, 200)]
        [InlineData("Jacket", 100, -1)]
        [InlineData("Jacket", 100, 10_000_001)]
        public async Task CreateAsync_InvalidRequest_ReturnsValidationError(string title, decimal purchasePrice, decimal salePrice)
        {
            var store = _db.AddStore();
            var request = new SaveProductRequestDto { Title = title, PurchasePrice = purchasePrice, SalePrice = salePrice };

            var result = await CreateService().CreateAsync(store.Id, request);

            Assert.False(result.IsSuccess);
            Assert.Equal(ProductError.Validation, result.Error);
            using var context = _db.CreateContext();
            Assert.Empty(context.Products);
        }

        [Fact]
        public async Task CreateAsync_TooLongTitle_ReturnsValidationError()
        {
            var store = _db.AddStore();

            var result = await CreateService().CreateAsync(store.Id, ValidRequest(new string('a', ProductService.MaxTitleLength + 1)));

            Assert.Equal(ProductError.Validation, result.Error);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsOnlyOwnStoreProducts_SortedByTitle()
        {
            var store = _db.AddStore("Store A");
            var otherStore = _db.AddStore("Store B");
            _db.AddProduct(store.Id, "Nike Hoodie");
            _db.AddProduct(store.Id, "Adidas Jacket");
            _db.AddProduct(otherStore.Id, "Foreign Item");

            var products = await CreateService().GetAllAsync(store.Id);

            Assert.Equal(new[] { "Adidas Jacket", "Nike Hoodie" }, products.Select(p => p.Title));
        }

        [Fact]
        public async Task GetAllAsync_WithStatus_FiltersProducts()
        {
            var store = _db.AddStore();
            _db.AddProduct(store.Id, "In stock");
            _db.AddProduct(store.Id, "Sold", ProductStatus.Sold);

            var products = await CreateService().GetAllAsync(store.Id, ProductStatus.Sold);

            Assert.Equal("Sold", Assert.Single(products).Title);
        }

        [Fact]
        public async Task GetAsync_ProductFromOtherStore_ReturnsNotFound()
        {
            var store = _db.AddStore("Store A");
            var otherStore = _db.AddStore("Store B");
            var foreign = _db.AddProduct(otherStore.Id);

            var result = await CreateService().GetAsync(store.Id, foreign.Id);

            Assert.Equal(ProductError.NotFound, result.Error);
            Assert.Null(result.Product);
        }

        [Fact]
        public async Task UpdateAsync_InStockProduct_UpdatesFields()
        {
            var store = _db.AddStore();
            var product = _db.AddProduct(store.Id);

            var result = await CreateService().UpdateAsync(store.Id, product.Id, ValidRequest("New title"));

            Assert.True(result.IsSuccess);
            using var context = _db.CreateContext();
            var saved = context.Products.Single();
            Assert.Equal("New title", saved.Title);
            Assert.Equal(400m, saved.PurchasePrice);
        }

        [Fact]
        public async Task UpdateAsync_SoldProduct_ReturnsConflict()
        {
            var store = _db.AddStore();
            var product = _db.AddProduct(store.Id, status: ProductStatus.Sold);

            var result = await CreateService().UpdateAsync(store.Id, product.Id, ValidRequest("New title"));

            Assert.Equal(ProductError.Conflict, result.Error);
        }

        [Fact]
        public async Task UpdateAsync_UnknownProduct_ReturnsNotFound()
        {
            var store = _db.AddStore();

            var result = await CreateService().UpdateAsync(store.Id, Guid.NewGuid(), ValidRequest());

            Assert.Equal(ProductError.NotFound, result.Error);
        }

        [Fact]
        public async Task SellAsync_InStockProduct_MarksSoldWithCurrentTime()
        {
            var store = _db.AddStore();
            var product = _db.AddProduct(store.Id);

            var result = await CreateService().SellAsync(store.Id, product.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(ProductStatus.Sold, result.Product!.Status);
            using var context = _db.CreateContext();
            var saved = context.Products.Single();
            Assert.Equal(ProductStatus.Sold, saved.Status);
            Assert.Equal(Now.UtcDateTime, saved.SoldDate);
        }

        [Fact]
        public async Task SellAsync_AlreadySold_ReturnsConflictAndKeepsOriginalDate()
        {
            var store = _db.AddStore();
            var product = _db.AddProduct(store.Id);
            await CreateService().SellAsync(store.Id, product.Id);

            var secondSale = await CreateService().SellAsync(store.Id, product.Id);

            Assert.Equal(ProductError.Conflict, secondSale.Error);
            using var context = _db.CreateContext();
            Assert.Equal(Now.UtcDateTime, context.Products.Single().SoldDate);
        }

        [Fact]
        public async Task SellAsync_ProductFromOtherStore_ReturnsNotFoundAndDoesNotSell()
        {
            var store = _db.AddStore("Store A");
            var otherStore = _db.AddStore("Store B");
            var foreign = _db.AddProduct(otherStore.Id);

            var result = await CreateService().SellAsync(store.Id, foreign.Id);

            Assert.Equal(ProductError.NotFound, result.Error);
            using var context = _db.CreateContext();
            Assert.Equal(ProductStatus.InStock, context.Products.Single().Status);
        }

        [Fact]
        public async Task DeleteAsync_InStockProduct_RemovesIt()
        {
            var store = _db.AddStore();
            var product = _db.AddProduct(store.Id);

            var result = await CreateService().DeleteAsync(store.Id, product.Id);

            Assert.True(result.IsSuccess);
            using var context = _db.CreateContext();
            Assert.Empty(context.Products);
        }

        [Fact]
        public async Task DeleteAsync_SoldProduct_ReturnsConflictAndKeepsIt()
        {
            var store = _db.AddStore();
            var product = _db.AddProduct(store.Id, status: ProductStatus.Sold);

            var result = await CreateService().DeleteAsync(store.Id, product.Id);

            Assert.Equal(ProductError.Conflict, result.Error);
            using var context = _db.CreateContext();
            Assert.Single(context.Products);
        }
    }
}
