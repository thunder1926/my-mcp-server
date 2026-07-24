using System.ComponentModel;
using ModelContextProtocol.Server;

namespace MyMcpServer.Tools;

/// <summary>
/// File operations restricted to a single allow-listed root directory.
/// Never trust a path straight from the model — always resolve it against
/// AllowedRoot and reject anything that escapes it.
/// </summary>
[McpServerToolType]
public static class FileTools
{
    // TODO: change this to the folder you actually want the agent to touch.
    private static readonly string AllowedRoot =
        Environment.GetEnvironmentVariable("MCP_ALLOWED_ROOT")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "mcp-workspace");

    private static string ResolveSafePath(string relativePath)
    {
        Directory.CreateDirectory(AllowedRoot);

        var fullPath = Path.GetFullPath(Path.Combine(AllowedRoot, relativePath));
        var normalizedRoot = Path.GetFullPath(AllowedRoot);

        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                $"Path '{relativePath}' escapes the allowed root '{normalizedRoot}'.");
        }

        return fullPath;
    }

    [McpServerTool, Description("Lists files and folders under a relative path inside the allowed workspace root.")]
    public static string ListFiles(
        [Description("Path relative to the workspace root. Use '.' for the root itself.")] string relativePath = ".")
    {
        var target = ResolveSafePath(relativePath);

        if (!Directory.Exists(target))
        {
            return $"Directory not found: {relativePath}";
        }

        var entries = Directory.EnumerateFileSystemEntries(target)
            .Select(e => Path.GetRelativePath(AllowedRoot, e))
            .OrderBy(e => e);

        return string.Join("\n", entries);
    }

    [McpServerTool, Description(
        "Reads the text content of a file inside the allowed workspace root. " +
        "Large files are auto-compressed (kept head/tail) — call RetrieveOriginal " +
        "with the returned cache key for the full raw content. Uses 'lite' intensity " +
        "so small-to-medium files are always returned in full.")]
    public static async Task<string> ReadFile(
        [Description("Path to the file, relative to the workspace root.")] string relativePath)
    {
        var target = ResolveSafePath(relativePath);

        if (!File.Exists(target))
        {
            return $"File not found: {relativePath}";
        }

        var content = await File.ReadAllTextAsync(target);
        var cacheKey = $"file-{Path.GetFileName(target)}-{DateTime.UtcNow:HHmmss}";
        return CompressionTools.CompressAndCache(content, cacheKey, CompressionTools.Intensity.Lite);
    }

    [McpServerTool, Description("Writes text content to a file inside the allowed workspace root, creating folders as needed. Overwrites existing content.")]
    public static async Task<string> WriteFile(
        [Description("Path to the file, relative to the workspace root.")] string relativePath,
        [Description("The full text content to write.")] string content)
    {
        var target = ResolveSafePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        await File.WriteAllTextAsync(target, content);
        return $"Wrote {content.Length} characters to {relativePath}";
    }
}
