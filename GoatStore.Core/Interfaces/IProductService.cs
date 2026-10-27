using GoatStore.Core.DTOs;
using GoatStore.DataAccess.Entities;

namespace GoatStore.Core.Interfaces
{
    public interface IProductService
    {
        Task<IReadOnlyList<ProductDto>> GetAllAsync(Guid storeId, ProductStatus? status = null, CancellationToken cancellationToken = default);
        Task<ProductResponseDto> GetAsync(Guid storeId, Guid productId, CancellationToken cancellationToken = default);
        Task<ProductResponseDto> CreateAsync(Guid storeId, SaveProductRequestDto request, CancellationToken cancellationToken = default);
        Task<ProductResponseDto> UpdateAsync(Guid storeId, Guid productId, SaveProductRequestDto request, CancellationToken cancellationToken = default);
        Task<ProductResponseDto> SellAsync(Guid storeId, Guid productId, CancellationToken cancellationToken = default);
        Task<ProductResponseDto> DeleteAsync(Guid storeId, Guid productId, CancellationToken cancellationToken = default);
    }
}
