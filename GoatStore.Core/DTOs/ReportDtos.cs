using GoatStore.DataAccess.Entities;

namespace GoatStore.Core.DTOs
{
    public class BrandSalesDto
    {
        public string Brand { get; set; } = string.Empty;
        public int ItemsSold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class StaleProductDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public int DaysInStock { get; set; }
    }

    public class RecentOperationDto
    {
        public int Number { get; set; }
        public DateTime CreatedAt { get; set; }
        public decimal Total { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public bool HasReturns { get; set; }
    }

    public class ReportSummaryDto
    {
        public decimal MonthRevenue { get; set; }
        public decimal MonthNetProfit { get; set; }
        public int MonthReceiptsCount { get; set; }
        public decimal MonthAverageReceipt { get; set; }
        public decimal YearTurnover { get; set; }
        public decimal AnnualTurnoverLimit { get; set; }
        public decimal LimitUsedPercent { get; set; }
        public bool IsLimitWarning { get; set; }
        public IReadOnlyList<BrandSalesDto> TopBrands { get; set; } = [];
        public IReadOnlyList<StaleProductDto> StaleProducts { get; set; } = [];
        public IReadOnlyList<RecentOperationDto> RecentOperations { get; set; } = [];
    }
}
