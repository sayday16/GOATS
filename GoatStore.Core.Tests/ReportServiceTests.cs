using GoatStore.Core.DTOs;
using GoatStore.Core.Services;
using GoatStore.DataAccess.Entities;

namespace GoatStore.Core.Tests
{
    public sealed class ReportServiceTests : IDisposable
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

        private readonly TestDb _db = new();
        private readonly Store _store;
        private readonly User _user;

        public ReportServiceTests()
        {
            _store = _db.AddStore();
            _user = _db.AddUser(_store.Id);
        }

        public void Dispose() => _db.Dispose();

        private Task<ReportSummaryDto> SummaryAsync(Guid? storeId = null) =>
            new ReportService(_db.CreateContext(), new FixedTimeProvider(Now)).GetSummaryAsync(storeId ?? _store.Id);

        private async Task<SaleDto> SellAsync(DateTimeOffset at, decimal discount = 0m, string brand = "Nike", int products = 1)
        {
            var lines = Enumerable.Range(0, products)
                .Select(_ => new CreateSaleItemDto { ProductId = _db.AddProduct(_store.Id, brand: brand).Id })
                .ToList();

            var service = new SaleService(_db.CreateContext(), new FixedTimeProvider(at));
            var result = await service.CreateAsync(_store.Id, _user.Id,
                new CreateSaleRequestDto { Items = lines, Discount = discount });
            return result.Sale!;
        }

        private async Task ReturnFirstItemAsync(SaleDto sale)
        {
            var service = new SaleService(_db.CreateContext(), new FixedTimeProvider(Now));
            await service.ReturnAsync(_store.Id, sale.Number,
                new ReturnSaleRequestDto { Items = [new ReturnItemDto { SaleItemId = sale.Items[0].Id }] });
        }

        private void SetLimit(decimal limit)
        {
            using var context = _db.CreateContext();
            context.Stores.Single(s => s.Id == _store.Id).AnnualTurnoverLimit = limit;
            context.SaveChanges();
        }

        private Product AddAgedProduct(int daysOld, string title = "Старий товар", ProductStatus status = ProductStatus.InStock)
        {
            var product = _db.AddProduct(_store.Id, title, status);
            using var context = _db.CreateContext();
            context.Products.Single(p => p.Id == product.Id).CreatedAt = Now.UtcDateTime.AddDays(-daysOld);
            context.SaveChanges();
            return product;
        }

        [Fact]
        public async Task GetSummaryAsync_NoSales_ReturnsZeroRevenue()
        {
            var summary = await SummaryAsync();

            Assert.Equal(0m, summary.MonthRevenue);
        }

        [Fact]
        public async Task GetSummaryAsync_NoSales_ReturnsZeroAverageReceipt()
        {
            var summary = await SummaryAsync();

            Assert.Equal(0m, summary.MonthAverageReceipt);
        }

        [Fact]
        public async Task GetSummaryAsync_OneSale_ReturnsMonthRevenue()
        {
            await SellAsync(Now);

            var summary = await SummaryAsync();

            Assert.Equal(250m, summary.MonthRevenue);
        }

        [Fact]
        public async Task GetSummaryAsync_OneSale_ReturnsNetProfit()
        {
            await SellAsync(Now);

            var summary = await SummaryAsync();

            Assert.Equal(150m, summary.MonthNetProfit);
        }

        [Fact]
        public async Task GetSummaryAsync_TwoSales_ReturnsReceiptsCount()
        {
            await SellAsync(Now);
            await SellAsync(Now, products: 2);

            var summary = await SummaryAsync();

            Assert.Equal(2, summary.MonthReceiptsCount);
        }

        [Fact]
        public async Task GetSummaryAsync_TwoSales_ReturnsAverageReceipt()
        {
            await SellAsync(Now);
            await SellAsync(Now, products: 2);

            var summary = await SummaryAsync();

            Assert.Equal(375m, summary.MonthAverageReceipt);
        }

        private async Task<ReportSummaryDto> SummaryWithOneFullyReturnedSaleAsync()
        {
            var returned = await SellAsync(Now);
            await SellAsync(Now, products: 2);
            await ReturnFirstItemAsync(returned);
            return await SummaryAsync();
        }

        [Fact]
        public async Task GetSummaryAsync_FullyReturnedSale_ExcludedFromReceiptsCount()
        {
            var summary = await SummaryWithOneFullyReturnedSaleAsync();

            Assert.Equal(1, summary.MonthReceiptsCount);
        }

