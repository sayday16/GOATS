using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
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
            if (!response.IsSuccessStatusCode)
            {
                var message = await ReadServerMessageAsync(response, cancellationToken) ?? DescribeStatus(response.StatusCode);
                throw new HttpRequestException(message, null, response.StatusCode);
            }

            return await response.Content.ReadFromJsonAsync<ReportSummaryDto>(cancellationToken) ?? new();
        }

        private static async Task<string?> ReadServerMessageAsync(
            HttpResponseMessage response, CancellationToken cancellationToken)
        {
            try
            {
                var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
                return body.ValueKind == JsonValueKind.Object
                    && body.TryGetProperty("message", out var message)
                    && message.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(message.GetString())
                    ? message.GetString()
                    : null;
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                return null;
            }
        }

        private static string DescribeStatus(HttpStatusCode status) => status switch
        {
            HttpStatusCode.Unauthorized => "Сесія завершилась. Увійдіть знову.",
            HttpStatusCode.Forbidden => "Звіт доступний лише власнику магазину.",
            HttpStatusCode.ServiceUnavailable => "Сервер тимчасово недоступний. Спробуйте пізніше.",
            _ => $"Сервер повернув неочікувану відповідь ({(int)status})."
        };
    }
}
