using System.Text;
using System.Text.Json;

namespace AGXIOME.Interfaces.Mcp.Transports;

/// <summary>SSE-based HTTP MCP transport.</summary>
public sealed class SseHttpMcpTransport : IMcpTransport
{
    private readonly string _endpoint;
    private readonly HttpClient _http;
    private StreamReader? _sseReader;
    private bool _disposed;

    public SseHttpMcpTransport(string endpoint, HttpClient? http = null)
    {
        _endpoint = endpoint;
        _http = http ?? new HttpClient();
    }

    public Task ConnectAsync(CancellationToken ct = default)
    {
        // SSE connection is established lazily on first ReceiveAsync
        return Task.CompletedTask;
    }

    public async Task SendAsync(string message, CancellationToken ct = default)
    {
        var content = new StringContent(message, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(_endpoint, content, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string?> ReceiveAsync(CancellationToken ct = default)
    {
        if (_sseReader == null)
        {
            var response = await _http.GetAsync(_endpoint, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            _sseReader = new StreamReader(await response.Content.ReadAsStreamAsync(ct));
        }

        while (!_sseReader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await _sseReader.ReadLineAsync(ct);
            if (line == null) return null;
            if (line.StartsWith("data: "))
                return line[6..];
        }
        return null;
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        _sseReader?.Dispose();
        _sseReader = null;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            await DisconnectAsync();
            _http.Dispose();
        }
    }
}
