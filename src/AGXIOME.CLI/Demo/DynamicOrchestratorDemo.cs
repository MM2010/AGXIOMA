// DynamicOrchestratorDemo — Runtime agent instantiation driven by LLM planning.
//
// Flow:
//   1. User types a goal phrase
//   2. Orchestrator (DeepSeek) decomposes it into a JSON task plan
//   3. TaskRegistry stores each task in Redis (agxiome:tasks:<runId>:<taskId>)
//   4. Workers are instantiated at runtime — one per planned task
//   5. Each worker executes with DeepSeek, updates Redis status + result
//   6. Orchestrator waits for all tasks, reads context from Redis, synthesises
//   7. Full context timeline (who did what, in what order) printed at the end
//
// Usage:
//   agxiome demo orchestrator [--redis localhost:6379] [--model deepseek-chat]
//
// Requires: DEEPSEEK_API_KEY, Redis running on localhost:6379 (default)

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using AGXIOME.CLI.Demo.Tools;
using AGXIOME.Core.Agent;
using AGXIOME.LlmWiky;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AGXIOME.CLI.Demo;

public static class DynamicOrchestratorDemo
{
    // Redis key namespace — all keys scoped to the run; TTL = 1 hour
    private const string KeyPrefix = "agxiome:tasks";
    private const int    KeyTtlSec = 3600;

    // ── JSON schema the orchestrator MUST return ──────────────────────────────
    private const string PlanSchema = """
        {
          "tasks": [
            {
              "id": "t1",
              "agentName": "ResearchAgent",
              "agentRole": "researcher",
              "systemPrompt": "You are a focused researcher. Be concise.",
              "taskPrompt": "Research and summarise: <topic>",
              "dependsOn": []
            }
          ]
        }
        """;

    /// <summary>
    /// Prepended to every worker's SystemPrompt so the agent understands its tools
    /// and how to use them correctly, regardless of what the planner generated.
    /// </summary>
    private const string WorkerToolGuide =
        "You are an autonomous AI agent with access to the following tools:\n" +
        "  • http_fetch(url, max_chars?)   — HTTP GET any public URL; returns stripped plain text.\n" +
        "  • web_search(query, num_results?) — Multi-engine keyword search (DDG→Yahoo→Bing); returns titles, URLs, snippets.\n" +
        "  • file_save(filename, content)   — Save text to c:/temp/agxioma/<filename>.agmx. Returns saved path.\n\n" +
        "Tool usage rules:\n" +
        "  1. For a known URL, call http_fetch FIRST — no need for web_search.\n" +
        "  2. For open queries, call web_search to get URLs, then http_fetch to read each result.\n" +
        "  3. Always call tools to gather data; do NOT say you cannot access the internet.\n" +
        "  4. MANDATORY: When you have a result to persist, call file_save BEFORE writing your final answer.\n" +
        "     Generate the filename as: <task-description>-<short-guid>-<yyyyMMddHHmmss> (NO extension needed).\n" +
        "     Example: file_save('news-links-a1b2c3d4-20260513143000', '<content here>')\n" +
        "  5. After file_save confirms the path, include it in your final answer.\n" +
        "  6. If a tool returns an error, try an alternative URL or rephrase the search query.\n" +
        "  7. IMPORTANT — Google services block HTML scraping. Use RSS feeds instead:\n" +
        "       Google News Italy  → http_fetch('https://news.google.com/rss?hl=it&gl=IT&ceid=IT:it')\n" +
        "       Google Trends IT   → http_fetch('https://trends.google.com/trending/rss?geo=IT')\n" +
        "       Google Trends US   → http_fetch('https://trends.google.com/trending/rss?geo=US')\n" +
        "  8. DO NOT try to read local files with http_fetch. Results from previous agents are already\n" +
        "     injected in your prompt under '## Context from previous agents' — use that text directly.\n";

    // ══════════════════════════════════════════════════════════════════════════
    // ENTRY POINT
    // ══════════════════════════════════════════════════════════════════════════


