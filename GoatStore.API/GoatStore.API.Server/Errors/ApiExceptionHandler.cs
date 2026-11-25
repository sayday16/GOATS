using System.Data.Common;
using Microsoft.AspNetCore.Diagnostics;
using Npgsql;

namespace GoatStore.API.Server.Errors
{
    public sealed class ApiExceptionHandler : IExceptionHandler
    {
        public const string DatabaseUnavailableMessage = "База даних тимчасово недоступна. Спробуйте ще раз за хвилину.";
        public const string UnexpectedErrorMessage = "На сервері сталася помилка. Спробуйте ще раз.";

        private static readonly string[] UnavailableSqlStatePrefixes = ["08", "53", "57P"];

        private readonly ILogger<ApiExceptionHandler> _logger;

        public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var databaseUnavailable = IsDatabaseUnavailable(exception);

            _logger.LogError(exception, databaseUnavailable ? "Database is unavailable" : "Unhandled exception");

            httpContext.Response.StatusCode = databaseUnavailable
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status500InternalServerError;

            await httpContext.Response.WriteAsJsonAsync(new
            {
                isSuccess = false,
                message = databaseUnavailable ? DatabaseUnavailableMessage : UnexpectedErrorMessage
            }, cancellationToken);

            return true;
        }

        public static bool IsDatabaseUnavailable(Exception exception) => exception switch
        {
            PostgresException postgres => UnavailableSqlStatePrefixes.Any(prefix => postgres.SqlState.StartsWith(prefix, StringComparison.Ordinal)),
            NpgsqlException => true,
            TimeoutException => true,
            DbException { IsTransient: true } => true,
            _ => exception.InnerException is { } inner && IsDatabaseUnavailable(inner)
        };
    }
}
