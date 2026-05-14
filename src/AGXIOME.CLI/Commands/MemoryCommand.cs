using AGXIOME.Core.Storage;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Models;

namespace AGXIOME.CLI.Commands;

/// <summary>agxiome memory — manage markdown memory store.</summary>
public sealed class MemoryCommand : ICliCommand
{
    public string Name => "memory";
    public string Description => "Manage markdown memory store (init, add, search)";

    public ValueTask<CliResult> ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
            return ValueTask.FromResult(CliResult.Fail("Usage: agxiome memory <init|add|search> [args...]"));

        var sub = args[0].ToLowerInvariant();
        return sub switch
        {
            "init" => ExecuteInitAsync(args[1..], ct),
            "add" => ExecuteAddAsync(args[1..], ct),
            "search" => ExecuteSearchAsync(args[1..], ct),
            _ => ValueTask.FromResult(CliResult.Fail($"Unknown memory subcommand '{sub}'. Use: init, add, search"))
        };
    }

    private static ValueTask<CliResult> ExecuteInitAsync(string[] args, CancellationToken ct)
    {
        var path = args.Length > 0 ? args[0] : "./memory";
        Directory.CreateDirectory(path);
        var stateDir = Path.Combine(path, "state");
        Directory.CreateDirectory(stateDir);
        return ValueTask.FromResult(CliResult.Ok($"Memory store initialized at: {Path.GetFullPath(path)}"));
    }

    private static async ValueTask<CliResult> ExecuteAddAsync(string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
            return CliResult.Fail("Usage: agxiome memory add <file.md>");

        var file = args[0];
        if (!File.Exists(file))
            return CliResult.Fail($"File not found: {file}");

        var config = new MarkdownMemoryConfig { BasePath = Path.GetDirectoryName(file) ?? "./memory" };
        var store = new MarkdownMemoryStore(config);
        var content = await File.ReadAllTextAsync(file, ct);
        var interaction = new AgentInteraction("cli", "user", content, CreatedAt: DateTime.UtcNow);
        await store.SaveAsMarkdownAsync(file, interaction, ct);
        return CliResult.Ok($"Added: {file}");
    }

    private static async ValueTask<CliResult> ExecuteSearchAsync(string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
            return CliResult.Fail("Usage: agxiome memory search <query> [--dir <path>]");

        var query = args[0];
        var dir = "./memory";
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--dir" && i + 1 < args.Length)
                dir = args[i + 1];
        }

        var store = new MarkdownMemoryStore(new MarkdownMemoryConfig { BasePath = dir });
        var results = await store.SearchByGrepAsync(query, ct);
        if (results.Count == 0)
            return CliResult.Ok("No matches found.");

        var output = string.Join("\n", results.Select(r => $"  {r}"));
        return CliResult.Ok($"Found {results.Count} matches:\n{output}");
    }
}
