using System.Net.Http;
using System.Net.Http.Json;
using GoatStore.Core.DTOs;

namespace LogWindowUI.Services
{
    public interface IReportApiClient
    {
        Task<ReportSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    }

    public class ReportApiClient : IReportApiClient
    {
        private const string BasePath = "api/reports";

        private readonly HttpClient _httpClient;

        public ReportApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ReportSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync($"{BasePath}/summary", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ReportSummaryDto>(cancellationToken) ?? new();
        }
    }
}
