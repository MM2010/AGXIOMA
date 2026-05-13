using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Diagnostics;
using System.Text;

namespace AGXIOME.Core.Patterns;

/// <summary>
/// AGXIOMA MajorityVotingSwarm — N agents answer independently; majority wins.
///
/// Differenziatori vs Swarms:
///   - VotingMode: SimpleCount / WeightedScore / LLMJudge
///   - ConfidenceThreshold: se il consenso è basso, escalation opzionale
///   - AgentWeights: voto ponderato per reputazione/specializzazione
///   - DisagreementPolicy: Abstain / ForceBest / RequestRevote
///   - OnVoteComplete callback per audit trail
/// </summary>
public sealed class MajorityVotingSwarm
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];

    private readonly IReadOnlyList<IAgxiomeAgent> _agents;
    private readonly IAgxiomeAgent? _judgeAgent;
    private readonly MajorityVotingConfig _config;

    public MajorityVotingSwarm(
        IReadOnlyList<IAgxiomeAgent> agents,
        MajorityVotingConfig? config = null,
        IAgxiomeAgent? judgeAgent = null)
    {
        if (agents.Count < 2) throw new ArgumentException("MajorityVoting requires at least 2 agents.");
        _agents = agents;
        _judgeAgent = judgeAgent;
        _config = config ?? new MajorityVotingConfig();
    }

    public async Task<VotingResult> RunAsync(string question, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // All agents vote in parallel
        var votes = await Task.WhenAll(_agents.Select(async agent =>
        {
            try
            {
                var resp = await agent.RunAsync(new AgentRequest(question), ct);
                var weight = _config.AgentWeights.GetValueOrDefault(agent.Id, 1.0);
                return new AgentVote(agent.Id, resp.Content, weight, Success: true);
            }
            catch (Exception ex)
            {
                return new AgentVote(agent.Id, $"[ERROR: {ex.Message}]", 0, Success: false);
            }
        }));

        var validVotes = votes.Where(v => v.Success).ToList();
        if (validVotes.Count == 0) return new VotingResult("", votes, 0.0, sw.Elapsed, WinnerAgentId: null);

        string winner;
        double confidence;

        if (_config.VotingMode == VotingMode.LLMJudge && _judgeAgent is not null)
        {
            (winner, confidence) = await LLMJudgeAsync(question, validVotes, ct);
        }
        else if (_config.VotingMode == VotingMode.WeightedScore)
        {
            (winner, confidence) = WeightedVote(validVotes);
        }
        else
        {
            (winner, confidence) = SimpleCountVote(validVotes);
        }

        // Low confidence escalation
        if (confidence < _config.ConfidenceThreshold && _config.DisagreementPolicy == DisagreementPolicy.RequestRevote)
        {
            // Second round with refined question
            var refinedQuestion = $"[Previous round showed low consensus ({confidence:P0}). Re-evaluate carefully]\n{question}";
            var revoteResult = await RunAsync(refinedQuestion, ct);
            return revoteResult with { RevoteRound = true };
        }

        var result = new VotingResult(winner, votes, confidence, sw.Elapsed,
            WinnerAgentId: validVotes.FirstOrDefault(v => v.Content == winner)?.AgentId);
        _config.OnVoteComplete?.Invoke(result);
        return result;
    }

    private static (string winner, double confidence) SimpleCountVote(List<AgentVote> votes)
    {
        var grouped = votes.GroupBy(v => NormaliseAnswer(v.Content))
            .OrderByDescending(g => g.Count()).First();
        var confidence = (double)grouped.Count() / votes.Count;
        return (grouped.First().Content, confidence);
    }

    private static (string winner, double confidence) WeightedVote(List<AgentVote> votes)
    {
        var totalWeight = votes.Sum(v => v.Weight);
        var grouped = votes.GroupBy(v => NormaliseAnswer(v.Content))
            .Select(g => (Answer: g.First().Content, Weight: g.Sum(v => v.Weight)))
            .OrderByDescending(x => x.Weight).First();
        return (grouped.Answer, grouped.Weight / totalWeight);
    }

    private async Task<(string winner, double confidence)> LLMJudgeAsync(
        string question, List<AgentVote> votes, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Question: {question}");
        sb.AppendLine("\nAgent answers:");
        for (int i = 0; i < votes.Count; i++)
            sb.AppendLine($"Agent {votes[i].AgentId}: {votes[i].Content}");
        sb.AppendLine("\nIdentify the BEST answer. Reply with the agent ID and a confidence score 0-1.");
        sb.AppendLine("Format: WINNER:<agentId> CONFIDENCE:<0.0-1.0>\nFINAL_ANSWER:<the selected answer>");

        var resp = await _judgeAgent!.RunAsync(new AgentRequest(sb.ToString()), ct);
        var content = resp.Content;

        // Parse judge response
        var confMatch = System.Text.RegularExpressions.Regex.Match(content, @"CONFIDENCE:([0-9.]+)");
        var confidence = confMatch.Success && double.TryParse(confMatch.Groups[1].Value, out var c) ? c : 0.6;

        var answerMatch = System.Text.RegularExpressions.Regex.Match(content, @"FINAL_ANSWER:(.+)", System.Text.RegularExpressions.RegexOptions.Singleline);
        var winner = answerMatch.Success ? answerMatch.Groups[1].Value.Trim() : votes[0].Content;
        return (winner, confidence);
    }

    private static string NormaliseAnswer(string content)
        => content.Trim().ToLowerInvariant()[..Math.Min(200, content.Length)];
}

public enum VotingMode { SimpleCount, WeightedScore, LLMJudge }
public enum DisagreementPolicy { Abstain, ForceBest, RequestRevote }

public sealed record MajorityVotingConfig
{
    public VotingMode VotingMode { get; init; } = VotingMode.SimpleCount;
    public DisagreementPolicy DisagreementPolicy { get; init; } = DisagreementPolicy.ForceBest;
    public double ConfidenceThreshold { get; init; } = 0.5;
    public Dictionary<string, double> AgentWeights { get; init; } = new();
    public Action<VotingResult>? OnVoteComplete { get; init; }
}

public record AgentVote(string AgentId, string Content, double Weight, bool Success);
public record VotingResult(
    string WinnerContent,
    IReadOnlyList<AgentVote> AllVotes,
    double Confidence,
    TimeSpan Duration,
    string? WinnerAgentId,
    bool RevoteRound = false
);
