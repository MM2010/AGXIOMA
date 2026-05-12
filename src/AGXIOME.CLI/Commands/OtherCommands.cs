// MU-02 — CLI `agxiome list`
// Lists agents, teams, workflows, or sessions from the runtime server.

namespace AGXIOME.CLI.Commands;

/// <summary>
/// MU-02: list command — lists entities in the runtime.
/// <example>agxiome list --type agents</example>
/// </summary>
public sealed class ListCommand(IAgxiomeApiClient client, ICliOutput output) : ICliCommand
{
    public string Name        => "list";
    public string Description => "List agents, teams, workflows, or sessions.";

    private static readonly Dictionary<string, string> _pathMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["agents"]    = "api/agents",
        ["teams"]     = "api/teams",
        ["workflows"] = "api/workflows",
        ["sessions"]  = "api/sessions",
        ["runs"]      = "api/runs",
        ["tools"]     = "api/tools"
    };

    public async ValueTask<CliResult> ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        var type = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "agents";
        // Accept --type flag too
        var idx = Array.IndexOf(args, "--type");
        if (idx >= 0 && idx + 1 < args.Length) type = args[idx + 1];

        if (!_pathMap.TryGetValue(type, out var path))
            return CliResult.Fail($"Unknown type '{type}'. Supported: {string.Join(", ", _pathMap.Keys)}");

        if (!client.IsReachable) return CliResult.Fail("Runtime server unreachable", 2);

        try
        {
            var result = await client.GetAsync(path, ct);
            output.WriteLine(result);
            return CliResult.Ok(result);
        }
        catch (Exception ex)
        {
            return CliResult.Fail(ex.Message);
        }
    }
}

// ── MU-03 — CLI `agxiome session` ────────────────────────────────────────────

/// <summary>
/// MU-03: session command — create, delete, or inspect sessions.
/// <example>agxiome session --action create --agent agent-abc123</example>
/// </summary>
public sealed class SessionCommand(IAgxiomeApiClient client, ICliOutput output) : ICliCommand
{
    public string Name        => "session";
    public string Description => "Create, delete, or inspect agent sessions.";

    public async ValueTask<CliResult> ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        var opts   = ParseArgs(args);
        var action = opts.GetValueOrDefault("action", "list").ToLowerInvariant();

        if (!client.IsReachable) return CliResult.Fail("Runtime server unreachable", 2);

        try
        {
            var result = action switch
            {
                "create" when opts.TryGetValue("agent", out var agentId)
                    => await client.PostAsync("api/sessions", new { AgentId = agentId }, ct),

                "delete" when opts.TryGetValue("id", out var sessId)
                    => await client.DeleteAsync($"api/sessions/{sessId}", ct),

                "inspect" when opts.TryGetValue("id", out var sessId)
                    => await client.GetAsync($"api/sessions/{sessId}", ct),

                "list" => await client.GetAsync("api/sessions", ct),

                _ => null
            };

            if (result is null) return CliResult.Fail($"Invalid action '{action}'. Use: create | delete | inspect | list");

            output.WriteLine(result);
            return CliResult.Ok(result);
        }
        catch (Exception ex)
        {
            return CliResult.Fail(ex.Message);
        }
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                var key = args[i][2..];
                dict[key] = (i + 1 < args.Length && !args[i + 1].StartsWith("--")) ? args[++i] : "true";
            }
        }
        return dict;
    }
}

// ── MU-04 — CLI `agxiome admin` ──────────────────────────────────────────────

/// <summary>
/// MU-04: admin command — health check, metrics, logs.
/// <example>agxiome admin --action health</example>
/// </summary>
public sealed class AdminCommand(IAgxiomeApiClient client, ICliOutput output) : ICliCommand
{
    public string Name        => "admin";
    public string Description => "Health check, metrics, and log access.";

    public async ValueTask<CliResult> ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        var idx    = Array.IndexOf(args, "--action");
        var action = (idx >= 0 && idx + 1 < args.Length) ? args[idx + 1].ToLowerInvariant() : "health";

        if (!client.IsReachable) return CliResult.Fail("Runtime server unreachable", 2);

        try
        {
            var path = action switch
            {
                "health"  => "api/admin/health",
                "metrics" => "api/admin/metrics",
                "logs"    => "api/admin/logs",
                "errors"  => "api/admin/errors",
                "config"  => "api/admin/config",
                _ => null
            };

            if (path is null) return CliResult.Fail($"Unknown action '{action}'. Use: health | metrics | logs | errors | config");

            var result = await client.GetAsync(path, ct);
            output.WriteLine(result);
            return CliResult.Ok(result);
        }
        catch (Exception ex)
        {
            return CliResult.Fail(ex.Message);
        }
    }
}

// ── MU-05 — CLI `agxiome config` ─────────────────────────────────────────────

/// <summary>
/// MU-05: config command — get/set runtime configuration key-value pairs.
/// <example>agxiome config --action get --key log_level</example>
/// <example>agxiome config --action set --key log_level --value Warning</example>
/// </summary>
public sealed class ConfigCommand(IAgxiomeApiClient client, ICliOutput output) : ICliCommand
{
    public string Name        => "config";
    public string Description => "Get or set runtime configuration.";

    public async ValueTask<CliResult> ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        var opts   = ParseArgs(args);
        var action = opts.GetValueOrDefault("action", "get").ToLowerInvariant();

        if (!client.IsReachable) return CliResult.Fail("Runtime server unreachable", 2);

        try
        {
            string result;
            switch (action)
            {
                case "get" when opts.TryGetValue("key", out var key):
                    result = await client.GetAsync($"api/admin/config/{key}", ct);
                    break;

                case "get":
                    result = await client.GetAsync("api/admin/config", ct);
                    break;

                case "set" when opts.TryGetValue("key", out var key) && opts.TryGetValue("value", out var val):
                    result = await client.PostAsync("api/admin/config", new { Key = key, Value = val }, ct);
                    break;

                default:
                    return CliResult.Fail("Use: config --action get [--key KEY] | config --action set --key KEY --value VALUE");
            }

            output.WriteLine(result);
            return CliResult.Ok(result);
        }
        catch (Exception ex)
        {
            return CliResult.Fail(ex.Message);
        }
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                var key = args[i][2..];
                dict[key] = (i + 1 < args.Length && !args[i + 1].StartsWith("--")) ? args[++i] : "true";
            }
        }
        return dict;
    }
}
