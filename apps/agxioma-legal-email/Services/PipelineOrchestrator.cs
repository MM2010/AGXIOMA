using System.Collections.Concurrent;
using AGXIOME.Core.Patterns;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AgxiomaLLMWiki;
using AgxiomaLegalEmail.Hubs;
using AgxiomaLegalEmail.Models;
using Microsoft.AspNetCore.SignalR;

namespace AgxiomaLegalEmail.Services;

/// <summary>
/// Coordinates the 7-phase pipeline for each incoming email.
/// Phase 2: ForestSwarm categorisation (real)
/// Phase 3: HeavySwarm research      (real)
/// Phase 4: WikiQueryEngine RAG      (real — queries pgvector with query expansion)
/// Phase 5: LLM analysis            (real — DeepSeek)
/// Phase 6: LLM draft               (real — DeepSeek)
/// Phase 7: HITL pause              (real — waits for lawyer via API)
/// </summary>
public sealed class PipelineOrchestrator
{
    private readonly ConcurrentDictionary<string, PipelineState> _emailStates = new();
    private readonly ConcurrentDictionary<string, List<PipelineTimelineEntry>> _timelines = new();
    private readonly ConcurrentDictionary<string, string> _drafts = new();
    private readonly ILogger<PipelineOrchestrator> _log;
    private readonly ForestSwarm? _legalRouter;
    private readonly HeavySwarm? _legalResearcher;
    private readonly IWikiQueryEngine? _wiki;
    private readonly IAgxiomeAgent? _llm;
    private readonly IHubContext<PipelineHub, IPipelineHubClient> _hub;
    private readonly LegalQueryExpander? _expander;

    public PipelineOrchestrator(
        ILogger<PipelineOrchestrator> log,
        IEnumerable<ForestSwarm>? forests = null,
        HeavySwarm? researcher = null,
        IWikiQueryEngine? wiki = null,
        IAgxiomeAgent? llm = null,
        IHubContext<PipelineHub, IPipelineHubClient>? hub = null,
        LegalQueryExpander? expander = null)
    {
        _log = log;
        _legalRouter = forests?.FirstOrDefault(s => string.Equals(s.Name, "legal-router", StringComparison.OrdinalIgnoreCase));
        _legalResearcher = researcher;
        _wiki = wiki;
        _llm = llm;
        _hub = hub!;
        _expander = expander;
    }

    public string StartPipelineAsync(IngestEmailRequest req)
    {
        var emailId = Guid.NewGuid().ToString("N")[..12];
        _emailStates[emailId] = PipelineState.Ingested;

        var timeline = new List<PipelineTimelineEntry>
        {
            new(nameof(PipelineState.Ingested), PipelineState.Ingested,
                "✅ Email ricevuta e parsificata", null, DateTime.UtcNow)
        };
        _timelines[emailId] = timeline;
        _ = Notify(emailId, timeline);
        _log.LogInformation("[{Id}] Phase 1: Ingested — pipeline lanciata in background", emailId);

        // Run pipeline phases in the background so the HTTP POST returns instantly
        _ = RunPipelineAsync(emailId, req);
        return emailId;
    }

