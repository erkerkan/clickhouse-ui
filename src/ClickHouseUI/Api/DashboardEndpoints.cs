using ClickHouseUI.Services;
using Microsoft.AspNetCore.Http;

namespace ClickHouseUI.Api;

internal delegate Task<object?> DashboardEndpoint(
    HttpContext context,
    ClickHouseQueryService query,
    ClickHouseDashboardOptions options,
    CancellationToken cancellationToken);

/// <summary>
/// Lookup table of dashboard API endpoints. Keys are the path that follows the
/// dashboard mount point (lower-case). Adding a new endpoint is just a new row.
/// </summary>
internal static class DashboardEndpoints
{
    public static readonly IReadOnlyDictionary<string, DashboardEndpoint> All =
        new Dictionary<string, DashboardEndpoint>(StringComparer.OrdinalIgnoreCase)
        {
            ["/api/overview"]     = (ctx, q, opt, ct) => Cast(OverviewApi.GetAsync(q, opt, ct)),
            ["/api/metrics"]      = (ctx, q, opt, ct) => Cast(MetricsApi.GetAsync(q, ct)),
            ["/api/tables"]       = (ctx, q, opt, ct) => Cast(TablesApi.GetAsync(q, ct)),
            ["/api/parts"]        = (ctx, q, opt, ct) => Cast(TablesApi.GetPartsAsync(
                                        q,
                                        ctx.Request.Query["database"],
                                        ctx.Request.Query["table"],
                                        ct)),
            ["/api/slow-queries"] = (ctx, q, opt, ct) => Cast(QueriesApi.GetSlowAsync(q, opt, ct)),
            ["/api/explain"]      = (ctx, q, opt, ct) => Cast(ExplainApi.PostAsync(q, ctx)),
        };

    // Bridge each endpoint's Task<T> to the registry's Task<object?> without
    // boxing-induced gotchas. The local async wrapper keeps stack traces clean.
    private static async Task<object?> Cast<T>(Task<T> task) => await task.ConfigureAwait(false);
}
