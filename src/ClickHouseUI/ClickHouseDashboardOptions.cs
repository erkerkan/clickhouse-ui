using System;

namespace ClickHouseUI;

/// <summary>
/// Configuration options for the ClickHouse dashboard middleware.
/// </summary>
public sealed class ClickHouseDashboardOptions
{
    /// <summary>
    /// ADO.NET style connection string used by <c>ClickHouse.Client</c>.
    /// Example: <c>Host=localhost;Port=8123;User=default;Password=;Database=default</c>.
    /// </summary>
    public string ConnectionString { get; set; } = "Host=localhost;Port=8123;User=default;Database=default";

    /// <summary>
    /// Optional title rendered in the dashboard header. Useful for distinguishing
    /// dashboards mounted against different clusters/environments.
    /// </summary>
    public string Title { get; set; } = "ClickHouse Dashboard";

    /// <summary>
    /// Number of rows returned by the "slow queries" endpoint. Defaults to 100.
    /// </summary>
    public int SlowQueriesLimit { get; set; } = 100;

    /// <summary>
    /// Look-back window (hours) used when scanning <c>system.query_log</c>.
    /// </summary>
    public int QueryLogLookbackHours { get; set; } = 24;

    /// <summary>
    /// When <see langword="true"/>, the dashboard is exposed without any auth.
    /// In production you should host it behind your own auth middleware or set
    /// <see cref="Authorize"/> to a custom predicate.
    /// </summary>
    public bool AllowAnonymous { get; set; } = true;

    /// <summary>
    /// Custom authorization predicate evaluated for every request. Return
    /// <see langword="false"/> to reject. Ignored when <see cref="AllowAnonymous"/> is true.
    /// </summary>
    public Func<Microsoft.AspNetCore.Http.HttpContext, bool>? Authorize { get; set; }
}
