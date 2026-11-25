using GoatStore.Core.DTOs;
using GoatStore.Core.Interfaces;
using GoatStore.DataAccess;
using GoatStore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoatStore.Core.Services
{
    public class ReportService : IReportService
    {
        public const int StaleAfterDays = 30;
        public const decimal LimitWarningPercent = 90m;
        private const int TopBrandsCount = 5;
        private const int ListSize = 10;

        private static readonly TimeZoneInfo StoreTimeZone = FindStoreTimeZone();

        private readonly GoatStoreDbContext _context;
        private readonly TimeProvider _timeProvider;

        private static TimeZoneInfo FindStoreTimeZone()
        {
            foreach (var id in new[] { "Europe/Kyiv", "Europe/Kiev", "FLE Standard Time" })
            {
                if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
                    return zone;
            }

            return TimeZoneInfo.Utc;
        }

        private static DateTime LocalToUtc(DateTime local) =>
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), StoreTimeZone);

        public ReportService(GoatStoreDbContext context, TimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        public async Task<ReportSummaryDto> GetSummaryAsync(Guid storeId, CancellationToken cancellationToken = default)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, StoreTimeZone);
            var monthStart = LocalToUtc(new DateTime(localNow.Year, localNow.Month, 1));
            var yearStart = LocalToUtc(new DateTime(localNow.Year, 1, 1));

            var yearSales = await _context.Sales
                .AsNoTracking()
                .Include(s => s.Items).ThenInclude(i => i.Product)
                .Where(s => s.StoreId == storeId && s.CreatedAt >= yearStart)
                .ToListAsync(cancellationToken);

            var monthSales = yearSales.Where(s => s.CreatedAt >= monthStart).ToList();

            var monthRevenue = monthSales.Sum(SaleMath.Revenue);
            var yearTurnover = yearSales.Sum(SaleMath.Revenue);
            var limit = await _context.Stores
                .Where(s => s.Id == storeId)
                .Select(s => s.AnnualTurnoverLimit)
                .FirstOrDefaultAsync(cancellationToken);
            var limitPercent = limit > 0 ? Math.Round(yearTurnover / limit * 100, 1) : 0;

            return new ReportSummaryDto
            {
                MonthRevenue = monthRevenue,
                MonthNetProfit = monthRevenue - monthSales.Sum(SaleMath.Cost),
                MonthReceiptsCount = monthSales.Count,
                MonthAverageReceipt = monthSales.Count == 0 ? 0 : Math.Round(monthRevenue / monthSales.Count, 2),
                YearTurnover = yearTurnover,
                AnnualTurnoverLimit = limit,
                LimitUsedPercent = limitPercent,
                IsLimitWarning = limitPercent > LimitWarningPercent,
                TopBrands = BuildTopBrands(monthSales),
                StaleProducts = await GetStaleProductsAsync(storeId, now, cancellationToken),
                RecentOperations = yearSales
                    .OrderByDescending(s => s.CreatedAt)
                    .Take(ListSize)
                    .Select(s => new RecentOperationDto
                    {
                        Number = s.Number,
                        CreatedAt = s.CreatedAt,
                        Total = SaleMath.Total(s),
                        PaymentMethod = s.PaymentMethod,
                        HasReturns = s.Items.Any(i => i.ReturnedQuantity > 0)
                    })
                    .ToList()
            };
        }

        private static List<BrandSalesDto> BuildTopBrands(List<Sale> monthSales) =>
            monthSales
                .SelectMany(s => s.Items.Select(i => (Item: i, Ratio: SaleMath.DiscountRatio(s))))
                .Where(x => x.Item.Quantity > x.Item.ReturnedQuantity && x.Item.Product is not null)
                .GroupBy(x => x.Item.Product!.Brand)
                .Select(g => new BrandSalesDto
                {
                    Brand = g.Key,
                    ItemsSold = g.Sum(x => x.Item.Quantity - x.Item.ReturnedQuantity),
                    Revenue = Math.Round(g.Sum(x => x.Item.UnitPrice * (x.Item.Quantity - x.Item.ReturnedQuantity) * x.Ratio), 2)
                })
                .OrderByDescending(b => b.Revenue)
                .Take(TopBrandsCount)
                .ToList();

        private async Task<List<StaleProductDto>> GetStaleProductsAsync(
            Guid storeId, DateTime now, CancellationToken cancellationToken)
        {
            var threshold = now.AddDays(-StaleAfterDays);

            var products = await _context.Products
                .AsNoTracking()
                .Where(p => p.StoreId == storeId && p.Quantity > 0 && p.CreatedAt <= threshold)
                .OrderBy(p => p.CreatedAt)
                .Take(ListSize)
                .ToListAsync(cancellationToken);

            return products.Select(p => new StaleProductDto
            {
                Id = p.Id,
                Title = p.Title,
                Brand = p.Brand,
                DaysInStock = (now - p.CreatedAt).Days
            }).ToList();
        }
    }
}
