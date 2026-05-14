using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Interfaces.Mcp;

/// <summary>MCP transport abstraction — supports Stdio, SSE, Streamable HTTP.</summary>
public interface IMcpTransport : IAsyncDisposable
{
    /// <summary>Connect/initialize the transport.</summary>
    Task ConnectAsync(CancellationToken ct = default);
    /// <summary>Send a JSON-RPC message.</summary>
    Task SendAsync(string message, CancellationToken ct = default);
    /// <summary>Receive a JSON-RPC message. Returns null on clean close.</summary>
    Task<string?> ReceiveAsync(CancellationToken ct = default);
    /// <summary>Disconnect the transport.</summary>
    Task DisconnectAsync(CancellationToken ct = default);
}
