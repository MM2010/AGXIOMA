using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.LLMWIKY;

/// <summary>
/// Core LLMWIKY engine. Routes inference to configured provider,
/// handles fallback, streaming, embedding, and tool registration.
/// </summary>
public sealed class LLMWIKYEngine : ILLMWIKYEngine
{
    private readonly ILLMProvider _provider;
    private readonly ILLMProvider? _fallback;
    private readonly LLMWIKYConfig _config;
    private IReadOnlyList<System.Text.Json.Nodes.JsonObject>? _tools;

    public LLMWIKYEngine(LLMWIKYConfig config)
    {
        _config = config;
        _provider = LLMProviderFactory.Create(config);
        _fallback = config.FallbackProviders?.FirstOrDefault() is { } fb
            ? LLMProviderFactory.Create(fb) : null;
    }

    public async Task<LLMWIKYResponse> InferAsync(LLMWIKYRequest request, CancellationToken ct = default)
    {
        var llmReq = new LLMCompletionRequest(
            request.SystemPrompt, request.UserPrompt, request.History,
            request.Temperature ?? _config.DefaultTemperature,
            request.MaxTokens ?? _config.MaxTokens,
            _config.Model,
            _tools ?? request.Tools
        );

        try
        {
            return await _provider.CompleteAsync(llmReq, ct);
        }
        catch (Exception ex) when (_fallback != null
            && ex is TimeoutException or HttpRequestException)
        {
            return await _fallback.CompleteAsync(llmReq, ct);
        }
    }

    public IAsyncEnumerable<LLMWIKYChunk> InferStreamAsync(LLMWIKYRequest request, CancellationToken ct = default)
    {
        var llmReq = new LLMCompletionRequest(
            request.SystemPrompt, request.UserPrompt, request.History,
            request.Temperature ?? _config.DefaultTemperature,
            request.MaxTokens ?? _config.MaxTokens,
            _config.Model,
            _tools ?? request.Tools
        );
        return _provider.CompleteStreamAsync(llmReq, ct);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        => await _provider.EmbedAsync(text, ct);

    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        => await _provider.EmbedBatchAsync(texts, ct);

    public void RegisterTools(IReadOnlyList<System.Text.Json.Nodes.JsonObject> toolSchemas)
        => _tools = toolSchemas;
}
