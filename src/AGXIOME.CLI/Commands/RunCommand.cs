// MU-01 — CLI `agxiome run`
// Executes agent / team / workflow identified by --id and --type.
// Sends input via --input arg; streams output if --stream flag is set.

namespace AGXIOME.CLI.Commands;

/// <summary>
/// MU-01: run command — executes an agent, team, or workflow.
/// <example>agxiome run --type agent --id agent-abc123 --input "Hello" [--stream]</example>
/// </summary>
public sealed class RunCommand(IAgxiomeApiClient client, ICliOutput output) : ICliCommand
{
    public string Name        => "run";
    public string Description => "Execute an agent, team, or workflow.";

    public async ValueTask<CliResult> ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        var opts = ParseArgs(args);

        if (!opts.TryGetValue("id",    out var id))    return CliResult.Fail("--id is required");
        if (!opts.TryGetValue("type",  out var type))  return CliResult.Fail("--type is required (agent|team|workflow)");
        if (!opts.TryGetValue("input", out var input)) input = "";

        if (!client.IsReachable) return CliResult.Fail("Runtime server unreachable", 2);

        var path = type.ToLowerInvariant() switch
        {
            "agent"    => $"api/runs",
            "team"     => $"api/teams/{id}/coordinate",
            "workflow" => $"api/workflows/{id}/execute",
            _ => null
        };

        if (path is null) return CliResult.Fail($"Unknown type '{type}'. Use: agent | team | workflow");

        var body = type == "agent"
            ? (object)new { AgentId = id, Input = input, Stream = opts.ContainsKey("stream") }
            : new { Input = input };

        try
        {
            var result = await client.PostAsync(path, body, ct);
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
                // flag without value
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--"))
                    dict[key] = "true";
                else
                    dict[key] = args[++i];
            }
        }
        return dict;
    }
}
