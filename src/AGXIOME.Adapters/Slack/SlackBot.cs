using AGXIOME.Core.Agent;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Text.Json;

namespace AGXIOME.Adapters.Slack;

/// <summary>
/// Slack bot interface for AGXIOME agents.
/// Handles Slack Events API (slash commands, messages, app_mention).
/// Designed to be mounted as an ASP.NET Minimal API endpoint.
/// </summary>
public sealed class SlackBot
{
    private readonly string _botToken;
    private readonly string _signingSecret;
    private readonly IAgxiomeAgent _agent;
    private readonly HttpClient _http;

    public string? BotUserId { get; private set; }

    public SlackBot(string botToken, string signingSecret, IAgxiomeAgent agent)
    {
        _botToken = botToken; _signingSecret = signingSecret; _agent = agent;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_botToken}");
    }

    /// <summary>Process an incoming Slack event payload (JSON from Events API).</summary>
    public async Task<string?> ProcessEventAsync(string requestBody, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(requestBody);
        var root = doc.RootElement;

        // URL verification challenge
        if (root.TryGetProperty("challenge", out var challenge))
            return challenge.GetString();

        // Event callback
        if (root.TryGetProperty("event", out var evt))
        {
            var type = evt.GetProperty("type").GetString();
            var text = evt.TryGetProperty("text", out var t) ? t.GetString() : "";
            var channel = evt.TryGetProperty("channel", out var c) ? c.GetString() : "";
            var user = evt.TryGetProperty("user", out var u) ? u.GetString() : "";
            var threadTs = evt.TryGetProperty("thread_ts", out var ts) ? ts.GetString() : null;

            if (type == "app_mention" || type == "message")
            {
                // Run agent
                var result = await _agent.RunAsync(new AgentRequest(text ?? ""), ct);

                // Post reply
                var payload = new
                {
                    channel,
                    text = result.Content,
                    thread_ts = threadTs
                };
                var json = JsonSerializer.Serialize(payload);
                var response = await _http.PostAsync(
                    "https://slack.com/api/chat.postMessage",
                    new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
                    ct);
                response.EnsureSuccessStatusCode();
            }
        }

        return null;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        // Fetch bot user ID from Slack API
        var response = await _http.GetAsync("https://slack.com/api/auth.test", ct);
        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        BotUserId = doc.RootElement.GetProperty("user_id").GetString();
    }
}