        [Fact]
        public async Task GetSummaryAsync_FullyReturnedSale_ExcludedFromAverageReceipt()
        {
            var summary = await SummaryWithOneFullyReturnedSaleAsync();

            Assert.Equal(500m, summary.MonthAverageReceipt);
        }

        [Fact]
        public async Task GetSummaryAsync_PartiallyReturnedSale_StaysInReceiptsCount()
        {
            var sale = await SellAsync(Now, products: 2);
            await ReturnFirstItemAsync(sale);

            var summary = await SummaryAsync();

            Assert.Equal(1, summary.MonthReceiptsCount);
        }

        [Fact]
        public async Task GetSummaryAsync_SaleWithDiscount_ReturnsRevenueAfterDiscount()
        {
            await SellAsync(Now, discount: 100m, products: 2);

            var summary = await SummaryAsync();

            Assert.Equal(400m, summary.MonthRevenue);
        }

        [Fact]
        public async Task GetSummaryAsync_SaleWithDiscount_SubtractsDiscountFromProfit()
        {
            await SellAsync(Now, discount: 100m, products: 2);

            var summary = await SummaryAsync();

            Assert.Equal(200m, summary.MonthNetProfit);
        }

        [Fact]
        public async Task GetSummaryAsync_ReturnedItem_IsExcludedFromRevenue()
        {
            var sale = await SellAsync(Now, products: 2);
            await ReturnFirstItemAsync(sale);

            var summary = await SummaryAsync();

            Assert.Equal(250m, summary.MonthRevenue);
        }

        [Fact]
        public async Task GetSummaryAsync_ReturnedItem_IsExcludedFromProfit()
        {
            var sale = await SellAsync(Now, products: 2);
            await ReturnFirstItemAsync(sale);

            var summary = await SummaryAsync();

            Assert.Equal(150m, summary.MonthNetProfit);
        }

        [Fact]
        public async Task GetSummaryAsync_SaleFromPreviousMonth_IsNotInMonthRevenue()
        {
            await SellAsync(Now.AddMonths(-1));

            var summary = await SummaryAsync();

            Assert.Equal(0m, summary.MonthRevenue);
        }

        [Fact]
        public async Task GetSummaryAsync_SaleFromPreviousMonth_CountsInYearTurnover()
        {
            await SellAsync(Now.AddMonths(-1));

            var summary = await SummaryAsync();

            Assert.Equal(250m, summary.YearTurnover);
        }

        [Fact]
        public async Task GetSummaryAsync_SaleFromPreviousYear_IsNotInYearTurnover()
        {
            await SellAsync(Now.AddYears(-1));

            var summary = await SummaryAsync();

            Assert.Equal(0m, summary.YearTurnover);
        }

        [Fact]
        public async Task GetSummaryAsync_SaleAfterKyivMidnightOnFirstDay_CountsInThisMonth()
        {
            await SellAsync(new DateTimeOffset(2026, 9, 30, 21, 30, 0, TimeSpan.Zero));

            var summary = await SummaryAsync();

            Assert.Equal(250m, summary.MonthRevenue);
        }

        [Fact]
        public async Task GetSummaryAsync_SaleBeforeKyivMidnightOnLastDay_IsNotInThisMonth()
        {
            await SellAsync(new DateTimeOffset(2026, 9, 30, 20, 30, 0, TimeSpan.Zero));

            var summary = await SummaryAsync();

            Assert.Equal(0m, summary.MonthRevenue);
        }

        [Fact]
        public async Task GetSummaryAsync_SaleAfterKyivNewYearMidnight_CountsInThisYear()
        {
            await SellAsync(new DateTimeOffset(2025, 12, 31, 22, 30, 0, TimeSpan.Zero));

            var summary = await SummaryAsync();

            Assert.Equal(250m, summary.YearTurnover);
        }

        [Fact]
        public async Task GetSummaryAsync_SaleBeforeKyivNewYearMidnight_IsNotInThisYear()
        {
            await SellAsync(new DateTimeOffset(2025, 12, 31, 21, 30, 0, TimeSpan.Zero));

            var summary = await SummaryAsync();

            Assert.Equal(0m, summary.YearTurnover);
        }

        [Fact]
        public async Task GetSummaryAsync_SaleOfOtherStore_IsIgnored()
        {
            await SellAsync(Now);
            var otherStore = _db.AddStore("Other");

            var summary = await SummaryAsync(otherStore.Id);

            Assert.Equal(0m, summary.MonthRevenue);
        }

