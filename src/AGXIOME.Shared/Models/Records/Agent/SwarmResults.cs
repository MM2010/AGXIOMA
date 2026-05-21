namespace AGXIOME.Shared.Models;

/// <summary>Result of a HeavySwarm multi-phase research pipeline.</summary>
public record HeavySwarmResult(
    string Question,
    List<string> ResearchFindings,
    string Analysis,
    List<string> Alternatives,
    string Synthesis,
    Dictionary<string, TimeSpan> PhaseTiming
);

/// <summary>Result of a 4-agent Grok-mode swarm (Captain + Harper/Benjamin/Lucas).</summary>
public record GrokSwarmResult(
    string Task,
    string HarperOutput,
    string BenjaminOutput,
    string LucasOutput,
    string CaptainSynthesis,
    Dictionary<string, TimeSpan> PhaseTiming
);

/// <summary>Result of a 16-agent Grok-Heavy-mode swarm (Captain + 15 domain specialists).</summary>
public record GrokHeavySwarmResult(
    string Task,
    IReadOnlyDictionary<string, string> SpecialistOutputs,
    string CaptainSynthesis,
    Dictionary<string, TimeSpan> PhaseTiming
);

/// <summary>Result of a ForestSwarm routing and execution.</summary>
public record ForestSwarmResult(
    string Task,
    string SelectedTree,
    string SelectedAgent,
    string Output,
    IReadOnlyList<string> MatchedKeywords,
    TimeSpan Duration
);
