using AGXIOME.Shared.Models;

namespace AgxiomaSingleAgent;

// ── Typed settings bound from appsettings.json ────────────────────────────────

public sealed class AgentSettings
{
    public string   Id           { get; init; } = "MY_AGENT_NAME";
    public string?  Model        { get; init; }
    public string   SystemPrompt { get; init; } = "You are MY_AGENT_NAME, a helpful AI assistant. Be concise and precise.";
    public int      MaxLoops     { get; init; } = 10;
}

// ── API contracts ──────────────────────────────────────────────────────────────

public record ChatRequest(
    string Prompt,
    string SessionId = "default"
);

public record ChatResponse(
    string Content,
    string SessionId,
    int TurnCount
);

// ── In-memory conversation store (swap for Redis/DB in production) ─────────────

public sealed class ConversationStore
{
    private readonly Dictionary<string, List<AgentMessage>> _sessions = new();
    private readonly Lock _lock = new();

    public IReadOnlyList<AgentMessage> Get(string sessionId)
    {
        lock (_lock)
            return _sessions.TryGetValue(sessionId, out var history)
                ? history.AsReadOnly()
                : Array.Empty<AgentMessage>();
    }

    public void Append(string sessionId, string userPrompt, string agentResponse)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var history))
                _sessions[sessionId] = history = new List<AgentMessage>();

            history.Add(new AgentMessage("user",      userPrompt));
            history.Add(new AgentMessage("assistant", agentResponse));
        }
    }

    public void Clear(string sessionId)
    {
        lock (_lock) _sessions.Remove(sessionId);
    }
}
