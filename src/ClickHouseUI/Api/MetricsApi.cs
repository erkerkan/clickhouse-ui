using ClickHouseUI.Services;

namespace ClickHouseUI.Api;

internal static class MetricsApi
{
    // Curated subset of system.metrics that's most relevant for an operator.
    // Returning everything would be a lot of noise on the dashboard.
    private static readonly string[] InterestingMetrics =
    {
        "Query",
        "TCPConnection",
        "HTTPConnection",
        "BackgroundMergesAndMutationsPoolTask",
        "BackgroundFetchesPoolTask",
        "MemoryTracking",
        "ReplicatedFetch",
        "ReplicatedSend",
        "PartsActive",
        "PartsCommitted"
    };

    private static readonly string[] InterestingEvents =
    {
        "Query",
        "SelectQuery",
        "InsertQuery",
        "FailedQuery",
        "InsertedRows",
        "InsertedBytes",
        "SelectedRows",
        "SelectedBytes",
        "MergedRows",
        "MergedUncompressedBytes",
        "NetworkReceiveBytes",
        "NetworkSendBytes"
    };

    public static async Task<object> GetAsync(ClickHouseQueryService q, CancellationToken ct)
    {
        var metricsSql =
            "SELECT metric, value FROM system.metrics WHERE metric IN " +
            "(" + string.Join(',', InterestingMetrics.Select(m => $"'{m}'")) + ") ORDER BY metric";

        var eventsSql =
            "SELECT event, value FROM system.events WHERE event IN " +
            "(" + string.Join(',', InterestingEvents.Select(e => $"'{e}'")) + ") ORDER BY event";

        var asyncMetricsSql =
            "SELECT metric, value FROM system.asynchronous_metrics " +
            "WHERE metric IN ('OSCPUVirtualTimeMicroseconds','MemoryResident','LoadAverage1','TotalRowsOfMergeTreeTablesSystem','TotalBytesOfMergeTreeTablesSystem') " +
            "ORDER BY metric";

        var metricsTask = q.QueryAsync(metricsSql, cancellationToken: ct);
        var eventsTask = q.QueryAsync(eventsSql, cancellationToken: ct);
        var asyncTask = q.QueryAsync(asyncMetricsSql, cancellationToken: ct);

        await Task.WhenAll(metricsTask, eventsTask, asyncTask).ConfigureAwait(false);

        return new
        {
            timestamp = DateTimeOffset.UtcNow,
            metrics = metricsTask.Result,
            events = eventsTask.Result,
            asyncMetrics = asyncTask.Result
        };
    }
}
