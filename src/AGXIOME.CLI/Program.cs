// AGXIOME.CLI — Command dispatcher for the AGXIOME framework.
// Usage: agxiome <command> [args...]
// Commands: run | list | session | admin | config
// The runtime server URL is read from AGXIOME_API_URL env var (default: http://localhost:5250).

using AGXIOME.CLI.Commands;

namespace AGXIOME.CLI;

/// <summary>
/// AGXIOME CLI entry point. Dispatches to MU-01..MU-05 command handlers.
/// All commands require the runtime server (AGXIOME.Runtime) to be reachable.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        var baseUrl = Environment.GetEnvironmentVariable("AGXIOME_API_URL") ?? "http://localhost:5250";
        var client  = new HttpAgxiomeApiClient(baseUrl);
        var output  = new ConsoleCliOutput();

        // Registry of all commands (MU-01..MU-05)
        ICliCommand[] commands =
        [
            new RunCommand(client, output),
            new ListCommand(client, output),
            new SessionCommand(client, output),
            new AdminCommand(client, output),
            new ConfigCommand(client, output),
        ];

        var verb = args[0].ToLowerInvariant();
        var cmd  = Array.Find(commands, c => c.Name == verb);
        if (cmd is null)
        {
            output.WriteError($"Unknown command '{verb}'. Run 'agxiome --help' for usage.");
            return 1;
        }

        using var cts    = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        try
        {
            var result = await cmd.ExecuteAsync(args[1..], cts.Token);
            if (!result.Success && result.Error is not null)
                output.WriteError(result.Error);
            return result.ExitCode;
        }
        catch (OperationCanceledException)
        {
            output.WriteError("Cancelled.");
            return 130;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("AGXIOME v1.0 — Agentic Generative C# X-Linked Integrated Orchestration Memory Engine");
        Console.WriteLine();
        Console.WriteLine("Usage:  agxiome <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  run      Execute an agent, team, or workflow");
        Console.WriteLine("  list     List agents, teams, workflows, sessions, or runs");
        Console.WriteLine("  session  Create, delete, or inspect sessions");
        Console.WriteLine("  admin    Health check, metrics, and log access");
        Console.WriteLine("  config   Get or set runtime configuration");
        Console.WriteLine();
        Console.WriteLine("Environment:");
        Console.WriteLine("  AGXIOME_API_URL   Runtime base URL (default: http://localhost:5250)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  agxiome run --type agent --id my-agent --input \"Hello\"");
        Console.WriteLine("  agxiome list agents");
        Console.WriteLine("  agxiome session --action create --agent my-agent");
        Console.WriteLine("  agxiome admin --action health");
        Console.WriteLine("  agxiome config --action set --key log_level --value Warning");
    }
}

// ── HttpAgxiomeApiClient — production IAgxiomeApiClient over HttpClient ───────

/// <summary>
/// Production implementation of IAgxiomeApiClient backed by HttpClient.
/// IsReachable is tested once lazily via a HEAD request to /api/admin/health.
/// Secrets (API keys) must be injected via env vars or config, never hardcoded.
/// </summary>
internal sealed class HttpAgxiomeApiClient : IAgxiomeApiClient
{
    private readonly HttpClient _http;
    private bool? _reachable;

    public string BaseUrl { get; }

    public HttpAgxiomeApiClient(string baseUrl)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        _http   = new HttpClient { BaseAddress = new Uri(BaseUrl + "/"), Timeout = TimeSpan.FromSeconds(10) };
    }

    public bool IsReachable
    {
        get
        {
            if (_reachable.HasValue) return _reachable.Value;
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, "api/admin/health");
                using var res = _http.Send(req);
                _reachable = res.IsSuccessStatusCode;
            }
            catch
            {
                _reachable = false;
            }
            return _reachable.Value;
        }
    }

    public async ValueTask<string> GetAsync(string path, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync(path, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    public async ValueTask<string> PostAsync(string path, object body, CancellationToken ct = default)
    {
        var json    = System.Text.Json.JsonSerializer.Serialize(body);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var resp    = await _http.PostAsync(path, content, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    public async ValueTask<string> DeleteAsync(string path, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync(path, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }
}
