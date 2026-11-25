using System.Security.Claims;
using GoatStore.API.Server.Auth;
using GoatStore.Core.Interfaces;

namespace GoatStore.API.Server.Reports
{
    public static class ReportEndpoints
    {
        public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
        {
            var reports = app.MapGroup("/api/reports").RequireAuthorization(AuthSetup.AdminOnlyPolicy);

            reports.MapGet("/summary", async (ClaimsPrincipal user, IReportService reportService, CancellationToken cancellationToken) =>
                Guid.TryParse(user.FindFirstValue(GoatClaims.StoreId), out var storeId)
                    ? Results.Ok(await reportService.GetSummaryAsync(storeId, cancellationToken))
                    : Results.Unauthorized());

            return app;
        }
    }
}
