using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AgxiomaLegalEmail.Services;

/// <summary>
/// HyDE — Hypothetical Document Embedding.
/// Genera un estratto ipotetico di articolo di legge italiano che risponderebbe
/// alla query dell'utente, poi usa QUELLO come input per l'embedding invece
/// della query grezza. Risolve il vocabulary mismatch tra linguaggio colloquiale
/// e terminologia giuridica formale del corpus.
///
/// Costo: 1 chiamata DeepSeek per concetto (usa l'agente già connesso).
/// Latenza tipica: ~300-600ms per concetto (parallelo su batch multi-concetto).
/// Degradazione graceful: se l'agente fallisce, restituisce la query originale.
/// </summary>
public sealed class LegalHydeGenerator
{
    private readonly IAgxiomeAgent _agent;
    private readonly ILogger<LegalHydeGenerator> _log;

    public LegalHydeGenerator(IAgxiomeAgent agent, ILogger<LegalHydeGenerator> log)
    {
        _agent = agent;
        _log   = log;
    }

    /// <summary>
    /// Genera un documento ipotetico per il concetto/query dato.
    /// Il testo generato viene usato come query per pgvector al posto della query grezza.
    /// </summary>
    public async Task<(string Result, int Tokens)> GenerateAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 5)
            return (query, 0);

        var prompt = $"""
            Sei un assistente giuridico specializzato in diritto italiano.
            Scrivi un breve estratto (80-120 parole) di un ipotetico articolo del Codice Civile 
            o Codice Penale che risponderebbe direttamente al seguente concetto o domanda legale.
            Usa linguaggio giuridico formale e cita articoli specifici (es: art.624 c.p., art.2043 c.c.).
            
            Concetto/Domanda: {query}
            
            Scrivi SOLO il testo dell'ipotetico estratto normativo, senza titoli, introduzioni o spiegazioni.
            """;

        try
        {
            var resp      = await _agent.RunAsync(new AgentRequest(prompt), ct);
            var generated = resp.Content?.Trim() ?? query;
            var tokens    = resp.Metadata?.TotalTokens ?? 0;

            _log.LogInformation(
                "[HyDE] Query='{Q}' → GeneratedChars={Chars} Tokens={Tokens} Preview='{P}'",
                query, generated.Length, tokens,
                generated.Length > 120
                    ? generated[..120].Replace('\n', ' ') + "…"
                    : generated.Replace('\n', ' '));

            return (generated, tokens);
        }
        catch (Exception ex)
        {
            _log.LogWarning("[HyDE] Fallback to original query for '{Q}': {Err}", query, ex.Message);
            return (query, 0);   // graceful degradation — pipeline continua senza HyDE
        }
    }

    /// <summary>
    /// Genera documenti ipotetici per più concetti in parallelo.
    /// Usato da PipelineOrchestrator Phase 4 nel retrieval multi-concetto.
    /// </summary>
    public async Task<(string[] Results, int TotalTokens)> GenerateBatchAsync(string[] concepts, CancellationToken ct = default)
    {
        var tasks = concepts.Select(c => GenerateAsync(c, ct));
        var results = await Task.WhenAll(tasks);
        return (results.Select(r => r.Result).ToArray(), results.Sum(r => r.Tokens));
    }
}
