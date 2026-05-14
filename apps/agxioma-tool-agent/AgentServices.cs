using AGXIOME.Shared.Models;

namespace AgxiomaToolAgent;

// ── Configuration record ──────────────────────────────────────────────────────

public record AgentSettings
{
    public string Id           { get; init; } = "tool-agent";
    public string? Model       { get; init; }
    public string SystemPrompt { get; init; } =
        """
        You are an intelligent research assistant. You MUST use the tools provided to answer.

        Available tools:
        1. web_search(query) — search the web using DuckDuckGo. Use this for ANY question about current events, facts, news, or information you don't know.
        2. read_file(path) — read local files.

        CRITICAL RULE: You MUST call the appropriate tool for every question. Never answer from your own knowledge alone.
        When the user asks you to search or read, ALWAYS call the tool first.
        """;
    public int MaxLoops { get; init; } = 3;
}

// ── Request / Response models ─────────────────────────────────────────────────

public record ChatRequest(string Prompt, string SessionId = "default");

public record ChatResponse(
    string Answer,
    string SessionId,
    List<ToolCallSummary> ToolsUsed
);

public record ToolCallSummary(string Tool, bool Success);

// ── In-memory conversation store ──────────────────────────────────────────────

public sealed class ConversationStore
{
    private readonly Dictionary<string, List<AgentMessage>> _sessions = new();
    private readonly Lock _lock = new();

    public IReadOnlyList<AgentMessage> Get(string sessionId)
    {
        lock (_lock)
        {
            return _sessions.TryGetValue(sessionId, out var h)
                ? h.AsReadOnly()
                : Array.Empty<AgentMessage>();
        }
    }

    public void Append(string sessionId, string userMsg, string assistantMsg)
    {
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var h))
            {
                h = new List<AgentMessage>();
                _sessions[sessionId] = h;
            }
            h.Add(new AgentMessage("user", userMsg));
            h.Add(new AgentMessage("assistant", assistantMsg));
            // Keep last 20 exchanges (40 messages) to avoid context bloat
            if (h.Count > 40) h.RemoveRange(0, h.Count - 40);
        }
    }

    public void Delete(string sessionId)
    {
        lock (_lock) { _sessions.Remove(sessionId); }
    }
}
