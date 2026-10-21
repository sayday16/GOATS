using GoatStore.Core.DTOs;

namespace LogWindowUI.Services
{
    public static class AuthSession
    {
        public static AuthResponseDto? User { get; private set; }
        public static string? AccessToken { get; private set; }
        public static DateTime? ExpiresAt { get; private set; }

        public static bool IsActive =>
            AccessToken is not null && ExpiresAt is { } expires && expires > DateTime.UtcNow;

        public static void Start(AuthResponseDto response)
        {
            User = response;
            AccessToken = response.AccessToken;
            ExpiresAt = response.AccessTokenExpiresAt;
        }

        public static void Clear()
        {
            User = null;
            AccessToken = null;
            ExpiresAt = null;
        }
    }
}
