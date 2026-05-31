# AGXIOME — Demo Implementations & Examples

**Versione**: 0.0.1-beta | **Data**: 1 Giugno 2026 | **Totale demo**: 18

---

## Indice

1. [agxioma-single-agent](#1-agxioma-single-agent) — Agente API singolo con DeepSeek
2. [agxioma-agent-template](#2-agxioma-agent-template) — Template dotnet new
3. [agxioma-team](#3-agxioma-team) — ForestSwarm routing per dominio
4. [agxioma-agentic-builder](#4-agxioma-agentic-builder) — Generatore progetti da prompt LLM
5. [agxioma-code-reviewer](#5-agxioma-code-reviewer) — GrokSwarm code review a 3 specialisti
6. [agxioma-multi-agent-team](#6-agxioma-multi-agent-team) — Team multi-agente con routing
7. [agxioma-orchestrator](#7-agxioma-orchestrator) — HeavySwarm orchestrator di ricerca
8. [agxioma-stock-tracker](#8-agxioma-stock-tracker) — Tracker finanziario ForestSwarm
9. [agxioma-support-hub](#9-agxioma-support-hub) — Hub supporto clienti con tool
10. [agxioma-rag-pipeline](#10-agxioma-rag-pipeline) — Pipeline RAG con chunker + vector store
11. [agxioma-research-agent](#11-agxioma-research-agent) — Agente ricerca multi-fase
12. [agxioma-tool-agent](#12-agxioma-tool-agent) — Agente con tool multipli
13. [agxioma-hybrid-routing](#13-agxioma-hybrid-routing) — Routing ibrido (keyword + semantico)
14. [agxioma-llm-wiki](#14-agxioma-llm-wiki) — Wiki RAG con Ollama + Redis
15. [agxioma-legal-email](#15-agxioma-legal-email) — Pipeline email legali con SignalR
16. [agxioma-incident-pipeline](#16-agxioma-incident-pipeline) — Pipeline incident response
17. [agxioma-dynamic-skill-lab](#17-agxioma-dynamic-skill-lab) — Lab skill dinamiche
18. [agxioma-ocr-service](#18-agxioma-ocr-service) — Servizio OCR con Tesseract

---

## 1. agxioma-single-agent

**Tipo**: Web API | **Pattern**: Single Agent | **Complessità**: ★☆☆☆☆

### Descrizione
Il punto d'ingresso più semplice: un singolo agente AGXIOME esposto come Web API REST. Dimostra il pattern `AgxiomeBuilder` fluente con DeepSeek come LLM provider, gestione sessioni di conversazione multi-turno, e streaming SSE.

### Struttura
```
agxioma-single-agent/
├── Program.cs           ← Entry point: bootstrap agente + endpoint REST
├── AgentSettings.cs     ← Configurazione typed da appsettings.json
├── ConversationStore.cs ← Store in-memory per cronologia chat
└── appsettings.json     ← Configurazione agente e provider
```

### Pattern architetturale
```
Client HTTP → POST /chat → AgxiomeBuilder.Agent("single-agent")
                                .UseDeepSeek(apiKey, "deepseek-chat")
                                .WithSystemPrompt(...)
                                .WithAutoLoops(50)
                                .Build()
                           → agent.RunAsync(AgentRequest)
                           → JSON response
```

### Codice estratto

**Bootstrap agente con DeepSeek**:
```csharp
var agent = AgxiomeBuilder.Agent(cfg.Id)
    .UseDeepSeek(apiKey, cfg.Model ?? "deepseek-chat")
    .WithSystemPrompt(cfg.SystemPrompt)
    .WithAutoLoops(upperBound: cfg.MaxLoops)
    .Build();
```

**Endpoint chat con history**:
```csharp
app.MapPost("/chat", async (ChatRequest req, IAgxiomeAgent agent,
    ConversationStore store, CancellationToken ct) =>
{
    var history = store.Get(req.SessionId);
    var request = new AgentRequest(req.Prompt, History: history);
    var response = await agent.RunAsync(request, ct);
    store.Append(req.SessionId, req.Prompt, response.Content);
    return Results.Ok(new { response = response.Content });
});
```

**Streaming SSE**:
```csharp
app.MapGet("/chat/stream", async (string prompt, string sessionId,
    IAgxiomeAgent agent, HttpResponse res) =>
{
    res.ContentType = "text/event-stream";
    await foreach (var chunk in agent.RunStreamAsync(new AgentRequest(prompt)))
        await res.WriteAsync($"data: {JsonSerializer.Serialize(new { chunk.Content })}\n\n");
});
```

---

## 2. agxioma-agent-template

**Tipo**: Template dotnet new | **Pattern**: Single Agent | **Complessità**: ★☆☆☆☆

### Descrizione
Template installabile via `dotnet new` che genera un progetto AGXIOME minimal. Usa placeholder per provider e modello, pronti per essere sostituiti dall'utente. Il progetto output ha un endpoint `/chat` e un health check `/`.

### Struttura
```
agxioma-agent-template/
├── .template.config/
│   └── template.json          ← Metadati template dotnet new
├── Program.cs                 ← Template con placeholder LLMProviderType.PROVIDER_PLACEHOLDER
├── appsettings.json           ← Configurazione template
└── appsettings.Development.json
```

### Pattern architetturale
```
dotnet new agxioma-agent → Program.cs con placeholder → sostituisci provider → avvia
```

### Codice estratto
```csharp
var agent = AgxiomeBuilder.Agent("my-agent")
    .UseModel(LLMProviderType.PROVIDER_PLACEHOLDER, "MODEL_PLACEHOLDER")
    .WithSystemPrompt("You are a helpful AI assistant.")
    .WithAutoLoops(upperBound: 50)
    .Build();
```

---

## 3. agxioma-team

**Tipo**: Web API | **Pattern**: ForestSwarm | **Complessità**: ★★★☆☆

### Descrizione
Dimostra il pattern **ForestSwarm**: un team di agenti organizzati per dominio (billing, technical, sales, general) con routing automatico basato su keyword. Ogni "albero" della foresta gestisce un dominio specifico. Il client invia un messaggio e il ForestSwarm seleziona automaticamente l'agente migliore.

### Struttura
```
agxioma-team/
├── Program.cs    ← ForestSwarm con 4 alberi (billing, technical, sales, general)
└── appsettings.json
```

### Pattern architetturale
```
POST /chat → ForestSwarm("team")
                ├── Tree("billing")    → Agent("billing-specialist")    [keywords: invoice, payment, ...]
                ├── Tree("technical")  → Agent("technical-specialist")  [keywords: error, api, ...]
                ├── Tree("sales")      → Agent("sales-specialist")      [keywords: plan, pricing, ...]
                └── Tree("general")    → Agent("general-assistant")     [keywords: help, what, how, ...]
             → Risposta con: routedTo, tree, keywords, response, duration
```

### Codice estratto

**Registrazione ForestSwarm con 4 domini**:
```csharp
builder.Services.AddAgxiome()
    .UseProvider(provider, model)
    .AddForestSwarm("team", forest =>
    {
        forest.AddTree("billing")
            .AddAgent("billing-specialist", "You handle invoice, payment, refund questions.")
            .WithKeywords("invoice", "payment", "refund", "subscription", "billing");

        forest.AddTree("technical")
            .AddAgent("technical-specialist", "You diagnose API errors and performance issues.")
            .WithKeywords("error", "bug", "api", "auth", "timeout", "performance");

        forest.AddTree("sales")
            .AddAgent("sales-specialist", "You explain plans, pricing, and enterprise options.")
            .WithKeywords("plan", "pricing", "trial", "enterprise", "upgrade");

        forest.AddTree("general")
            .AddAgent("general-assistant", "You handle general questions.")
            .WithKeywords("help", "question", "explain", "what", "how");
    });
```

**Endpoint con metadata di routing**:
```csharp
app.MapPost("/chat", async (ChatRequest req, ForestSwarm team) =>
{
    var result = await team.RunAsync(req.Message);
    return Results.Ok(new
    {
        routedTo  = result.SelectedAgentId,
        tree      = result.SelectedTreeName,
        keywords  = result.MatchedKeywords,
        response  = result.Response,
        duration  = result.Duration.TotalMilliseconds
    });
});
```

---

## 4. agxioma-agentic-builder

**Tipo**: CLI / Web API | **Pattern**: Agentic Code Generation | **Complessità**: ★★★★★

### Descrizione
Il generatore di progetti più avanzato: usa LLM per generare un intero progetto software da una specifica testuale. Pipeline a 3 layer:
1. **Manifest Generation**: LLM interpreta il prompt e genera un `ProjectManifest`
2. **Scaffolding**: Genera file `.cs`, `.csproj`, `.json` da template
3. **Sandbox Build-Fix Loop**: Compila, rileva errori, li fa correggere al LLM, riprova (max 5 tentativi)

### Struttura
```
agxioma-agentic-builder/
├── Program.cs              ← Entry point CLI con parsing argomenti
├── GenRequest.cs           ← Modello richiesta generazione
├── prompt.md               ← Specifica di default (IncidentPlatform)
├── temp_prompt*.txt        ← Varianti di prompt per test
└── appsettings.json
```

### Pattern architetturale
```
Prompt testuale ──→ LLM genera ProjectManifest ──→ Scaffolder genera file
                                                         │
                                                    Build → Errori?
                                                    Sì → LLM fix → Retry (max 5)
                                                    No  → Smoke test → ✅
```

### Codice estratto

**Pipeline di generazione completa**:
```csharp
var generator = AGXIOME.Core.Generator.AGXIOME.Create()
    .GenerateProject("IncidentPlatform")
    .Prompt(promptText)
    .UseRegisteredAgentFactory(registeredAgentFactory)
    .UseManifestFromPrompt()       // LLM genera il manifest dallo prompt
    .OutputTo(outputPath)
    .WithMaxAttempts(maxAttempts);

if (useSandbox)
    generator.WithSandboxBuild(project => project.CompileAndFix());

var manifest = await generator.RunAsync();
```

**CLI usage**:
```bash
dotnet run -- --prompt "Create a REST API for managing support tickets"
dotnet run -- --prompt-file spec.md --sandbox --output ../my-project
dotnet run -- --serve   # modalità API server
```

---

## 5. agxioma-code-reviewer

**Tipo**: Web API | **Pattern**: GrokSwarm (Captain + Specialists) | **Complessità**: ★★★★☆

### Descrizione
Dimostra il pattern **GrokSwarm** applicato alla code review automatica. Un agente "Captain" decompone la review in domande specifiche, 3 specialisti analizzano aspetti diversi del codice, e il Captain sintetizza il report finale.

### Struttura
```
agxioma-code-reviewer/
├── Program.cs        ← GrokSwarm con 4 agenti
├── AgentServices.cs  ← Provider resolver helper
├── AgentSettings.cs  ← Configurazione typed
└── appsettings.json
```

### Pattern architetturale
```
POST /review → GrokSwarm
    ├── Captain:    decompone task → assegna domande → sintetizza report
    ├── Harper:     bug detection + security (OWASP Top 10, null safety, race conditions)
    ├── Benjamin:   performance + logic correctness (O(n), memory, async/await)
    └── Lucas:      architecture review + alternative approaches
```

### Codice estratto

**Captain — orchestratore della review**:
```csharp
var captain = AgxiomeBuilder.Agent("captain")
    .UseModel(provider, model, apiKey)
    .WithSystemPrompt(
        "You are the Lead Code Review Architect. Your job is to:\n" +
        "1. Decompose code submissions into focused review questions.\n" +
        "2. Synthesize findings into a structured report.\n\n" +
        "Report format:\n" +
        "## Summary\n## Bugs & Security\n## Performance\n## Architecture\n" +
        "## Overall Score (1-10)\n## Action Items (sorted by priority)")
    .Build();
```

**Harper — bug hunter + security specialist**:
```csharp
var harper = AgxiomeBuilder.Agent("harper")
    .UseModel(provider, model, apiKey)
    .WithSystemPrompt(
        "Focus ONLY on: NullReferenceException, off-by-one errors, " +
        "SQL injection, XSS, path traversal, auth bypass (OWASP Top 10), " +
        "race conditions, unhandled exceptions.\n" +
        "For each: LOCATION, SEVERITY (Critical/High/Medium/Low), RECOMMENDATION.")
    .Build();
```

---

## 6. agxioma-multi-agent-team

**Tipo**: Web API | **Pattern**: Team multi-ruolo | **Complessità**: ★★★☆☆

### Descrizione
Team di agenti con ruoli specializzati (Product Manager, Architect, Developer, Tester) che collaborano su un task di sviluppo software. Dimostra il coordinamento multi-agente con routing basato sul ruolo.

### Struttura
```
agxioma-multi-agent-team/
├── Program.cs        ← Team con 4 agenti specializzati
├── AgentServices.cs  ← Provider resolver
└── appsettings.json
```

---

## 7. agxioma-orchestrator

**Tipo**: Web API | **Pattern**: HeavySwarm | **Complessità**: ★★★★☆

### Descrizione
Dimostra il pattern **HeavySwarm** per ricerca approfondita: genera 5 domande di ricerca su un topic, le assegna a worker in parallelo, e sintetizza i risultati in un report strutturato con Executive Summary, Key Findings, Risk Factors, Opportunities, e Conclusion.

### Struttura
```
agxioma-orchestrator/
├── Program.cs    ← HeavySwarm con parallel research + synthesis
└── appsettings.json
```

### Pattern architetturale
```
POST /research → HeavySwarm
    ├── Phase 1: GenerateQuestions → 5 domande focalizzate
    ├── Phase 2: ParallelResearch  → workers rispondono in parallelo
    └── Phase 3: Synthesize        → report strutturato
```

### Codice estratto
```csharp
builder.Services.AddAgxiome()
    .UseProvider(provider, model)
    .AddHeavySwarm("researcher", heavy =>
    {
        heavy.WithWorkers(workers)
            .WithParallelResearch()
            .WithQuestionPrompt(
                "Generate 5 focused research questions. Cover: fundamentals, " +
                "context, risks, opportunities, and key metrics.")
            .WithSynthesisPrompt(
                "Synthesise into: 1. Executive Summary 2. Key Findings " +
                "3. Risk Factors 4. Opportunities 5. Conclusion");
    });
```

---

## 8. agxioma-stock-tracker

**Tipo**: Web API | **Pattern**: ForestSwarm | **Complessità**: ★★★★☆

### Descrizione
Tracker finanziario completo con ForestSwarm che instrada le richieste per asset class (stock, crypto, ETF). Include SQLite per persistenza prezzi, SignalR per aggiornamenti realtime, e analisi SWOT automatizzata.

### Struttura
```
agxioma-stock-tracker/
├── Program.cs           ← Entry point: ForestSwarm + security + SignalR
├── Swarms/
│   └── StockTrackerSwarms.cs  ← Definizione alberi ForestSwarm
├── Storage/
│   ├── PriceStore.cs          ← SQLite per prezzi storici
│   └── AnalysisJobStore.cs    ← Coda job di analisi
├── MarketDataClient.cs        ← Client HTTP per dati mercato
└── appsettings.json
```

### Pattern architetturale
```
POST /analyze?ticker=AAPL → ForestSwarm
    ├── Tree("stocks")  → Agent("stock-analyst")   [keywords: AAPL, MSFT, ...]
    ├── Tree("crypto")  → Agent("crypto-analyst")  [keywords: BTC, ETH, ...]
    └── Tree("etf")     → Agent("etf-analyst")     [keywords: SPY, QQQ, ...]
                         → Analisi SWOT + P/E + momentum + persistenza SQLite
GET  /prices/{ticker}   → PriceStore (SQLite)
WS   /ws/prices          → SignalR streaming prezzi realtime
```

### Codice estratto
```csharp
var stockAnalyst = AgxiomeBuilder.Agent("stock-analyst")
    .UseModel(provider, model, apiKey)
    .WithSystemPrompt(
        "You are an expert equity analyst. When given a ticker, produce " +
        "a SWOT analysis, P/E and PEG context, sector comparison, and " +
        "30-day price momentum. State all caveats. Never give financial advice.")
    .Build();
```

---

## 9. agxioma-support-hub

**Tipo**: Web API + Frontend | **Pattern**: ForestSwarm + Tool | **Complessità**: ★★★★★

### Descrizione
Hub di supporto clienti enterprise-grade con ForestSwarm per routing, tool integrati (InvoiceStatus, Refund, Ticket, SystemStatus), CORS per frontend Next.js, e RBAC security. La demo più completa per pattern architetturale.

### Struttura
```
agxioma-support-hub/
├── Program.cs              ← Bootstrap agente + tool + CORS + security
├── Tools/
│   ├── GetInvoiceStatusTool.cs
│   ├── ProcessRefundTool.cs
│   ├── CreateTicketTool.cs
│   └── GetSystemStatusTool.cs
├── BackofficeRuntimeService.cs  ← Servizi backend
└── appsettings.json
```

### Pattern architetturale
```
Frontend Next.js (localhost:3000)
    │ CORS allow
    ▼
POST /support/chat → ForestSwarm("support")
    ├── Tree("billing")
    │   ├── Agent("billing-specialist")
    │   │   WithTool: GetInvoiceStatusTool, ProcessRefundTool
    │   └── Agent("escalation-agent")
    │       WithTool: GetSystemStatusTool, CreateTicketTool
    └── Tree("technical")
        ├── Agent("tech-specialist")
        │   WithTool: GetSystemStatusTool
        └── Agent("escalation-agent")
            WithTool: CreateTicketTool
```

### Codice estratto
```csharp
forest.AddTree("billing")
    .AddAgent("billing-specialist",
        "You are a billing support specialist. When a customer asks for a refund " +
        "on a paid invoice, use the process_refund tool. Always confirm the invoice ID.")
    .WithTool(new GetInvoiceStatusTool())
    .WithTool(new ProcessRefundTool())
    .WithKeywords("invoice", "payment", "refund", "subscription", "billing");
```

---

## 10. agxioma-rag-pipeline

**Tipo**: Web API | **Pattern**: RAG (Retrieval-Augmented Generation) | **Complessità**: ★★★★☆

### Descrizione
Pipeline completa di Retrieval-Augmented Generation: ingestione documenti, chunking (Recursive + Semantic), embedding via Ollama/OpenAI, vector search su pgvector/Qdrant, e generazione risposta contestualizzata.

### Struttura
```
agxioma-rag-pipeline/
├── Program.cs               ← Entry point
├── Ingest/
│   ├── DocumentIngester.cs  ← Pipeline ingestione documenti
│   └── ChunkingPipeline.cs  ← Strategie di chunking
├── Search/
│   ├── VectorSearchService.cs  ← Ricerca vettoriale
│   └── HybridSearchService.cs  ← Ricerca ibrida vettori + keyword
├── RAG/
│   └── RagAgent.cs          ← Agente RAG: retrieve → augment → generate
└── appsettings.json
```

### Pattern architetturale
```
Documento PDF/MD ──→ Reader ──→ Chunker (Recursive/Semantic)
                                    │
                              Embedder (Ollama/OpenAI)
                                    │
                              VectorStore (pgvector/Qdrant)
                                    │
Query utente ──→ Embed ──→ Vector Search (top-K chunks)
                                    │
                              LLM + contesto chunks ──→ Risposta
```

---

## 11. agxioma-research-agent

**Tipo**: Web API | **Pattern**: Multi-phase Research | **Complessità**: ★★★★☆

### Descrizione
Agente di ricerca multi-fase: ricerca web → estrazione contenuti → sintesi → report. Integra tool di web search (Tavily/Brave), scraping estrattivo, e generazione report strutturato con citazioni.

### Struttura
```
agxioma-research-agent/
├── Program.cs                 ← Entry point
├── ResearchOrchestrator.cs    ← Orchestratore fasi ricerca
├── Tools/
│   ├── WebSearchTool.cs       ← Tavily / Brave Search
│   ├── ContentExtractorTool.cs ← Estrazione contenuti web
│   └── CitationManager.cs     ← Gestione citazioni
├── Models/
│   └── ResearchReport.cs      ← Modello report strutturato
└── appsettings.json
```

---

## 12. agxioma-tool-agent

**Tipo**: Web API | **Pattern**: Agent + Multiple Tools | **Complessità**: ★★★☆☆

### Descrizione
Dimostra l'integrazione di tool multipli in un singolo agente. Include: file system tool, web search, code execution, API caller, e database query. L'agente decide autonomamente quale tool usare in base al prompt.

### Struttura
```
agxioma-tool-agent/
├── Program.cs             ← Bootstrap agente con 5+ tool
├── Tools/
│   ├── FileSystemTool.cs
│   ├── WebSearchTool.cs
│   ├── CodeExecutionTool.cs
│   ├── ApiCallerTool.cs
│   └── DatabaseQueryTool.cs
└── appsettings.json
```

---

## 13. agxioma-hybrid-routing

**Tipo**: Web API + Test Script | **Pattern**: Routing Ibrido | **Complessità**: ★★★★☆

### Descrizione
Sistema di routing ibrido che combina keyword matching con semantic similarity. Include test script PowerShell per validazione con 500 richieste. Dimostra il `SwarmPatternFactory` per creare swarm personalizzati.

### Struttura
```
agxioma-hybrid-routing/
├── Program.cs                ← Entry point + configurazione routing ibrido
├── Routing/
│   ├── HybridRouter.cs       ← Router keyword + semantico
│   ├── KeywordMatcher.cs     ← Matching basato su keyword
│   └── SemanticMatcher.cs    ← Matching via embedding similarity
├── test-t9-routing.ps1       ← Script test 500 richieste
└── appsettings.json
```

---

## 14. agxioma-llm-wiki

**Tipo**: Web API | **Pattern**: RAG Wiki | **Complessità**: ★★★★★

### Descrizione
Wiki aziendale potenziato da LLM con RAG search, Ollama embedding, Redis cache, e pipeline di ingestione documentale. Include `WikiLintEngine` per validazione qualità contenuti e `WikiQueryEngine` per ricerca semantica.

### Struttura
```
agxioma-llm-wiki/
├── Program.cs              ← Bootstrap: Ollama embedder + Redis + Wiki
├── WikiContracts.cs        ← Modelli dato wiki
├── WikiDatabase.cs         ← Persistenza documenti
├── WikiIngester.cs         ← Pipeline ingestione
├── WikiLintEngine.cs       ← Validazione qualità
├── WikiQueryEngine.cs      ← Ricerca semantica
├── WikiPageWriter.cs       ← Generazione pagine
├── Models.cs               ← Modelli dominio
└── WIKI_SCHEMA.md          ← Documentazione schema
```

### Pattern architetturale
```
Documento Markdown ──→ WikiIngester ──→ Chunker ──→ OllamaMxbaiEmbedder
                                                        │
                                                  Redis Vector Store
                                                        │
Query utente ──→ Embed ──→ WikiQueryEngine (Redis Search)
                                │
                          WikiPageWriter ──→ Pagina wiki con contesto RAG
                                │
                          WikiLintEngine ──→ Validazione qualità automatica
```

### Codice estratto
```csharp
// OllamaMxbaiEmbedder per embedding locale
builder.Services.AddSingleton<OllamaMxbaiEmbedder>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("ollama");
    return new OllamaMxbaiEmbedder(http, new OllamaEmbedderOptions
    {
        BaseUrl = wikiSettings.OllamaBaseUrl,
        Model   = wikiSettings.EmbedModel
    });
});

// Redis con graceful degradation
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    try
    {
        return ConnectionMultiplexer.Connect(
            builder.Configuration.GetConnectionString("Redis")
            ?? "localhost:6379,abortConnect=false,connectTimeout=3000");
    }
    catch { /* fallback */ }
});
```

---

## 15. agxioma-legal-email

**Tipo**: Web API + Frontend | **Pattern**: Pipeline + SignalR | **Complessità**: ★★★★★

### Descrizione
Pipeline di analisi email legali con classificazione automatica (GDPR, contratti, compliance), estrazione entità, SignalR per notifiche realtime, e frontend React. Include Dockerfile per deployment containerizzato.

### Struttura
```
agxioma-legal-email/
├── Program.cs              ← Bootstrap pipeline + SignalR hub
├── Models/
│   └── LegalEmailModels.cs ← Modelli dominio legale
├── Services/
│   ├── EmailClassifierService.cs  ← Classificazione GDPR/contratti
│   └── EntityExtractorService.cs  ← Estrazione entità (PII, date, importi)
├── Tools/
│   └── LegalAnalysisTools.cs      ← Tool analisi legale
├── Hubs/
│   └── LegalNotificationHub.cs    ← SignalR hub notifiche
├── frontend/               ← React frontend
├── Dockerfile              ← Container Docker
└── appsettings.json
```

---

## 16. agxioma-incident-pipeline

**Tipo**: Console App | **Pattern**: Pipeline | **Complessità**: ★★★☆☆

### Descrizione
Pipeline di gestione incidenti: detection → classification → assignment → resolution → post-mortem. Dimostra il pattern workflow a step con transizioni condizionali e stato persistente.

### Struttura
```
agxioma-incident-pipeline/
├── Program.cs                        ← Entry point orchestrazione
├── IncidentPipelineOrchestrator.cs   ← Orchestratore workflow
├── IncidentStore.cs                  ← Persistenza stato incidenti
├── Tools/
│   ├── IncidentClassifierTool.cs     ← Classificazione automatica
│   └── PostMortemGeneratorTool.cs    ← Generazione post-mortem
└── appsettings.json
```

---

## 17. agxioma-dynamic-skill-lab

**Tipo**: Web API + Test | **Pattern**: Dynamic Skills | **Complessità**: ★★★★★

### Descrizione
Laboratorio per skill dinamiche: compilazione runtime di skill C#, sandbox execution, validazione sicurezza, e test di robustness. Include `DynamicSkillRegistry`, `SkillAdapter`, e integrazione con `skills.sh` per download remoto.

### Struttura
```
agxioma-dynamic-skill-lab/
├── Program.cs              ← Bootstrap + security hardening
├── AgentServices.cs        ← Provider resolver
├── Engine/
│   └── DynamicSkillEngine.cs  ← Compilazione + esecuzione runtime
├── Orchestrator/
│   └── SkillOrchestrator.cs   ← Orchestrazione multi-skill
├── Storage/
│   └── SkillCacheStore.cs     ← Cache skill compilate
├── test-t8-robustness.ps1     ← Script test robustness
└── appsettings.json
```

---

## 18. agxioma-ocr-service

**Tipo**: Web API | **Pattern**: Tool Service | **Complessità**: ★★☆☆☆

### Descrizione
Servizio OCR basato su Tesseract integrato come tool AGXIOME. Espone endpoint REST per upload immagini e restituisce testo estratto. Dimostra il pattern di integrazione tool esterni come servizi AGXIOME.

### Struttura
```
agxioma-ocr-service/
├── Program.cs            ← Bootstrap OCR tool + endpoint REST
├── OcrTool.cs            ← Implementazione tool OCR
└── appsettings.json
```

### Pattern architetturale
```
POST /ocr (multipart image) → Tesseract OCR Engine → testo estratto
     oppure
Agente AGXIOME → RegisterTool(OcrTool) → agente può "vedere" testo da immagini
```

---

## Riepilogo Pattern Architetturali

| Pattern | Demo che lo usano | Descrizione |
|---------|-------------------|-------------|
| **Single Agent** | single-agent, agent-template, tool-agent | Un agente con tool registrati |
| **ForestSwarm** | team, stock-tracker, support-hub | Routing per dominio con keyword |
| **GrokSwarm** | code-reviewer | Captain + Specialists con decomposizione task |
| **HeavySwarm** | orchestrator | Ricerca parallela multi-fase con sintesi |
| **Multi-Agent Team** | multi-agent-team | Team con ruoli fissi e collaborazione |
| **RAG Pipeline** | rag-pipeline, llm-wiki | Retrieve → Augment → Generate |
| **Agentic Code Gen** | agentic-builder | LLM genera progetto → build → fix loop |
| **Hybrid Routing** | hybrid-routing | Keyword + semantic similarity routing |
| **Pipeline Workflow** | incident-pipeline, legal-email | Workflow a step con stato persistente |
| **Dynamic Skills** | dynamic-skill-lab | Compilazione runtime skill + sandbox |
| **Tool Service** | ocr-service | Tool esterno integrato come servizio |

## Stack Tecnologico per Demo

| Tecnologia | Demo |
|------------|------|
| **ASP.NET Core Minimal API** | Tutte le demo Web API |
| **SignalR** | stock-tracker, legal-email, support-hub |
| **SQLite** | stock-tracker |
| **Redis** | llm-wiki, support-hub |
| **Ollama (embedding)** | llm-wiki, rag-pipeline |
| **DeepSeek (LLM)** | single-agent, code-reviewer |
| **OpenAI (LLM)** | agentic-builder, support-hub |
| **Tesseract (OCR)** | ocr-service |
| **Docker** | legal-email |
| **React/Next.js** | support-hub, legal-email |
| **RBAC Security** | stock-tracker, llm-wiki, support-hub, legal-email |

---

*Documento generato il 1 Giugno 2026 — AGXIOME Team*
