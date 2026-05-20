// DeepSeekDemo — Interactive console session using DeepSeek as LLM provider.
// Usage: agxiome demo deepseek [--model deepseek-chat|deepseek-reasoner] [--system "..."]
//
// Requires env var: DEEPSEEK_API_KEY
// Optional:         DEEPSEEK_MODEL   (default: deepseek-chat)
//
// The agent runs a ReAct-style loop: Think → Act (via tools if any) → Respond.
// Streaming is enabled: tokens are printed as they arrive.

using AGXIOME.Core.Agent;
using AGXIOME.LlmWiky;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AGXIOME.CLI.Demo;

/// <summary>
/// MU-01 demo: interactive DeepSeek agent session from the command line.
/// Wire: DeepSeekDemo.RunAsync(args, ct) from the CLI dispatcher.
/// </summary>
public static class DeepSeekDemo
{
    private const string DefaultModel      = "deepseek-chat";
    private const string DefaultSystemPrompt =
        "You are AGXIOME Assistant, a helpful and concise AI agent. " +
        "Answer questions clearly and accurately. " +
        "If you don't know something, say so honestly.";

    /// <summary>Entry point called from Program.cs when verb is 'demo'.</summary>
    public static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        // ── Resolve config ────────────────────────────────────────────────────
        var apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("[AGXIOME] DEEPSEEK_API_KEY is not set.");
            Console.Error.WriteLine("          Set it with: $env:DEEPSEEK_API_KEY = \"sk-...\"");
            Console.ResetColor();
            return 1;
        }

        var model      = GetArg(args, "--model")  ?? Environment.GetEnvironmentVariable("DEEPSEEK_MODEL") ?? DefaultModel;
        var systemPrompt = GetArg(args, "--system") ?? DefaultSystemPrompt;
        var stream     = args.Contains("--stream");

        // ── Build LLMWIKYEngine with DeepSeek provider ────────────────────────
        var llmConfig = new LLMWIKYConfig
        {
            Provider    = LLMProviderType.DeepSeek,
            Model       = model,
            ApiKey      = apiKey,
            // DeepSeek uses OpenAI-compatible endpoint — factory patches this automatically
            MaxTokens   = 4096,
            DefaultTemperature = 0.7,
            MaxRetries  = 3,
        };

        var engine = new LLMWIKYEngine(llmConfig);

        // ── Create agent ──────────────────────────────────────────────────────
        var agentConfig = new AgentConfig
        {
            Id           = "deepseek-demo",
            Name         = "DeepSeek Demo Agent",
            SystemPrompt = systemPrompt,
            MaxIterations = 10,
            Timeout      = TimeSpan.FromSeconds(60),
            ReasoningEngine = llmConfig,
        };

        // Wire the engine directly — bypasses AgentFactory stub.
        // Suppress agent internals (Info/Debug) from polluting the interactive console;
        // only show Warning+ from the AGXIOME.Core category.
        AgentFactory.Configure(LoggerFactory.Create(b => b
            .AddConsole()
            .SetMinimumLevel(LogLevel.Warning)
            .AddFilter("AGXIOME", LogLevel.Warning)
            .AddFilter("Microsoft", LogLevel.Error)));
        var agent = AgentFactory.CreateWithEngine(agentConfig, engine);

        // ── Print header ──────────────────────────────────────────────────────
        PrintHeader(model, stream);

        // ── REPL loop ─────────────────────────────────────────────────────────
        var history = new List<AgentMessage>();
        while (!ct.IsCancellationRequested)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("\nYou > ");
            Console.ResetColor();

            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Trim().ToLower() is "exit" or "quit" or "/bye") break;
            if (input.Trim() == "/clear") { history.Clear(); Console.Clear(); PrintHeader(model, stream); continue; }
            if (input.Trim() == "/history") { PrintHistory(history); continue; }

            var request = new AgentRequest(input, History: history);

            try
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"\n[{model}] > ");
                Console.ResetColor();

                if (stream)
                {
                    // Streaming mode: print tokens as they arrive
                    var fullContent = new System.Text.StringBuilder();
                    await foreach (var chunk in agent.RunStreamAsync(request, ct))
                    {
                        Console.Write(chunk.Content);
                        fullContent.Append(chunk.Content);
                    }
                    Console.WriteLine();

                    // Add to history
                    history.Add(new AgentMessage("user",      input));
                    history.Add(new AgentMessage("assistant", fullContent.ToString()));
                }
                else
                {
                    var response = await agent.RunAsync(request, ct);
                    Console.WriteLine(response.Content);
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    var meta = response.Metadata;
                    var statsTokens    = meta?.TotalTokens is > 0 ? $"{meta.TotalTokens} tokens" : "";
                    var statsInf       = meta?.InferenceCount is > 0 ? $"{meta.InferenceCount} calls" : "";
                    var statsMs        = meta?.Duration.TotalMilliseconds is > 0 ? $"{meta.Duration.TotalMilliseconds:0}ms" : "";
                    var statsLine      = string.Join(" | ", new[] { statsTokens, statsInf, statsMs }.Where(s => s.Length > 0));
                    if (statsLine.Length > 0) Console.WriteLine($"  [{statsLine}]");
                    Console.ResetColor();

                    // Add to history for multi-turn conversation
                    history.Add(new AgentMessage("user",      input));
                    history.Add(new AgentMessage("assistant", response.Content));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("401"))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("[AGXIOME] Authentication failed — check your DEEPSEEK_API_KEY.");
                Console.ResetColor();
                return 2;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"[AGXIOME] Error: {ex.Message}");
                Console.ResetColor();
            }
        }

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("\n[AGXIOME] Session ended.");
        Console.ResetColor();
        return 0;
    }

    private static void PrintHeader(string model, bool stream)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║  AGXIOME — DeepSeek Interactive Demo                ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine($"  Model  : {model}");
        Console.WriteLine($"  Mode   : {(stream ? "streaming" : "blocking")}");
        Console.WriteLine($"  Exit   : type 'exit' or Ctrl+C");
        Console.WriteLine($"  Cmds   : /clear  /history");
        Console.WriteLine();
    }

    private static void PrintHistory(List<AgentMessage> history)
    {
        if (history.Count == 0) { Console.WriteLine("  (no history yet)"); return; }
        Console.ForegroundColor = ConsoleColor.DarkGray;
        foreach (var msg in history)
        {
            var label = msg.Role == "user" ? "You" : "Agent";
            var preview = msg.Content.Length > 80 ? msg.Content[..80] + "…" : msg.Content;
            Console.WriteLine($"  [{label}] {preview}");
        }
        Console.ResetColor();
    }

    private static string? GetArg(string[] args, string flag)
    {
        var idx = Array.IndexOf(args, flag);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }
}