        [Fact]
        public async Task GetSummaryAsync_ReturnsStoreLimit()
        {
            SetLimit(500m);

            var summary = await SummaryAsync();

            Assert.Equal(500m, summary.AnnualTurnoverLimit);
        }

        [Fact]
        public async Task GetSummaryAsync_ReturnsLimitUsedPercent()
        {
            SetLimit(500m);
            await SellAsync(Now);

            var summary = await SummaryAsync();

            Assert.Equal(50m, summary.LimitUsedPercent);
        }

        [Fact]
        public async Task GetSummaryAsync_TurnoverAboveNinetyPercent_ShowsLimitWarning()
        {
            SetLimit(260m);
            await SellAsync(Now);

            var summary = await SummaryAsync();

            Assert.True(summary.IsLimitWarning);
        }

        [Fact]
        public async Task GetSummaryAsync_TurnoverBelowNinetyPercent_HidesLimitWarning()
        {
            SetLimit(500m);
            await SellAsync(Now);

            var summary = await SummaryAsync();

            Assert.False(summary.IsLimitWarning);
        }

        [Fact]
        public async Task GetSummaryAsync_ZeroLimit_HidesLimitWarning()
        {
            SetLimit(0m);
            await SellAsync(Now);

            var summary = await SummaryAsync();

            Assert.False(summary.IsLimitWarning);
        }

        [Fact]
        public async Task GetSummaryAsync_TopBrands_AreOrderedByRevenue()
        {
            await SellAsync(Now, brand: "Adidas");
            await SellAsync(Now, brand: "Nike", products: 2);

            var summary = await SummaryAsync();

            Assert.Equal(new[] { "Nike", "Adidas" }, summary.TopBrands.Select(b => b.Brand));
        }

        [Fact]
        public async Task GetSummaryAsync_TopBrands_CountSoldItems()
        {
            await SellAsync(Now, brand: "Nike", products: 2);

            var summary = await SummaryAsync();

            Assert.Equal(2, summary.TopBrands.Single().ItemsSold);
        }

        [Fact]
        public async Task GetSummaryAsync_TopBrands_ExcludeReturnedItems()
        {
            var sale = await SellAsync(Now, brand: "Nike", products: 2);
            await ReturnFirstItemAsync(sale);

            var summary = await SummaryAsync();

            Assert.Equal(1, summary.TopBrands.Single().ItemsSold);
        }

        [Fact]
        public async Task GetSummaryAsync_ProductOlderThanThirtyDays_IsStale()
        {
            AddAgedProduct(40, "Завислий");

            var summary = await SummaryAsync();

            Assert.Equal(new[] { "Завислий" }, summary.StaleProducts.Select(p => p.Title));
        }

        [Fact]
        public async Task GetSummaryAsync_StaleProduct_ReturnsDaysInStock()
        {
            AddAgedProduct(40);

            var summary = await SummaryAsync();

            Assert.Equal(40, summary.StaleProducts.Single().DaysInStock);
        }

        [Fact]
        public async Task GetSummaryAsync_FreshProduct_IsNotStale()
        {
            AddAgedProduct(10);

            var summary = await SummaryAsync();

            Assert.Empty(summary.StaleProducts);
        }

        [Fact]
        public async Task GetSummaryAsync_SoldOldProduct_IsNotStale()
        {
            AddAgedProduct(40, status: ProductStatus.Sold);

            var summary = await SummaryAsync();

            Assert.Empty(summary.StaleProducts);
        }

        [Fact]
        public async Task GetSummaryAsync_RecentOperations_StartWithNewestSale()
        {
            await SellAsync(Now.AddDays(-1));
            await SellAsync(Now);

            var summary = await SummaryAsync();

            Assert.Equal(new[] { 2, 1 }, summary.RecentOperations.Select(o => o.Number));
        }

        [Fact]
        public async Task GetSummaryAsync_RecentOperations_ReturnReceiptTotal()
        {
            await SellAsync(Now, discount: 100m, products: 2);

            var summary = await SummaryAsync();

            Assert.Equal(400m, summary.RecentOperations.Single().Total);
        }

        [Fact]
        public async Task GetSummaryAsync_SaleWithReturn_IsFlaggedInRecentOperations()
        {
            var sale = await SellAsync(Now, products: 2);
            await ReturnFirstItemAsync(sale);

            var summary = await SummaryAsync();

            Assert.True(summary.RecentOperations.Single().HasReturns);
        }
    }
}
