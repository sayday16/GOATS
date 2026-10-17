using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using GoatStore.Core.DTOs;
using GoatStore.Core.Interfaces;

namespace LogWindowUI.Services
{
    public class AuthApiClient : IAuthService
    {
        private readonly HttpClient _httpClient;

        public AuthApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public Task<AuthResponseDto> RegisterAsync(
            RegisterRequestDto request, CancellationToken cancellationToken = default) =>
            PostAsync("api/auth/register", request, cancellationToken);

        public Task<AuthResponseDto> LoginAsync(
            LoginRequestDto request, CancellationToken cancellationToken = default) =>
            PostAsync("api/auth/login", request, cancellationToken);

        public void Dispose()
        {
        }

        private async Task<AuthResponseDto> PostAsync<TRequest>(
            string path, TRequest request, CancellationToken cancellationToken)
        {
            using HttpResponseMessage response =
                await _httpClient.PostAsJsonAsync(path, request, cancellationToken);

            AuthResponseDto? body = null;
            try
            {
                body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(cancellationToken);
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
            {
            }

            if (body is not null)
                return body;

            return new AuthResponseDto
            {
                IsSuccess = false,
                Message = response.StatusCode == HttpStatusCode.TooManyRequests
                    ? "Забагато спроб. Спробуйте ще раз за хвилину."
                    : $"Сервер повернув неочікувану відповідь ({(int)response.StatusCode})."
            };
        }
    }
}
