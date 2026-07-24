using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// MCP servers must not write anything to stdout except protocol messages,
// so all logging goes to stderr.
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = Microsoft.Extensions.Logging.LogLevel.Trace;
});

// Register a single named HttpClient the ExternalApiTools can reuse.
builder.Services.AddHttpClient("external-api", client =>
{
    // TODO: point this at your real external API base address.
    client.BaseAddress = new Uri("https://api.example.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(); // auto-discovers every [McpServerToolType] class below

await builder.Build().RunAsync();
