using System.Globalization;
using ClickHouseUI.Services;

namespace ClickHouseUI.Api;

internal static class QueriesApi
{
    public static async Task<object> GetSlowAsync(
        ClickHouseQueryService q,
        ClickHouseDashboardOptions options,
        CancellationToken ct)
    {
        var limit = Math.Clamp(options.SlowQueriesLimit, 1, 1000);
        var hours = Math.Clamp(options.QueryLogLookbackHours, 1, 24 * 30);

        var sql = string.Format(CultureInfo.InvariantCulture, @"
SELECT
    query_id,
    user,
    type,
    event_time,
    query_duration_ms,
    read_rows,
    read_bytes,
    result_rows,
    memory_usage,
    formatReadableSize(memory_usage) AS memory,
    formatReadableSize(read_bytes)   AS read_size,
    substring(replaceAll(query, '\n', ' '), 1, 500) AS query
FROM system.query_log
WHERE type = 'QueryFinish'
  AND event_time > now() - INTERVAL {0} HOUR
ORDER BY query_duration_ms DESC
LIMIT {1}", hours, limit);

        var rows = await q.QueryAsync(sql, cancellationToken: ct).ConfigureAwait(false);
        return new
        {
            lookbackHours = hours,
            limit,
            queries = rows
        };
    }
}
