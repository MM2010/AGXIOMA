namespace AgxiomaMultiAgentTeam;

// ── Configuration ─────────────────────────────────────────────────────────────

public record AgentSettings
{
    public string Id        { get; init; } = "MY_AGENT_NAME";
    public string? Provider { get; init; }
    public string? Model    { get; init; }
}

// ── Request / Response ────────────────────────────────────────────────────────

public record SupportRequest(
    string Message,
    string SessionId = "default"
);

public record SupportResponse(
    string Message,
    string SessionId,
    string RoutedTo,
    string TeamCategory,
    List<string> MatchedKeywords,
    string Answer,
    string Duration
);

// ── Session store ─────────────────────────────────────────────────────────────

public record ConversationMessage(string Role, string Content);

public sealed class SupportSessionStore
{
    private readonly Dictionary<string, List<ConversationMessage>> _sessions = new();
    private readonly Lock _lock = new();

    public IReadOnlyList<ConversationMessage> Get(string sessionId)
    {
        lock (_lock)
            return _sessions.TryGetValue(sessionId, out var h) ? h.AsReadOnly() : [];
    }

    public void Append(string sessionId, string userMsg, string assistantMsg)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var h))
            {
                h = new List<ConversationMessage>();
                _sessions[sessionId] = h;
            }
            h.Add(new ConversationMessage("user",      userMsg));
            h.Add(new ConversationMessage("assistant", assistantMsg));
            if (h.Count > 30) h.RemoveRange(0, h.Count - 30);
        }
    }

    public void Delete(string sessionId)
    {
        lock (_lock) { _sessions.Remove(sessionId); }
    }
}
