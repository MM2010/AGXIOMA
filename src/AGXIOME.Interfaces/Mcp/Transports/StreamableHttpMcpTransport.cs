using System.Text;
using System.Text.Json;

namespace AGXIOME.Interfaces.Mcp.Transports;

/// <summary>Streamable HTTP MCP transport — uses SSE for streaming responses.</summary>
public sealed class StreamableHttpMcpTransport : IMcpTransport
{
    private readonly string _endpoint;
    private readonly HttpClient _http;
    private readonly string _sessionId;
    private bool _disposed;

    public StreamableHttpMcpTransport(string endpoint, HttpClient? http = null, string? sessionId = null)
    {
        _endpoint = endpoint;
        _http = http ?? new HttpClient();
        _sessionId = sessionId ?? Guid.NewGuid().ToString("N");
    }

    public string SessionId => _sessionId;

    public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;

    public async Task SendAsync(string message, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(message, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("MCP-Session-Id", _sessionId);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string?> ReceiveAsync(CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_endpoint}?sessionId={_sessionId}");
        request.Headers.Add("MCP-Session-Id", _sessionId);
        request.Headers.Add("Accept", "text/event-stream");

        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) return null;
            if (line.StartsWith("data: "))
                return line[6..];
        }
        return null;
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        // Streamable HTTP has no explicit disconnect
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _http.Dispose();
        }
    }
}
