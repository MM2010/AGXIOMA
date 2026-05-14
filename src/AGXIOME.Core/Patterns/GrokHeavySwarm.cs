using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Patterns;

/// <summary>
/// GrokHeavySwarm — 16-agent domain-specialist analysis pattern.
/// Grok Captain orchestrates 15 domain specialists working in parallel, then synthesizes.
///
/// The 15 specialists and their domains:
///   Harper    — Creative Writing and Storytelling
///   Benjamin  — Data, Finance and Economics
///   Lucas     — Coding, Programming and Technical
///   Olivia    — Literature, Arts and Culture
///   James     — History, Politics and Philosophy
///   Charlotte — Math, Statistics and Logic
///   Henry     — Engineering, Robotics and Innovation
///   Mia       — Biology, Health and Medicine
///   William   — Business Strategy and Entrepreneurship
///   Sebastian — Physics, Astronomy and Hard Sciences
///   Jack      — Psychology and Human Behavior
///   Owen      — Environment, Sustainability and Global Systems
///   Luna      — Space Exploration and Futurism
///   Elizabeth — Ethics, Policy and Critical Thinking
///   Noah      — Long-Term Innovation and Systems Thinking
///
/// Parity with Swarms Python: HeavySwarm(use_grok_heavy=True).
/// </summary>
public sealed class GrokHeavySwarm
{
    private readonly GrokHeavySwarmConfig _config;
    private readonly IAgxiomeAgent _captain;
    private readonly IReadOnlyDictionary<string, IAgxiomeAgent> _specialists;
    private readonly List<HeavySwarmPhaseLog> _auditTrail = new();

    /// <summary>Required specialist names in order (15 agents).</summary>
    public static readonly IReadOnlyList<string> SpecialistNames = new[]
    {
        "Harper", "Benjamin", "Lucas", "Olivia", "James",
        "Charlotte", "Henry", "Mia", "William", "Sebastian",
        "Jack", "Owen", "Luna", "Elizabeth", "Noah"
    };

    public static readonly IReadOnlyDictionary<string, string> SpecialistDescriptions =
        new Dictionary<string, string>
        {
            ["Harper"]    = "Creative Writing and Storytelling specialist",
            ["Benjamin"]  = "Data, Finance and Economics specialist",
            ["Lucas"]     = "Coding, Programming and Technical specialist",
            ["Olivia"]    = "Literature, Arts and Culture specialist",
            ["James"]     = "History, Politics and Philosophy specialist",
            ["Charlotte"] = "Math, Statistics and Logic specialist",
            ["Henry"]     = "Engineering, Robotics and Innovation specialist",
            ["Mia"]       = "Biology, Health and Medicine specialist",
            ["William"]   = "Business Strategy and Entrepreneurship specialist",
            ["Sebastian"] = "Physics, Astronomy and Hard Sciences specialist",
            ["Jack"]      = "Psychology and Human Behavior specialist",
            ["Owen"]      = "Environment, Sustainability and Global Systems specialist",
            ["Luna"]      = "Space Exploration and Futurism specialist",
            ["Elizabeth"] = "Ethics, Policy and Critical Thinking specialist",
            ["Noah"]      = "Long-Term Innovation and Systems Thinking specialist",
        };

