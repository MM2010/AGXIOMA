using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AgxiomaLegalEmail.Services;

/// <summary>
/// Uses the configured LLM agent (DeepSeek) to extract 3-6 focused legal concepts from an email.
/// Each concept is short (≤8 words) and suitable for precise vector embedding.
///
/// Example output for an email about recesso + falso in bilancio + brevetto:
///   ["recesso socio srl giusta causa", "falso bilancio comunicazioni sociali",
///    "estorsione minaccia denuncia art629", "brevetto invenzione dipendente cpi"]
/// </summary>
public sealed class LegalConceptExtractor
{
    private readonly IAgxiomeAgent _agent;
    private readonly ILogger<LegalConceptExtractor> _log;
    private const int MaxBodyChars = 600;

    public LegalConceptExtractor(IAgxiomeAgent agent, ILogger<LegalConceptExtractor> log)
    {
        _agent = agent;
        _log   = log;
    }

    /// <summary>
    /// Extracts legal concepts. Falls back to [subject] on failure so the
    /// caller always gets at least one query term.
    /// </summary>
    public async Task<string[]> ExtractAsync(string subject, string body, CancellationToken ct = default)
    {
        var bodySnippet = body.Length > MaxBodyChars ? body[..MaxBodyChars] + "…" : body;

        var prompt = $"""
            Sei un esperto di diritto italiano.
            Leggi l'email e identifica i CONCETTI LEGALI DISTINTI presenti.
            
            Rispondi con UNA LISTA di 3-6 concetti, uno per riga.
            Ogni concetto: BREVE (max 8 parole), specifico, con termini giuridici italiani
            (es. nomi di istituti, articoli di legge, reati, istituti civilistici).
            NON aggiungere numerazione, trattini, spiegazioni o testo extra.
            
            Oggetto: {subject}
            Corpo: {bodySnippet}
            """;

        try
        {
            var resp = await _agent.RunAsync(new AgentRequest(prompt), ct);
            var raw  = resp.Content.Trim();

            var concepts = raw
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(l => l.Length >= 4 && l.Length <= 120)
                .Select(l => l.TrimStart('-', '*', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', ' '))
                .Where(l => l.Length >= 4)
                .Take(6)
                .ToArray();

            if (concepts.Length == 0)
            {
                _log.LogWarning("[ConceptExtractor] Empty response — falling back to subject");
                return [subject];
            }

            _log.LogInformation("[ConceptExtractor] {N} concepts: {C}",
                concepts.Length, string.Join(" | ", concepts));
            return concepts;
        }
        catch (Exception ex)
        {
            _log.LogWarning("[ConceptExtractor] Failed ({E}) — falling back to subject", ex.Message);
            return [subject];
        }
    }
}

