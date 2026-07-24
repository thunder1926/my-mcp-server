using System.ComponentModel;
using System.Diagnostics;
using ModelContextProtocol.Server;

namespace MyMcpServer.Tools;

/// <summary>
/// Wraps common dotnet CLI workflow steps (build/test) so an agent can
/// run them without a raw shell-exec tool.
/// </summary>
[McpServerToolType]
public static class BuildTools
{
    private static async Task<string> RunDotnet(string arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo("dotnet", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory
        };

        using var proc = Process.Start(psi)!;
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        var result = stdout;
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            result += $"\n--- stderr ---\n{stderr}";
        }
        result += $"\n--- exit code: {proc.ExitCode} ---";
        return result;
    }

    [McpServerTool, Description(
        "Runs 'dotnet build' on a project or solution file and returns the build output. " +
        "Large output is auto-compressed (kept errors/warnings, collapsed noise) — " +
        "call RetrieveOriginal with the returned cache key for the full raw log.")]
    public static async Task<string> BuildProject(
        [Description("Absolute path to the .csproj or .sln file to build.")] string projectPath)
    {
        var dir = Path.GetDirectoryName(projectPath) ?? ".";
        var file = Path.GetFileName(projectPath);
        var output = await RunDotnet($"build \"{file}\"", dir);

        var cacheKey = $"build-{Path.GetFileNameWithoutExtension(file)}-{DateTime.UtcNow:HHmmss}";
        return CompressionTools.CompressAndCache(output, cacheKey, CompressionTools.Intensity.Full);
    }

    [McpServerTool, Description(
        "Runs 'dotnet test' on a project or solution file and returns the test output. " +
        "Large output is auto-compressed (kept errors/failures, collapsed noise) — " +
        "call RetrieveOriginal with the returned cache key for the full raw log.")]
    public static async Task<string> RunTests(
        [Description("Absolute path to the .csproj or .sln file containing tests.")] string projectPath)
    {
        var dir = Path.GetDirectoryName(projectPath) ?? ".";
        var file = Path.GetFileName(projectPath);
        var output = await RunDotnet($"test \"{file}\"", dir);

        var cacheKey = $"test-{Path.GetFileNameWithoutExtension(file)}-{DateTime.UtcNow:HHmmss}";
        return CompressionTools.CompressAndCache(output, cacheKey, CompressionTools.Intensity.Full);
    }
}
