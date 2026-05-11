using System.Reflection;
using System.Text.Json;
using ClickHouseUI.Api;
using ClickHouseUI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace ClickHouseUI;

internal sealed class ClickHouseDashboardMiddleware
{
    private const string IndexFileName = "index.html";

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

        // Resolve the wwwroot namespace from the assembly name rather than hard
        // coding it, so the package keeps working if it's renamed or forked.
        var asm = typeof(ClickHouseDashboardMiddleware).GetTypeInfo().Assembly;
        var rootNamespace = (asm.GetName().Name ?? nameof(ClickHouseUI)) + ".wwwroot";
        _fileProvider = new EmbeddedFileProvider(asm, rootNamespace);

        _queryService = new ClickHouseQueryService(options.ConnectionString);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!IsUnderBasePath(path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (!IsAuthorized(context))
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

    // /clickhouse should match /clickhouse and /clickhouse/* but NOT /clickhouse-other.
    private bool IsUnderBasePath(string path)
    {
        if (!path.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase)) return false;
        return path.Length == _basePath.Length || path[_basePath.Length] == '/';
    }

    private bool IsAuthorized(HttpContext context)
    {
        if (_options.AllowAnonymous) return true;
        if (_options.Authorize is { } predicate) return predicate(context);
        // Sensible default when AllowAnonymous=false but no predicate was supplied:
        // require an authenticated user. Avoids the "locked out forever" foot-gun.
        return context.User.Identity?.IsAuthenticated == true;
    }

    private async Task HandleApiAsync(HttpContext context, string subPath)
    {
        if (!DashboardEndpoints.All.TryGetValue(subPath, out var endpoint))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        try
        {
            var payload = await endpoint(context, _queryService, _options, context.RequestAborted).ConfigureAwait(false);
            if (payload is null)
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

            context.Response.ContentType = "application/json; charset=utf-8";
            await JsonSerializer.SerializeAsync(context.Response.Body, payload, JsonOptions, context.RequestAborted).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected mid-request; nothing to report.
        }
        catch (Exception ex)
        {
            await WriteJsonErrorAsync(context, ex).ConfigureAwait(false);
        }
    }

    private static async Task WriteJsonErrorAsync(HttpContext context, Exception ex)
    {
        // Once the response has started flushing we can't change status code or
        // headers without corrupting the stream. Bail out and let the host log.
        if (context.Response.HasStarted) return;

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json; charset=utf-8";
        await JsonSerializer.SerializeAsync(context.Response.Body, new
        {
            error = ex.GetType().Name,
            message = ex.Message
        }, JsonOptions).ConfigureAwait(false);
    }

    private async Task ServeStaticAsync(HttpContext context, string subPath)
    {
        // The dashboard is a single-page app; serve index.html for the root
        // and as a fallback for any unknown path that has no file extension.
        var resourcePath = subPath == "/" ? "/" + IndexFileName : subPath;
        var fileInfo = _fileProvider.GetFileInfo(resourcePath);

        if (!fileInfo.Exists)
        {
            if (!Path.HasExtension(resourcePath))
            {
                fileInfo = _fileProvider.GetFileInfo("/" + IndexFileName);
            }
            if (!fileInfo.Exists)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
        }

        context.Response.ContentType = GetContentType(fileInfo.Name);

        if (fileInfo.Name.Equals(IndexFileName, StringComparison.OrdinalIgnoreCase))
        {
            await ServeIndexHtmlAsync(context, fileInfo).ConfigureAwait(false);
            return;
        }

        await using var stream = fileInfo.CreateReadStream();
        await stream.CopyToAsync(context.Response.Body, context.RequestAborted).ConfigureAwait(false);
    }

    // Inject runtime configuration into index.html so the frontend knows its
    // mount path and title without needing a server-side template engine.
    private async Task ServeIndexHtmlAsync(HttpContext context, IFileInfo fileInfo)
    {
        using var reader = new StreamReader(fileInfo.CreateReadStream());
        var html = await reader.ReadToEndAsync().ConfigureAwait(false);
        html = html
            .Replace("__BASE_PATH__", _basePath)
            .Replace("__DASHBOARD_TITLE__", System.Net.WebUtility.HtmlEncode(_options.Title));
        await context.Response.WriteAsync(html, context.RequestAborted).ConfigureAwait(false);
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
        _       => "application/octet-stream"
    };
}
