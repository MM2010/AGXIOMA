using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Patterns;

/// <summary>
/// GrokSwarm — 4-agent debate-style analysis pattern.
/// Captain decomposes task → Harper/Benjamin/Lucas work in parallel → Captain synthesizes.
/// 
/// Agent roles:
///   Captain:  Orchestrates, decomposes task, synthesizes with conflict resolution.
///   Harper:   Research, facts, evidence collection, source verification.
///   Benjamin: Logic, mathematics, code analysis, statistical reasoning.
///   Lucas:    Creative alternatives, contrarian thinking, scenario planning.
/// 
/// Parity with Swarms Python: HeavySwarm(use_grok_agents=True).
/// </summary>
public sealed class GrokSwarm
{
    private readonly GrokSwarmConfig _config;
    private readonly IAgxiomeAgent _captain;
    private readonly IAgxiomeAgent _harper;
    private readonly IAgxiomeAgent _benjamin;
    private readonly IAgxiomeAgent _lucas;
    private readonly List<HeavySwarmPhaseLog> _auditTrail = new();

    public GrokSwarm(
        GrokSwarmConfig config,
        IAgxiomeAgent captain,
        IAgxiomeAgent harper,
        IAgxiomeAgent benjamin,
        IAgxiomeAgent lucas)
    {
        _config   = config   ?? throw new ArgumentNullException(nameof(config));
        _captain  = captain  ?? throw new ArgumentNullException(nameof(captain));
        _harper   = harper   ?? throw new ArgumentNullException(nameof(harper));
        _benjamin = benjamin ?? throw new ArgumentNullException(nameof(benjamin));
        _lucas    = lucas    ?? throw new ArgumentNullException(nameof(lucas));
    }

    public IReadOnlyList<HeavySwarmPhaseLog> AuditTrail => _auditTrail.AsReadOnly();

