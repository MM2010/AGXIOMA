// OpenClaw — Program.cs
// CLI agent powered by AGXIOME. Commands: review, analyze, chat.
// Provider: DeepSeek (default). Override via env DEEPSEEK_API_KEY or OPENAI_API_KEY.
using AGXIOME.Core;
using AGXIOME.Shared.Config;
using Microsoft.Extensions.Configuration;
using OpenClaw;
using OpenClaw.Skills;

// ── Configuration ──────────────────────────────────────────────────────────
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var apiKey  = configuration["LLMProvider:ApiKey"]
           ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
           ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
           ?? "";

var model   = configuration["LLMProvider:Model"]   ?? "deepseek-chat";
var rootArg = configuration["OpenClaw:Root"]       ?? Directory.GetCurrentDirectory();

// ── Parse CLI args ─────────────────────────────────────────────────────────
// Usage:
//   openclaw review [path]       — code review of path (default: cwd)
//   openclaw analyze [path]      — project structure analysis
//   openclaw chat "<prompt>"     — free-form conversation
if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

var command   = args[0].ToLowerInvariant();
var targetArg = args.Length > 1 ? args[1] : rootArg;

// Resolve target path: use as-is if absolute, else relative to cwd.
var targetPath = Path.IsPathRooted(targetArg)
    ? targetArg
    : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), targetArg));

// ── Build agent ────────────────────────────────────────────────────────────
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("[OpenClaw] ERROR: No API key found. Set DEEPSEEK_API_KEY or OPENAI_API_KEY.");
    return 2;
}

using var agent = AgxiomeBuilder.Agent("openclaw")
    .UseDeepSeek(apiKey, model)
    .WithAutoLoops(upperBound: 10)
    .Build();

var runner = new ClawRunner(agent);
var cts    = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// ── Dispatch ───────────────────────────────────────────────────────────────
try
{
    switch (command)
    {
        case "review":
        {
            Console.WriteLine($"[OpenClaw] Code review → {targetPath}");
            var skill  = CodeReviewSkill.Create(targetPath);
            var prompt = $"Perform a code review of the project at '{targetPath}'. Start with the top-level structure and drill into the most critical files.";
            var result = await runner.RunAsync(skill, prompt, cts.Token);
            Console.WriteLine(result);
            break;
        }

        case "analyze":
        {
            Console.WriteLine($"[OpenClaw] Project analysis → {targetPath}");
            var skill  = ProjectAnalysisSkill.Create(targetPath);
            var prompt = $"Analyse the project at '{targetPath}'. Produce a structured markdown report.";
            var result = await runner.RunAsync(skill, prompt, cts.Token);
            Console.WriteLine(result);
            break;
        }

        case "chat":
        {
            // Chat mode: prompt is the remaining args joined as a string.
            var prompt = args.Length > 1
                ? string.Join(' ', args[1..])
                : ReadPromptInteractive();

            if (string.IsNullOrWhiteSpace(prompt))
            {
                Console.Error.WriteLine("[OpenClaw] No prompt provided.");
                return 3;
            }

            var result = await runner.ChatAsync(prompt, cts.Token);
            Console.WriteLine(result);
            break;
        }

        default:
            Console.Error.WriteLine($"[OpenClaw] Unknown command '{command}'.");
            PrintUsage();
            return 4;
    }
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("\n[OpenClaw] Cancelled.");
    return 130;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[OpenClaw] ERROR: {ex.Message}");
    return 5;
}

return 0;

// ── Helpers ─────────────────────────────────────────────────────────────────

static void PrintUsage()
{
    Console.WriteLine("""
        OpenClaw — AI-powered code agent (AGXIOME)

        Usage:
          openclaw review  [path]         Code review of the given path (default: cwd)
          openclaw analyze [path]         Project structure analysis report
          openclaw chat    <prompt...>    Free-form conversation with the agent

        Environment variables:
          DEEPSEEK_API_KEY   DeepSeek API key (default provider)
          OPENAI_API_KEY     OpenAI API key (fallback)
        """);
}

static string ReadPromptInteractive()
{
    Console.Write("[OpenClaw] Enter prompt: ");
    return Console.ReadLine() ?? "";
}
