using AGXIOME.Core.Agent;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Adapters.Telegram;

/// <summary>
/// Telegram bot interface for AGXIOME agents.
/// Handles webhook updates from Telegram Bot API.
/// </summary>
public sealed class TelegramBot
{
    private readonly string _token;
    private readonly IAgxiomeAgent _agent;
    private readonly HttpClient _http;
    private string _baseUrl => $"https://api.telegram.org/bot{_token}";

    public TelegramBot(string botToken, IAgxiomeAgent agent)
    {
        _token = botToken; _agent = agent;
        _http = new HttpClient();
    }

    /// <summary>Set webhook URL for receiving updates.</summary>
    public async Task SetWebhookAsync(string url, CancellationToken ct = default)
    {
        var payload = new { url };
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var response = await _http.PostAsync(
            $"{_baseUrl}/setWebhook",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"), ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Process an incoming Telegram update (JSON from webhook).</summary>
    public async Task<TelegramMessage?> ProcessUpdateAsync(string requestBody, CancellationToken ct = default)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(requestBody);
        var root = doc.RootElement;

        if (!root.TryGetProperty("message", out var msg))
            return null;

        var chatId = msg.GetProperty("chat").GetProperty("id").GetInt64();
        var text = msg.TryGetProperty("text", out var t) ? t.GetString() : "";
        var messageId = msg.GetProperty("message_id").GetInt32();

        // Run agent
        var result = await _agent.RunAsync(new AgentRequest(text ?? ""), ct);

        // Send reply
        var replyPayload = new { chat_id = chatId, text = result.Content, reply_to_message_id = messageId };
        var json = System.Text.Json.JsonSerializer.Serialize(replyPayload);
        await _http.PostAsync(
            $"{_baseUrl}/sendMessage",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"), ct);

        return new TelegramMessage(chatId, text ?? "", result.Content, messageId);
    }

    /// <summary>Delete webhook.</summary>
    public async Task DeleteWebhookAsync(CancellationToken ct = default)
        => await _http.GetAsync($"{_baseUrl}/deleteWebhook", ct);
}

public record TelegramMessage(long ChatId, string UserText, string BotResponse, int MessageId);
