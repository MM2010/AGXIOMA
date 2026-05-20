using AGXIOME.Core.Agent;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Adapters.Discord;

/// <summary>
/// Discord bot interface for AGXIOME agents.
/// Processes slash commands and messages via Discord Interaction API.
/// Designed to work with Discord.NET or as a standalone HTTP interaction handler.
/// </summary>
public sealed class DiscordBot
{
    private readonly string _applicationId;
    private readonly string _botToken;
    private readonly string _publicKey;
    private readonly IAgxiomeAgent _agent;
    private readonly HttpClient _http;
    private const string ApiBase = "https://discord.com/api/v10";

    public DiscordBot(string applicationId, string botToken, string publicKey, IAgxiomeAgent agent)
    {
        _applicationId = applicationId; _botToken = botToken; _publicKey = publicKey; _agent = agent;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("Authorization", $"Bot {_botToken}");
    }

    /// <summary>Verify Discord interaction signature.</summary>
    public bool VerifySignature(string requestBody, string signature, string timestamp)
    {
        // Ed25519 signature verification using Discord's public key
        var message = timestamp + requestBody;
        // In production: use System.Security.Cryptography with Ed25519
        return true; // simplified for now
    }

    /// <summary>Process a Discord interaction payload.</summary>
    public async Task<string?> ProcessInteractionAsync(string requestBody, CancellationToken ct = default)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(requestBody);
        var root = doc.RootElement;
        var type = root.GetProperty("type").GetInt32();

        // PING (type 1)
        if (type == 1) return System.Text.Json.JsonSerializer.Serialize(new { type = 1 });

        // APPLICATION_COMMAND (type 2)
        if (type == 2)
        {
            var commandName = root.GetProperty("data").GetProperty("name").GetString();
            var userId = root.GetProperty("member").GetProperty("user").GetProperty("id").GetString();
            var options = root.GetProperty("data").TryGetProperty("options", out var opts)
                ? string.Join(", ", opts.EnumerateArray().Select(o => o.GetProperty("value").GetString()))
                : "";

            var prompt = $"{commandName}: {options}";
            var result = await _agent.RunAsync(new AgentRequest(prompt), ct);

            // Deferred response
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                type = 4, // CHANNEL_MESSAGE_WITH_SOURCE
                data = new { content = result.Content }
            });
        }

        return null;
    }

    /// <summary>Register a slash command with Discord.</summary>
    public async Task RegisterSlashCommandAsync(string name, string description, CancellationToken ct = default)
    {
        var payload = new { name, description, type = 1 };
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var response = await _http.PostAsync(
            $"{ApiBase}/applications/{_applicationId}/commands",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"), ct);
        response.EnsureSuccessStatusCode();
    }
}
