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

// ─── Conversation models ──────────────────────────────────────────────────────

public record ConversationMessage(string Role, string Content);

/// <summary>Serializable session state stored via ISessionStore&lt;ConversationSession&gt;.</summary>
public record ConversationSession
{
    public string SessionId { get; init; } = string.Empty;
    public List<ConversationMessage> Messages { get; init; } = new();
    public DateTime LastActive { get; init; } = DateTime.UtcNow;
}

// ─── App configuration ────────────────────────────────────────────────────────

public sealed class AgentSettings
{
    public string Id       { get; set; } = "hybrid-support-team";
    public string Provider { get; set; } = "deepseek";
    public string Model    { get; set; } = "deepseek-v4-flash";
}
