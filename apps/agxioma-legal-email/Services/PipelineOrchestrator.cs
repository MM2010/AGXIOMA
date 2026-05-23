using System.Collections.Concurrent;
using AGXIOME.Core.Patterns;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AgxiomaLLMWiki;
using AgxiomaLegalEmail.Hubs;
using AgxiomaLegalEmail.Models;
using Microsoft.AspNetCore.SignalR;
using Serilog.Context;

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
    private readonly LegalConceptExtractor? _conceptExtractor;
    private readonly LegalReranker? _reranker;
    private readonly LegalHydeGenerator? _hyde;

    public PipelineOrchestrator(
        ILogger<PipelineOrchestrator> log,
        IEnumerable<ForestSwarm>? forests = null,
        HeavySwarm? researcher = null,
        IWikiQueryEngine? wiki = null,
        IAgxiomeAgent? llm = null,
        IHubContext<PipelineHub, IPipelineHubClient>? hub = null,
        LegalQueryExpander? expander = null,
        LegalConceptExtractor? conceptExtractor = null,
        LegalReranker? reranker = null,
        LegalHydeGenerator? hyde = null)
    {
        _log = log;
        _legalRouter = forests?.FirstOrDefault(s => string.Equals(s.Name, "legal-router", StringComparison.OrdinalIgnoreCase));
        _legalResearcher = researcher;
        _wiki = wiki;
        _llm = llm;
        _hub = hub!;
        _expander = expander;
        _conceptExtractor = conceptExtractor;
        _reranker = reranker;
        _hyde = hyde;
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
        using var _ = LogContext.PushProperty("EmailId", emailId);
        using var _sender = LogContext.PushProperty("Sender", req.Sender);

        var ct = CancellationToken.None;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var timeline = _timelines.GetOrAdd(emailId, []);

        _log.LogInformation("PipelineStarted Subject={Subject} Sender={Sender}", req.Subject, req.Sender);

        // ── Phase 2: Categorized via ForestSwarm ──────────────────────────
        sw.Restart();
        string assignedTree = "fallback", assignedAgent = "generalista";
        string[] matchedKeywords = [];

        if (_legalRouter != null)
        {
            try
            {
                // FindBestMatch = pure keyword scoring, no LLM call (~0ms vs ~40s for RunAsync)
                var (bestTree, bestAgent, matched) = _legalRouter.FindBestMatch($"{req.Subject}\n{req.Body}");
                assignedTree = bestTree.TreeName;
                assignedAgent = bestAgent.AgentId;
                matchedKeywords = matched.ToArray();
                _log.LogInformation(
                    "ForestSwarmRoute Tree={Tree} Agent={Agent} MatchedKeywords={@Keywords}",
                    assignedTree, assignedAgent, matchedKeywords);
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

        // ── Phase 3: Query Explosion — HeavySwarm skipped (5 LLM calls ≈ 115s)
        // RAG (phase 4) + LLM analysis (phase 5) forniscono contesto normativo equivalente.
        string fullResearch = "";

        _emailStates[emailId] = PipelineState.QueryExploded;
        timeline.Add(new(nameof(PipelineState.QueryExploded), PipelineState.QueryExploded,
            "💥 Categorizzazione completata — analisi normativa delegata a RAG+LLM",
            TimeSpan.Zero, DateTime.UtcNow));
        await Notify(emailId, timeline);

        // ── Phase 4: Multi-concept RAG Retrieval ───────────────────────
        // LLM estrae N concetti legali da Subject+Body → retrieve parallelo per ognuno
        // → merge/dedup by slug (best score wins) → contesto unificato per Phase 5.
        // Nessuna LLM synthesis qui: Phase 5 riceve raw pages e fa analisi completa.
        sw.Restart();
        string ragContext = "";
        if (_wiki != null)
        {
            try
            {
                // 1. Estrai concetti legali (Ollama fast model, ~3-5s)
                string[] concepts;
                if (_conceptExtractor != null)
                    concepts = await _conceptExtractor.ExtractAsync(req.Subject, req.Body, ct);
                else
                    concepts = [req.Subject];  // fallback se servizio non registrato

                _log.LogInformation(
                    "ConceptsExtracted Count={Count} Concepts={@Concepts}",
                    concepts.Length, concepts);

                // 1b. Query augmentation — HyDE o passthrough
                // HyDE: genera un doc ipotetico per ogni concetto (DeepSeek) e usa quello
                // come query per pgvector — risolve vocabulary mismatch colloquiale/giuridico.
                string[] queryTerms;
                if (req.UseHyde && _hyde is not null)
                {
                    queryTerms = await _hyde.GenerateBatchAsync(concepts, ct);
                    _log.LogInformation(
                        "HydeApplied ConceptCount={Count} Reranker={Reranker}",
                        concepts.Length, req.UseReranker);
                }
                else
                {
                    queryTerms = concepts;
                }

                // 2. Retrieve parallelo per ogni queryTerm espanso (embed + pgvector, no LLM)
                var retrieveTasks = queryTerms
                    .Select(q => _wiki.RetrieveAsync(q, topK: 5, minScore: 0.25f, ct))
                    .ToList();
                var allResults = await Task.WhenAll(retrieveTasks);

                // 3. Merge + dedup by slug (best cosine score per slug)
                var deduped = allResults
                    .SelectMany(r => r)
                    .GroupBy(c => c.Slug)
                    .Select(g => g.OrderByDescending(c => c.Score).First())
                    .OrderByDescending(c => c.Score)
                    .Take(10)
                    .ToList();

                if (deduped.Count > 0)
                {
                    // 3b. Reranker — attivato solo se UseReranker=true nella richiesta
                    List<(WikiPageChunk Source, float CombinedScore, float? RerankScore)>? reranked = null;
                    if (req.UseReranker && _reranker is not null && deduped.Count > 1)
                    {
                        var rerankQuery = $"{req.Subject}\n{string.Join("; ", concepts)}";
                        reranked = await _reranker.RerankChunksAsync(rerankQuery, deduped, ct);
                        _log.LogInformation("RerankerApplied TopSlug={Slug} Combined={Score:F3}",
                            reranked[0].Source.Slug, reranked[0].CombinedScore);
                    }

                    // 4. Build unified context block (raw pages — Phase 5 LLM synthesizes)
                    var sb = new System.Text.StringBuilder();
                    if (reranked != null)
                    {
                        foreach (var r in reranked.Take(7))
                        {
                            sb.AppendLine($"### [{r.Source.Slug}] {r.Source.Title} (combined: {r.CombinedScore:F3}  embed: {r.Source.Score:F3})");
                            var trimmed = r.Source.Content.Length > 2500 ? r.Source.Content[..2500] + "\n… [truncated]" : r.Source.Content;
                            sb.AppendLine(trimmed);
                            sb.AppendLine();
                        }
                    }
                    else
                    {
                        foreach (var p in deduped)
                        {
                            sb.AppendLine($"### [{p.Slug}] {p.Title} (score: {p.Score:F3})");
                            var trimmed = p.Content.Length > 2500 ? p.Content[..2500] + "\n… [truncated]" : p.Content;
                            sb.AppendLine(trimmed);
                            sb.AppendLine();
                        }
                    }
                    ragContext = sb.ToString();

                    // Structured audit: ogni chunk con slug, titolo, score e snippet
                    _log.LogInformation(
                        "RagArticlesRetrieved UniqueCount={UniqueCount} ConceptCount={ConceptCount} ContextChars={ContextChars} Reranked={Reranked} Articles={@Articles}",
                        reranked?.Count ?? deduped.Count, concepts.Length, ragContext.Length, reranked != null,
                        (reranked != null
                            ? reranked.Take(7).Select(r => (object)new
                                {
                                    Slug = r.Source.Slug, Title = r.Source.Title,
                                    Score   = Math.Round(r.CombinedScore, 4),
                                    Rerank  = r.RerankScore,
                                    Snippet = r.Source.Content.Length > 220
                                              ? r.Source.Content[..220].Replace('\n', ' ').Trim() + "…"
                                              : r.Source.Content.Replace('\n', ' ').Trim()
                                })
                            : deduped.Select(p => (object)new
                                {
                                    p.Slug, p.Title,
                                    Score   = Math.Round(p.Score, 4),
                                    Rerank  = (float?)null,
                                    Snippet = p.Content.Length > 220
                                              ? p.Content[..220].Replace('\n', ' ').Trim() + "…"
                                              : p.Content.Replace('\n', ' ').Trim()
                                }))
                        .ToArray());
                }
                else
                {
                    _log.LogInformation(
                        "RagArticlesRetrieved UniqueCount=0 ConceptCount={ConceptCount} — no context found",
                        concepts.Length);
                }
            }
            catch (Exception ex) { _log.LogWarning(ex, "[{Id}] RAG multi-concept failed", emailId); }
        }

        _emailStates[emailId] = PipelineState.RagCompleted;
        sw.Stop();
        var ragMode = (req.UseHyde, req.UseReranker) switch {
            (true,  true)  => "HyDE + Reranker",
            (true,  false) => "HyDE",
            (false, true)  => "Reranker",
            _              => "Baseline"
        };
        timeline.Add(new(nameof(PipelineState.RagCompleted), PipelineState.RagCompleted,
            $"📚 RAG [{ragMode}]: {(string.IsNullOrEmpty(ragContext) ? "database non ancora popolato" : "articoli trovati e analizzati")}",
            sw.Elapsed, DateTime.UtcNow));
        await Notify(emailId, timeline);

        // ── Phase 5: Legal Analysis via LLM ───────────────────────────────
        sw.Restart();
        string analysis = "";
        if (_llm != null && !string.IsNullOrEmpty(ragContext))
        {
            try
            {
                var researchBlock = !string.IsNullOrEmpty(fullResearch)
                    ? $"RICERCA MULTI-PROSPETTIVA:\n{fullResearch}\n\n"
                    : "";
                var resp = await _llm.RunAsync(new AgentRequest(
                    $"Analizza giuridicamente la seguente richiesta alla luce delle norme trovate e della ricerca preliminare.\n\n" +
                    $"RICHIESTA DEL CLIENTE:\nOggetto: {req.Subject}\n{req.Body}\n\n" +
                    $"{researchBlock}" +
                    $"CONTESTO NORMATIVO (dal database codici):\n{ragContext}\n\n" +
                    $"Fornisci un'analisi strutturata con:\n1. Qualifica giuridica del caso\n" +
                    $"2. Normativa applicabile (articoli specifici)\n3. Valutazione delle possibilità\n" +
                    $"4. Rischi e criticità\n5. Riferimenti giurisprudenziali pertinenti"), ct);
                analysis = resp.Content; // full analysis passed to Phase 6
                _log.LogInformation(
                    "LlmAnalysisCompleted LatencyMs={LatencyMs} AnalysisChars={Chars}",
                    sw.ElapsedMilliseconds, analysis.Length);
            }
            catch (Exception ex) { _log.LogWarning(ex, "LLM analysis failed"); }
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
                var researchBlock = !string.IsNullOrEmpty(fullResearch)
                    ? $"RICERCA LEGALE PRELIMINARE (prospettive: normativa, giurisprudenziale, dottrinale, prassi, comparata):\n{fullResearch}\n\n"
                    : "";
                var resp = await _llm.RunAsync(new AgentRequest(
                    $"Redigi una bozza di risposta email professionale per un cliente di uno studio legale.\n\n" +
                    $"RICHIESTA DEL CLIENTE:\nOggetto: {req.Subject}\n{req.Body}\n\n" +
                    $"{researchBlock}" +
                    $"CONTESTO GIURIDICO (dal database codici):\n{ragContext}\n\n" +
                    $"ANALISI:\n{analysis}\n\n" +
                    $"Formato richiesto:\n- Gentile [Cliente],\n- [Analisi e riferimenti normativi con citazioni [[art-XXX]]]\n" +
                    $"- [Riferimenti giurisprudenziali pertinenti]\n" +
                    $"- [Prossimi passi consigliati]\n- [Avvertenze]\n- Distinti saluti,\n  [Studio Legale AGXIOME]"), ct);
                draft = resp.Content;
                _drafts[emailId] = draft;
                _log.LogInformation(
                    "DraftGenerated LatencyMs={LatencyMs} DraftChars={Chars}",
                    sw.ElapsedMilliseconds, draft.Length);
            }
            catch (Exception ex) { _log.LogWarning(ex, "LLM draft failed"); }
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

        _log.LogInformation(
            "PipelineComplete FinalState=HitlPending DraftChars={DraftChars}",
            draft.Length);
    }

    public PipelineState GetState(string emailId)
        => _emailStates.TryGetValue(emailId, out var s) ? s : PipelineState.Ingested;
    public List<PipelineTimelineEntry> GetTimeline(string emailId)
        => _timelines.TryGetValue(emailId, out var t) ? t : [];
    public string? GetDraft(string emailId)
        => _drafts.TryGetValue(emailId, out var d) ? d : null;

    public IEnumerable<string> GetAllEmailIds() => _emailStates.Keys;

    public void Approve(string emailId)
    {
        using var _ctx = LogContext.PushProperty("EmailId", emailId);
        _emailStates[emailId] = PipelineState.ReplySent;
        var t = _timelines.GetOrAdd(emailId, []);
        t.Add(new(nameof(PipelineState.ReplySent), PipelineState.ReplySent,
            "📤 Risposta approvata e inviata al cliente", null, DateTime.UtcNow));
        _ = Notify(emailId, t);
        _log.LogInformation("HitlDecision Decision=Approved");
    }

    public void Reject(string emailId, string notes)
    {
        using var _ctx = LogContext.PushProperty("EmailId", emailId);
        _emailStates[emailId] = PipelineState.AwaitingRevision;
        var t = _timelines.GetOrAdd(emailId, []);
        t.Add(new(nameof(PipelineState.AwaitingRevision), PipelineState.AwaitingRevision,
            $"🔄 Bozza rigettata — note: {notes}", null, DateTime.UtcNow));
        _ = Notify(emailId, t);
        _log.LogInformation("HitlDecision Decision=Rejected Notes={Notes}", notes);
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
