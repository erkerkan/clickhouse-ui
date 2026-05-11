using Microsoft.AspNetCore.Builder;

namespace ClickHouseUI;

/// <summary>
/// Extension methods that mount the ClickHouse dashboard middleware into the ASP.NET Core pipeline.
/// </summary>
public static class ClickHouseDashboardExtensions
{
    /// <summary>
    /// Mounts the dashboard at <c>/clickhouse</c> using the supplied connection string.
    /// </summary>
    public static IApplicationBuilder UseClickHouseDashboard(this IApplicationBuilder app, string connectionString)
        => app.UseClickHouseDashboard("/clickhouse", new ClickHouseDashboardOptions { ConnectionString = connectionString });

    /// <summary>
    /// Mounts the dashboard at the given <paramref name="path"/> using the supplied connection string.
    /// </summary>
    public static IApplicationBuilder UseClickHouseDashboard(this IApplicationBuilder app, string path, string connectionString)
        => app.UseClickHouseDashboard(path, new ClickHouseDashboardOptions { ConnectionString = connectionString });

    /// <summary>
    /// Mounts the dashboard using a configuration callback. Default path: <c>/clickhouse</c>.
    /// </summary>
    public static IApplicationBuilder UseClickHouseDashboard(this IApplicationBuilder app, Action<ClickHouseDashboardOptions> configure)
        => app.UseClickHouseDashboard("/clickhouse", configure);

    /// <summary>
    /// Mounts the dashboard at the given <paramref name="path"/> using a configuration callback.
    /// </summary>
    public static IApplicationBuilder UseClickHouseDashboard(this IApplicationBuilder app, string path, Action<ClickHouseDashboardOptions> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        var options = new ClickHouseDashboardOptions();
        configure(options);
        return app.UseClickHouseDashboard(path, options);
    }

    /// <summary>
    /// Mounts the dashboard at the given <paramref name="path"/> using an explicit options object.
    /// </summary>
    public static IApplicationBuilder UseClickHouseDashboard(this IApplicationBuilder app, string path, ClickHouseDashboardOptions options)
    {
        if (app is null) throw new ArgumentNullException(nameof(app));
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be empty.", nameof(path));
        if (!path.StartsWith('/')) path = "/" + path;

        return app.UseMiddleware<ClickHouseDashboardMiddleware>(options, path);
    }
}
