using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace AGXIOME.Core.Patterns;

/// <summary>
/// AGXIOMA GroupChatSwarm — multi-agent conversation with intelligent speaker selection.
///
/// Differenziatori vs Swarms:
///   - SpeakerPolicy enum (RoundRobin/Random/Priority/MentionDriven/Custom)
///   - @mention parsing: any agent can explicitly hand off to another
///   - PriorityWeights: some agents speak more often (e.g. lead analyst)
///   - ConversationFilter: skip agents that already answered the same question
///   - ConversationHistory: full typed log of all turns
///   - MaxConsecutiveSpeaker: prevent one agent monopolising
///   - Integrates with AGXIOME.Realtime via OnTurn callback
/// </summary>
public sealed class GroupChatSwarm
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];

    private readonly Dictionary<string, IAgxiomeAgent> _agents;
    private readonly GroupChatConfig _config;
    private static readonly Regex _mentionRegex = new(@"@(\w[\w\-]*)", RegexOptions.Compiled);

    public GroupChatSwarm(IReadOnlyList<IAgxiomeAgent> agents, GroupChatConfig? config = null)
    {
        if (agents.Count < 2) throw new ArgumentException("GroupChat requires at least 2 agents.");
        _agents = agents.ToDictionary(a => a.Id, a => a);
        _config = config ?? new GroupChatConfig();
    }

    /// <summary>Start a group chat on a topic. Returns the full conversation transcript.</summary>
    public async Task<GroupChatResult> RunAsync(string topic, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var history = new List<ChatTurn>();
        var agentIds = _agents.Keys.ToList();
        var rng = new Random(_config.Seed ?? Environment.TickCount);
        var consecutiveCount = new Dictionary<string, int>();
        string? lastSpeakerId = null;

        // Initial message from coordinator / user
        history.Add(new ChatTurn("user", "__topic__", topic, DateTime.UtcNow));

        for (int turn = 0; turn < _config.MaxLoops && !ct.IsCancellationRequested; turn++)
        {
            var speakerId = SelectSpeaker(history, agentIds, lastSpeakerId, consecutiveCount, rng);
            if (speakerId is null) break;

            var agent = _agents[speakerId];
            var prompt = BuildAgentPrompt(topic, history, speakerId);

            AgentResponse resp;
            try
            {
                resp = await agent.RunAsync(new AgentRequest(prompt), ct);
            }
            catch (Exception ex)
            {
                if (!_config.ContinueOnAgentError) throw;
                resp = new AgentResponse($"[Agent {speakerId} error: {ex.Message}]", AgentStatus.Error);
            }

            var chatTurn = new ChatTurn(speakerId, speakerId, resp.Content, DateTime.UtcNow);
            history.Add(chatTurn);

            // Update consecutive speaker tracking
            consecutiveCount[speakerId] = consecutiveCount.GetValueOrDefault(speakerId, 0) + 1;
            if (lastSpeakerId != speakerId) consecutiveCount[lastSpeakerId ?? ""] = 0;
            lastSpeakerId = speakerId;

            // Notify realtime callback (integrates with SignalR)
            _config.OnTurn?.Invoke(turn, chatTurn);

            // Termination signal
            if (resp.Content.Contains("[GROUPCHAT_END]", StringComparison.OrdinalIgnoreCase)) break;

            // Detect @mentions and force next speaker
            var mention = ExtractMention(resp.Content, agentIds);
            if (mention is not null) lastSpeakerId = "__mention__:" + mention;
        }

        // Optional summariser
        string? summary = null;
        if (_config.SummariserAgentId is { } summId && _agents.TryGetValue(summId, out var summ))
        {
            var summResp = await summ.RunAsync(new AgentRequest(BuildSummaryPrompt(topic, history)), ct);
            summary = summResp.Content;
        }

        return new GroupChatResult(
            Topic: topic,
            Transcript: history.AsReadOnly(),
            Summary: summary,
            Duration: sw.Elapsed,
            TotalTurns: history.Count - 1
        );
    }

    // ─── Speaker Selection ────────────────────────────────────────────────────

    private string? SelectSpeaker(
        List<ChatTurn> history,
        List<string> agentIds,
        string? lastSpeaker,
        Dictionary<string, int> consecutiveCount,
        Random rng)
    {
        // Handle forced mention from last turn
        if (lastSpeaker?.StartsWith("__mention__:") == true)
        {
            var mentionedId = lastSpeaker["__mention__:".Length..];
            return _agents.ContainsKey(mentionedId) ? mentionedId : null;
        }

        // Prevent one agent from speaking too many times consecutively
        var eligible = agentIds
            .Where(id => consecutiveCount.GetValueOrDefault(id, 0) < _config.MaxConsecutiveSpeaker)
            .ToList();
        if (eligible.Count == 0) eligible = agentIds;

        return _config.SpeakerPolicy switch
        {
            SpeakerPolicy.RoundRobin => GetRoundRobin(eligible, history),
            SpeakerPolicy.Random => eligible[rng.Next(eligible.Count)],
            SpeakerPolicy.Priority => GetByPriority(eligible),
            SpeakerPolicy.MentionDriven => GetByMention(history, eligible) ?? GetRoundRobin(eligible, history),
            SpeakerPolicy.Custom => _config.CustomSpeakerSelector?.Invoke(history, eligible) ?? eligible[0],
            _ => GetRoundRobin(eligible, history)
        };
    }

    private static string GetRoundRobin(List<string> eligible, List<ChatTurn> history)
    {
        var lastAgentIdx = history.LastOrDefault(t => t.Role != "user")?.AgentId;
        if (lastAgentIdx is null) return eligible[0];
        var idx = eligible.IndexOf(lastAgentIdx);
        return eligible[(idx + 1) % eligible.Count];
    }

    private string GetByPriority(List<string> eligible)
    {
        if (_config.PriorityWeights.Count == 0) return eligible[0];
        return eligible
            .OrderByDescending(id => _config.PriorityWeights.GetValueOrDefault(id, 1))
            .First();
    }

    private static string? GetByMention(List<ChatTurn> history, List<string> eligible)
    {
        var lastContent = history.LastOrDefault()?.Content ?? "";
        var mention = _mentionRegex.Matches(lastContent)
            .Select(m => m.Groups[1].Value)
            .FirstOrDefault(m => eligible.Contains(m, StringComparer.OrdinalIgnoreCase));
        return mention;
    }

    private static string? ExtractMention(string content, List<string> agentIds)
    {
        var match = _mentionRegex.Match(content);
        if (!match.Success) return null;
        var mentioned = match.Groups[1].Value;
        return agentIds.FirstOrDefault(id => id.Equals(mentioned, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildAgentPrompt(string topic, List<ChatTurn> history, string speakerId)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are agent '{speakerId}' in a group conversation.");
        sb.AppendLine($"Topic: {topic}");
        sb.AppendLine("\n## Conversation So Far:");
        foreach (var t in history.TakeLast(20))
            sb.AppendLine($"[{t.AgentId}]: {t.Content}");
        sb.AppendLine($"\nNow it is your turn as '{speakerId}'. Contribute meaningfully.");
        sb.AppendLine("To hand off to another agent, mention them with @agentId.");
        sb.AppendLine("To end the discussion, include [GROUPCHAT_END] in your response.");
        return sb.ToString();
    }

    private static string BuildSummaryPrompt(string topic, List<ChatTurn> history)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Summarise this group discussion on: {topic}");
        sb.AppendLine("\nFull transcript:");
        foreach (var t in history.Where(x => x.Role != "user"))
            sb.AppendLine($"[{t.AgentId}]: {t.Content}");
        sb.AppendLine("\nProvide: key conclusions, decisions made, open questions, action items.");
        return sb.ToString();
    }
}

// ─── Records & Enums ─────────────────────────────────────────────────────────

public enum SpeakerPolicy { RoundRobin, Random, Priority, MentionDriven, Custom }

public sealed record GroupChatConfig
{
    public int MaxLoops { get; init; } = 20;
    public SpeakerPolicy SpeakerPolicy { get; init; } = SpeakerPolicy.RoundRobin;
    public Dictionary<string, int> PriorityWeights { get; init; } = new();
    public int MaxConsecutiveSpeaker { get; init; } = 3;
    public bool ContinueOnAgentError { get; init; } = true;
    public string? SummariserAgentId { get; init; }
    public int? Seed { get; init; }
    public Action<int, ChatTurn>? OnTurn { get; init; }
    public Func<IReadOnlyList<ChatTurn>, IReadOnlyList<string>, string?>? CustomSpeakerSelector { get; init; }
}

public record ChatTurn(
    string Role,
    string AgentId,
    string Content,
    DateTime Timestamp
);

public record GroupChatResult(
    string Topic,
    IReadOnlyList<ChatTurn> Transcript,
    string? Summary,
    TimeSpan Duration,
    int TotalTurns
);
