using System.Data;
using ClickHouse.Client.ADO;

namespace ClickHouseUI.Services;

/// <summary>
/// Thin convenience wrapper around <see cref="ClickHouseConnection"/> that returns
/// query results as plain dictionaries so the JSON serializer can emit them
/// without needing per-table DTOs.
/// </summary>
internal sealed class ClickHouseQueryService
{
    private readonly string _connectionString;

    public ClickHouseQueryService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<Dictionary<string, object?>>> QueryAsync(
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new ClickHouseConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        if (parameters is not null)
        {
            foreach (var kv in parameters)
            {
                var p = command.CreateParameter();
                p.ParameterName = kv.Key;
                p.Value = kv.Value ?? DBNull.Value;
                command.Parameters.Add(p);
            }
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new Dictionary<string, object?>(reader.FieldCount);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[reader.GetName(i)] = NormalizeValue(value);
            }
            rows.Add(row);
        }
        return rows;
    }

    public async Task<string> ScalarStringAsync(string sql, CancellationToken cancellationToken = default)
    {
        await using var connection = new ClickHouseConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result?.ToString() ?? string.Empty;
    }

    // ClickHouse.Client surfaces several CLR types (UInt64, decimals, DateTime,
    // arrays of tuples, etc.) that System.Text.Json doesn't always handle well.
    // Coerce them into JSON-friendly primitives.
    private static object? NormalizeValue(object? value) => value switch
    {
        null => null,
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss"),
        DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ssK"),
        decimal d => (double)d,
        ulong ul => ul > long.MaxValue ? (object)ul.ToString() : (long)ul,
        Array arr => arr.Cast<object?>().Select(NormalizeValue).ToArray(),
        _ => value
    };
}
