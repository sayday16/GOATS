using GoatStore.Core.DTOs;
using GoatStore.Core.Services;
using GoatStore.DataAccess;
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

        private TResult Query<TResult>(Func<GoatStoreDbContext, TResult> query)
        {
            using var context = _db.CreateContext();
            return query(context);
        }

        private static SaveProductRequestDto ValidRequest(string title = "Levi's 501") => new()
        {
            Title = title,
            PurchasePrice = 400m,
            SalePrice = 1200m
        };

        private async Task<(Store Store, ProductResponseDto Result)> CreateValidProductAsync()
        {
            var store = _db.AddStore();
            var result = await CreateService().CreateAsync(store.Id, ValidRequest("  Levi's 501  "));
            return (store, result);
        }

        private Task<ProductResponseDto> CreateInvalidProductAsync(string title, decimal purchasePrice, decimal salePrice)
        {
            var store = _db.AddStore();
            var request = new SaveProductRequestDto { Title = title, PurchasePrice = purchasePrice, SalePrice = salePrice };
            return CreateService().CreateAsync(store.Id, request);
        }

        private Task<ProductResponseDto> GetForeignProductAsync()
        {
            var store = _db.AddStore("Store A");
            var otherStore = _db.AddStore("Store B");
            var foreign = _db.AddProduct(otherStore.Id);
            return CreateService().GetAsync(store.Id, foreign.Id);
        }

        private Task<ProductResponseDto> UpdateInStockProductAsync()
        {
            var store = _db.AddStore();
            var product = _db.AddProduct(store.Id);
            return CreateService().UpdateAsync(store.Id, product.Id, ValidRequest("New title"));
        }

        private Task<ProductResponseDto> UpdateWithInvalidTitleAsync()
        {
            var store = _db.AddStore();
            var product = _db.AddProduct(store.Id, "Old Title");
            return CreateService().UpdateAsync(store.Id, product.Id, ValidRequest("   "));
        }

        private Task<ProductResponseDto> SellInStockProductAsync()
        {
            var store = _db.AddStore();
            var product = _db.AddProduct(store.Id);
            return CreateService().SellAsync(store.Id, product.Id);
        }

        private async Task<ProductResponseDto> SellTwiceAsync()
        {
            var store = _db.AddStore();
            var product = _db.AddProduct(store.Id);
            await CreateService().SellAsync(store.Id, product.Id);
            return await CreateService().SellAsync(store.Id, product.Id);
        }

        private Task<ProductResponseDto> SellForeignProductAsync()
        {
            var store = _db.AddStore("Store A");
            var otherStore = _db.AddStore("Store B");
            var foreign = _db.AddProduct(otherStore.Id);
            return CreateService().SellAsync(store.Id, foreign.Id);
        }

        private Task<ProductResponseDto> DeleteProductAsync(ProductStatus status)
        {
            var store = _db.AddStore();
            var product = _db.AddProduct(store.Id, status: status);
            return CreateService().DeleteAsync(store.Id, product.Id);
        }

        [Fact]
        public async Task CreateAsync_ValidRequest_ReturnsSuccess()
        {
            var (_, result) = await CreateValidProductAsync();

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task CreateAsync_ValidRequest_ReturnsNoError()
        {
            var (_, result) = await CreateValidProductAsync();

            Assert.Equal(ProductError.None, result.Error);
        }

        [Fact]
        public async Task CreateAsync_ValidRequest_TrimsTitle()
        {
            var (_, result) = await CreateValidProductAsync();

            Assert.Equal("Levi's 501", result.Product?.Title);
        }

        [Fact]
        public async Task CreateAsync_ValidRequest_SetsInStockStatus()
        {
            var (_, result) = await CreateValidProductAsync();

            Assert.Equal(ProductStatus.InStock, result.Product?.Status);
        }

        [Fact]
        public async Task CreateAsync_ValidRequest_SavesOneProduct()
        {
            await CreateValidProductAsync();

            Assert.Single(Query(c => c.Products.ToList()));
        }

        [Fact]
        public async Task CreateAsync_ValidRequest_SavesProductToStore()
        {
            var (store, _) = await CreateValidProductAsync();

            Assert.Equal(store.Id, Query(c => c.Products.Single().StoreId));
        }

        [Fact]
        public async Task CreateAsync_ValidRequest_SavesSalePrice()
        {
            await CreateValidProductAsync();

            Assert.Equal(1200m, Query(c => c.Products.Single().SalePrice));
        }

        [Theory]
        [InlineData("", 100, 200)]
        [InlineData("   ", 100, 200)]
        [InlineData("Jacket", -1, 200)]
        [InlineData("Jacket", 100, -1)]
        [InlineData("Jacket", 100, 10_000_001)]
        public async Task CreateAsync_InvalidRequest_IsNotSuccessful(string title, decimal purchasePrice, decimal salePrice)
        {
            var result = await CreateInvalidProductAsync(title, purchasePrice, salePrice);

            Assert.False(result.IsSuccess);
        }

        [Theory]
        [InlineData("", 100, 200)]
        [InlineData("   ", 100, 200)]
        [InlineData("Jacket", -1, 200)]
        [InlineData("Jacket", 100, -1)]
        [InlineData("Jacket", 100, 10_000_001)]
        public async Task CreateAsync_InvalidRequest_ReturnsValidationError(string title, decimal purchasePrice, decimal salePrice)
        {
            var result = await CreateInvalidProductAsync(title, purchasePrice, salePrice);

            Assert.Equal(ProductError.Validation, result.Error);
        }

        [Theory]
        [InlineData("", 100, 200)]
        [InlineData("   ", 100, 200)]
        [InlineData("Jacket", -1, 200)]
        [InlineData("Jacket", 100, -1)]
        [InlineData("Jacket", 100, 10_000_001)]
        public async Task CreateAsync_InvalidRequest_DoesNotSaveProduct(string title, decimal purchasePrice, decimal salePrice)
        {
            await CreateInvalidProductAsync(title, purchasePrice, salePrice);

            Assert.Empty(Query(c => c.Products.ToList()));
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
        public async Task GetAllAsync_WithStatus_ReturnsOnlyProductsWithThatStatus()
        {
            var store = _db.AddStore();
            _db.AddProduct(store.Id, "In stock");
            _db.AddProduct(store.Id, "Sold", ProductStatus.Sold);

            var products = await CreateService().GetAllAsync(store.Id, ProductStatus.Sold);

            Assert.Equal(new[] { "Sold" }, products.Select(p => p.Title));
        }

        [Fact]
        public async Task GetAsync_ProductFromOtherStore_ReturnsNotFound()
        {
            var result = await GetForeignProductAsync();

            Assert.Equal(ProductError.NotFound, result.Error);
        }

        [Fact]
        public async Task GetAsync_ProductFromOtherStore_ReturnsNoProduct()
        {
            var result = await GetForeignProductAsync();

            Assert.Null(result.Product);
        }

        [Fact]
        public async Task UpdateAsync_InStockProduct_ReturnsSuccess()
        {
            var result = await UpdateInStockProductAsync();

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task UpdateAsync_InStockProduct_SavesNewTitle()
        {
            await UpdateInStockProductAsync();

            Assert.Equal("New title", Query(c => c.Products.Single().Title));
        }

        [Fact]
        public async Task UpdateAsync_InStockProduct_SavesNewPurchasePrice()
        {
            await UpdateInStockProductAsync();

            Assert.Equal(400m, Query(c => c.Products.Single().PurchasePrice));
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
        public async Task UpdateAsync_InvalidRequest_ReturnsValidationError()
        {
            var result = await UpdateWithInvalidTitleAsync();

            Assert.Equal(ProductError.Validation, result.Error);
        }

        [Fact]
        public async Task UpdateAsync_InvalidRequest_KeepsOriginalTitle()
        {
            await UpdateWithInvalidTitleAsync();

            Assert.Equal("Old Title", Query(c => c.Products.Single().Title));
        }

        [Fact]
        public async Task SellAsync_InStockProduct_ReturnsSuccess()
        {
            var result = await SellInStockProductAsync();

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task SellAsync_InStockProduct_ReturnsSoldStatus()
        {
            var result = await SellInStockProductAsync();

            Assert.Equal(ProductStatus.Sold, result.Product?.Status);
        }

        [Fact]
        public async Task SellAsync_InStockProduct_SavesSoldStatus()
        {
            await SellInStockProductAsync();

            Assert.Equal(ProductStatus.Sold, Query(c => c.Products.Single().Status));
        }

        [Fact]
        public async Task SellAsync_InStockProduct_SavesCurrentTimeAsSoldDate()
        {
            await SellInStockProductAsync();

            Assert.Equal(Now.UtcDateTime, Query(c => c.Products.Single().SoldDate));
        }

        [Fact]
        public async Task SellAsync_AlreadySold_ReturnsConflict()
        {
            var secondSale = await SellTwiceAsync();

            Assert.Equal(ProductError.Conflict, secondSale.Error);
        }

        [Fact]
        public async Task SellAsync_AlreadySold_KeepsOriginalSoldDate()
        {
            await SellTwiceAsync();

            Assert.Equal(Now.UtcDateTime, Query(c => c.Products.Single().SoldDate));
        }

        [Fact]
        public async Task SellAsync_ProductFromOtherStore_ReturnsNotFound()
        {
            var result = await SellForeignProductAsync();

            Assert.Equal(ProductError.NotFound, result.Error);
        }

        [Fact]
        public async Task SellAsync_ProductFromOtherStore_DoesNotSellIt()
        {
            await SellForeignProductAsync();

            Assert.Equal(ProductStatus.InStock, Query(c => c.Products.Single().Status));
        }

        [Fact]
        public async Task DeleteAsync_InStockProduct_ReturnsSuccess()
        {
            var result = await DeleteProductAsync(ProductStatus.InStock);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task DeleteAsync_InStockProduct_RemovesIt()
        {
            await DeleteProductAsync(ProductStatus.InStock);

            Assert.Empty(Query(c => c.Products.ToList()));
        }

        [Fact]
        public async Task DeleteAsync_SoldProduct_ReturnsConflict()
        {
            var result = await DeleteProductAsync(ProductStatus.Sold);

            Assert.Equal(ProductError.Conflict, result.Error);
        }

        [Fact]
        public async Task DeleteAsync_SoldProduct_KeepsIt()
        {
            await DeleteProductAsync(ProductStatus.Sold);

            Assert.Single(Query(c => c.Products.ToList()));
        }

        [Fact]
        public async Task DeleteAsync_UnknownProduct_ReturnsNotFound()
        {
            var store = _db.AddStore();

            var result = await CreateService().DeleteAsync(store.Id, Guid.NewGuid());

            Assert.Equal(ProductError.NotFound, result.Error);
        }
    }
}
