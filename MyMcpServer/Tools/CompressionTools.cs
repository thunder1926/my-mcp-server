using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace MyMcpServer.Tools;

/// <summary>
/// POC combining two ideas from the deck:
///
///   Headroom  -> reversible compression (CCR pattern): compress what's returned
///                to the model, but keep the original retrievable by a key.
///
///   Ponytail  -> intensity levels: let the caller dial how aggressively
///                content gets trimmed, instead of one fixed rule for everything.
///
/// This is intentionally a small, in-memory POC — not production caching
/// (no eviction, no persistence across restarts). Good enough to prove the
/// pattern works end-to-end through a real MCP client.
/// </summary>
[McpServerToolType]
public static class CompressionTools
{
    // cacheKey -> original content. In-memory only; lost on process restart.
    private static readonly ConcurrentDictionary<string, string> _cache = new();

    public enum Intensity
    {
        Lite,   // trims only if it's clearly worth it (>4000 chars)
        Full,   // default: trims most large payloads
        Ultra   // aggressive: trims aggressively, keeps only the essentials
    }

    [McpServerTool, Description(
        "Compresses large text or JSON before it's returned to the model, and caches the " +
        "original under a cache key so it can be retrieved later with RetrieveOriginal. " +
        "Use 'intensity' to control how aggressively it trims: lite, full, or ultra.")]
    public static string CompressAndCache(
        [Description("The text or JSON content to compress.")] string content,
        [Description("A short identifier to retrieve this content later, e.g. 'build-log-1'.")] string cacheKey,
        [Description("How aggressively to compress: lite, full, or ultra. Defaults to full.")] Intensity intensity = Intensity.Full)
    {
        _cache[cacheKey] = content;

        var threshold = intensity switch
        {
            Intensity.Lite => 4000,
            Intensity.Full => 1500,
            Intensity.Ultra => 400,
            _ => 1500
        };

        if (content.Length <= threshold)
        {
            return content; // not worth compressing at this intensity
        }

        var compressed = LooksLikeJson(content)
            ? CompressJson(content, intensity)
            : CompressText(content, intensity);

        return $"{compressed}\n\n[compressed {content.Length}\u2192{compressed.Length} chars, intensity={intensity}; " +
               $"call RetrieveOriginal(\"{cacheKey}\") for the full content]";
    }

    [McpServerTool, Description("Retrieves the original, uncompressed content previously stored under a cache key by CompressAndCache.")]
    public static string RetrieveOriginal(
        [Description("The cache key used when the content was compressed.")] string cacheKey)
    {
        return _cache.TryGetValue(cacheKey, out var original)
            ? original
            : $"No cached content found for key '{cacheKey}'. It may have expired (process restart) or was never stored.";
    }

    // ---- naive content-type routing (Headroom's ContentRouter, simplified) ----

    private static bool LooksLikeJson(string s)
    {
        var t = s.TrimStart();
        return t.StartsWith('{') || t.StartsWith('[');
    }

    private static string CompressJson(string content, Intensity intensity)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var keep = intensity switch
            {
                Intensity.Lite => 10,
                Intensity.Full => 3,
                Intensity.Ultra => 1,
                _ => 3
            };
            return SummarizeElement(doc.RootElement, keep);
        }
        catch (JsonException)
        {
            // not actually valid JSON despite looking like it — fall back to text handling
            return CompressText(content, intensity);
        }
    }

    private static string SummarizeElement(JsonElement el, int keep)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Array:
                var items = el.EnumerateArray().ToList();
                var head = items.Take(keep).Select(i => i.ToString());
                var suffix = items.Count > keep ? $" ...(+{items.Count - keep} more items)" : "";
                return $"[{string.Join(", ", head)}{suffix}]";

            case JsonValueKind.Object:
                var props = el.EnumerateObject().Select(p => p.Name);
                return $"{{ keys: [{string.Join(", ", props)}] }}";

            default:
                return el.ToString();
        }
    }

    private static string CompressText(string content, Intensity intensity)
    {
        var lines = content.Split('\n');
        var keepEachEnd = intensity switch
        {
            Intensity.Lite => 40,
            Intensity.Full => 15,
            Intensity.Ultra => 5,
            _ => 15
        };

        if (lines.Length <= keepEachEnd * 2)
        {
            return content; // short enough already, nothing to trim
        }

        var head = lines.Take(keepEachEnd);
        var tail = lines.Skip(lines.Length - keepEachEnd);
        var omitted = lines.Length - keepEachEnd * 2;

        return string.Join('\n', head) +
               $"\n\n... {omitted} lines omitted ...\n\n" +
               string.Join('\n', tail);
    }
}
