using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GoatStore.Core.DTOs;
using LogWindowUI.Services;

namespace LogWindowUI.Tests
{
    public class FakeProductApiClient : IProductApiClient
    {
        public List<ProductDto> ProductsToReturn { get; set; } = new();
        public ProductFilterOptionsDto FilterOptionsToReturn { get; set; } = new()
        {
            Categories = new List<string> { "Одяг", "Взуття" },
            Brands = new List<string> { "Nike", "Adidas" }
        };
        public ProductResponseDto CreateResponse { get; set; } = new() { IsSuccess = true };
        public ProductResponseDto SellResponse { get; set; } = new() { IsSuccess = true };

        public ProductFilterDto? LastGetAllFilter { get; private set; }
        public SaveProductRequestDto? LastCreateRequest { get; private set; }
        public Guid? LastSellId { get; private set; }

        public Task<IReadOnlyList<ProductDto>> GetAllAsync(ProductFilterDto? filter = null, CancellationToken cancellationToken = default)
        {
            LastGetAllFilter = filter;
            return Task.FromResult<IReadOnlyList<ProductDto>>(ProductsToReturn);
        }

        public Task<ProductFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(FilterOptionsToReturn);
        }

        public Task<ProductResponseDto> GetAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProductResponseDto { IsSuccess = true });
        }

        public Task<ProductResponseDto> CreateAsync(SaveProductRequestDto request, CancellationToken cancellationToken = default)
        {
            LastCreateRequest = request;
            return Task.FromResult(CreateResponse);
        }

        public Task<ProductResponseDto> UpdateAsync(Guid productId, SaveProductRequestDto request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProductResponseDto { IsSuccess = true });
        }

        public Task<ProductResponseDto> SellAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            LastSellId = productId;
            return Task.FromResult(SellResponse);
        }

        public Task<ProductResponseDto> DeleteAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProductResponseDto { IsSuccess = true });
        }
    }
}
