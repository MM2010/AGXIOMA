using AGXIOME.Core.Patterns;
using AGXIOME.Realtime.Hubs;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace AGXIOME.Realtime;

/// <summary>
/// RealtimeGroupChatSwarm — GroupChat con token streaming via SignalR WebSocket.
///
/// DIFFERENZIATORE ASSOLUTO: Swarms è single-process Python, zero WebSocket nativo.
/// AGXIOMA streamma ogni token di ogni agente in real-time al browser/client.
///
/// Architettura:
///   - Ogni turno agente → streamma chunk via IHubContext&lt;AgentHub, IAgentClient&gt;
///   - Client SignalR riceve: OnChunk (token), OnComplete (turno finito), OnStatusChange
///   - GroupId = "chat:{Id}" — i client fanno JoinGroup per ricevere lo stream
///   - OnTurn callback server-side opzionale per logging/audit
///   - SpeakerPolicy, @mention, MaxLoops: identici a GroupChatSwarm
///
/// Usage (server-side, ASP.NET Core):
///   var swarm = new RealtimeGroupChatSwarm(agents, hubContext, config);
///   var result = await swarm.RunAsync("Analizza il mercato tech 2026", ct);
///
/// Client (JavaScript/TypeScript):
///   connection.on("OnChunk",    chunk => appendToken(chunk.text));
///   connection.on("OnComplete", resp  => endTurn(resp));
///   await connection.invoke("JoinAgentSession", chatId);
/// </summary>
public sealed class RealtimeGroupChatSwarm
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];

    private readonly Dictionary<string, IAgxiomeAgent> _agents;
    private readonly RealtimeGroupChatConfig _config;
    private readonly IHubContext<AgentHub, IAgentClient>? _hub;
    private static readonly Regex _mentionRegex = new(@"@(\w[\w\-]*)", RegexOptions.Compiled);

    public RealtimeGroupChatSwarm(
        IReadOnlyList<IAgxiomeAgent> agents,
        IHubContext<AgentHub, IAgentClient>? hub = null,
        RealtimeGroupChatConfig? config = null)
    {
        if (agents.Count < 2) throw new ArgumentException("RealtimeGroupChat requires at least 2 agents.");
        _agents = agents.ToDictionary(a => a.Id, a => a);
        _hub = hub;
        _config = config ?? new RealtimeGroupChatConfig();
    }

    // ─── Main execution ──────────────────────────────────────────────────────

    /// <summary>Run the group chat. Each turn streams tokens via SignalR.</summary>
    public async Task<GroupChatResult> RunAsync(string topic, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var history = new List<ChatTurn>();
        var agentIds = _agents.Keys.ToList();
        var rng = new Random(_config.Seed ?? Environment.TickCount);
        var consecutiveCount = new Dictionary<string, int>();
        string? lastSpeakerId = null;

        var groupName = $"chat:{Id}";

        // Announce start
        if (_hub is not null)
            await _hub.Clients.Group(groupName).OnStatusChange(Id, AgentStatus.Streaming);

        history.Add(new ChatTurn("user", "__topic__", topic, DateTime.UtcNow));

        for (int turn = 0; turn < _config.MaxLoops && !ct.IsCancellationRequested; turn++)
        {
            var speakerId = SelectSpeaker(history, agentIds, lastSpeakerId, consecutiveCount, rng);
            if (speakerId is null) break;

            var agent = _agents[speakerId];
            var prompt = BuildAgentPrompt(topic, history, speakerId);
            var fullOutput = new StringBuilder();

            // Announce turn start
            if (_hub is not null)
                await _hub.Clients.Group(groupName).OnStatusChange(speakerId, AgentStatus.Streaming);

            try
            {
                if (_config.UseStreaming)
                {
                    // Stream token by token via SignalR
                    await foreach (var streamChunk in agent.RunStreamAsync(new AgentRequest(prompt), ct))
                    {
                        fullOutput.Append(streamChunk.Content);
                        if (_hub is not null)
                            await _hub.Clients.Group(groupName).OnChunk(
                                new AgentChunk(streamChunk.Content, IsFinal: streamChunk.IsFinal));
                    }
                }
                else
                {
                    // Non-streaming: run and broadcast full output
                    var agentResp = await agent.RunAsync(new AgentRequest(prompt), ct);
                    fullOutput.Append(agentResp.Content);
                    if (_hub is not null)
                        await _hub.Clients.Group(groupName).OnChunk(
                            new AgentChunk(agentResp.Content, IsFinal: true));
                }

                var output = fullOutput.ToString();
                var chatTurn = new ChatTurn(speakerId, speakerId, output, DateTime.UtcNow);
                history.Add(chatTurn);

                // Broadcast complete turn
                if (_hub is not null)
                    await _hub.Clients.Group(groupName).OnComplete(
                        new AgentResponse(output, AgentStatus.Completed));

                _config.OnTurn?.Invoke(chatTurn);

                // Update consecutive speaker tracking
                consecutiveCount[speakerId] = (consecutiveCount.GetValueOrDefault(speakerId)) + 1;
                if (lastSpeakerId != null && lastSpeakerId != speakerId)
                    consecutiveCount[lastSpeakerId] = 0;
                lastSpeakerId = speakerId;

                // Check termination
                if (_config.StopCondition?.Invoke(history) == true) break;
            }
            catch (Exception ex)
            {
                if (_hub is not null)
                    await _hub.Clients.Group(groupName).OnError(Id, $"Agent {speakerId} failed: {ex.Message}");
                break;
            }
        }

        // Summariser pass
        string? summary = null;
        if (_config.SummariserAgentId is not null && _agents.TryGetValue(_config.SummariserAgentId, out var summariser))
        {
            var summaryPrompt = $"Summarize this conversation:\n{string.Join("\n", history.Skip(1).Select(t => $"{t.AgentId}: {t.Content}"))}";
            var sumResp = await summariser.RunAsync(new AgentRequest(summaryPrompt), ct);
            summary = sumResp.Content;
            if (_hub is not null)
                await _hub.Clients.Group(groupName).OnComplete(
                    new AgentResponse(summary, AgentStatus.Completed));
        }

        // Announce end
        if (_hub is not null)
            await _hub.Clients.Group(groupName).OnStatusChange(Id, AgentStatus.Completed);

        sw.Stop();
        return new GroupChatResult(topic, history, summary, sw.Elapsed, history.Count - 1);
    }

    // ─── Speaker selection ───────────────────────────────────────────────────

    private string? SelectSpeaker(
        List<ChatTurn> history,
        List<string> agentIds,
        string? lastSpeakerId,
        Dictionary<string, int> consecutive,
        Random rng)
    {
        // Check @mention in last message
        if (history.Count > 0)
        {
            var lastContent = history[^1].Content;
            var mentions = _mentionRegex.Matches(lastContent)
                .Select(m => m.Groups[1].Value)
                .FirstOrDefault(m => _agents.ContainsKey(m));
            if (mentions is not null) return mentions;
        }

        // Filter out agents over consecutive limit
        var eligible = agentIds.Where(id =>
            id != lastSpeakerId || // always eligible if wasn't last
            (consecutive.GetValueOrDefault(id) < _config.MaxConsecutiveSpeaker)).ToList();

        if (eligible.Count == 0) eligible = agentIds;

        return _config.SpeakerPolicy switch
        {
            SpeakerPolicy.RoundRobin => eligible[(history.Count - 1) % eligible.Count],
            SpeakerPolicy.Random => eligible[rng.Next(eligible.Count)],
            SpeakerPolicy.Priority when _config.PriorityWeights.Count > 0 => SelectByWeight(eligible, rng),
            SpeakerPolicy.Custom when _config.CustomSpeakerSelector is not null =>
                _config.CustomSpeakerSelector(history, eligible),
            _ => eligible[(history.Count - 1) % eligible.Count]
        };
    }

    private string SelectByWeight(List<string> eligible, Random rng)
    {
        var totalWeight = eligible.Sum(id => _config.PriorityWeights.GetValueOrDefault(id, 1.0));
        var roll = rng.NextDouble() * totalWeight;
        double cumulative = 0;
        foreach (var id in eligible)
        {
            cumulative += _config.PriorityWeights.GetValueOrDefault(id, 1.0);
            if (roll <= cumulative) return id;
        }
        return eligible[^1];
    }

    private static string BuildAgentPrompt(string topic, List<ChatTurn> history, string currentSpeakerId)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are participating in a group discussion about: {topic}");
        sb.AppendLine();
        sb.AppendLine("Recent conversation:");
        foreach (var turn in history.TakeLast(10).Where(t => t.Role != "user" || t.AgentId == "__topic__"))
            sb.AppendLine($"{turn.AgentId}: {turn.Content}");
        sb.AppendLine();
        sb.AppendLine($"Now it's your turn ({currentSpeakerId}). Contribute meaningfully. " +
                      "You can mention another agent with @agentId to pass to them next.");
        return sb.ToString();
    }
}

// ─── Config ─────────────────

public sealed record RealtimeGroupChatConfig
{
    public int MaxLoops { get; init; } = 10;
    public bool UseStreaming { get; init; } = true;
    public SpeakerPolicy SpeakerPolicy { get; init; } = SpeakerPolicy.RoundRobin;
    public int MaxConsecutiveSpeaker { get; init; } = 2;
    public Dictionary<string, double> PriorityWeights { get; init; } = new();
    public string? SummariserAgentId { get; init; }
    public Func<List<ChatTurn>, bool>? StopCondition { get; init; }
    public Func<List<ChatTurn>, List<string>, string>? CustomSpeakerSelector { get; init; }
    public Action<ChatTurn>? OnTurn { get; init; }
    public int? Seed { get; init; }
}

// Note: AgentChunk and AgentResponse types are defined in AGXIOME.Realtime.Hubs (AgentHub.cs)
