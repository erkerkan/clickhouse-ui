using System.Reflection;
using System.Text.Json;
using ClickHouseUI.Api;
using ClickHouseUI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace ClickHouseUI;

internal sealed class ClickHouseDashboardMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly RequestDelegate _next;
    private readonly ClickHouseDashboardOptions _options;
    private readonly string _basePath;
    private readonly EmbeddedFileProvider _fileProvider;
    private readonly ClickHouseQueryService _queryService;

    public ClickHouseDashboardMiddleware(
        RequestDelegate next,
        ClickHouseDashboardOptions options,
        string basePath)
    {
        _next = next;
        _options = options;
        _basePath = basePath.TrimEnd('/');
        _fileProvider = new EmbeddedFileProvider(
            typeof(ClickHouseDashboardMiddleware).GetTypeInfo().Assembly,
            "ClickHouseUI.wwwroot");
        _queryService = new ClickHouseQueryService(options.ConnectionString);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (!_options.AllowAnonymous && _options.Authorize is { } predicate && !predicate(context))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var subPath = path.Length > _basePath.Length ? path[_basePath.Length..] : "/";
        if (string.IsNullOrEmpty(subPath)) subPath = "/";

        if (subPath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await HandleApiAsync(context, subPath).ConfigureAwait(false);
            return;
        }

        await ServeStaticAsync(context, subPath).ConfigureAwait(false);
    }

    private async Task HandleApiAsync(HttpContext context, string subPath)
    {
        try
        {
            object? payload = subPath.ToLowerInvariant() switch
            {
                "/api/overview"      => await OverviewApi.GetAsync(_queryService, _options, context.RequestAborted).ConfigureAwait(false),
                "/api/metrics"       => await MetricsApi.GetAsync(_queryService, context.RequestAborted).ConfigureAwait(false),
                "/api/tables"        => await TablesApi.GetAsync(_queryService, context.RequestAborted).ConfigureAwait(false),
                "/api/parts"         => await TablesApi.GetPartsAsync(_queryService, context.Request.Query["database"], context.Request.Query["table"], context.RequestAborted).ConfigureAwait(false),
                "/api/slow-queries"  => await QueriesApi.GetSlowAsync(_queryService, _options, context.RequestAborted).ConfigureAwait(false),
                "/api/explain"       => await ExplainApi.PostAsync(_queryService, context).ConfigureAwait(false),
                _ => null
            };

            if (payload is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "application/json; charset=utf-8";
            await JsonSerializer.SerializeAsync(context.Response.Body, payload, JsonOptions, context.RequestAborted).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected.
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json; charset=utf-8";
            await JsonSerializer.SerializeAsync(context.Response.Body, new
            {
                error = ex.GetType().Name,
                message = ex.Message
            }, JsonOptions).ConfigureAwait(false);
        }
    }

    private async Task ServeStaticAsync(HttpContext context, string subPath)
    {
        // The dashboard is a single-page app; serve index.html for the root
        // and for any unknown path that doesn't have a file extension.
        var resourcePath = subPath == "/" ? "/index.html" : subPath;
        var fileInfo = _fileProvider.GetFileInfo(resourcePath);

        if (!fileInfo.Exists)
        {
            // SPA fallback
            if (!Path.HasExtension(resourcePath))
            {
                fileInfo = _fileProvider.GetFileInfo("/index.html");
            }
            if (!fileInfo.Exists)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
        }

        context.Response.ContentType = GetContentType(fileInfo.Name);

        // Inject runtime configuration into index.html so the frontend knows its
        // base path and title without needing a server-rendered template engine.
        if (fileInfo.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(fileInfo.CreateReadStream());
            var html = await reader.ReadToEndAsync().ConfigureAwait(false);
            html = html
                .Replace("__BASE_PATH__", _basePath)
                .Replace("__DASHBOARD_TITLE__", System.Net.WebUtility.HtmlEncode(_options.Title));
            await context.Response.WriteAsync(html, context.RequestAborted).ConfigureAwait(false);
            return;
        }

        await using var stream = fileInfo.CreateReadStream();
        await stream.CopyToAsync(context.Response.Body, context.RequestAborted).ConfigureAwait(false);
    }

    private static string GetContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".js"   => "application/javascript; charset=utf-8",
        ".css"  => "text/css; charset=utf-8",
        ".svg"  => "image/svg+xml",
        ".ico"  => "image/x-icon",
        ".png"  => "image/png",
        ".json" => "application/json; charset=utf-8",
        _ => "application/octet-stream"
    };
}
