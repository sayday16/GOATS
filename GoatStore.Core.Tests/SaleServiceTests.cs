using GoatStore.Core.DTOs;
using GoatStore.Core.Services;
using GoatStore.DataAccess;
using GoatStore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoatStore.Core.Tests
{
    public sealed class SaleServiceTests : IDisposable
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

        private readonly TestDb _db = new();
        private readonly Store _store;
        private readonly User _user;

        public SaleServiceTests()
        {
            _store = _db.AddStore();
            _user = _db.AddUser(_store.Id);
        }

        public void Dispose() => _db.Dispose();

        private SaleService CreateService() => new(_db.CreateContext(), new FixedTimeProvider(Now));

        private TResult Query<TResult>(Func<GoatStoreDbContext, TResult> query)
        {
            using var context = _db.CreateContext();
            return query(context);
        }

        private static CreateSaleItemDto Line(Product product, int quantity = 1) =>
            new() { ProductId = product.Id, Quantity = quantity };

        private Task<SaleResponseDto> SellAsync(params CreateSaleItemDto[] items) =>
            SellAsync(0m, items);

        private Task<SaleResponseDto> SellAsync(decimal discount, params CreateSaleItemDto[] items) =>
            CreateService().CreateAsync(_store.Id, _user.Id, new CreateSaleRequestDto { Items = items.ToList(), Discount = discount });

        private Product AddStocked(int quantity, string title = "Футболка Basic")
        {
            using var context = _db.CreateContext();
            var product = new Product
            {
                Title = title,
                Brand = "Nike",
                Size = "M",
                Category = "Одяг",
                PurchasePrice = 100m,
                SalePrice = 250m,
                IsUnique = false,
                Quantity = quantity,
                StoreId = _store.Id
            };
            context.Products.Add(product);
            context.SaveChanges();
            return product;
        }

        private async Task<SaleDto> SellTwoProductsAsync(decimal discount = 0m)
        {
            var first = _db.AddProduct(_store.Id, "Худі");
            var second = _db.AddProduct(_store.Id, "Кепка");
            var result = await SellAsync(discount, Line(first), Line(second));
            return result.Sale!;
        }

        private async Task<SaleResponseDto> ReturnAsync(int number, params (Guid ItemId, int Quantity)[] items) =>
            await CreateService().ReturnAsync(_store.Id, number, new ReturnSaleRequestDto
            {
                Items = items.Select(i => new ReturnItemDto { SaleItemId = i.ItemId, Quantity = i.Quantity }).ToList()
            });

        private async Task<(Product Product, SaleDto Sale)> SellUniqueAsync()
        {
            var product = _db.AddProduct(_store.Id);
            var result = await SellAsync(Line(product));
            return (product, result.Sale!);
        }

        private async Task<(Product Product, SaleDto Sale)> SellStockedAsync(int stock, int sold)
        {
            var product = AddStocked(stock);
            var result = await SellAsync(Line(product, sold));
            return (product, result.Sale!);
        }

        [Fact]
        public async Task CreateAsync_OneProduct_ReturnsSuccess()
        {
            var product = _db.AddProduct(_store.Id);

            var result = await SellAsync(Line(product));

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task CreateAsync_FirstSale_GetsNumberOne()
        {
            var product = _db.AddProduct(_store.Id);

            var result = await SellAsync(Line(product));

            Assert.Equal(1, result.Sale?.Number);
        }

        [Fact]
        public async Task CreateAsync_SecondSale_GetsNumberTwo()
        {
            await SellUniqueAsync();
            var product = _db.AddProduct(_store.Id, "Другий");

            var result = await SellAsync(Line(product));

            Assert.Equal(2, result.Sale?.Number);
        }

        [Fact]
        public async Task CreateAsync_OtherStoreHasSales_NumberingStartsFromOne()
        {
            var otherStore = _db.AddStore("Other");
            var otherUser = _db.AddUser(otherStore.Id, "other@test.com");
            var otherProduct = _db.AddProduct(otherStore.Id);
            await CreateService().CreateAsync(otherStore.Id, otherUser.Id,
                new CreateSaleRequestDto { Items = [Line(otherProduct)] });
            var product = _db.AddProduct(_store.Id);

            var result = await SellAsync(Line(product));

            Assert.Equal(1, result.Sale?.Number);
        }

        [Fact]
        public async Task CreateAsync_UniqueProduct_MarksItSold()
        {
            var (product, _) = await SellUniqueAsync();

            Assert.Equal(ProductStatus.Sold, Query(c => c.Products.Single(p => p.Id == product.Id).Status));
        }

        [Fact]
        public async Task CreateAsync_UniqueProduct_SetsQuantityToZero()
        {
            var (product, _) = await SellUniqueAsync();

            Assert.Equal(0, Query(c => c.Products.Single(p => p.Id == product.Id).Quantity));
        }

        [Fact]
        public async Task CreateAsync_UniqueProduct_SetsSoldDate()
        {
            var (product, _) = await SellUniqueAsync();

            Assert.Equal(Now.UtcDateTime, Query(c => c.Products.Single(p => p.Id == product.Id).SoldDate));
        }

        [Fact]
        public async Task CreateAsync_OneProduct_SavesSale()
        {
            await SellUniqueAsync();

            Assert.Equal(1, Query(c => c.Sales.Count(s => s.StoreId == _store.Id)));
        }

        [Fact]
        public async Task CreateAsync_OneProduct_SavesSeller()
        {
            await SellUniqueAsync();

            Assert.Equal(_user.Id, Query(c => c.Sales.Single().SoldByUserId));
        }

        [Fact]
        public async Task CreateAsync_OneProduct_SnapshotsSalePrice()
        {
            await SellUniqueAsync();

            Assert.Equal(250m, Query(c => c.SaleItems.Single().UnitPrice));
        }

        [Fact]
        public async Task CreateAsync_OneProduct_SnapshotsPurchasePrice()
        {
            await SellUniqueAsync();

            Assert.Equal(100m, Query(c => c.SaleItems.Single().PurchasePrice));
        }

        [Fact]
        public async Task CreateAsync_TwoProducts_ReturnsSubtotal()
        {
            var sale = await SellTwoProductsAsync();

            Assert.Equal(500m, sale.Subtotal);
        }

        [Fact]
        public async Task CreateAsync_WithDiscount_ReturnsReducedTotal()
        {
            var sale = await SellTwoProductsAsync(discount: 100m);

            Assert.Equal(400m, sale.Total);
        }

        [Fact]
        public async Task CreateAsync_CashPayment_SavesPaymentMethod()
        {
            var product = _db.AddProduct(_store.Id);

            await CreateService().CreateAsync(_store.Id, _user.Id,
                new CreateSaleRequestDto { Items = [Line(product)], PaymentMethod = PaymentMethod.Cash });

            Assert.Equal(PaymentMethod.Cash, Query(c => c.Sales.Single().PaymentMethod));
        }

        [Fact]
        public async Task CreateAsync_StockedProduct_DecreasesQuantity()
        {
            var (product, _) = await SellStockedAsync(stock: 5, sold: 2);

            Assert.Equal(3, Query(c => c.Products.Single(p => p.Id == product.Id).Quantity));
        }

        [Fact]
        public async Task CreateAsync_StockedProductLeftInStock_KeepsInStockStatus()
        {
            var (product, _) = await SellStockedAsync(stock: 5, sold: 2);

            Assert.Equal(ProductStatus.InStock, Query(c => c.Products.Single(p => p.Id == product.Id).Status));
        }

        [Fact]
        public async Task CreateAsync_StockedProductSoldOut_MarksItSold()
        {
            var (product, _) = await SellStockedAsync(stock: 2, sold: 2);

            Assert.Equal(ProductStatus.Sold, Query(c => c.Products.Single(p => p.Id == product.Id).Status));
        }

        [Fact]
        public async Task CreateAsync_NotEnoughStock_ReturnsConflict()
        {
            var product = AddStocked(1, "Футболка");

            var result = await SellAsync(Line(product, 2));

            Assert.Equal(SaleError.Conflict, result.Error);
        }

        [Fact]
        public async Task CreateAsync_NotEnoughStock_ExplainsWhichProduct()
        {
            var product = AddStocked(1, "Футболка");

            var result = await SellAsync(Line(product, 2));

            Assert.Equal("Недостатньо товару «Футболка» на складі.", result.Message);
        }

        [Fact]
        public async Task CreateAsync_ReservedQuantity_IsNotSellable()
        {
            var product = AddStocked(2);
            using (var context = _db.CreateContext())
            {
                context.Products.Single(p => p.Id == product.Id).ReservedQuantity = 1;
                context.SaveChanges();
            }

            var result = await SellAsync(Line(product, 2));

            Assert.Equal(SaleError.Conflict, result.Error);
        }

        [Fact]
        public async Task CreateAsync_AlreadySoldProduct_ReturnsConflict()
        {
            var product = _db.AddProduct(_store.Id, status: ProductStatus.Sold);

            var result = await SellAsync(Line(product));

            Assert.Equal(SaleError.Conflict, result.Error);
        }

        [Fact]
        public async Task CreateAsync_AlreadySoldProduct_ExplainsWhichProduct()
        {
            var product = _db.AddProduct(_store.Id, "Куртка", ProductStatus.Sold);

            var result = await SellAsync(Line(product));

            Assert.Equal("Товар «Куртка» уже продано.", result.Message);
        }

        [Fact]
        public async Task CreateAsync_SecondItemAlreadySold_KeepsFirstProductInStock()
        {
            var first = _db.AddProduct(_store.Id, "Перший");
            var second = _db.AddProduct(_store.Id, "Другий", ProductStatus.Sold);

            await SellAsync(Line(first), Line(second));

            Assert.Equal(1, Query(c => c.Products.Single(p => p.Id == first.Id).Quantity));
        }

        [Fact]
        public async Task CreateAsync_SecondItemAlreadySold_DoesNotSaveSale()
        {
            var first = _db.AddProduct(_store.Id, "Перший");
            var second = _db.AddProduct(_store.Id, "Другий", ProductStatus.Sold);

            await SellAsync(Line(first), Line(second));

            Assert.Equal(0, Query(c => c.Sales.Count()));
        }

        [Fact]
        public async Task CreateAsync_UniqueProductTwoPieces_ReturnsValidationError()
        {
            var product = _db.AddProduct(_store.Id);

            var result = await SellAsync(Line(product, 2));

            Assert.Equal(SaleError.Validation, result.Error);
        }

        [Fact]
        public async Task CreateAsync_SameUniqueProductTwiceInRequest_ReturnsValidationError()
        {
            var product = _db.AddProduct(_store.Id);

            var result = await SellAsync(Line(product), Line(product));

            Assert.Equal(SaleError.Validation, result.Error);
        }

        [Fact]
        public async Task CreateAsync_SameStockedProductTwiceInRequest_SellsMergedQuantity()
        {
            var product = AddStocked(5);

            await SellAsync(Line(product), Line(product));

            Assert.Equal(3, Query(c => c.Products.Single(p => p.Id == product.Id).Quantity));
        }

        [Fact]
        public async Task CreateAsync_NoItems_ReturnsValidationError()
        {
            var result = await SellAsync();

            Assert.Equal(SaleError.Validation, result.Error);
        }

        [Fact]
        public async Task CreateAsync_ZeroQuantity_ReturnsValidationError()
        {
            var product = _db.AddProduct(_store.Id);

            var result = await SellAsync(Line(product, 0));

            Assert.Equal(SaleError.Validation, result.Error);
        }

        [Fact]
        public async Task CreateAsync_DiscountAboveSubtotal_ReturnsValidationError()
        {
            var product = _db.AddProduct(_store.Id);

            var result = await SellAsync(251m, Line(product));

            Assert.Equal(SaleError.Validation, result.Error);
        }

        [Fact]
        public async Task CreateAsync_DiscountAboveSubtotal_DoesNotSellProduct()
        {
            var product = _db.AddProduct(_store.Id);

            await SellAsync(251m, Line(product));

            Assert.Equal(ProductStatus.InStock, Query(c => c.Products.Single(p => p.Id == product.Id).Status));
        }

        [Fact]
        public async Task CreateAsync_NegativeDiscount_ReturnsValidationError()
        {
            var product = _db.AddProduct(_store.Id);

            var result = await SellAsync(-1m, Line(product));

            Assert.Equal(SaleError.Validation, result.Error);
        }

        [Fact]
        public async Task CreateAsync_UnknownPaymentMethod_ReturnsValidationError()
        {
            var product = _db.AddProduct(_store.Id);

            var result = await CreateService().CreateAsync(_store.Id, _user.Id,
                new CreateSaleRequestDto { Items = [Line(product)], PaymentMethod = (PaymentMethod)99 });

            Assert.Equal(SaleError.Validation, result.Error);
        }

        [Fact]
        public async Task CreateAsync_UnknownProduct_ReturnsNotFound()
        {
            var result = await SellAsync(new CreateSaleItemDto { ProductId = Guid.NewGuid() });

            Assert.Equal(SaleError.NotFound, result.Error);
        }

        [Fact]
        public async Task CreateAsync_ProductOfOtherStore_ReturnsNotFound()
        {
            var otherStore = _db.AddStore("Other");
            var foreign = _db.AddProduct(otherStore.Id);

            var result = await SellAsync(Line(foreign));

            Assert.Equal(SaleError.NotFound, result.Error);
        }

        [Fact]
        public async Task CreateAsync_ProductOfOtherStore_KeepsItInStock()
        {
            var otherStore = _db.AddStore("Other");
            var foreign = _db.AddProduct(otherStore.Id);

            await SellAsync(Line(foreign));

            Assert.Equal(1, Query(c => c.Products.Single(p => p.Id == foreign.Id).Quantity));
        }

        [Fact]
        public async Task GetByNumberAsync_ExistingSale_ReturnsItsItems()
        {
            var (_, sale) = await SellUniqueAsync();

            var result = await CreateService().GetByNumberAsync(_store.Id, sale.Number);

            Assert.Equal(new[] { "Test Product" }, result.Sale?.Items.Select(i => i.Title));
        }

        [Fact]
        public async Task GetByNumberAsync_UnknownNumber_ReturnsNotFound()
        {
            var result = await CreateService().GetByNumberAsync(_store.Id, 99);

            Assert.Equal(SaleError.NotFound, result.Error);
        }

        [Fact]
        public async Task GetByNumberAsync_SaleOfOtherStore_ReturnsNotFound()
        {
            var (_, sale) = await SellUniqueAsync();
            var otherStore = _db.AddStore("Other");

            var result = await CreateService().GetByNumberAsync(otherStore.Id, sale.Number);

            Assert.Equal(SaleError.NotFound, result.Error);
        }

        [Fact]
        public async Task ReturnAsync_SoldItem_ReturnsSuccess()
        {
            var (_, sale) = await SellUniqueAsync();

            var result = await ReturnAsync(sale.Number, (sale.Items[0].Id, 1));

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task ReturnAsync_UniqueProduct_PutsItBackInStock()
        {
            var (product, sale) = await SellUniqueAsync();

            await ReturnAsync(sale.Number, (sale.Items[0].Id, 1));

            Assert.Equal(ProductStatus.InStock, Query(c => c.Products.Single(p => p.Id == product.Id).Status));
        }

        [Fact]
        public async Task ReturnAsync_UniqueProduct_RestoresQuantity()
        {
            var (product, sale) = await SellUniqueAsync();

            await ReturnAsync(sale.Number, (sale.Items[0].Id, 1));

            Assert.Equal(1, Query(c => c.Products.Single(p => p.Id == product.Id).Quantity));
        }

        [Fact]
        public async Task ReturnAsync_UniqueProduct_ClearsSoldDate()
        {
            var (product, sale) = await SellUniqueAsync();

            await ReturnAsync(sale.Number, (sale.Items[0].Id, 1));

            Assert.Null(Query(c => c.Products.Single(p => p.Id == product.Id).SoldDate));
        }

        [Fact]
        public async Task ReturnAsync_PartOfStockedItem_RestoresOnlyReturnedQuantity()
        {
            var (product, sale) = await SellStockedAsync(stock: 5, sold: 3);

            await ReturnAsync(sale.Number, (sale.Items[0].Id, 1));

            Assert.Equal(3, Query(c => c.Products.Single(p => p.Id == product.Id).Quantity));
        }

        [Fact]
        public async Task ReturnAsync_PartOfStockedItem_SavesReturnedQuantity()
        {
            var (_, sale) = await SellStockedAsync(stock: 5, sold: 3);

            await ReturnAsync(sale.Number, (sale.Items[0].Id, 1));

            Assert.Equal(1, Query(c => c.SaleItems.Single().ReturnedQuantity));
        }

        [Fact]
        public async Task ReturnAsync_OneOfTwoItems_RefundsItsPrice()
        {
            var sale = await SellTwoProductsAsync();

            var result = await ReturnAsync(sale.Number, (sale.Items[0].Id, 1));

            Assert.Equal(250m, result.Sale?.RefundedTotal);
        }

        [Fact]
        public async Task ReturnAsync_SaleWithDiscount_RefundsProportionally()
        {
            var sale = await SellTwoProductsAsync(discount: 100m);

            var result = await ReturnAsync(sale.Number, (sale.Items[0].Id, 1));

            Assert.Equal(200m, result.Sale?.RefundedTotal);
        }

        [Fact]
        public async Task ReturnAsync_SameItemTwice_ReturnsConflict()
        {
            var (_, sale) = await SellUniqueAsync();
            await ReturnAsync(sale.Number, (sale.Items[0].Id, 1));

            var result = await ReturnAsync(sale.Number, (sale.Items[0].Id, 1));

            Assert.Equal(SaleError.Conflict, result.Error);
        }

        [Fact]
        public async Task ReturnAsync_SameItemTwice_DoesNotRestoreStockTwice()
        {
            var (product, sale) = await SellStockedAsync(stock: 5, sold: 1);
            await ReturnAsync(sale.Number, (sale.Items[0].Id, 1));

            await ReturnAsync(sale.Number, (sale.Items[0].Id, 1));

            Assert.Equal(5, Query(c => c.Products.Single(p => p.Id == product.Id).Quantity));
        }

        [Fact]
        public async Task ReturnAsync_MoreThanSold_ReturnsConflict()
        {
            var (_, sale) = await SellStockedAsync(stock: 5, sold: 2);

            var result = await ReturnAsync(sale.Number, (sale.Items[0].Id, 3));

            Assert.Equal(SaleError.Conflict, result.Error);
        }

        [Fact]
        public async Task ReturnAsync_SecondItemFails_DoesNotRestoreFirstItem()
        {
            var sale = await SellTwoProductsAsync();
            var firstProductId = sale.Items[0].ProductId;

            await ReturnAsync(sale.Number, (sale.Items[0].Id, 1), (sale.Items[1].Id, 2));

            Assert.Equal(0, Query(c => c.Products.Single(p => p.Id == firstProductId).Quantity));
        }

        [Fact]
        public async Task ReturnAsync_UnknownSale_ReturnsNotFound()
        {
            var result = await ReturnAsync(99, (Guid.NewGuid(), 1));

            Assert.Equal(SaleError.NotFound, result.Error);
        }

        [Fact]
        public async Task ReturnAsync_ItemOfAnotherSale_ReturnsValidationError()
        {
            var (_, first) = await SellUniqueAsync();
            var second = _db.AddProduct(_store.Id, "Другий");
            var secondSale = (await SellAsync(Line(second))).Sale!;

            var result = await ReturnAsync(first.Number, (secondSale.Items[0].Id, 1));

            Assert.Equal(SaleError.Validation, result.Error);
        }

        [Fact]
        public async Task ReturnAsync_NoItems_ReturnsValidationError()
        {
            var (_, sale) = await SellUniqueAsync();

            var result = await ReturnAsync(sale.Number);

            Assert.Equal(SaleError.Validation, result.Error);
        }

        [Fact]
        public async Task ReturnAsync_ZeroQuantity_ReturnsValidationError()
        {
            var (_, sale) = await SellUniqueAsync();

            var result = await ReturnAsync(sale.Number, (sale.Items[0].Id, 0));

            Assert.Equal(SaleError.Validation, result.Error);
        }

        [Fact]
        public async Task ReturnAsync_SaleOfOtherStore_ReturnsNotFound()
        {
            var (_, sale) = await SellUniqueAsync();
            var otherStore = _db.AddStore("Other");

            var result = await CreateService().ReturnAsync(otherStore.Id, sale.Number,
                new ReturnSaleRequestDto { Items = [new ReturnItemDto { SaleItemId = sale.Items[0].Id }] });

            Assert.Equal(SaleError.NotFound, result.Error);
        }
    }
}
