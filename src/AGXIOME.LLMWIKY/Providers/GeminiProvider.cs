using AGXIOME.LlmWiky.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.LlmWiky.Providers;

/// <summary>Google Gemini provider with streaming and embedding support.</summary>
public sealed class GeminiProvider : ILLMProvider
{
    public string Name => "Gemini";
    public LLMProviderCapabilities Capabilities => new(true, true, true, 768, 1_048_576,
        ["gemini-2.0-flash", "gemini-1.5-pro", "gemini-2.0-flash-thinking", "gemini-2.5-pro"]);
    public int EmbeddingDimensions => _config.EmbeddingDimensions;

    private readonly HttpClient _http;
    private readonly LLMWIKYConfig _config;

    public GeminiProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
    {
        _config = config;
        if (httpClientFactory is not null)
            _http = httpClientFactory.CreateClient(AgxiomeHttpClientDefaults.ClientName(LLMProviderType.Gemini));
        else
        {
            _http = new HttpClient();
            _http.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta");
        }
    }

    public async Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest request, CancellationToken ct = default)
    {
        var body = BuildBody(request);
        var url = $"/models/{request.Model}:generateContent?key={_config.ApiKey}";
        var httpContent = new StringContent(body.ToJsonString(), Encoding.UTF8);
        var response = await _http.PostAsync(url, httpContent, ct);
        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return ParseResponse(doc.RootElement);
    }

    public async IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var body = BuildBody(request);
        var url = $"/models/{request.Model}:streamGenerateContent?alt=sse&key={_config.ApiKey}";
        var httpContent = new StringContent(body.ToJsonString(), Encoding.UTF8);
        var response = await _http.PostAsync(url, httpContent, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        string? line;
        while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (!line.StartsWith("data: ")) continue;
            var data = line[6..];
            if (data == "[DONE]") { yield return new LLMWIKYChunk("", IsFinal: true); yield break; }

            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var parts = candidates[0].GetProperty("content").GetProperty("parts");
                foreach (var p in parts.EnumerateArray())
                    if (p.TryGetProperty("text", out var text))
                        yield return new LLMWIKYChunk(text.GetString() ?? "");
            }
        }
        yield return new LLMWIKYChunk("", IsFinal: true);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var body = new JsonObject
        {
            ["model"] = $"models/{_config.Model}",
            ["content"] = new JsonObject { ["parts"] = new JsonArray { new JsonObject { ["text"] = text } } }
        };
        var url = $"/models/{_config.Model}:embedContent?key={_config.ApiKey}";
        var httpContent = new StringContent(body.ToJsonString(), Encoding.UTF8);
        var response = await _http.PostAsync(url, httpContent, ct);
        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc.RootElement.GetProperty("embedding").GetProperty("values")
            .EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
    }

    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var results = new List<Task<float[]>>();
        foreach (var t in texts) results.Add(EmbedAsync(t, ct));
        return await Task.WhenAll(results);
    }

    private static JsonObject BuildBody(LLMCompletionRequest req)
    {
        var parts = new JsonArray { new JsonObject { ["text"] = req.UserPrompt } };
        var body = new JsonObject
        {
            ["contents"] = new JsonArray { new JsonObject { ["role"] = "user", ["parts"] = parts } },
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray { new JsonObject { ["text"] = req.SystemPrompt } } }
        };
        if (req.Tools?.Count > 0)
            body["tools"] = new JsonArray { new JsonObject { ["functionDeclarations"] = new JsonArray(req.Tools.Select(t =>
                new JsonObject { ["name"] = t["name"]?.GetValue<string>() ?? "", ["description"] = t["description"]?.GetValue<string>() ?? "" }).ToArray()) } };
        return body;
    }

    private static LLMWIKYResponse ParseResponse(JsonElement root)
    {
        var text = root.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
        return new LLMWIKYResponse(text ?? "");
    }
}
