using GoatStore.Core.DTOs;

namespace GoatStore.Core.Interfaces
{
    public interface ISaleService
    {
        Task<SaleResponseDto> CreateAsync(Guid storeId, Guid userId, CreateSaleRequestDto request, CancellationToken cancellationToken = default);
        Task<SaleResponseDto> GetByNumberAsync(Guid storeId, int number, CancellationToken cancellationToken = default);
        Task<SaleResponseDto> ReturnAsync(Guid storeId, int number, ReturnSaleRequestDto request, CancellationToken cancellationToken = default);
    }

    public interface IReportService
    {
        Task<ReportSummaryDto> GetSummaryAsync(Guid storeId, CancellationToken cancellationToken = default);
    }
}
