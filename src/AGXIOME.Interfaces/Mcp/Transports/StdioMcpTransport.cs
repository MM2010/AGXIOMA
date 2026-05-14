using System.Text;
using System.Text.Json;

namespace AGXIOME.Interfaces.Mcp.Transports;

/// <summary>Stdio MCP transport — communicates over process stdin/stdout.</summary>
public sealed class StdioMcpTransport : IMcpTransport
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private bool _disposed;

    public StdioMcpTransport(TextReader? input = null, TextWriter? output = null)
    {
        _input = input ?? Console.In;
        _output = output ?? Console.Out;
    }

    public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;

    public async Task SendAsync(string message, CancellationToken ct = default)
    {
        var header = $"Content-Length: {Encoding.UTF8.GetByteCount(message)}\r\n\r\n";
        await _output.WriteAsync(header);
        await _output.WriteAsync(message);
        await _output.FlushAsync();
    }

    public async Task<string?> ReceiveAsync(CancellationToken ct = default)
    {
        // Read Content-Length header
        var headerLine = await _input.ReadLineAsync();
        if (headerLine == null) return null;
        if (!headerLine.StartsWith("Content-Length: ")) return null;

        var length = int.Parse(headerLine["Content-Length: ".Length..]);
        await _input.ReadLineAsync(); // blank line

        var buffer = new char[length];
        var totalRead = 0;
        while (totalRead < length)
        {
            var read = await _input.ReadAsync(buffer, totalRead, length - totalRead);
            if (read == 0) return null;
            totalRead += read;
        }

        return new string(buffer);
    }

    public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            await DisconnectAsync();
        }
    }
}
