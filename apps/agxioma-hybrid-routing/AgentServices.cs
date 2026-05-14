namespace AgxiomaHybridRouting.AgentServices;

// ─── API models ───────────────────────────────────────────────────────────────

public record SupportRequest(
    string Message,
    string SessionId = "default");

public record SupportResponse(
    string Message,
    string SessionId,
    string RoutedTo,
    string TeamCategory,
    List<string> MatchedKeywords,
    string Answer,
    string Duration,
    object? RoutingDiagnostics = null);

// ─── Conversation history ─────────────────────────────────────────────────────

public record ConversationMessage(string Role, string Content);

// ─── Session store ────────────────────────────────────────────────────────────

public sealed class SupportSessionStore
{
    private readonly Dictionary<string, List<ConversationMessage>> _sessions = new();
    private readonly object _lock = new();

    public List<ConversationMessage> Get(string sessionId)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var history))
            {
                history = new List<ConversationMessage>();
                _sessions[sessionId] = history;
            }
            return history;
        }
    }

    public void Append(string sessionId, string role, string content)
    {
        lock (_lock)
        {
            var history = Get(sessionId);
            history.Add(new ConversationMessage(role, content));
            // Keep last 20 turns to avoid unbounded growth
            if (history.Count > 20)
                history.RemoveAt(0);
        }
    }
}

// ─── App configuration ────────────────────────────────────────────────────────

public sealed class AgentSettings
{
    public string Id       { get; set; } = "hybrid-support-team";
    public string Provider { get; set; } = "deepseek";
    public string Model    { get; set; } = "deepseek-v4-flash";
}
