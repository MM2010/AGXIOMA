using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgxiomaLegalEmail.Services;

/// <summary>
/// Expands user queries with legal synonyms and related terms using a small Ollama model.
/// Improves vector search recall by enriching the query before embedding.
/// Es: "furto in appartamento" → "furto in appartamento violazione di domicilio art624bis denuncia querela"
/// </summary>
public sealed class LegalQueryExpander
{
    private readonly HttpClient _http;
    private readonly ILogger<LegalQueryExpander> _log;
    private const string Model = "qwen2.5-coder:3b";

    public LegalQueryExpander(IHttpClientFactory httpFactory, ILogger<LegalQueryExpander> log)
    {
        _http = httpFactory.CreateClient("ollama");
        _log = log;
    }

    public async Task<string> ExpandAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 5)
            return query;

        var prompt = $"""
            Sei un assistente legale specializzato in diritto italiano.
            Arricchisci la query dell'utente con termini legali pertinenti (sinonimi, articoli di legge, concetti correlati).
            
            Query utente: {query}
            
            Rispondi SOLO con la query arricchita, massimo 200 caratteri, senza spiegazioni.
            Esempio: "furto in appartamento" → "furto in appartamento art624bis codice penale denuncia querela violazione domicilio"
            """;

        try
        {
            var body = new { model = Model, prompt, stream = false, options = new { num_predict = 128, temperature = 0.2 } };
            var resp = await _http.PostAsJsonAsync("api/generate", body, ct);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadFromJsonAsync<OllamaResponse>(ct);
            var expanded = json?.Response?.Trim() ?? query;
            _log.LogInformation("[QueryExpander] '{Q}' → '{E}'", query, expanded);
            return expanded;
        }
        catch (Exception ex)
        {
            _log.LogWarning("[QueryExpander] Fallback to original query: {Error}", ex.Message);
            return query;
        }
    }

    private sealed class OllamaResponse { [JsonPropertyName("response")] public string Response { get; set; } = ""; }
}
