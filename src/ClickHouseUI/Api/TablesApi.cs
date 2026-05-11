using ClickHouseUI.Services;

namespace ClickHouseUI.Api;

internal static class TablesApi
{
    public static async Task<object> GetAsync(ClickHouseQueryService q, CancellationToken ct)
    {
        // NOTE: avoid aliasing an aggregate with the same name as a source column
        // (e.g. `sum(rows) AS rows`). Modern ClickHouse resolves the inner reference
        // back to the alias and raises ILLEGAL_AGGREGATION.
        const string sql = @"
SELECT
    database,
    table,
    sum(rows)                                      AS total_rows,
    sum(bytes_on_disk)                             AS total_bytes,
    formatReadableSize(sum(bytes_on_disk))         AS size,
    sum(data_compressed_bytes)                     AS compressed_bytes,
    sum(data_uncompressed_bytes)                   AS uncompressed_bytes,
    round(sum(data_uncompressed_bytes) / nullIf(sum(data_compressed_bytes), 0), 2) AS compression_ratio,
    count()                                        AS parts_count,
    max(modification_time)                         AS last_modified
FROM system.parts
WHERE active AND database NOT IN ('system','INFORMATION_SCHEMA','information_schema')
GROUP BY database, table
ORDER BY total_bytes DESC
LIMIT 500";

        var rows = await q.QueryAsync(sql, cancellationToken: ct).ConfigureAwait(false);
        return new { tables = rows };
    }

    public static async Task<object> GetPartsAsync(
        ClickHouseQueryService q,
        string? database,
        string? table,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(database) || string.IsNullOrWhiteSpace(table))
        {
            return new { error = "database and table query parameters are required" };
        }

        const string sql = @"
SELECT
    partition,
    name AS part,
    active,
    rows,
    formatReadableSize(bytes_on_disk) AS size,
    bytes_on_disk,
    marks,
    level,
    modification_time,
    min_time,
    max_time
FROM system.parts
WHERE database = {db:String} AND table = {tbl:String}
ORDER BY active DESC, partition, name
LIMIT 1000";

        var rows = await q.QueryAsync(sql, new Dictionary<string, object?>
        {
            ["db"] = database,
            ["tbl"] = table
        }, ct).ConfigureAwait(false);

        return new { database, table, parts = rows };
    }
}
