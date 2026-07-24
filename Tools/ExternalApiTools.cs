using System.Net.Http;
using System.ComponentModel;

using ModelContextProtocol.Server;

namespace MyMcpServer.Tools;

/// <summary>
/// Example wrapper around an external HTTP API. Replace the endpoint and
/// response handling with your real service.
///
/// Instance (non-static) tool classes get their dependencies injected
/// per-call from the DI container that Program.cs configured — that's
/// how IHttpClientFactory gets here.
/// </summary>
[McpServerToolType]
public class ExternalApiTools
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ExternalApiTools(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [McpServerTool, Description("Calls the external API's status endpoint and returns the raw JSON response.")]
    public async Task<string> GetApiStatus()
    {
        var client = _httpClientFactory.CreateClient("external-api");
        var response = await client.GetAsync("status");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    [McpServerTool, Description("Sends a GET request to a specific relative path on the external API and returns the response body.")]
    public async Task<string> GetResource(
        [Description("Path relative to the API base address, e.g. 'orders/123'.")] string path)
    {
        var client = _httpClientFactory.CreateClient("external-api");
        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return $"Request failed ({(int)response.StatusCode}): {body}";
        }

        return body;
    }
}
