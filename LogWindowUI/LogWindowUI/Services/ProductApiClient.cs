using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using GoatStore.Core.DTOs;

namespace LogWindowUI.Services
{
    public interface IProductApiClient
    {
        Task<IReadOnlyList<ProductDto>> GetAllAsync(ProductFilterDto? filter = null, CancellationToken cancellationToken = default);
        Task<ProductFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken = default);
        Task<ProductResponseDto> GetAsync(Guid productId, CancellationToken cancellationToken = default);
        Task<ProductResponseDto> CreateAsync(SaveProductRequestDto request, CancellationToken cancellationToken = default);
        Task<ProductResponseDto> UpdateAsync(Guid productId, SaveProductRequestDto request, CancellationToken cancellationToken = default);
        Task<ProductResponseDto> SellAsync(Guid productId, CancellationToken cancellationToken = default);
        Task<ProductResponseDto> DeleteAsync(Guid productId, CancellationToken cancellationToken = default);
    }

    // Магазин береться з токена на сервері, тому storeId клієнт не передає
    public class ProductApiClient : IProductApiClient
    {
        private const string BasePath = "api/products";

        private readonly HttpClient _httpClient;

        public ProductApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IReadOnlyList<ProductDto>> GetAllAsync(
            ProductFilterDto? filter = null, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync(BasePath + BuildQuery(filter), cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<ProductDto>>(cancellationToken) ?? [];
        }

        public async Task<ProductFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync($"{BasePath}/filters", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ProductFilterOptionsDto>(cancellationToken) ?? new();
        }

        public async Task<ProductResponseDto> GetAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync($"{BasePath}/{productId}", cancellationToken);
            return await ReadAsync(response, cancellationToken);
        }

        public async Task<ProductResponseDto> CreateAsync(
            SaveProductRequestDto request, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsJsonAsync(BasePath, request, cancellationToken);
            return await ReadAsync(response, cancellationToken);
        }

        public async Task<ProductResponseDto> UpdateAsync(
            Guid productId, SaveProductRequestDto request, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PutAsJsonAsync($"{BasePath}/{productId}", request, cancellationToken);
            return await ReadAsync(response, cancellationToken);
        }

        public async Task<ProductResponseDto> SellAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsync($"{BasePath}/{productId}/sell", null, cancellationToken);
            return await ReadAsync(response, cancellationToken);
        }

        public async Task<ProductResponseDto> DeleteAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.DeleteAsync($"{BasePath}/{productId}", cancellationToken);
            return await ReadAsync(response, cancellationToken);
        }

        private static string BuildQuery(ProductFilterDto? filter)
        {
            if (filter is null)
                return string.Empty;

            var parameters = new List<string>();
            Add(parameters, "search", filter.Search);
            Add(parameters, "category", filter.Category);
            Add(parameters, "brand", filter.Brand);
            Add(parameters, "status", filter.Status?.ToString());

            return parameters.Count == 0 ? string.Empty : "?" + string.Join("&", parameters);
        }

        private static void Add(List<string> parameters, string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                parameters.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        }

        private static async Task<ProductResponseDto> ReadAsync(
            HttpResponseMessage response, CancellationToken cancellationToken)
        {
            ProductResponseDto? body = null;
            try
            {
                body = await response.Content.ReadFromJsonAsync<ProductResponseDto>(cancellationToken);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
            {
            }

            if (body is not null && (response.IsSuccessStatusCode || !string.IsNullOrWhiteSpace(body.Message)))
                return body;

            return new ProductResponseDto
            {
                IsSuccess = false,
                Message = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "Сесія завершилась. Увійдіть знову.",
                    HttpStatusCode.Forbidden => "Недостатньо прав для цієї дії.",
                    HttpStatusCode.NotFound => "Товар не знайдено.",
                    _ => $"Сервер повернув неочікувану відповідь ({(int)response.StatusCode})."
                }
            };
        }
    }
}