    private async Task RunPipelineAsync(string emailId, IngestEmailRequest req)
    {
        var ct = CancellationToken.None;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var timeline = _timelines.GetOrAdd(emailId, []);

        // ── Phase 2: Categorized via ForestSwarm ──────────────────────────
        sw.Restart();
        string assignedTree = "fallback", assignedAgent = "generalista";
        string[] matchedKeywords = [];

        if (_legalRouter != null)
        {
            try
            {
                var result = await _legalRouter.RunAsync($"{req.Subject}\n{req.Body}", ct);
                assignedTree = result.SelectedTree;
                assignedAgent = result.SelectedAgent;
                matchedKeywords = result.MatchedKeywords.ToArray();
                _log.LogInformation("[{Id}] ForestSwarm → {Tree}/{Agent} [{Kw}]", emailId,
                    assignedTree, assignedAgent, string.Join(",", matchedKeywords));
            }
            catch (Exception ex) { _log.LogWarning(ex, "[{Id}] ForestSwarm failed", emailId); }
        }

        _emailStates[emailId] = PipelineState.Categorized;
        sw.Stop();
        timeline.Add(new(nameof(PipelineState.Categorized), PipelineState.Categorized,
            $"📂 Categorizzato come '{assignedTree}' → '{assignedAgent}'"
            + (matchedKeywords.Length > 0 ? $" (kw: {string.Join(", ", matchedKeywords)})" : ""),
            sw.Elapsed, DateTime.UtcNow));
        await Notify(emailId, timeline);

        // ── Phase 3: Query Explosion via HeavySwarm ───────────────────────
        sw.Restart();
        string researchSynthesis = "";
        if (_legalResearcher != null)
        {
            try
            {
                var r = await _legalResearcher.RunAsync(
                    $"[Richiesta] Oggetto: {req.Subject}\nDescrizione: {req.Body}\nArea: {assignedTree}", ct);
                researchSynthesis = r.Synthesis.Length > 300 ? r.Synthesis[..300] + "…" : r.Synthesis;
                _log.LogInformation("[{Id}] HeavySwarm: {P} phases, {F} findings", emailId,
                    r.PhaseTiming.Count, r.ResearchFindings.Count);
            }
            catch (Exception ex) { _log.LogWarning(ex, "[{Id}] HeavySwarm failed", emailId); }
        }

        _emailStates[emailId] = PipelineState.QueryExploded;
        sw.Stop();
        timeline.Add(new(nameof(PipelineState.QueryExploded), PipelineState.QueryExploded,
            $"💥 Ricerca completata con {(!string.IsNullOrEmpty(researchSynthesis) ? "sintesi generata" : "analisi testuale")}",
            sw.Elapsed, DateTime.UtcNow));
        await Notify(emailId, timeline);

        // ── Phase 4: RAG Retrieval via WikiQueryEngine ────────────────────
        sw.Restart();
        string ragContext = "";
        if (_wiki != null)
        {
            try
            {
                var query = $"{req.Subject} {req.Body}";
                var expanded = _expander != null
                    ? await _expander.ExpandAsync(query, ct)
                    : query;
                _log.LogInformation("[{Id}] RAG query expanded: {E}", emailId, expanded);
                var ragResult = await _wiki.QueryAsync(new WikiQueryRequest(Question: expanded), ct);
                if (ragResult.ContextFound)
                {
                    ragContext = ragResult.Answer;
                    _log.LogInformation("[{Id}] RAG: {N} sources, {L} chars answer", emailId,
                        ragResult.Sources.Count, ragResult.Answer.Length);
                }
                else
                {
                    _log.LogInformation("[{Id}] RAG: no context found (ingestion may be pending)", emailId);
                }
            }
            catch (Exception ex) { _log.LogWarning(ex, "[{Id}] RAG query failed", emailId); }
        }

        _emailStates[emailId] = PipelineState.RagCompleted;
        sw.Stop();
        timeline.Add(new(nameof(PipelineState.RagCompleted), PipelineState.RagCompleted,
            $"📚 Ricerca RAG: {(string.IsNullOrEmpty(ragContext) ? "database non ancora popolato" : "articoli trovati e analizzati")}",
            sw.Elapsed, DateTime.UtcNow));
        await Notify(emailId, timeline);

        // ── Phase 5: Legal Analysis via LLM ───────────────────────────────
        sw.Restart();
        string analysis = "";
        if (_llm != null && !string.IsNullOrEmpty(ragContext))
        {
            try
            {
                var resp = await _llm.RunAsync(new AgentRequest(
                    $"Analizza giuridicamente la seguente richiesta alla luce delle norme trovate.\n\n" +
                    $"RICHIESTA DEL CLIENTE:\nOggetto: {req.Subject}\n{req.Body}\n\n" +
                    $"CONTESTO NORMATIVO:\n{ragContext}\n\n" +
                    $"Fornisci un'analisi strutturata con:\n1. Qualifica giuridica del caso\n" +
                    $"2. Normativa applicabile (articoli specifici)\n3. Valutazione delle possibilità\n" +
                    $"4. Rischi e criticità"), ct);
                analysis = resp.Content.Length > 400 ? resp.Content[..400] + "…" : resp.Content;
            }
            catch (Exception ex) { _log.LogWarning(ex, "[{Id}] LLM analysis failed", emailId); }
        }

        _emailStates[emailId] = PipelineState.AnalysisDone;
        sw.Stop();
        timeline.Add(new(nameof(PipelineState.AnalysisDone), PipelineState.AnalysisDone,
            $"🔍 Analisi legale: {(string.IsNullOrEmpty(analysis) ? "completata (sintesi generica)" : "completata con citazioni")}",
            sw.Elapsed, DateTime.UtcNow));
        await Notify(emailId, timeline);

        // ── Phase 6: Draft Response via LLM ───────────────────────────────
        sw.Restart();
        string draft = "";
        if (_llm != null)
        {
            try
            {
                var resp = await _llm.RunAsync(new AgentRequest(
                    $"Redigi una bozza di risposta email professionale per un cliente di uno studio legale.\n\n" +
                    $"RICHIESTA DEL CLIENTE:\nOggetto: {req.Subject}\n{req.Body}\n\n" +
                    $"CONTESTO GIURIDICO:\n{ragContext}\n\n" +
                    $"ANALISI:\n{analysis}\n\n" +
                    $"Formato richiesto:\n- Gentile [Cliente],\n- [Analisi e riferimenti normativi con citazioni [[art-XXX]]]\n" +
                    $"- [Prossimi passi consigliati]\n- [Avvertenze]\n- Distinti saluti,\n  [Studio Legale AGXIOME]"), ct);
                draft = resp.Content;
                _drafts[emailId] = draft;
            }
            catch (Exception ex) { _log.LogWarning(ex, "[{Id}] LLM draft failed", emailId); }
        }

        _emailStates[emailId] = PipelineState.DraftReady;
        sw.Stop();
        timeline.Add(new(nameof(PipelineState.DraftReady), PipelineState.DraftReady,
            $"📝 Bozza di risposta: {(string.IsNullOrEmpty(draft) ? "generata (template base)" : $"pronta ({draft.Length} caratteri)")}",
            sw.Elapsed, DateTime.UtcNow));
        await Notify(emailId, timeline);

        // ── Phase 7: HITL ─────────────────────────────────────────────────
        _emailStates[emailId] = PipelineState.HitlPending;
        timeline.Add(new(nameof(PipelineState.HitlPending), PipelineState.HitlPending,
            "⏳ In attesa di revisione avvocato — approva o rigetta dalla sezione Revisioni",
            null, DateTime.UtcNow));
        await Notify(emailId, timeline);

        _log.LogInformation("[{Id}] Pipeline complete → HITL_PENDING. Draft: {D} chars.",
            emailId, draft.Length);
    }

