using System.Text.Json;
using ClickHouseUI.Services;
using Microsoft.AspNetCore.Http;

namespace ClickHouseUI.Api;

internal static class ExplainApi
{
    private sealed class ExplainRequest
    {
        public string Query { get; set; } = string.Empty;
        public string Kind { get; set; } = "PLAN"; // PLAN | PIPELINE | SYNTAX | INDEXES
    }

    public sealed class PlanNode
    {
        public string Label { get; set; } = string.Empty;
        public List<string> Details { get; set; } = new();
        public List<PlanNode> Children { get; set; } = new();
    }

    public static async Task<object> PostAsync(ClickHouseQueryService q, HttpContext context)
    {
        ExplainRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<ExplainRequest>(
                context.Request.Body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                context.RequestAborted).ConfigureAwait(false);
        }
        catch
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return new { error = "invalid JSON body" };
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Query))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return new { error = "query is required" };
        }

        var kind = (body.Kind ?? "PLAN").Trim().ToUpperInvariant();
        if (kind is not ("PLAN" or "PIPELINE" or "SYNTAX" or "INDEXES"))
        {
            kind = "PLAN";
        }

        var explainOptions = kind == "PLAN"
            ? "actions = 1, indexes = 1, header = 1"
            : string.Empty;

        var sql = string.IsNullOrEmpty(explainOptions)
            ? $"EXPLAIN {kind} {body.Query}"
            : $"EXPLAIN {kind} {explainOptions} {body.Query}";

        var rows = await q.QueryAsync(sql, cancellationToken: context.RequestAborted).ConfigureAwait(false);

        // EXPLAIN returns a single column named "explain" with one row per line of
        // text. Concatenate them and parse the indentation-based tree.
        var lines = rows
            .Select(r => r.Values.FirstOrDefault()?.ToString() ?? string.Empty)
            .ToArray();

        var tree = ParseTree(lines);

        return new
        {
            kind,
            raw = string.Join('\n', lines),
            tree
        };
    }

    // ClickHouse EXPLAIN uses 2-space indentation. Lines that start deeper than
    // the previous one are children of the most recent parent at that depth.
    // Lines that are not nodes (descriptors like "  Sort description:") become
    // detail entries on the closest node.
    private static List<PlanNode> ParseTree(string[] lines)
    {
        var roots = new List<PlanNode>();
        var stack = new Stack<(int Indent, PlanNode Node)>();

        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var indent = CountLeadingSpaces(raw);
            var content = raw.TrimStart();

            // Heuristic: node labels typically don't start with a lowercase
            // descriptor like "Header:" / "Sort description:" / "ReadType:".
            // Treat them as details of the closest node on the stack.
            var isDetail = stack.Count > 0 && content.Contains(':') && char.IsLower(content[0]);

            if (isDetail)
            {
                stack.Peek().Node.Details.Add(content);
                continue;
            }

            var node = new PlanNode { Label = content };

            while (stack.Count > 0 && stack.Peek().Indent >= indent)
            {
                stack.Pop();
            }

            if (stack.Count == 0)
            {
                roots.Add(node);
            }
            else
            {
                stack.Peek().Node.Children.Add(node);
            }

            stack.Push((indent, node));
        }

        return roots;
    }

    private static int CountLeadingSpaces(string s)
    {
        var count = 0;
        foreach (var c in s)
        {
            if (c == ' ') count++;
            else break;
        }
        return count;
    }
}
