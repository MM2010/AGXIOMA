using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace AgxiomaHybridRouting.Routing;

/// <summary>
/// Embedding client backed by a local Ollama instance.
/// Gracefully falls back to keyword-only routing when Ollama is unavailable.
/// </summary>
public sealed class OllamaRouteEmbedder : IRouteEmbedder
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly ILogger<OllamaRouteEmbedder> _logger;
    private bool _available = true;   // optimistic — will be set false on first failure

    public bool IsAvailable => _available;

    public OllamaRouteEmbedder(
        string ollamaEndpoint,
        string embedModel,
        ILogger<OllamaRouteEmbedder> logger)
    {
        _model  = embedModel;
        _logger = logger;
        _http   = new HttpClient
        {
            BaseAddress = new Uri(ollamaEndpoint.TrimEnd('/') + "/"),
            Timeout     = TimeSpan.FromSeconds(10)
        };
    }

    /// <summary>
    /// Calls <c>POST /api/embeddings</c> on Ollama.
    /// Returns null (and marks service unavailable) on any error.
    /// </summary>
    public async Task<float[]?> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (!_available) return null;

        try
        {
            var requestBody = new { model = _model, prompt = text };
            using var response = await _http.PostAsJsonAsync("api/embeddings", requestBody, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Ollama returned {Status} for model '{Model}'. Falling back to keyword routing.",
                    (int)response.StatusCode, _model);
                _available = false;
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken: ct);
            if (result?.Embedding is null || result.Embedding.Length == 0)
            {
                _logger.LogWarning("Ollama returned empty embedding. Falling back to keyword routing.");
                _available = false;
                return null;
            }

            return result.Embedding;
        }
        catch (Exception ex) when (
            ex is HttpRequestException or
            TaskCanceledException or
            OperationCanceledException)
        {
            _logger.LogWarning(
                "Ollama not reachable ({Message}). Hybrid routing will use keyword-only fallback.",
                ex.Message);
            _available = false;
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while calling Ollama embeddings API.");
            _available = false;
            return null;
        }
    }

    // ─── DTO ──────────────────────────────────────────────────────────────────

    private sealed class OllamaEmbeddingResponse
    {
        public float[]? Embedding { get; set; }
    }
}