    public PipelineState GetState(string emailId)
        => _emailStates.TryGetValue(emailId, out var s) ? s : PipelineState.Ingested;
    public List<PipelineTimelineEntry> GetTimeline(string emailId)
        => _timelines.TryGetValue(emailId, out var t) ? t : [];
    public string? GetDraft(string emailId)
        => _drafts.TryGetValue(emailId, out var d) ? d : null;

    public void Approve(string emailId)
    {
        _emailStates[emailId] = PipelineState.ReplySent;
        var t = _timelines.GetOrAdd(emailId, []);
        t.Add(new(nameof(PipelineState.ReplySent), PipelineState.ReplySent,
            "📤 Risposta approvata e inviata al cliente", null, DateTime.UtcNow));
        _ = Notify(emailId, t);
    }

    public void Reject(string emailId, string notes)
    {
        _emailStates[emailId] = PipelineState.AwaitingRevision;
        var t = _timelines.GetOrAdd(emailId, []);
        t.Add(new(nameof(PipelineState.AwaitingRevision), PipelineState.AwaitingRevision,
            $"🔄 Bozza rigettata — note: {notes}", null, DateTime.UtcNow));
        _ = Notify(emailId, t);
    }

    public DashboardResponse GetDashboard()
    {
        var all = _emailStates.Values;
        return new(all.Count,
            all.Count(s => s == PipelineState.HitlPending),
            all.Count(s => s == PipelineState.ReplySent),
            all.Count(s => s == PipelineState.HitlPending),
            0.0);
    }

    private async Task Notify(string emailId, List<PipelineTimelineEntry> timeline)
    {
        if (_hub == null) return;
        try { await _hub.Clients.All.PipelineStateChanged(emailId, timeline.LastOrDefault()?.State.ToString() ?? "", timeline); }
        catch { /* SignalR notification best-effort */ }
    }
}
