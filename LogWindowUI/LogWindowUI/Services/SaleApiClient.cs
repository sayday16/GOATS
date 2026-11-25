using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using GoatStore.Core.DTOs;

namespace LogWindowUI.Services
{
    public interface ISaleApiClient
    {
        Task<SaleResponseDto> CreateAsync(CreateSaleRequestDto request, CancellationToken cancellationToken = default);
        Task<SaleResponseDto> GetByNumberAsync(int number, CancellationToken cancellationToken = default);
        Task<SaleResponseDto> ReturnAsync(int number, ReturnSaleRequestDto request, CancellationToken cancellationToken = default);
    }

    public class SaleApiClient : ISaleApiClient
    {
        private const string BasePath = "api/sales";

        private readonly HttpClient _httpClient;

        public SaleApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<SaleResponseDto> CreateAsync(
            CreateSaleRequestDto request, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsJsonAsync(BasePath, request, cancellationToken);
            return await ReadAsync(response, cancellationToken);
        }

        public async Task<SaleResponseDto> GetByNumberAsync(int number, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync($"{BasePath}/{number}", cancellationToken);
            return await ReadAsync(response, cancellationToken);
        }

        public async Task<SaleResponseDto> ReturnAsync(
            int number, ReturnSaleRequestDto request, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsJsonAsync($"{BasePath}/{number}/returns", request, cancellationToken);
            return await ReadAsync(response, cancellationToken);
        }

        private static async Task<SaleResponseDto> ReadAsync(
            HttpResponseMessage response, CancellationToken cancellationToken)
        {
            SaleResponseDto? body = null;
            try
            {
                body = await response.Content.ReadFromJsonAsync<SaleResponseDto>(cancellationToken);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
            {
            }

            if (body is not null && (response.IsSuccessStatusCode || !string.IsNullOrWhiteSpace(body.Message)))
                return body;

            return new SaleResponseDto
            {
                IsSuccess = false,
                Message = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "Сесія завершилась. Увійдіть знову.",
                    HttpStatusCode.Forbidden => "Недостатньо прав для цієї дії.",
                    HttpStatusCode.NotFound => "Чек не знайдено.",
                    _ => $"Сервер повернув неочікувану відповідь ({(int)response.StatusCode})."
                }
            };
        }
    }
}
