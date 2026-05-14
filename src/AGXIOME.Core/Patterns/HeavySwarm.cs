using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Patterns;

/// <summary>
/// HeavySwarm — 5-phase Grok-style deep analysis pattern.
/// Phases: QuestionGen → Research → Analysis → Alternatives → Synthesis.
/// Each phase can use different models and tools.
/// Parity with Swarms: HeavySwarm(question, workers, synthesizer).
/// </summary>
public sealed class HeavySwarm
{
    private readonly HeavySwarmConfig _config;
    private readonly IAgxiomeAgent _questionAgent;
    private readonly IReadOnlyList<IAgxiomeAgent> _workers;
    private readonly IAgxiomeAgent _synthesizer;
    private readonly List<HeavySwarmPhaseLog> _auditTrail = new();

    public HeavySwarm(HeavySwarmConfig config, IAgxiomeAgent questionAgent, IReadOnlyList<IAgxiomeAgent> workers, IAgxiomeAgent synthesizer)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _questionAgent = questionAgent ?? throw new ArgumentNullException(nameof(questionAgent));
        _workers = workers ?? throw new ArgumentNullException(nameof(workers));
        _synthesizer = synthesizer ?? throw new ArgumentNullException(nameof(synthesizer));
    }

    public IReadOnlyList<HeavySwarmPhaseLog> AuditTrail => _auditTrail.AsReadOnly();

    /// <summary>Run the full 5-phase analysis pipeline.</summary>
    public async Task<HeavySwarmResult> RunAsync(string input, CancellationToken ct = default)
    {
        var phaseTiming = new Dictionary<string, TimeSpan>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Phase 1: Question Generation
        var phase1Sw = System.Diagnostics.Stopwatch.StartNew();
        var question = await PhaseQuestionGenAsync(input, ct);
        phase1Sw.Stop();
        phaseTiming["QuestionGen"] = phase1Sw.Elapsed;
        _auditTrail.Add(new HeavySwarmPhaseLog("QuestionGen", question, phase1Sw.Elapsed));

        // Phase 2: Research (parallel workers)
        var phase2Sw = System.Diagnostics.Stopwatch.StartNew();
        var researchFindings = await PhaseResearchAsync(question, ct);
        phase2Sw.Stop();
        phaseTiming["Research"] = phase2Sw.Elapsed;
        _auditTrail.Add(new HeavySwarmPhaseLog("Research", string.Join("\n", researchFindings), phase2Sw.Elapsed));

        // Phase 3: Analysis
        var phase3Sw = System.Diagnostics.Stopwatch.StartNew();
        var analysis = await PhaseAnalysisAsync(question, researchFindings, ct);
        phase3Sw.Stop();
        phaseTiming["Analysis"] = phase3Sw.Elapsed;
        _auditTrail.Add(new HeavySwarmPhaseLog("Analysis", analysis, phase3Sw.Elapsed));

        // Phase 4: Alternatives
        var phase4Sw = System.Diagnostics.Stopwatch.StartNew();
        var alternatives = await PhaseAlternativesAsync(question, analysis, ct);
        phase4Sw.Stop();
        phaseTiming["Alternatives"] = phase4Sw.Elapsed;
        _auditTrail.Add(new HeavySwarmPhaseLog("Alternatives", string.Join("\n", alternatives), phase4Sw.Elapsed));

        // Phase 5: Synthesis
        var phase5Sw = System.Diagnostics.Stopwatch.StartNew();
        var synthesis = await PhaseSynthesisAsync(question, analysis, alternatives, ct);
        phase5Sw.Stop();
        phaseTiming["Synthesis"] = phase5Sw.Elapsed;
        _auditTrail.Add(new HeavySwarmPhaseLog("Synthesis", synthesis, phase5Sw.Elapsed));

        sw.Stop();
        return new HeavySwarmResult(question, researchFindings, analysis, alternatives, synthesis, phaseTiming);
    }

    private async Task<string> PhaseQuestionGenAsync(string input, CancellationToken ct)
    {
        var prompt = $@"Analyze this input and generate the most important research question to answer.

Input: {input}

Generate a single, focused research question that would provide the most valuable insight.";
        var resp = await _questionAgent.RunAsync(new AgentRequest(prompt), ct);
        return resp.Content;
    }

    private async Task<List<string>> PhaseResearchAsync(string question, CancellationToken ct)
    {
        var findings = new List<string>();
        var researchTasks = _workers.Select((worker, i) =>
        {
            var prompt = $@"Research question: {question}

As researcher {i + 1}, investigate this question thoroughly. Provide factual findings, data points, and insights.
Be comprehensive but concise.";
            return worker.RunAsync(new AgentRequest(prompt), ct);
        });

        var responses = await Task.WhenAll(researchTasks);
        foreach (var resp in responses)
            findings.Add(resp.Content);

        return findings;
    }

    private async Task<string> PhaseAnalysisAsync(string question, List<string> findings, CancellationToken ct)
    {
        var combinedFindings = string.Join("\n\n---\n\n", findings.Select((f, i) => $"Finding {i + 1}:\n{f}"));
        var prompt = $@"Question: {question}

Research Findings:
{combinedFindings}

Analyze these findings. Identify patterns, contradictions, key insights, and implications.
Provide a structured analysis.";
        var resp = await _synthesizer.RunAsync(new AgentRequest(prompt), ct);
        return resp.Content;
    }

    private async Task<List<string>> PhaseAlternativesAsync(string question, string analysis, CancellationToken ct)
    {
        var prompt = $@"Question: {question}

Analysis: {analysis}

Based on this analysis, generate 3-5 alternative approaches, solutions, or perspectives.
For each alternative, explain its rationale and trade-offs.";
        var resp = await _synthesizer.RunAsync(new AgentRequest(prompt), ct);
        var alternatives = resp.Content.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        return alternatives.Count > 0 ? alternatives : new List<string> { resp.Content };
    }

    private async Task<string> PhaseSynthesisAsync(string question, string analysis, List<string> alternatives, CancellationToken ct)
    {
        var combinedAlt = string.Join("\n\n", alternatives.Select((a, i) => $"Alternative {i + 1}: {a}"));
        var prompt = $@"Question: {question}

Analysis: {analysis}

Alternatives:
{combinedAlt}

Synthesize all the above into a comprehensive final answer. Include:
1. Direct answer to the question
2. Key findings and evidence
3. Recommended approach with rationale
4. Caveats and limitations";
        var resp = await _synthesizer.RunAsync(new AgentRequest(prompt), ct);
        return resp.Content;
    }
}

/// <summary>Configuration for HeavySwarm.</summary>
public record HeavySwarmConfig
{
    public string QuestionModel { get; init; } = "gpt-4o";
    public string WorkerModel { get; init; } = "gpt-4o-mini";
    public string SynthesisModel { get; init; } = "gpt-4o";
    public List<string> WorkerTools { get; init; } = new();
    public bool EnableParallelResearch { get; init; } = true;
}

/// <summary>Audit trail entry for a single HeavySwarm phase.</summary>
public record HeavySwarmPhaseLog(string Phase, string Output, TimeSpan Duration);