    public static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        var apiKey     = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("[AGXIOME] DEEPSEEK_API_KEY not set.");
            Console.ResetColor();
            return 1;
        }

        var redisConn  = GetArg(args, "--redis") ?? "localhost:6379,abortConnect=false,connectTimeout=3000";
        var model      = GetArg(args, "--model") ?? "deepseek-chat";
        var runId      = Guid.NewGuid().ToString("N")[..12];

        // ── Connect to Redis ──────────────────────────────────────────────────
        IDatabase? redis = null;
        IConnectionMultiplexer? mux = null;
        try
        {
            mux   = await ConnectionMultiplexer.ConnectAsync(redisConn);
            redis = mux.GetDatabase();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[Redis] Connected to {redisConn}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[Redis] Could not connect ({ex.Message}) — falling back to in-memory task store.");
            Console.ResetColor();
        }

        var taskStore = new TaskStore(redis, runId);

        // ── Setup shared DeepSeek engine factory ──────────────────────────────
        AgentFactory.Configure(LoggerFactory.Create(b => b
            .AddConsole()
            .AddFilter("AGXIOME", LogLevel.Warning)
            .AddFilter("Microsoft", LogLevel.Error)));

        var llmBase = new LLMWIKYConfig
        {
            Provider           = LLMProviderType.DeepSeek,
            Model              = model,
            ApiKey             = apiKey,
            MaxTokens          = 4096,
            DefaultTemperature = 0.3,
            MaxRetries         = 2
        };

        // ══════════════════════════════════════════════════════════════════════
        // INTERACTIVE LOOP
        // ══════════════════════════════════════════════════════════════════════
        PrintBanner(model, redisConn, runId);

        while (!ct.IsCancellationRequested)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("\nGoal > ");
            Console.ResetColor();
            var goal = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(goal)) continue;
            if (goal.ToLower() is "exit" or "quit") break;

            await RunOrchestratedSessionAsync(goal, runId, llmBase, taskStore, ct);
        }

        mux?.Close();
        return 0;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ORCHESTRATED SESSION
    // ══════════════════════════════════════════════════════════════════════════

    private static async Task RunOrchestratedSessionAsync(
        string goal,
        string runId,
        LLMWIKYConfig llmBase,
        TaskStore taskStore,
        CancellationToken ct)
    {
        var sessionId = Guid.NewGuid().ToString("N")[..8];
        var sw        = System.Diagnostics.Stopwatch.StartNew();

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"╔══ Orchestrator [{sessionId}] ══════════════════════════════════════╗");
        Console.WriteLine($"  Goal: {goal}");
        Console.WriteLine($"╚════════════════════════════════════════════════════════════════╝");
        Console.ResetColor();

        // ── Phase 1: Orchestrator plans tasks ─────────────────────────────────
        Console.WriteLine("\n[Phase 1] Orchestrator decomposing goal into task plan…");
        var plan = await PlanTasksAsync(goal, llmBase, ct);

        if (plan is null || plan.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  Orchestrator returned no tasks — check the goal phrase.");
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  Plan: {plan.Count} task(s) identified");
        Console.ResetColor();

        // ── Phase 2: Register all tasks in Redis ──────────────────────────────
        Console.WriteLine("\n[Phase 2] Registering tasks in Redis…");
        foreach (var task in plan)
        {
            await taskStore.RegisterAsync(sessionId, task, ct);
            Console.WriteLine($"  ↳ [{task.Id}] {task.AgentName}: {task.TaskPrompt[..Math.Min(60, task.TaskPrompt.Length)]}…");
        }

        // Save orchestrator context (goal + plan) to Redis
        await taskStore.SaveContextAsync(sessionId, "orchestrator", new OrchestratorContext(
            Goal: goal, SessionId: sessionId, RunId: taskStore.RunId,
            PlannedTasks: plan.Select(t => t.Id).ToList(),
            StartedAt: DateTime.UtcNow), ct);

        // ── Phase 3: Spawn workers in parallel (respecting dependencies) ───────
        Console.WriteLine("\n[Phase 3] Spawning worker agents…");
        var completionChannel = Channel.CreateBounded<TaskCompletion>(plan.Count);
        var workerTasks       = new List<Task>();

        // Topological execution: tasks with no deps start immediately;
        // tasks with deps wait for their deps to complete
        var completedIds = new HashSet<string>();
        var remaining    = plan.ToList();

        while (remaining.Count > 0 && !ct.IsCancellationRequested)
        {
            // Find tasks whose dependencies are all done
            var ready = remaining
                .Where(t => t.DependsOn.All(dep => completedIds.Contains(dep)))
                .ToList();

            if (ready.Count == 0)
            {
                // Circular dependency guard
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [WARNING] Circular dependency or unresolvable deps in remaining {remaining.Count} tasks");
                Console.ResetColor();
                break;
            }

            // Launch ready tasks in parallel
            var batch = ready.Select(task => RunWorkerAgentAsync(
                task, sessionId, llmBase, taskStore, completionChannel.Writer, ct)).ToList();

            await Task.WhenAll(batch);

            // Collect completions from this batch
            for (int i = 0; i < ready.Count; i++)
            {
                var completion = await completionChannel.Reader.ReadAsync(ct);
                completedIds.Add(completion.TaskId);
                remaining.RemoveAll(t => t.Id == completion.TaskId);
            }
        }

        completionChannel.Writer.Complete();

        // ── Phase 4: Orchestrator reads all results from Redis, synthesises ────
        Console.WriteLine("\n[Phase 4] Orchestrator reading context and synthesising…");
        var allResults = await taskStore.ReadAllResultsAsync(sessionId, ct);
        var synthesis  = await SynthesiseAsync(goal, allResults, llmBase, ct);

        // Save final synthesis to Redis
        await taskStore.SaveContextAsync(sessionId, "synthesis", synthesis, ct);

        // ── Phase 5: Print context timeline ────────────────────────────────────
        sw.Stop();
        PrintTimeline(sessionId, goal, allResults, synthesis, sw.Elapsed);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PHASE 1: LLM PLANNING
    // ══════════════════════════════════════════════════════════════════════════

    private static async Task<List<TaskPlan>> PlanTasksAsync(
        string goal, LLMWIKYConfig llmBase, CancellationToken ct)
    {
        var plannerConfig = new AgentConfig
        {
            Id           = "planner",
            Name         = "Task Planner",
            SystemPrompt =
                "You are a task planner for a multi-agent AI system. " +
                "Decompose the user goal into 2–5 focused subtasks for specialist agents. " +
                $"Return ONLY a valid JSON object matching this schema:\n{PlanSchema}\n" +
                "Rules:\n" +
                "- Each task must have: id (t1,t2…), agentName, agentRole, systemPrompt, taskPrompt, dependsOn (list of ids or [])\n" +
                "- taskPrompt must be a concrete, self-contained instruction for that agent\n" +
                "- Use dependsOn to express sequencing (e.g. a writer depends on a researcher)\n" +
                "- Return ONLY the JSON, no markdown fences, no explanation\n" +
                "\nEach worker agent has access to these tools:\n" +
                "  • http_fetch(url, max_chars?)  — HTTP GET any public URL, returns stripped plain text\n" +
                "  • web_search(query, num_results?) — DuckDuckGo search; may hit bot-blocks on some queries\n" +
                "  • file_save(filename, content) — persist text to a local file\n" +
                "\nPlanning guidance:\n" +
                "- For scraping a specific known URL, prefer http_fetch over web_search\n" +
                "- For open-ended research queries, use web_search then http_fetch to follow links\n" +
                "- If a task needs to visit multiple pages, assign it to one agent with http_fetch\n" +
                "- Set agentRole to 'scraper' for URL-fetch tasks, 'researcher' for search tasks, 'writer' for synthesis tasks\n" +
                "\nCRITICAL — Google blocks HTML scraping. Always use RSS for Google services:\n" +
                "  • Google News Italy: http_fetch('https://news.google.com/rss?hl=it&gl=IT&ceid=IT:it')\n" +
                "  • Google Trends IT:  http_fetch('https://trends.google.com/trending/rss?geo=IT')\n" +
                "  • Google Trends US:  http_fetch('https://trends.google.com/trending/rss?geo=US')\n" +
                "\nInter-agent dependencies:\n" +
                "  • When a task depends on a previous one (via dependsOn), the orchestrator automatically\n" +
                "    injects that agent's output into the prompt under '## Context from previous agents'.\n" +
                "  • DO NOT instruct a dependent agent to read a local file — it will receive the data directly.\n" +
                "  • In taskPrompt for a dependent task, write: 'Using the context provided above, ...' \n" +
                "  • Files are saved to c:/temp/agxioma/ with .agmx extension.\n" +
                "    When file_save is needed, the agent generates the filename as: <descriptive>-<guid-short>-<yyyyMMddHHmmss>\n",
            MaxIterations = 1,
            Timeout       = TimeSpan.FromSeconds(30),
            ReasoningEngine = llmBase with { DefaultTemperature = 0.1 }
        };

        var engine = new LLMWIKYEngine(llmBase with { DefaultTemperature = 0.1 });
        var agent  = AgentFactory.CreateWithEngine(plannerConfig, engine);

        try
        {
            var resp    = await agent.RunAsync(new AgentRequest(goal), ct);
            var content = resp.Content.Trim();

            // Strip markdown code fences if LLM added them anyway
            if (content.StartsWith("```")) content = StripMarkdownFence(content);

            var doc = JsonDocument.Parse(content);
            var arr = doc.RootElement.GetProperty("tasks");

            return arr.EnumerateArray().Select(t => new TaskPlan(
                Id:           t.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N")[..4],
                AgentName:    t.GetProperty("agentName").GetString() ?? "Worker",
                AgentRole:    t.GetProperty("agentRole").GetString() ?? "specialist",
                SystemPrompt: t.GetProperty("systemPrompt").GetString() ?? "You are a helpful assistant.",
                TaskPrompt:   t.GetProperty("taskPrompt").GetString() ?? goal,
                DependsOn:    t.TryGetProperty("dependsOn", out var deps)
                                ? deps.EnumerateArray().Select(d => d.GetString()!).ToList()
                                : []
            )).ToList();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  [Planner] Failed to parse JSON plan ({ex.GetType().Name}): {ex.Message[..Math.Min(80, ex.Message.Length)]}");
            Console.WriteLine("  Falling back to single default task.");
            Console.ResetColor();

            // Graceful fallback: one task = the whole goal
            return [new TaskPlan("t1", "GeneralistAgent", "generalist",
                "You are a helpful AI assistant. Be thorough and clear.", goal, [])];
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PHASE 3: WORKER AGENT
    // ══════════════════════════════════════════════════════════════════════════

    private static async Task RunWorkerAgentAsync(
        TaskPlan task,
        string sessionId,
        LLMWIKYConfig llmBase,
        TaskStore taskStore,
        ChannelWriter<TaskCompletion> completionWriter,
        CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"  ↳ [{task.Id}] {task.AgentName} starting…");
        Console.ResetColor();

        // Update Redis: RUNNING
        await taskStore.UpdateStatusAsync(sessionId, task.Id, TaskStatus.Running, null, ct);

        try
        {
            // Instantiate worker agent at runtime from task plan
            var workerConfig = new AgentConfig
            {
                Id           = $"{task.Id}-{task.AgentName.ToLower().Replace(" ", "")}",
                Name         = task.AgentName,
                // Prepend a tool-usage guide so every worker knows exactly how to call tools,
                // regardless of what the planner put in systemPrompt.
                SystemPrompt = WorkerToolGuide + "\n\n" + task.SystemPrompt,
                // Writer/analyst roles need more iterations: they fetch data AND write analysis AND save.
                // Scraper/researcher roles may also need many iterations for multi-page fetches.
                MaxIterations = task.AgentRole.ToLowerInvariant() switch {
                    "writer" or "analyst" or "reporter" => 16,
                    "scraper" or "crawler" or "researcher" => 12,
                    _ => 10
                },
                Timeout       = TimeSpan.FromSeconds(180),
                ReasoningEngine = llmBase
            };

            var engine = new LLMWIKYEngine(llmBase);
            var agent  = AgentFactory.CreateWithEngine(workerConfig, engine);

            // Register real tools based on agent role — this is what gives the agent
            // actual capabilities (web search, HTTP fetch, file save).
            // The tools are injected into the agent's registry; MapRequest will
            // automatically forward their schemas to the LLM in the next InferAsync call.
            // Files always go to c:/temp/agxioma/ (enforced by FileSaveTool default).
            var outputDir = Path.Combine("c:", "temp", "agxioma");
            foreach (var tool in AgxiomeToolCatalogue.ForRole(task.AgentRole, outputDir))
                agent.RegisterTool(tool);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            var toolNames = AgxiomeToolCatalogue.ForRole(task.AgentRole)
                .Select(t => t.Name).ToList();
            Console.WriteLine($"  [{task.Id}] Tools: {string.Join(", ", toolNames)}");
            Console.ResetColor();

            // If task has deps, inject the dep results into context
            var prompt = task.TaskPrompt;
            if (task.DependsOn.Count > 0)
            {
                var depResults = await taskStore.ReadResultsForAsync(sessionId, task.DependsOn, ct);
                if (depResults.Count > 0)
                {
                    var context = string.Join("\n\n", depResults.Select(r =>
                        $"[Result from {r.AgentName} on '{r.TaskPrompt[..Math.Min(40, r.TaskPrompt.Length)]}…']:\n{r.Result}"));
                    prompt = $"{prompt}\n\n## Context from previous agents:\n{context}";
                }
            }

            var response = await agent.RunAsync(new AgentRequest(prompt), ct);
            sw.Stop();

            // Auto-save fallback: if the agent produced substantial content (>500 chars)
            // and has file_save capability but didn't call it (LLM skipped the tool call),
            // persist the result automatically so the pipeline never loses data.
            var hasFileSave = AgxiomeToolCatalogue.ForRole(task.AgentRole)
                .Any(t => t.Name == "file_save");
            if (hasFileSave && response.Content.Length > 500
                && !response.Content.Contains("c:/temp/agxioma/", StringComparison.OrdinalIgnoreCase)
                && !response.Content.Contains("File saved:", StringComparison.OrdinalIgnoreCase))
            {
                // LLM answered without calling file_save — auto-persist
                var autoTimestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                var autoGuid      = Guid.NewGuid().ToString("N")[..8];
                var autoFilename  = $"auto-{task.AgentName.ToLower().Replace(" ", "-")}-{autoGuid}-{autoTimestamp}.agmx";
                var autoPath      = Path.Combine("c:", "temp", "agxioma", autoFilename);
                try
                {
                    Directory.CreateDirectory(Path.Combine("c:", "temp", "agxioma"));
                    await File.WriteAllTextAsync(autoPath, response.Content, Encoding.UTF8, ct);
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"  [{task.Id}] Auto-saved result → {autoPath}");
                    Console.ResetColor();
                }
                catch (Exception saveEx)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"  [{task.Id}] Auto-save failed: {saveEx.Message}");
                    Console.ResetColor();
                }
            }

            // Save result to Redis
            var result = new TaskResult(
                TaskId:    task.Id,
                AgentName: task.AgentName,
                AgentRole: task.AgentRole,
                TaskPrompt: task.TaskPrompt,
                Result:    response.Content,
                Status:    TaskStatus.Done,
                Tokens:    response.Metadata?.TotalTokens,
                ElapsedMs: sw.ElapsedMilliseconds,
                CompletedAt: DateTime.UtcNow);

            await taskStore.UpdateStatusAsync(sessionId, task.Id, TaskStatus.Done, result, ct);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ [{task.Id}] {task.AgentName} done ({sw.ElapsedMilliseconds}ms" +
                              $"{(result.Tokens.HasValue ? $", {result.Tokens} tokens" : "")})");
            Console.ResetColor();

            await completionWriter.WriteAsync(new TaskCompletion(task.Id, true, null), ct);
        }
        catch (Exception ex)
        {
            sw.Stop();
            await taskStore.UpdateStatusAsync(sessionId, task.Id, TaskStatus.Failed,
                new TaskResult(task.Id, task.AgentName, task.AgentRole, task.TaskPrompt,
                    $"ERROR: {ex.Message}", TaskStatus.Failed, null, sw.ElapsedMilliseconds, DateTime.UtcNow), ct);

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ✗ [{task.Id}] {task.AgentName} failed: {ex.Message[..Math.Min(60, ex.Message.Length)]}");
            Console.ResetColor();

            await completionWriter.WriteAsync(new TaskCompletion(task.Id, false, ex.Message), ct);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PHASE 4: SYNTHESIS
    // ══════════════════════════════════════════════════════════════════════════

    private static async Task<string> SynthesiseAsync(
        string goal, List<TaskResult> results, LLMWIKYConfig llmBase, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Original goal: {goal}");
        sb.AppendLine();
        sb.AppendLine("Worker results:");
        foreach (var r in results)
        {
            sb.AppendLine($"\n## {r.AgentName} ({r.AgentRole}) — [{r.Status}]");
            sb.AppendLine($"Task: {r.TaskPrompt}");
            sb.AppendLine($"Result:\n{r.Result}");
        }
        sb.AppendLine();
        sb.AppendLine("Synthesise all worker results into a coherent, well-structured final answer for the original goal. Be concise.");

        var synthConfig = new AgentConfig
        {
            Id           = "synthesiser",
            SystemPrompt = "You are a synthesis expert. Combine specialist agent outputs into a unified, concise answer.",
            MaxIterations = 1,
            Timeout       = TimeSpan.FromSeconds(60),
            ReasoningEngine = llmBase
        };

        try
        {
            var engine = new LLMWIKYEngine(llmBase with { DefaultTemperature = 0.4 });
            var agent  = AgentFactory.CreateWithEngine(synthConfig, engine);
            var resp   = await agent.RunAsync(new AgentRequest(sb.ToString()), ct);
            return resp.Content;
        }
        catch (Exception ex)
        {
            return $"[Synthesis failed: {ex.Message}]";
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CONSOLE OUTPUT
    // ══════════════════════════════════════════════════════════════════════════

    private static void PrintBanner(string model, string redis, string runId)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  AGXIOME — Dynamic Orchestrator Demo                        ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine($"  Model  : {model}");
        Console.WriteLine($"  Redis  : {redis}");
        Console.WriteLine($"  RunId  : {runId}");
        Console.WriteLine($"  Keys   : agxiome:tasks:<sessionId>:*");
        Console.WriteLine($"  Exit   : type 'exit'");
        Console.WriteLine();
        Console.WriteLine("Type your goal. The orchestrator will plan, spawn agents, and synthesise.");
    }

    private static void PrintTimeline(
        string sessionId, string goal, List<TaskResult> results, string synthesis, TimeSpan elapsed)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("══ Context Timeline ═══════════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine($"  Session : {sessionId}");
        Console.WriteLine($"  Goal    : {goal}");
        Console.WriteLine($"  Agents  : {results.Count}");
        Console.WriteLine($"  Elapsed : {elapsed.TotalSeconds:F1}s total");
        Console.WriteLine();

        foreach (var r in results.OrderBy(r => r.CompletedAt))
        {
            var statusColor = r.Status == TaskStatus.Done ? ConsoleColor.Green : ConsoleColor.Red;
            Console.ForegroundColor = statusColor;
            Console.Write($"  [{r.Status,-6}] ");
            Console.ResetColor();
            Console.Write($"{r.AgentName,-20} {r.ElapsedMs,5}ms");
            if (r.Tokens.HasValue) Console.Write($"  {r.Tokens} tok");
            Console.WriteLine();
            // Print first 150 chars of result
            var preview = r.Result.Length > 150 ? r.Result[..150] + "…" : r.Result;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"           {preview.Replace("\n", " ")}");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("══ Final Synthesis ════════════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine(synthesis);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // REDIS TASK STORE
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Abstracts task state persistence behind Redis (or in-memory fallback).
    /// Key structure:
    ///   agxiome:tasks:{runId}:{sessionId}:plan:{taskId}    → TaskPlan JSON
    ///   agxiome:tasks:{runId}:{sessionId}:status:{taskId}  → TaskStatus string
    ///   agxiome:tasks:{runId}:{sessionId}:result:{taskId}  → TaskResult JSON
    ///   agxiome:tasks:{runId}:{sessionId}:ctx:{scope}      → context JSON
    /// </summary>
    private sealed class TaskStore(IDatabase? redis, string runId)
    {
        public string RunId => runId;

        // In-memory fallback when Redis is unavailable
        private readonly Dictionary<string, string> _mem = new();

        private async Task SetAsync(string key, string value, CancellationToken ct)
        {
            if (redis is not null)
                await redis.StringSetAsync(key, value, TimeSpan.FromSeconds(KeyTtlSec));
            else
                _mem[key] = value;
        }

        private async Task<string?> GetAsync(string key)
        {
            if (redis is not null)
                return await redis.StringGetAsync(key);
            _mem.TryGetValue(key, out var v);
            return v;
        }

        private async Task<IEnumerable<string>> ScanAsync(string pattern)
        {
            if (redis is not null)
            {
                // SCAN on first endpoint — suitable for dev/single-node Redis
                var server  = redis.Multiplexer.GetServers().First();
                var keys    = server.KeysAsync(pattern: pattern);
                var results = new List<string>();
                await foreach (var k in keys)
                    results.Add((string)k!);
                return results;
            }
            var prefix = pattern.Replace("*", "");
            return _mem.Keys.Where(k => k.StartsWith(prefix));
        }

        public async Task RegisterAsync(string sessionId, TaskPlan task, CancellationToken ct)
        {
            var planKey   = $"{KeyPrefix}:{runId}:{sessionId}:plan:{task.Id}";
            var statusKey = $"{KeyPrefix}:{runId}:{sessionId}:status:{task.Id}";
            await SetAsync(planKey,   JsonSerializer.Serialize(task), ct);
            await SetAsync(statusKey, TaskStatus.Pending.ToString(),  ct);
        }

        public async Task UpdateStatusAsync(
            string sessionId, string taskId, TaskStatus status, TaskResult? result, CancellationToken ct)
        {
            var statusKey = $"{KeyPrefix}:{runId}:{sessionId}:status:{taskId}";
            await SetAsync(statusKey, status.ToString(), ct);
            if (result is not null)
            {
                var resultKey = $"{KeyPrefix}:{runId}:{sessionId}:result:{taskId}";
                await SetAsync(resultKey, JsonSerializer.Serialize(result), ct);
            }
        }

        public async Task SaveContextAsync<T>(string sessionId, string scope, T ctx, CancellationToken ct)
        {
            var key = $"{KeyPrefix}:{runId}:{sessionId}:ctx:{scope}";
            await SetAsync(key, JsonSerializer.Serialize(ctx), ct);
        }

        public async Task<List<TaskResult>> ReadAllResultsAsync(string sessionId, CancellationToken ct)
        {
            var pattern = $"{KeyPrefix}:{runId}:{sessionId}:result:*";
            var keys    = await ScanAsync(pattern);
            var results = new List<TaskResult>();
            foreach (var key in keys)
            {
                var raw = await GetAsync(key);
                if (raw is not null)
                    results.Add(JsonSerializer.Deserialize<TaskResult>(raw)!);
            }
            return results;
        }

        public async Task<List<TaskResult>> ReadResultsForAsync(
            string sessionId, IReadOnlyList<string> taskIds, CancellationToken ct)
        {
            var results = new List<TaskResult>();
            foreach (var id in taskIds)
            {
                var key = $"{KeyPrefix}:{runId}:{sessionId}:result:{id}";
                var raw = await GetAsync(key);
                if (raw is not null)
                    results.Add(JsonSerializer.Deserialize<TaskResult>(raw)!);
            }
            return results;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // HELPERS & RECORDS
    // ══════════════════════════════════════════════════════════════════════════

    private static string StripMarkdownFence(string s)
    {
        var lines = s.Split('\n').ToList();
        if (lines.Count >= 2 && lines[0].StartsWith("```")) lines.RemoveAt(0);
        if (lines.Count >= 1 && lines[^1].TrimEnd() == "```") lines.RemoveAt(lines.Count - 1);
        return string.Join('\n', lines);
    }

    private static string? GetArg(string[] args, string flag)
    {
        var idx = Array.IndexOf(args, flag);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }

    private enum TaskStatus { Pending, Running, Done, Failed }

    private record TaskPlan(
        string Id,
        string AgentName,
        string AgentRole,
        string SystemPrompt,
        string TaskPrompt,
        List<string> DependsOn);

    private record TaskCompletion(string TaskId, bool Success, string? Error);

    private record TaskResult(
        string TaskId,
        string AgentName,
        string AgentRole,
        string TaskPrompt,
        string Result,
        TaskStatus Status,
        int? Tokens,
        long ElapsedMs,
        DateTime CompletedAt);

    private record OrchestratorContext(
        string Goal,
        string SessionId,
        string RunId,
        List<string> PlannedTasks,
        DateTime StartedAt);
}
