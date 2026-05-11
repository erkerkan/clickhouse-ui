using ClickHouseUI.Services;

namespace ClickHouseUI.Api;

internal static class TablesApi
{
    public static async Task<object> GetAsync(ClickHouseQueryService q, CancellationToken ct)
    {
        const string sql = @"
SELECT
    database,
    table,
    sum(rows)                                      AS rows,
    sum(bytes_on_disk)                             AS bytes_on_disk,
    formatReadableSize(sum(bytes_on_disk))         AS size,
    sum(data_compressed_bytes)                     AS compressed,
    sum(data_uncompressed_bytes)                   AS uncompressed,
    round(sum(data_uncompressed_bytes) / nullIf(sum(data_compressed_bytes), 0), 2) AS compression_ratio,
    count()                                        AS parts,
    max(modification_time)                         AS last_modified
FROM system.parts
WHERE active AND database NOT IN ('system','INFORMATION_SCHEMA','information_schema')
GROUP BY database, table
ORDER BY bytes_on_disk DESC
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
