using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.DynamicRuntime.Bridge;

/// <summary>
/// Service locator statico per il codice generato a runtime.
/// Il codice compilato dinamicamente non può ricevere dipendenze via costruttore
/// (usa Activator.CreateInstance), ma può chiamare AgentServices.Llm, .Http, ecc.
///
/// Registrazione avviene in DynamicRuntimeServiceExtensions via Initialize().
/// </summary>
public static class AgentServices
{
    private static ILLMProvider? _llm;
    private static HttpClient? _http;

    /// <summary>LLM provider. Usabile dal codice generato per chiamate AI.</summary>
    public static ILLMProvider Llm =>
        _llm ?? throw new InvalidOperationException(
            "AgentServices not initialized. Call AgentServices.Initialize() at startup.");

    /// <summary>HttpClient condiviso. Usabile dal codice generato per chiamate HTTP.</summary>
    public static HttpClient Http =>
        _http ?? throw new InvalidOperationException(
            "AgentServices not initialized. Call AgentServices.Initialize() at startup.");

    /// <summary>
    /// Shortcut: chiama l'LLM con un semplice prompt user → restituisce la risposta come stringa.
    /// Pensato per uso diretto nel codice generato.
    /// </summary>
    public static async Task<string> AskAsync(
        string prompt,
        string? systemPrompt = null,
        double temperature = 0.7,
        int maxTokens = 1000,
        CancellationToken ct = default)
    {
        var request = new LLMCompletionRequest(
            SystemPrompt: systemPrompt ?? "You are a helpful assistant.",
            UserPrompt: prompt,
            History: null,
            Temperature: temperature,
            MaxTokens: maxTokens,
            Model: string.Empty,
            Tools: null
        );
        var response = await Llm.CompleteAsync(request, ct);
        return response.Content;
    }

    /// <summary>
    /// Chiamata HTTP GET semplice → restituisce il body come stringa.
    /// </summary>
    public static async Task<string> GetAsync(string url, CancellationToken ct = default)
        => await Http.GetStringAsync(url, ct);

    /// <summary>
    /// Chiamata HTTP POST JSON → restituisce il body come stringa.
    /// </summary>
    public static async Task<string> PostJsonAsync(string url, object payload, CancellationToken ct = default)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await Http.PostAsync(url, content, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    /// <summary>Inizializza i servizi. Chiamato una volta allo startup.</summary>
    public static void Initialize(ILLMProvider llm, HttpClient? http = null)
    {
        _llm = llm;
        _http = http ?? new HttpClient();
    }
}
