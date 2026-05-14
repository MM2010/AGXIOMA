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

// ── MU-06 — CLI `agxiome doctor` ─────────────────────────────────────────────

/// <summary>
/// MU-06: doctor command — verifies runtime server, API keys, and Redis connectivity.
/// <example>agxiome doctor</example>
/// </summary>
public sealed class DoctorCommand(IAgxiomeApiClient client, ICliOutput output) : ICliCommand
{
    public string Name        => "doctor";
    public string Description => "Verify runtime server, API keys, and connectivity.";

    private static readonly (string EnvVar, string Provider)[] _apiKeys =
    [
        ("OPENAI_API_KEY",    "OpenAI"),
        ("ANTHROPIC_API_KEY", "Anthropic"),
        ("DEEPSEEK_API_KEY",  "DeepSeek"),
        ("GEMINI_API_KEY",    "Gemini"),
        ("GROQ_API_KEY",      "Groq"),
        ("MISTRAL_API_KEY",   "Mistral"),
        ("COHERE_API_KEY",    "Cohere"),
        ("XAI_API_KEY",       "xAI Grok"),
        ("TOGETHER_API_KEY",  "Together AI"),
        ("FIREWORKS_API_KEY", "Fireworks AI"),
    ];

    public async ValueTask<CliResult> ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("AGXIOME Doctor — System Check");
        sb.AppendLine(new string('─', 40));

        // 1. Runtime server
        sb.Append($"  Runtime server ({client.BaseUrl})  ");
        string serverStatus;
        if (client.IsReachable)
        {
            try   { await client.GetAsync("api/health", ct); }
            catch { /* no health endpoint — still reachable */ }
            sb.AppendLine("✅  reachable");
            serverStatus = "ok";
        }
        else
        {
            sb.AppendLine("❌  unreachable  →  start AGXIOME.Runtime first");
            serverStatus = "fail";
        }

        // 2. API keys
        sb.AppendLine();
        sb.AppendLine("  API Keys:");
        int keysFound = 0;
        foreach (var (envVar, provider) in _apiKeys)
        {
            var val = Environment.GetEnvironmentVariable(envVar);
            if (val is { Length: > 8 })
            {
                sb.AppendLine($"    ✅  {provider,-15} {envVar} = {val[..4]}...{val[^4..]}");
                keysFound++;
            }
            else
            {
                sb.AppendLine($"    ℹ️  {provider,-15} {envVar} not set");
            }
        }
        if (keysFound == 0)
            sb.AppendLine("    ⚠️  No LLM API keys found. Set at least one to use cloud providers.");

        // 3. Ollama (local — no key needed)
        sb.AppendLine();
        sb.Append("  Ollama (local, http://localhost:11434)  ");
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var resp = await http.GetAsync("http://localhost:11434/api/tags", ct);
            sb.AppendLine(resp.IsSuccessStatusCode ? "✅  running" : "⚠️  unreachable");
        }
        catch
        {
            sb.AppendLine("ℹ️  not running (optional)");
        }

        // 4. Summary
        sb.AppendLine();
        sb.AppendLine(serverStatus == "ok" && keysFound > 0
            ? "✅  All checks passed."
            : "⚠️  Some checks failed — see above.");

        var report = sb.ToString();
        output.WriteLine(report);
        return serverStatus == "ok" ? CliResult.Ok(report) : CliResult.Fail(report, 1);
    }
}

// ── MU-07 — CLI `agxiome benchmark` ──────────────────────────────────────────

/// <summary>
/// MU-07: benchmark command — measures runtime agent throughput and latency.
/// <example>agxiome benchmark</example>
/// <example>agxiome benchmark --requests 20 --agent my-agent --input "Hello"</example>
/// </summary>
public sealed class BenchmarkCommand(IAgxiomeApiClient client, ICliOutput output) : ICliCommand
{
    public string Name        => "benchmark";
    public string Description => "Measure agent throughput and latency (p50/p95/p99).";

    public async ValueTask<CliResult> ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        var opts      = ParseBenchArgs(args);
        int n         = int.TryParse(opts.GetValueOrDefault("requests", "10"), out var r) ? r : 10;
        var agentId   = opts.GetValueOrDefault("agent", "default");
        var input     = opts.GetValueOrDefault("input", "Hello, how are you?");
        int parallel  = int.TryParse(opts.GetValueOrDefault("parallel", "1"), out var p) ? p : 1;

        if (!client.IsReachable)
            return CliResult.Fail("Runtime server unreachable. Start AGXIOME.Runtime first.", 2);

        output.WriteLine($"Benchmarking agent '{agentId}': {n} requests (parallel={parallel})");
        output.WriteLine(new string('─', 50));

        var latencies = new List<double>(n);
        var errors    = 0;
        var sw        = System.Diagnostics.Stopwatch.StartNew();
        var semaphore = new SemaphoreSlim(parallel);

        var tasks = Enumerable.Range(0, n).Select(async i =>
        {
            await semaphore.WaitAsync(ct);
            var reqSw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await client.PostAsync($"api/agents/{agentId}/run",
                    new { Input = input, SessionId = $"bench-{i}" }, ct);
                lock (latencies) latencies.Add(reqSw.Elapsed.TotalMilliseconds);
            }
            catch
            {
                Interlocked.Increment(ref errors);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        sw.Stop();

        if (latencies.Count == 0)
            return CliResult.Fail($"All {n} requests failed. Is the agent '{agentId}' registered?");

        var sorted     = latencies.OrderBy(x => x).ToArray();
        var p50        = Percentile(sorted, 0.50);
        var p95        = Percentile(sorted, 0.95);
        var p99        = Percentile(sorted, 0.99);
        var throughput = latencies.Count / sw.Elapsed.TotalSeconds;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Results ({latencies.Count}/{n} succeeded, {errors} errors):");
        sb.AppendLine($"  Mean       {latencies.Average(),8:F1} ms");
        sb.AppendLine($"  p50        {p50,8:F1} ms");
        sb.AppendLine($"  p95        {p95,8:F1} ms");
        sb.AppendLine($"  p99        {p99,8:F1} ms");
        sb.AppendLine($"  Total      {sw.Elapsed.TotalSeconds,8:F2} s");
        sb.AppendLine($"  Throughput {throughput:F2} req/s");

        var report = sb.ToString();
        output.WriteLine(report);
        return CliResult.Ok(report);
    }

    private static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0) return 0;
        var idx = (int)Math.Ceiling(p * sorted.Length) - 1;
        return sorted[Math.Clamp(idx, 0, sorted.Length - 1)];
    }

    private static Dictionary<string, string> ParseBenchArgs(string[] args)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
            if (args[i].StartsWith("--"))
            {
                var key = args[i][2..];
                dict[key] = (i + 1 < args.Length && !args[i + 1].StartsWith("--")) ? args[++i] : "true";
            }
        return dict;
    }
}