    /// <param name="captain">The Grok captain agent (orchestrates and synthesizes).</param>
    /// <param name="specialists">
    ///   Dictionary keyed by specialist name (case-insensitive).
    ///   Must contain all 15 names from <see cref="SpecialistNames"/>.
    /// </param>
    public GrokHeavySwarm(
        GrokHeavySwarmConfig config,
        IAgxiomeAgent captain,
        IReadOnlyDictionary<string, IAgxiomeAgent> specialists)
    {
        _config     = config     ?? throw new ArgumentNullException(nameof(config));
        _captain    = captain    ?? throw new ArgumentNullException(nameof(captain));
        _specialists = specialists ?? throw new ArgumentNullException(nameof(specialists));

        // Validate all 15 specialist slots are present
        var missing = SpecialistNames
            .Where(n => !specialists.Keys.Any(k => string.Equals(k, n, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (missing.Count > 0)
            throw new ArgumentException($"Missing specialists: {string.Join(", ", missing)}", nameof(specialists));
    }

    public IReadOnlyList<HeavySwarmPhaseLog> AuditTrail => _auditTrail.AsReadOnly();

    /// <summary>
    /// Run the Grok Heavy 3-phase pipeline:
    /// 1. Captain generates 15 targeted questions (one per specialist domain).
    /// 2. All 15 specialists work in parallel.
    /// 3. Captain synthesizes with cross-domain convergence.
    /// </summary>
    public async Task<GrokHeavySwarmResult> RunAsync(string task, CancellationToken ct = default)
    {
        var phaseTiming = new Dictionary<string, TimeSpan>();
        var totalLoops  = _config.MaxLoops < 1 ? 1 : _config.MaxLoops;

        Dictionary<string, string> specialistOutputs = new();
        string captainSynthesis = string.Empty;

        for (int loop = 0; loop < totalLoops; loop++)
        {
            ct.ThrowIfCancellationRequested();
            var suffix = totalLoops > 1 ? $"[Loop {loop + 1}/{totalLoops}]" : string.Empty;

            // ── Phase 1: Captain generates 15 domain-specific questions ───────
            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            var questions = await GenerateQuestionsAsync(task, captainSynthesis, loop, ct);
            sw1.Stop();
            phaseTiming[$"QuestionGen{suffix}"] = sw1.Elapsed;
            _auditTrail.Add(new HeavySwarmPhaseLog($"QuestionGen{suffix}",
                string.Join("\n", questions.Select(kvp => $"{kvp.Key}: {kvp.Value}")),
                sw1.Elapsed));

            // ── Phase 2: All 15 specialists in parallel ────────────────────────
            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            specialistOutputs = await RunSpecialistsAsync(questions, ct);
            sw2.Stop();
            phaseTiming[$"Specialists{suffix}"] = sw2.Elapsed;
            _auditTrail.Add(new HeavySwarmPhaseLog($"Specialists{suffix}",
                string.Join("\n\n", specialistOutputs.Select(kvp => $"{kvp.Key}:\n{kvp.Value}")),
                sw2.Elapsed));

            // ── Phase 3: Captain cross-domain synthesis ───────────────────────
            var sw3 = System.Diagnostics.Stopwatch.StartNew();
            captainSynthesis = await SynthesizeAsync(task, specialistOutputs, ct);
            sw3.Stop();
            phaseTiming[$"Synthesis{suffix}"] = sw3.Elapsed;
            _auditTrail.Add(new HeavySwarmPhaseLog($"Synthesis{suffix}", captainSynthesis, sw3.Elapsed));
        }

        return new GrokHeavySwarmResult(task, specialistOutputs, captainSynthesis, phaseTiming);
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    private async Task<Dictionary<string, string>> GenerateQuestionsAsync(
        string task, string previousSynthesis, int loop, CancellationToken ct)
    {
        var specialistList = string.Join("\n", SpecialistNames.Select(
            n => $"- {n} ({SpecialistDescriptions[n]})"));

        var prompt = loop == 0
            ? $$"""
               You are the Grok Captain orchestrating a 15-specialist analysis team.
               Generate one targeted analytical question for EACH specialist below,
               tailored to their domain expertise.

               Specialists:
               {{specialistList}}

               Respond with JSON (no markdown):
               {
                 "Harper": "<question>",
                 "Benjamin": "<question>",
                 "Lucas": "<question>",
                 "Olivia": "<question>",
                 "James": "<question>",
                 "Charlotte": "<question>",
                 "Henry": "<question>",
                 "Mia": "<question>",
                 "William": "<question>",
                 "Sebastian": "<question>",
                 "Jack": "<question>",
                 "Owen": "<question>",
                 "Luna": "<question>",
                 "Elizabeth": "<question>",
                 "Noah": "<question>"
               }

               Task: {{task}}
               """
            : $$"""
               You are the Grok Captain (iteration {{loop + 1}}).
               Based on your previous synthesis, generate refined questions to fill remaining gaps.

               Previous synthesis:
               {{previousSynthesis}}

               Original task: {{task}}

               Respond with the same JSON structure as before (all 15 specialists, no markdown).
               """;

        var response = await _captain.RunAsync(new AgentRequest(prompt), ct);
        return ParseQuestions(response.Content);
    }

    private async Task<Dictionary<string, string>> RunSpecialistsAsync(
        Dictionary<string, string> questions, CancellationToken ct)
    {
        var tasks = questions
            .Where(q => _specialists.Keys.Any(k => string.Equals(k, q.Key, StringComparison.OrdinalIgnoreCase)))
            .Select(q =>
            {
                var agent = _specialists.First(s => string.Equals(s.Key, q.Key, StringComparison.OrdinalIgnoreCase)).Value;
                return (name: q.Key, task: agent.RunAsync(new AgentRequest(q.Value), ct));
            })
            .ToList();

        await Task.WhenAll(tasks.Select(t => t.task));

        return tasks.ToDictionary(t => t.name, t => t.task.Result.Content);
    }

    private async Task<string> SynthesizeAsync(
        string originalTask,
        Dictionary<string, string> specialistOutputs,
        CancellationToken ct)
    {
        var specialistSection = string.Join("\n\n", specialistOutputs.Select(
            kvp => $"{kvp.Key} ({SpecialistDescriptions.GetValueOrDefault(kvp.Key, "Specialist")}):\n{kvp.Value}"));

        var prompt = $$"""
            You are the Grok Captain. Synthesize the following 15 specialist analyses
            into a unified, comprehensive answer with cross-domain convergence.
            Identify key agreements, tensions, and emergent insights.

            Original task: {{originalTask}}

            {{specialistSection}}

            Provide a master synthesis covering: key findings, cross-domain patterns,
            conflicting perspectives, and your final integrated conclusion.
            """;

        var response = await _captain.RunAsync(new AgentRequest(prompt), ct);
        return response.Content;
    }

    private static Dictionary<string, string> ParseQuestions(string json)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in SpecialistNames)
        {
            var key = $"\"{name}\"";
            var idx = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) { result[name] = $"Analyze the task from your {SpecialistDescriptions[name]} perspective."; continue; }
            var colon = json.IndexOf(':', idx);
            if (colon < 0) { result[name] = json; continue; }
            var start = json.IndexOf('"', colon + 1);
            if (start < 0) { result[name] = json; continue; }
            var end = json.IndexOf('"', start + 1);
            while (end > 0 && json[end - 1] == '\\')
                end = json.IndexOf('"', end + 1);
            result[name] = end > start ? json[(start + 1)..end] : json;
        }
        return result;
    }
}

/// <summary>Configuration for a GrokHeavySwarm.</summary>
public record GrokHeavySwarmConfig
{
    /// <summary>Number of Captain → Specialists → Synthesis loops. Default 1.</summary>
    public int MaxLoops { get; init; } = 1;
}
