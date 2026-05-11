using ClickHouseUI.Services;

namespace ClickHouseUI.Api;

internal static class OverviewApi
{
    public static async Task<object> GetAsync(
        ClickHouseQueryService q,
        ClickHouseDashboardOptions options,
        CancellationToken ct)
    {
        // Pull the small pieces in parallel so the overview tab loads snappy.
        var versionTask = q.ScalarStringAsync("SELECT version()", ct);
        var uptimeTask  = q.ScalarStringAsync("SELECT uptime()", ct);
        var clustersTask = q.QueryAsync(
            "SELECT cluster, host_address, port, is_local, replica_num, shard_num FROM system.clusters ORDER BY cluster, shard_num, replica_num",
            cancellationToken: ct);

        var diskTask = q.QueryAsync(
            "SELECT name, path, formatReadableSize(free_space) AS free, formatReadableSize(total_space) AS total, " +
            "round(100 * (total_space - free_space) / total_space, 1) AS used_percent " +
            "FROM system.disks",
            cancellationToken: ct);

        var dbCountTask = q.ScalarStringAsync("SELECT count() FROM system.databases", ct);
        var tableCountTask = q.ScalarStringAsync("SELECT count() FROM system.tables WHERE database NOT IN ('system','INFORMATION_SCHEMA','information_schema')", ct);

        await Task.WhenAll(versionTask, uptimeTask, clustersTask, diskTask, dbCountTask, tableCountTask).ConfigureAwait(false);

        return new
        {
            title = options.Title,
            version = versionTask.Result,
            uptimeSeconds = long.TryParse(uptimeTask.Result, out var u) ? u : 0,
            databases = long.TryParse(dbCountTask.Result, out var db) ? db : 0,
            tables = long.TryParse(tableCountTask.Result, out var t) ? t : 0,
            disks = diskTask.Result,
            clusters = clustersTask.Result
        };
    }
}
