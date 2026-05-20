using AGXIOME.Core.Agent;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace AGXIOME.Adapters.AgUi;

/// <summary>
/// AG-UI: ChatGPT-like web interface for AGXIOME agents.
/// Manages sessions, streaming, and conversation history.
/// Designed to be hosted inside any ASP.NET Minimal API or SignalR hub.
/// </summary>
public sealed class AgUiHost
{
    private readonly ConcurrentDictionary<string, AgUiSession> _sessions = new();

    /// <summary>Create a new chat session for a user+agent combo.</summary>
    public AgUiSession CreateSession(string userId, IAgxiomeAgent agent, string? title = null)
    {
        var session = new AgUiSession(Guid.NewGuid().ToString("N")[..12], userId, agent, title ?? $"Chat {DateTime.UtcNow:HH:mm}");
        _sessions[session.Id] = session;
        return session;
    }

    /// <summary>Get existing session.</summary>
    public AgUiSession? GetSession(string sessionId)
        => _sessions.GetValueOrDefault(sessionId);

    /// <summary>List sessions for a user.</summary>
    public IReadOnlyList<AgUiSession> ListSessions(string userId)
        => _sessions.Values.Where(s => s.UserId == userId).OrderByDescending(s => s.UpdatedAt).ToList();

    /// <summary>Delete a session.</summary>
    public void DeleteSession(string sessionId) => _sessions.TryRemove(sessionId, out _);

    /// <summary>Send a message and get response (blocks until complete).</summary>
    public async Task<AgUiMessage> SendMessageAsync(string sessionId, string text, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new KeyNotFoundException($"Session '{sessionId}' not found");

        session.Messages.Add(new AgUiMessage("user", text, DateTime.UtcNow));

        var result = await session.Agent.RunAsync(new AgentRequest(text), ct);
        var response = new AgUiMessage("assistant", result.Content, DateTime.UtcNow);
        session.Messages.Add(response);
        session.Touch();
        return response;
    }

    /// <summary>Send a message and stream response chunks.</summary>
    public async IAsyncEnumerable<string> SendMessageStreamAsync(string sessionId, string text, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new KeyNotFoundException($"Session '{sessionId}' not found");

        session.Messages.Add(new AgUiMessage("user", text, DateTime.UtcNow));
        var fullResponse = new StringBuilder();

        await foreach (var chunk in session.Agent.RunStreamAsync(new AgentRequest(text), ct))
        {
            if (chunk.Content != null) fullResponse.Append(chunk.Content);
            yield return chunk.Content ?? "";
            if (chunk.IsFinal) break;
        }

        session.Messages.Add(new AgUiMessage("assistant", fullResponse.ToString(), DateTime.UtcNow));
        session.Touch();
    }

    /// <summary>Get chat history for a session (as JSON).</summary>
    public string GetHistoryJson(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return "[]";
        return JsonSerializer.Serialize(session.Messages, _json);
    }

    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };
}

/// <summary>A single chat session in AG-UI.</summary>
public sealed class AgUiSession
{
    public string Id { get; }
    public string UserId { get; }
    public IAgxiomeAgent Agent { get; }
    public string Title { get; set; }
    public List<AgUiMessage> Messages { get; } = new();
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    internal void Touch() => UpdatedAt = DateTime.UtcNow;

    public AgUiSession(string id, string userId, IAgxiomeAgent agent, string title)
    {
        Id = id; UserId = userId; Agent = agent; Title = title;
    }
}

/// <summary>A single message in AG-UI.</summary>
public record AgUiMessage(string Role, string Content, DateTime Timestamp);
