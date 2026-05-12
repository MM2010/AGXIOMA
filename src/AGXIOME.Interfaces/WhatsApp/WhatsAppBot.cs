using AGXIOME.Core.Agent;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Interfaces.WhatsApp;

/// <summary>
/// WhatsApp Business API interface for AGXIOME agents.
/// Handles incoming webhook messages and sends replies via WhatsApp Cloud API.
/// </summary>
public sealed class WhatsAppBot
{
    private readonly string _phoneNumberId;
    private readonly string _accessToken;
    private readonly IAgxiomeAgent _agent;
    private readonly HttpClient _http;
    private const string ApiBase = "https://graph.facebook.com/v22.0";

    public WhatsAppBot(string phoneNumberId, string accessToken, IAgxiomeAgent agent)
    {
        _phoneNumberId = phoneNumberId; _accessToken = accessToken; _agent = agent;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_accessToken}");
    }

    /// <summary>Process an incoming WhatsApp webhook payload.</summary>
    public async Task<bool> ProcessWebhookAsync(string requestBody, CancellationToken ct = default)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(requestBody);
        var root = doc.RootElement;

        if (!root.TryGetProperty("entry", out var entries)) return false;

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes)) continue;
            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value)) continue;
                if (!value.TryGetProperty("messages", out var messages)) continue;

                foreach (var msg in messages.EnumerateArray())
                {
                    var from = msg.GetProperty("from").GetString();
                    var text = msg.GetProperty("text").GetProperty("body").GetString();
                    var msgId = msg.GetProperty("id").GetString();

                    // Run agent
                    var result = await _agent.RunAsync(new AgentRequest(text ?? ""), ct);

                    // Send reply
                    var replyPayload = new
                    {
                        messaging_product = "whatsapp",
                        to = from,
                        type = "text",
                        text = new { body = result.Content }
                    };
                    var json = System.Text.Json.JsonSerializer.Serialize(replyPayload);
                    await _http.PostAsync(
                        $"{ApiBase}/{_phoneNumberId}/messages",
                        new StringContent(json, System.Text.Encoding.UTF8, "application/json"), ct);
                }
            }
        }
        return true;
    }
}
