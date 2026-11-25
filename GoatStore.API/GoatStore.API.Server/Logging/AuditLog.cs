using System.Security.Claims;
using GoatStore.API.Server.Auth;
using GoatStore.Core.DTOs;

namespace GoatStore.API.Server.Logging
{
    public sealed class AuditLog
    {
        private readonly ILogger<AuditLog> _logger;

        public AuditLog(ILogger<AuditLog> logger)
        {
            _logger = logger;
        }

        public void Login(string? email, AuthResponseDto result)
        {
            if (result.IsSuccess)
                _logger.LogInformation("Login succeeded: user {UserId}, role {Role}, store {StoreId}",
                    result.UserId?.ToString(), result.Role?.ToString(), result.StoreId?.ToString());
            else
                _logger.LogWarning("Login failed for {Email}: {Reason}", MaskEmail(email), result.Message);
        }

        public void Registration(string? email, AuthResponseDto result)
        {
            if (result.IsSuccess)
                _logger.LogInformation("User registered: user {UserId}, role {Role}, store {StoreId}",
                    result.UserId?.ToString(), result.Role?.ToString(), result.StoreId?.ToString());
            else
                _logger.LogWarning("Registration rejected for {Email}: {Reason}", MaskEmail(email), result.Message);
        }

        public void Product(string action, ClaimsPrincipal user, Guid? productId, ProductResponseDto result)
        {
            if (result.IsSuccess)
                _logger.LogInformation("Product {Action}: product {ProductId}, store {StoreId}, user {UserId}",
                    action, (result.Product?.Id ?? productId)?.ToString(), StoreOf(user), UserOf(user));
            else
                _logger.LogWarning("Product not {Action}: product {ProductId}, store {StoreId}, user {UserId}, reason: {Reason}",
                    action, productId?.ToString() ?? "new", StoreOf(user), UserOf(user), result.Message);
        }

        public void Sale(string action, ClaimsPrincipal user, int? number, SaleResponseDto result)
        {
            if (result.IsSuccess)
                _logger.LogInformation("Sale {Action}: receipt {Number}, total {Total}, refunded {Refunded}, store {StoreId}, user {UserId}",
                    action, result.Sale?.Number ?? number, result.Sale?.Total, result.Sale?.RefundedTotal, StoreOf(user), UserOf(user));
            else
                _logger.LogWarning("Sale not {Action}: receipt {Number}, store {StoreId}, user {UserId}, reason: {Reason}",
                    action, number?.ToString() ?? "new", StoreOf(user), UserOf(user), result.Message);
        }

        public static string MaskEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return "(empty)";

            var trimmed = email.Trim();
            var at = trimmed.IndexOf('@');
            return at <= 0 ? "***" : $"{trimmed[0]}***{trimmed[at..]}";
        }

        private static string? StoreOf(ClaimsPrincipal user) => user.FindFirstValue(GoatClaims.StoreId);

        private static string? UserOf(ClaimsPrincipal user) => user.FindFirstValue(GoatClaims.UserId);
    }
}
