using System;
using System.Linq;
using GoatStore.Core.DTOs;
using LogWindowUI.ViewModels;

namespace LogWindowUI.Tests
{
    public class AnalyticsViewModelTests
    {
        private readonly FakeReportApiClient _reportApi = new();

        [Fact]
        public void Constructor_SetsMonthRevenue()
        {
            _reportApi.Summary = new ReportSummaryDto { MonthRevenue = 50000m };

            var vm = new AnalyticsViewModel(_reportApi);

            Assert.Equal(50000m, vm.MonthRevenue);
        }

        [Fact]
        public void Constructor_SetsMonthNetProfit()
        {
            _reportApi.Summary = new ReportSummaryDto { MonthNetProfit = 25000m };

            var vm = new AnalyticsViewModel(_reportApi);

            Assert.Equal(25000m, vm.MonthNetProfit);
        }

        [Fact]
        public void Constructor_SetsMonthReceiptsCount()
        {
            _reportApi.Summary = new ReportSummaryDto { MonthReceiptsCount = 42 };

            var vm = new AnalyticsViewModel(_reportApi);

            Assert.Equal(42, vm.MonthReceiptsCount);
        }

        [Fact]
        public void Constructor_SetsMonthAverageReceipt()
        {
            _reportApi.Summary = new ReportSummaryDto { MonthAverageReceipt = 1190.5m };

            var vm = new AnalyticsViewModel(_reportApi);

            Assert.Equal(1190.5m, vm.MonthAverageReceipt);
        }

        [Fact]
        public void Constructor_SetsYearTurnover()
        {
            _reportApi.Summary = new ReportSummaryDto { YearTurnover = 350000m };

            var vm = new AnalyticsViewModel(_reportApi);

            Assert.Equal(350000m, vm.YearTurnover);
        }

        [Fact]
        public void Constructor_SetsAnnualTurnoverLimit()
        {
            _reportApi.Summary = new ReportSummaryDto { AnnualTurnoverLimit = 1000000m };

            var vm = new AnalyticsViewModel(_reportApi);

            Assert.Equal(1000000m, vm.AnnualTurnoverLimit);
        }

        [Fact]
        public void Constructor_SetsLimitUsedPercent()
        {
            _reportApi.Summary = new ReportSummaryDto { LimitUsedPercent = 35m };

            var vm = new AnalyticsViewModel(_reportApi);

            Assert.Equal(35m, vm.LimitUsedPercent);
        }

        [Fact]
        public void Constructor_SetsIsLimitWarning()
        {
            _reportApi.Summary = new ReportSummaryDto { IsLimitWarning = true };

            var vm = new AnalyticsViewModel(_reportApi);

            Assert.True(vm.IsLimitWarning);
        }

        [Fact]
        public void Constructor_SetsTopBrands()
        {
            _reportApi.Summary = new ReportSummaryDto
            {
                TopBrands = [new BrandSalesDto { Brand = "Nike", ItemsSold = 10, Revenue = 15000m }]
            };

            var vm = new AnalyticsViewModel(_reportApi);

            Assert.Single(vm.TopBrands);
        }

        [Fact]
        public void Constructor_SetsStaleProducts()
        {
            _reportApi.Summary = new ReportSummaryDto
            {
                StaleProducts = [new StaleProductDto { Title = "Jeans", Brand = "Levi's", DaysInStock = 45 }]
            };

            var vm = new AnalyticsViewModel(_reportApi);

            Assert.Single(vm.StaleProducts);
        }

        [Fact]
        public void Constructor_SetsRecentOperations()
        {
            _reportApi.Summary = new ReportSummaryDto
            {
                RecentOperations = [new RecentOperationDto { Number = 101, Total = 1200m }]
            };

            var vm = new AnalyticsViewModel(_reportApi);

            Assert.Single(vm.RecentOperations);
        }

        [Fact]
        public void Constructor_RecentOperationInUtc_ShowsLocalTime()
        {
            var createdAtUtc = new DateTime(2026, 11, 26, 7, 15, 0, DateTimeKind.Utc);
            _reportApi.Summary = new ReportSummaryDto
            {
                RecentOperations = [new RecentOperationDto { Number = 4, CreatedAt = createdAtUtc }]
            };

            var vm = new AnalyticsViewModel(_reportApi);

            Assert.Equal(createdAtUtc.ToLocalTime(), vm.RecentOperations.Single().CreatedAt);
        }

        [Fact]
        public void Constructor_WhenApiFails_SetsErrorMessage()
        {
            _reportApi.Exception = new InvalidOperationException("Помилка 403");

            var vm = new AnalyticsViewModel(_reportApi);

            Assert.Contains("Помилка 403", vm.ErrorMessage);
        }

        [Fact]
        public void Constructor_WhenApiFails_SetsHasErrorTrue()
        {
            _reportApi.Exception = new InvalidOperationException("Помилка");

            var vm = new AnalyticsViewModel(_reportApi);

            Assert.True(vm.HasError);
        }

        [Fact]
        public void RefreshCommand_ReloadsReportFromApi()
        {
            var vm = new AnalyticsViewModel(_reportApi);
            _reportApi.Summary = new ReportSummaryDto { MonthRevenue = 99999m };

            vm.RefreshCommand.Execute(null);

            Assert.Equal(99999m, vm.MonthRevenue);
        }
    }
}
