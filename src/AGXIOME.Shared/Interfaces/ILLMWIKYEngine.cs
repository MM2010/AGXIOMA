using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

/// <summary>LLMWIKY engine: inference, streaming, embedding, tool registration.</summary>
public interface ILLMWIKYEngine
{
    Task<LLMWIKYResponse> InferAsync(LLMWIKYRequest request, CancellationToken ct = default);
    IAsyncEnumerable<LLMWIKYChunk> InferStreamAsync(LLMWIKYRequest request, CancellationToken ct = default);
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
    void RegisterTools(IReadOnlyList<System.Text.Json.Nodes.JsonObject> toolSchemas);
}
