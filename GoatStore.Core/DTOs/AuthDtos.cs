using System.Text.Json.Serialization;
using GoatStore.DataAccess.Entities;

namespace GoatStore.Core.DTOs
{
    public class RegisterRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;

        public UserRole? Role { get; set; }

        public string StoreName { get; set; } = string.Empty;
        public string StorePassword { get; set; } = string.Empty;
    }

    public class LoginRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public enum AuthError
    {
        None = 0,
        Validation,    // 400
        Unauthorized,  // 401
        Forbidden,     // 403
        Conflict       // 409
    }

    public class AuthResponseDto
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public Guid? UserId { get; set; }
        public string? Email { get; set; }
        public UserRole? Role { get; set; }

        public Guid? StoreId { get; set; }
        public string? StoreName { get; set; }

        [JsonIgnore]
        public AuthError Error { get; set; }
    }
}