    /// <summary>
    /// Run the Grok 3-phase pipeline (optionally repeated up to MaxLoops times).
    /// Loop: Decompose → Parallel specialist work → Captain synthesis.
    /// </summary>
    public async Task<GrokSwarmResult> RunAsync(string task, CancellationToken ct = default)
    {
        var phaseTiming = new Dictionary<string, TimeSpan>();
        string captainSynthesis = string.Empty;
        string harperOutput = string.Empty;
        string benjaminOutput = string.Empty;
        string lucasOutput = string.Empty;

        var totalLoops = _config.MaxLoops < 1 ? 1 : _config.MaxLoops;

        for (int loop = 0; loop < totalLoops; loop++)
        {
            ct.ThrowIfCancellationRequested();
            var loopSuffix = totalLoops > 1 ? $"[Loop {loop + 1}/{totalLoops}]" : string.Empty;

            // ── Phase 1: Captain decomposes task into 3 targeted questions ─────
            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            var decomposition = await DecomposeAsync(task, captainSynthesis, loop, ct);
            sw1.Stop();
            phaseTiming[$"Decompose{loopSuffix}"] = sw1.Elapsed;
            _auditTrail.Add(new HeavySwarmPhaseLog($"Decompose{loopSuffix}", decomposition.Combined, sw1.Elapsed));

            // ── Phase 2: Specialists work in parallel ─────────────────────────
            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            (harperOutput, benjaminOutput, lucasOutput) =
                await RunSpecialistsAsync(decomposition, ct);
            sw2.Stop();
            phaseTiming[$"Specialists{loopSuffix}"] = sw2.Elapsed;
            _auditTrail.Add(new HeavySwarmPhaseLog($"Specialists{loopSuffix}",
                $"Harper:\n{harperOutput}\n\nBenjamin:\n{benjaminOutput}\n\nLucas:\n{lucasOutput}",
                sw2.Elapsed));

            // ── Phase 3: Captain synthesizes with conflict resolution ──────────
            var sw3 = System.Diagnostics.Stopwatch.StartNew();
            captainSynthesis = await SynthesizeAsync(task, harperOutput, benjaminOutput, lucasOutput, ct);
            sw3.Stop();
            phaseTiming[$"Synthesis{loopSuffix}"] = sw3.Elapsed;
            _auditTrail.Add(new HeavySwarmPhaseLog($"Synthesis{loopSuffix}", captainSynthesis, sw3.Elapsed));
        }

        return new GrokSwarmResult(task, harperOutput, benjaminOutput, lucasOutput, captainSynthesis, phaseTiming);
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    private async Task<GrokDecomposition> DecomposeAsync(
        string task, string previousSynthesis, int loop, CancellationToken ct)
    {
        var prompt = loop == 0
            ? $$"""
               You are the Captain Swarm orchestrator. Decompose the following task into
               exactly 3 non-overlapping analytical questions for your team:

               - Research question (for Harper): focus on facts, evidence, data sources.
               - Logic question (for Benjamin): focus on reasoning, math, code, or analysis.
               - Alternatives question (for Lucas): focus on creative options, edge cases, contrarian views.

               Respond with exactly this JSON structure (no markdown):
               {
                 "research": "<question for Harper>",
                 "logic": "<question for Benjamin>",
                 "alternatives": "<question for Lucas>"
               }

               Task: {{task}}
               """
            : $$"""
               You are the Captain Swarm orchestrator (iteration {{loop + 1}}).
               Based on your previous synthesis below, identify the remaining gaps and
               decompose them into 3 refined questions for your team.

               Previous synthesis:
               {{previousSynthesis}}

               Original task: {{task}}

               Respond with exactly this JSON structure (no markdown):
               {
                 "research": "<refined question for Harper>",
                 "logic": "<refined question for Benjamin>",
                 "alternatives": "<refined question for Lucas>"
               }
               """;

        var response = await _captain.RunAsync(new AgentRequest(prompt), ct);
        return GrokDecomposition.Parse(response.Content);
    }

    private async Task<(string harper, string benjamin, string lucas)> RunSpecialistsAsync(
        GrokDecomposition decomposition, CancellationToken ct)
    {
        var tasks = new[]
        {
            _harper.RunAsync(new AgentRequest(decomposition.Research), ct),
            _benjamin.RunAsync(new AgentRequest(decomposition.Logic), ct),
            _lucas.RunAsync(new AgentRequest(decomposition.Alternatives), ct),
        };

        var results = await Task.WhenAll(tasks);
        return (results[0].Content, results[1].Content, results[2].Content);
    }

    private async Task<string> SynthesizeAsync(
        string originalTask,
        string harperOutput, string benjaminOutput, string lucasOutput,
        CancellationToken ct)
    {
        var prompt = $$"""
            You are the Captain Swarm. Synthesize the following specialist inputs into
            a coherent, conflict-resolved final answer for the original task.
            Where specialists disagree, acknowledge the tension and provide your best judgment.

            Original task: {{originalTask}}

            Harper (Research/Facts):
            {{harperOutput}}

            Benjamin (Logic/Analysis):
            {{benjaminOutput}}

            Lucas (Creative/Contrarian):
            {{lucasOutput}}

            Provide a comprehensive synthesis that integrates all perspectives.
            """;

        var response = await _captain.RunAsync(new AgentRequest(prompt), ct);
        return response.Content;
    }

    // ─── Inner record ─────────────────────────────────────────────────────────

    private sealed record GrokDecomposition(string Research, string Logic, string Alternatives)
    {
        public string Combined => $"Research: {Research}\nLogic: {Logic}\nAlternatives: {Alternatives}";

        public static GrokDecomposition Parse(string json)
        {
            // Simple JSON extraction — no System.Text.Json dep needed
            static string Extract(string src, string key)
            {
                var pattern = $"\"{key}\"";
                var idx = src.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) return src; // fallback: return whole content
                var colon = src.IndexOf(':', idx);
                if (colon < 0) return src;
                var start = src.IndexOf('"', colon + 1);
                if (start < 0) return src;
                var end = src.IndexOf('"', start + 1);
                while (end > 0 && src[end - 1] == '\\')
                    end = src.IndexOf('"', end + 1);
                return end > start ? src[(start + 1)..end] : src;
            }

            return new GrokDecomposition(
                Extract(json, "research"),
                Extract(json, "logic"),
                Extract(json, "alternatives")
            );
        }
    }
}

/// <summary>Configuration for a GrokSwarm.</summary>
public record GrokSwarmConfig
{
    /// <summary>Number of Captain → Specialists → Synthesis loops. Default 1.</summary>
    public int MaxLoops { get; init; } = 1;
}
