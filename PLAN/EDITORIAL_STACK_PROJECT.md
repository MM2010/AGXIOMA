# EDITORIAL STACK PROJECT — AI Editorial Intelligence Runtime su AGXIOME

> **Versione**: 1.0 — 22 Maggio 2026  
> **Scope**: Sistema editoriale AI avanzato multi-verticale basato su AGXIOME  
> **Verticali target**: Tech, Finance, Health, Legal, Real Estate  
> **Principio**: Non generare contenuti isolati. Costruire un knowledge graph editoriale persistente.

---

## INDICE

1. [Architettura Generale](#1-architettura-generale)
2. [Pipeline AGXIOME Completa](#2-pipeline-agxiome-completa)
3. [Moduli del Sistema](#3-moduli-del-sistema)
4. [Schema Dati PostgreSQL](#4-schema-dati-postgresql)
5. [Redis Key Strategy](#5-redis-key-strategy)
6. [Modelli JSON](#6-modelli-json)
7. [Regole Operative](#7-regole-operative)
8. [Piano Generazione Contenuti Multilivello](#8-piano-generazione-contenuti-multilivello)
9. [Piano Aggiornamento Contenuti](#9-piano-aggiornamento-contenuti)
10. [Piano Monitoraggio Performance](#10-piano-monitoraggio-performance)
11. [Esempi Concreti per Verticale](#11-esempi-concreti-per-verticale)
12. [Componenti AGXIOME Necessari](#12-componenti-agxiome-necessari)
13. [Struttura Progetto .NET](#13-struttura-progetto-net)
14. [Decision & Simulation Layer](#14-decision--simulation-layer)
15. [Governance Lifecycle](#15-governance-lifecycle)
16. [Frontend Management Console](#16-frontend-management-console)

---

## 1. ARCHITETTURA GENERALE

### 1.1 Layering

```
┌────────────────────────────────────────────────────────────────────┐
│  PRESENTATION LAYER (ASP.NET Core / Razor / API)                   │
│  - Dynamic page rendering from PostgreSQL + Redis                  │
│  - JSON-LD / OpenGraph / hreflang injection at render time         │
└────────────────────┬───────────────────────────────────────────────┘
                     │
┌────────────────────▼───────────────────────────────────────────────┐
│  EDITORIAL INTELLIGENCE RUNTIME (AGXIOME-based)                    │
│  TrendScanner → TopicEvaluator → SerpAnalyzer → QueryExpansion     │
│  → TopicWikiBuilder → SemanticGraphBuilder → ClusterPlanner        │
│  → ContentGenerator → SEOOptimizer → EEATGovernance               │
│  → HITLWorkflow → PublishingRuntime                                │
└────────────────────┬───────────────────────────────────────────────┘
                     │
┌────────────────────▼───────────────────────────────────────────────┐
│  AGXIOME CORE LAYER                                                 │
│  HeavySwarm / GrokSwarm / ForestSwarm / AgxiomeDagWorkflow         │
│  IEmbedder / IVectorStore / IChunker / IReranker                   │
│  ToolkitRegistry / ToolRouter / SemanticToolSelector               │
│  ToolkitPolicyEnforcer / BudgetAwareExecutor                       │
└────────────────────┬───────────────────────────────────────────────┘
                     │
┌────────────────────▼───────────────────────────────────────────────┐
│  PERSISTENCE LAYER                                                  │
│  PostgreSQL (source of truth) + pgvector                           │
│  Redis (cache + pub/sub invalidazione)                             │
│  Elasticsearch/OpenSearch (full-text + semantic search)            │
│  HNSWLib / Qdrant (semantic retrieval per RAG interno)             │
└────────────────────────────────────────────────────────────────────┘
```

### 1.2 Principi architetturali

| Principio | Implementazione |
|-----------|----------------|
| **DB-first rendering** | Nessuna pagina è un file statico primario. Ogni pagina è una view sul DB |
| **Knowledge graph persistente** | Ogni topic → nodi Entity + Claim + Source nel grafo semantico |
| **HITL obbligatorio YMYL** | Contenuti Health/Legal/Finance non pubblicabili senza review umana |
| **Auditabilità totale** | Ogni write in `history.audit_log` con hash SHA-256 del payload |
| **Cache invalidazione selettiva** | Redis con chiavi strutturate per granularità articolo/cluster/verticale |
| **Nessun contenuto senza fonte** | Vincolo applicativo: `article_claims.source_id NOT NULL` |
| **Anti-cannibalizzazione** | Canonical enforced, similarity check prima della creazione di nuovi nodi |

---

## 2. PIPELINE AGXIOME COMPLETA

### 2.1 Entry Point: `TopicRunPipeline`

Ogni esecuzione è un **TopicRun** identificato da `run_id UUID`. La pipeline è implementata come `AgxiomeDagWorkflow` con nodi paralleli dove possibile.

```
[INPUT: vertical_id + topic + locale + run_config]
         │
         ▼
┌─────────────────┐
│  TrendScanner   │ ← ForestSwarm (routing per fonte: Google/Reddit/RSS)
│  Agent          │   Tools: WebSearchToolkit, RssToolkit, SerpApiToolkit
└────────┬────────┘
         │ trend_signals[]
         ▼
┌─────────────────┐
│ TopicEvaluator  │ ← HeavySwarm (6-phase scoring)
│ Agent           │   Phase 1: TrendScore + MomentumScore
└────────┬────────┘   Phase 2: PersistenceScore + CommercialValueScore
         │ topic_score_card   Phase 3-6: altri score
         ▼
┌─────────────────┐
│  SerpAnalyzer   │ ← GrokSwarm (captain + specialisti paralleli)
│  Agent          │   captain: coordina 20 risultati SERP
└────────┬────────┘   specialist_1: entity extraction
         │             specialist_2: claim extraction
         │ serp_results[]   specialist_3: gap analysis
         ▼
┌──────────────────────┐
│ QueryExpansionEngine │ ← SingleAgent (DeepSeek/GPT-4o)
│ Agent                │   Tool: SerpApiToolkit.RelatedQueries
└──────────┬───────────┘
           │ query_set[]
           ▼
┌──────────────────────┐
│  TopicWikiBuilder    │ ← HeavySwarm (6 fasi)
│  Agent               │   Fase 1: definizione + contesto
└──────────┬───────────┘   Fase 2: entità principali + relazioni
           │               Fase 3: fonti autorevoli + dati
           │ topic_wiki    Fase 4: controversie + gap
           ▼               Fase 5: angoli editoriali
┌──────────────────────┐   Fase 6: risk assessment
│ SemanticGraphBuilder │ ← SingleAgent con IVectorStore + pgvector
│ Agent                │   Costruisce nodi e relazioni
└──────────┬───────────┘   Similarity check anti-duplicazione
           │ graph_nodes[]
           ▼
┌──────────────────────┐
│   ClusterPlanner     │ ← GrokSwarm
│   Agent              │   captain: struttura cluster
└──────────┬───────────┘   specialist: slug + intent + schema markup
           │ cluster_plan
           ▼
┌──────────────────────┐
│  ContentGenerator    │ ← GrokSwarm per ogni nodo del cluster
│  Agent               │   captain: orchestrazione
└──────────┬───────────┘   specialist_1: hero + intro + sections
           │               specialist_2: FAQ + citations + schema
           │ content_nodes[]   specialist_3: linking interno
           ▼
┌──────────────────────┐
│   SEOOptimizer       │ ← SingleAgent
│   Agent              │   JSON-LD, hreflang, canonical, PAA blocks
└──────────┬───────────┘
           │ seo_metadata[]
           ▼
┌──────────────────────┐
│  EEATGovernance      │ ← HeavySwarm
│  Agent               │   Verifica autore, fonti, claim confidence
└──────────┬───────────┘   Assegna risk_level + disclaimer
           │ eeat_record
           ▼
┌──────────────────────┐
│   HITLWorkflow       │ ← ForestSwarm (routing per risk_level)
│   Agent              │   LOW → auto-approve
└──────────┬───────────┘   MEDIUM → review queue
           │               HIGH/YMYL → mandatory human review
           │ hitl_status
           ▼
┌──────────────────────┐
│  PublishingRuntime   │ ← Salva in PostgreSQL
│  Agent               │   Indicizza su Elasticsearch
└──────────────────────┘   Popola Redis cache
                           Invalida cache correlate
```

### 2.2 DAG Workflow Definition (AGXIOME)

```csharp
// EditorialRunPipeline.cs
public class EditorialRunPipeline : AgxiomeDagWorkflow
{
    public EditorialRunPipeline(IServiceProvider sp) : base(sp)
    {
        AddNode("trend_scan",      new TrendScannerAgent(sp));
        AddNode("topic_eval",      new TopicEvaluatorAgent(sp));
        AddNode("serp_analyze",    new SerpAnalyzerAgent(sp));
        AddNode("query_expand",    new QueryExpansionAgent(sp));
        AddNode("wiki_build",      new TopicWikiBuilderAgent(sp));
        AddNode("graph_build",     new SemanticGraphBuilderAgent(sp));
        AddNode("cluster_plan",    new ClusterPlannerAgent(sp));
        AddNode("content_gen",     new ContentGeneratorAgent(sp));
        AddNode("seo_optimize",    new SEOOptimizerAgent(sp));
        AddNode("eeat_govern",     new EEATGovernanceAgent(sp));
        AddNode("hitl_gate",       new HITLWorkflowAgent(sp));
        AddNode("publish",         new PublishingRuntimeAgent(sp));

        // Dipendenze sequenziali critiche
        AddEdge("trend_scan",   "topic_eval");
        AddEdge("topic_eval",   "serp_analyze");
        AddEdge("serp_analyze", "query_expand");
        AddEdge("query_expand", "wiki_build");
        AddEdge("wiki_build",   "graph_build");
        AddEdge("graph_build",  "cluster_plan");
        AddEdge("cluster_plan", "content_gen");
        AddEdge("content_gen",  "seo_optimize");
        AddEdge("seo_optimize", "eeat_govern");
        AddEdge("eeat_govern",  "hitl_gate");
        AddEdge("hitl_gate",    "publish");
    }
}
```

---

## 3. MODULI DEL SISTEMA

### 3.1 TrendScanner

**Tipo AGXIOME**: `ForestSwarm` (routing per fonte)  
**Tool richiesti**: `SearchKnowledgeToolkit`, `WebSocialToolkit`, `FileStorageToolkit` (RSS)

```csharp
public class TrendScannerAgent : ForestSwarmAgent
{
    // Routing: google_trends | serp | google_news | reddit | rss | competitor_sitemap
    protected override string[] GetRouteKeywords(string input) => 
        ExtractSources(input); // ["google_trends", "reddit", ...]

    // Specialisti
    // "google_trends" → GoogleTrendsSpecialist (SerpApiToolkit.Trends)
    // "serp"          → SerpSpecialist (SerpApiToolkit.Search)
    // "reddit"        → RedditSpecialist (WebSocialToolkit.Reddit)
    // "rss"           → RssSpecialist (WebSearchToolkit.Rss)
    // "competitor"    → CompetitorSpecialist (WebSearchToolkit.Sitemap)
}
```

**Output**: `TrendSignal[]` con campo `country_code`, `language`, `vertical_id`, `period`, `volume`, `velocity`

---

### 3.2 TopicEvaluator

**Tipo AGXIOME**: `HeavySwarm` (6 fasi di scoring parallelo)  
**Score prodotti**:

| Score | Formula indicativa | Range |
|-------|--------------------|-------|
| `TrendScore` | `search_volume_30d / search_volume_90d` normalizzato | 0-100 |
| `MomentumScore` | Derivata prima del volume trend | 0-100 |
| `PersistenceScore` | Volume minimo negli ultimi 12 mesi | 0-100 |
| `CommercialValueScore` | CPC medio × click-through-rate stimato | 0-100 |
| `CompetitionScore` | Inverso della domain authority mediana SERP | 0-100 |
| `ClusterDepthScore` | N. di query correlate con volume > threshold | 0-100 |
| `AuthorityPotentialScore` | Gap tra nostro DA e DA mediana SERP | 0-100 |
| `FreshnessScore` | % articoli SERP con data < 6 mesi | 0-100 |
| `RiskScore` | YMYL flag × sensibilità topic × controversie | 0-100 |

**Score composito**: `priority_score = (TrendScore × 0.15) + (MomentumScore × 0.15) + (CommercialValueScore × 0.20) + (CompetitionScore × 0.20) + (ClusterDepthScore × 0.15) + (AuthorityPotentialScore × 0.15)`

---

### 3.3 SerpAnalyzer

**Tipo AGXIOME**: `GrokSwarm` (captain + 3 specialisti)  
**Analizza**: top 20 risultati per query principale + top 10 per ogni query espansa

**Struttura di estrazione per ogni URL**:
```
URL → HTML fetch → CheerioParser
    → entity_extraction (NER via LLM)
    → claim_extraction (LLM)
    → heading_extraction (DOM)
    → schema_detection (JSON-LD parser)
    → link_analysis (internal/external ratio)
    → intent_classification (LLM: informational/navigational/commercial/transactional)
    → gap_identification (confronto con topic_wiki_context)
    → depth_score (word count × heading depth × citations count)
```

---

### 3.4 QueryExpansionEngine

**Tipo AGXIOME**: `SingleAgent`  
**Output**: `QuerySet` con 9 categorie di query

```
primary_queries:          ["best laptop 2025", "laptop buying guide"]
related_queries:          ["laptop vs desktop", "ultrabook recommendations"]
long_tail:                ["best budget laptop under 800 euros 2025"]
paa_questions:            ["Which laptop brand is most reliable?", ...]
comparison_queries:       ["MacBook Air vs Dell XPS 13", ...]
how_to_queries:           ["how to choose a laptop for video editing"]
definitional_queries:     ["what is a laptop", "laptop definition"]
commercial_queries:       ["buy laptop online", "laptop deals black friday"]
update_news_queries:      ["new laptop releases 2025", "laptop news this week"]
```

---

### 3.5 TopicWikiBuilder

**Tipo AGXIOME**: `HeavySwarm`  
**Output**: `TopicWikiPage` — JSON strutturato salvato in `semantic.topic_wiki_pages`  
**RAG**: usa `IVectorStore` + `IEmbedder` per recuperare fonti già indicizzate prima di generare

Struttura → vedi [Sezione 6.1 Modello JSON TopicWiki](#61-modello-json-topicwiki)

---

### 3.6 SemanticGraphBuilder

**Tipo AGXIOME**: `SingleAgent` con `IVectorStore` (pgvector)  
**Nodi**:

| Tipo | Campi chiave |
|------|-------------|
| `Topic` | `id, name, vertical_id, canonical_slug` |
| `Entity` | `id, name, type (person/org/place/concept), wikidata_id` |
| `Source` | `id, url, domain, authority_score, last_crawled` |
| `Claim` | `id, text, confidence, source_id, verified_at` |
| `Article` | `id, slug, topic_id, cluster_id, status` |
| `PageNode` | `id, article_id, render_key, cache_ttl` |
| `Cluster` | `id, topic_id, pillar_article_id, depth_levels` |
| `FAQ` | `id, question, answer, article_id, paa_match` |
| `PAA` | `id, question, source_serp_position, answer_snippet` |
| `DataPoint` | `id, value, unit, date, source_id` |
| `Author` | `id, name, bio, expertise_tags, authority_score` |
| `ReviewStatus` | `id, article_id, reviewer_id, status, reviewed_at` |

**Relazioni** (tabella `semantic.entity_relations`):

```
mentions, supports, contradicts, expands, depends_on, related_to,
has_subtopic, links_to, should_link_to, canonical_of, updates,
belongs_to_cluster
```

**Anti-duplicazione**: prima di creare un nodo `Entity`, similarity search su pgvector con threshold `cosine > 0.92`. Se esiste, merge invece di create.

---

### 3.7 ClusterPlanner

**Tipo AGXIOME**: `GrokSwarm`  
**Output**: `ClusterPlan` con struttura multilivello

```
Pillar Page (L0):
  target_query: "best laptop 2025"
  schema: Article + BreadcrumbList + FAQPage
  │
  ├── L1: "Best Laptops for Students 2025"
  │     target_query: "best student laptop"
  │     schema: Article + ItemList
  │
  ├── L1: "Best Laptops for Video Editing"
  │     target_query: "laptop for video editing"
  │     schema: Article + ItemList
  │
  │   ├── L2: "MacBook Pro M3 Review"
  │   │     target_query: "macbook pro m3 review"
  │   │     schema: Review
  │   │
  │   └── L2: "Dell XPS 15 vs MacBook Pro M3"
  │         target_query: "dell xps 15 vs macbook pro m3"
  │         schema: Article
  │
  └── L3 (FAQ/How-to):
        "How to check laptop battery health"
        "What is RAM in a laptop"
        "How to connect two monitors to a laptop"
```

**Per ogni nodo**:
```json
{
  "slug": "best-laptops-video-editing-2025",
  "title": "Best Laptops for Video Editing in 2025",
  "meta_title": "Best Laptops for Video Editing 2025 | Tested & Ranked",
  "meta_description": "We tested 18 laptops for video editing. Here are the top picks by budget, performance, and display quality.",
  "search_intent": "commercial_investigation",
  "target_query": "best laptop for video editing 2025",
  "secondary_queries": ["laptop video editing budget", "macbook vs windows video editing"],
  "entity_targets": ["MacBook Pro M3", "DaVinci Resolve", "Adobe Premiere"],
  "schema_markup": ["Article", "ItemList", "FAQPage"],
  "internal_links_in": ["best-laptop-2025", "macbook-pro-m3-review"],
  "internal_links_out": ["macbook-pro-m3-review", "dell-xps-15-review"],
  "priority": 2,
  "hitl_status": "pending_review"
}
```

---

### 3.8 ContentGenerator

**Tipo AGXIOME**: `GrokSwarm` (per-nodo)  
**Output**: `ContentNode` — struttura modulare salvata in colonne separate su DB

Moduli generati:
```
hero_answer       → risposta diretta ≤150 parole (SGE/AI Overview target)
intro             → contesto + hook + CTA implicita
sections[]        → H2/H3 strutturati, con fonti inline
faqs[]            → Q&A formattati per FAQPage JSON-LD
key_takeaways[]   → bullet list ≤5 punti
tables[]          → confronti, dati, comparazioni
citations[]       → citation map con source_id
pros_cons         → structured pros/cons block
comparison_blocks → side-by-side comparison
author_note       → competenza dell'autore sul topic
review_note       → nota del reviewer (se HITL completato)
last_updated      → data aggiornamento esplicita
schema_blocks[]   → JSON-LD precompilato
```

---

### 3.9 SEOOptimizer

**Tipo AGXIOME**: `SingleAgent`  
**Produce**:
- `seo_metadata` record in `seo.seo_metadata`
- `jsonld_blocks[]` record in `seo.jsonld_blocks`
- Validazione canonical, hreflang, duplicate title detection

---

### 3.10 EEATGovernance

**Tipo AGXIOME**: `HeavySwarm`  
**Produce**: `EEATRecord` per ogni articolo

```
expertise_level:     junior|mid|senior|expert
experience_signals:  first_person_account, case_studies, data_cited
authoritativeness:   domain_authority_author, citations_external
trustworthiness:     sources_count, claim_verification_rate, disclaimer_present
risk_level:          LOW|MEDIUM|HIGH|YMYL
hitl_required:       true/false (YMYL → sempre true)
```

---

### 3.11 HITLWorkflow

**Tipo AGXIOME**: `ForestSwarm` (routing per risk_level)  
**Stati**: `draft → pending_review → approved → rejected → needs_revision → published → scheduled_update → deprecated → merged`

**Routing**:
```
risk_level = LOW    → auto-approve → published
risk_level = MEDIUM → pending_review (SLA 48h) → human decision
risk_level = HIGH   → pending_review (SLA 24h) → human decision obbligatorio
risk_level = YMYL   → pending_review (SLA 12h) → subject_matter_expert_required
```

**Tool**: `ProductivityToolkit.Notion` o `ProductivityToolkit.Jira` per task creation automatica

---

### 3.12 PublishingRuntime

**Tipo AGXIOME**: `SingleAgent` + `IAgentStorageBridge`

```csharp
// Sequenza di publish
await postgresStore.SaveArticleAsync(article);           // source of truth
await elasticsearchStore.IndexArticleAsync(article);     // full-text search
await vectorStore.UpsertAsync(article.EmbeddingRecord);  // semantic retrieval
await redisCache.SetAsync(BuildPageKey(article), page);  // cache
await redisCache.PublishAsync("invalidate", articleId);  // pub/sub invalidazione
```

---

## 4. SCHEMA DATI POSTGRESQL

### Convenzioni

- UUIDs come PK (tipo `uuid DEFAULT gen_random_uuid()`)
- `created_at TIMESTAMPTZ DEFAULT NOW()`, `updated_at TIMESTAMPTZ`
- `content_hash TEXT` — SHA-256 del campo principale per audit/dedup
- Campi JSONB per dati semi-strutturati con indice GIN
- Schema logici separati per isolation

---

### Schema: `editorial`

```sql
-- Verticali editoriali
CREATE TABLE editorial.verticals (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name        TEXT NOT NULL UNIQUE,               -- 'tech', 'finance', 'health'
    display_name TEXT NOT NULL,
    locale      TEXT[] NOT NULL DEFAULT '{it}',
    config      JSONB NOT NULL DEFAULT '{}',        -- verticale-specific config
    active      BOOLEAN NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ
);
CREATE INDEX idx_verticals_name ON editorial.verticals(name);

-- Topic per verticale
CREATE TABLE editorial.topics (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    vertical_id     UUID NOT NULL REFERENCES editorial.verticals(id),
    name            TEXT NOT NULL,
    canonical_slug  TEXT NOT NULL UNIQUE,
    locale          TEXT NOT NULL DEFAULT 'it',
    is_ymyl         BOOLEAN NOT NULL DEFAULT FALSE,
    priority_score  NUMERIC(5,2),                  -- calcolato da TopicEvaluator
    status          TEXT NOT NULL DEFAULT 'active', -- active|archived|merged
    merged_into     UUID REFERENCES editorial.topics(id),
    metadata        JSONB NOT NULL DEFAULT '{}',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ
);
CREATE INDEX idx_topics_vertical ON editorial.topics(vertical_id);
CREATE INDEX idx_topics_slug ON editorial.topics(canonical_slug);
CREATE INDEX idx_topics_priority ON editorial.topics(priority_score DESC);
CREATE INDEX idx_topics_meta ON editorial.topics USING GIN(metadata);

-- Run del pipeline per un topic
CREATE TABLE editorial.topic_runs (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    topic_id        UUID NOT NULL REFERENCES editorial.topics(id),
    run_type        TEXT NOT NULL,     -- 'initial'|'refresh'|'competitor_update'
    status          TEXT NOT NULL DEFAULT 'running',
    started_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at    TIMESTAMPTZ,
    error_details   TEXT,
    pipeline_config JSONB NOT NULL DEFAULT '{}',
    agent_log       JSONB NOT NULL DEFAULT '[]', -- array di step log
    metrics         JSONB NOT NULL DEFAULT '{}'  -- token usage, duration, cost
);
CREATE INDEX idx_topic_runs_topic ON editorial.topic_runs(topic_id);
CREATE INDEX idx_topic_runs_status ON editorial.topic_runs(status);
```

---

### Schema: `editorial` — Trend e SERP

```sql
-- Segnali di trend da TrendScanner
CREATE TABLE editorial.trend_signals (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    topic_id        UUID NOT NULL REFERENCES editorial.topics(id),
    run_id          UUID NOT NULL REFERENCES editorial.topic_runs(id),
    source          TEXT NOT NULL,  -- 'google_trends'|'serp'|'reddit'|'rss'|'competitor'
    country_code    TEXT NOT NULL DEFAULT 'IT',
    language        TEXT NOT NULL DEFAULT 'it',
    period          TEXT NOT NULL,  -- '7d'|'30d'|'90d'|'12m'
    search_volume   INTEGER,
    trend_index     NUMERIC(5,2),   -- 0-100 Google Trends normalized
    velocity        NUMERIC(5,2),   -- variazione % rispetto al periodo precedente
    raw_data        JSONB NOT NULL DEFAULT '{}',
    captured_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_trend_signals_topic ON editorial.trend_signals(topic_id);
CREATE INDEX idx_trend_signals_source ON editorial.trend_signals(source, captured_at DESC);

-- Risultati SERP analizzati
CREATE TABLE editorial.serp_results (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    run_id          UUID NOT NULL REFERENCES editorial.topic_runs(id),
    query           TEXT NOT NULL,
    position        SMALLINT NOT NULL,
    url             TEXT NOT NULL,
    domain          TEXT NOT NULL,
    title           TEXT,
    author          TEXT,
    published_at    DATE,
    updated_at      DATE,
    word_count      INTEGER,
    headings        JSONB NOT NULL DEFAULT '[]',    -- [{level, text}]
    entities        JSONB NOT NULL DEFAULT '[]',    -- [{name, type, count}]
    claims          JSONB NOT NULL DEFAULT '[]',    -- [{text, supported_by}]
    cited_sources   JSONB NOT NULL DEFAULT '[]',
    viewpoints      JSONB NOT NULL DEFAULT '[]',
    info_gaps       JSONB NOT NULL DEFAULT '[]',
    search_intent   TEXT,   -- informational|navigational|commercial|transactional
    content_depth   SMALLINT,  -- 1-5 heuristic
    schema_types    TEXT[],    -- ['Article', 'FAQPage', ...]
    internal_links  JSONB NOT NULL DEFAULT '[]',
    external_links  JSONB NOT NULL DEFAULT '[]',
    content_hash    TEXT,
    crawled_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_serp_results_run ON editorial.serp_results(run_id);
CREATE INDEX idx_serp_results_domain ON editorial.serp_results(domain);
CREATE INDEX idx_serp_entities ON editorial.serp_results USING GIN(entities);
```

---

### Schema: `sources`

```sql
CREATE TABLE sources.source_documents (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    url             TEXT NOT NULL UNIQUE,
    domain          TEXT NOT NULL,
    title           TEXT,
    author          TEXT,
    published_at    DATE,
    last_crawled    TIMESTAMPTZ,
    authority_score NUMERIC(4,2) DEFAULT 0,
    content_type    TEXT,      -- 'article'|'study'|'dataset'|'official'|'wiki'
    language        TEXT NOT NULL DEFAULT 'it',
    content_hash    TEXT,
    raw_content     TEXT,
    extracted_data  JSONB NOT NULL DEFAULT '{}',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_sources_domain ON sources.source_documents(domain);
CREATE INDEX idx_sources_authority ON sources.source_documents(authority_score DESC);
CREATE UNIQUE INDEX idx_sources_url ON sources.source_documents(url);
```

---

### Schema: `semantic`

```sql
-- Entità estratte
CREATE TABLE semantic.extracted_entities (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name            TEXT NOT NULL,
    entity_type     TEXT NOT NULL,  -- person|org|place|concept|product|event|law
    wikidata_id     TEXT,
    description     TEXT,
    aliases         TEXT[] NOT NULL DEFAULT '{}',
    vertical_id     UUID REFERENCES editorial.verticals(id),
    embedding       vector(1536),   -- pgvector
    metadata        JSONB NOT NULL DEFAULT '{}',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_entities_name ON semantic.extracted_entities(name);
CREATE INDEX idx_entities_type ON semantic.extracted_entities(entity_type);
CREATE INDEX idx_entities_embedding ON semantic.extracted_entities 
    USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100);

-- Relazioni tra entità e nodi
CREATE TABLE semantic.entity_relations (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_id       UUID NOT NULL,
    source_type     TEXT NOT NULL,  -- entity|topic|article|claim|source
    target_id       UUID NOT NULL,
    target_type     TEXT NOT NULL,
    relation_type   TEXT NOT NULL,  -- mentions|supports|contradicts|expands|...
    weight          NUMERIC(4,3) DEFAULT 1.0,
    metadata        JSONB NOT NULL DEFAULT '{}',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_relations_source ON semantic.entity_relations(source_id, source_type);
CREATE INDEX idx_relations_target ON semantic.entity_relations(target_id, target_type);
CREATE INDEX idx_relations_type ON semantic.entity_relations(relation_type);

-- Wiki semantica per topic
CREATE TABLE semantic.topic_wiki_pages (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    topic_id        UUID NOT NULL REFERENCES editorial.topics(id),
    run_id          UUID NOT NULL REFERENCES editorial.topic_runs(id),
    version         INTEGER NOT NULL DEFAULT 1,
    content         JSONB NOT NULL,   -- TopicWikiJson (vedi sezione 6.1)
    content_hash    TEXT NOT NULL,
    embedding       vector(1536),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (topic_id, version)
);
CREATE INDEX idx_wiki_topic ON semantic.topic_wiki_pages(topic_id, version DESC);
```

---

### Schema: `editorial` — Cluster e Contenuti

```sql
-- Cluster editoriali
CREATE TABLE editorial.content_clusters (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    topic_id        UUID NOT NULL REFERENCES editorial.topics(id),
    run_id          UUID NOT NULL REFERENCES editorial.topic_runs(id),
    name            TEXT NOT NULL,
    pillar_slug     TEXT,
    depth_levels    SMALLINT NOT NULL DEFAULT 3,
    status          TEXT NOT NULL DEFAULT 'planned',
    cluster_data    JSONB NOT NULL DEFAULT '{}',  -- struttura completa del cluster
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ
);
CREATE INDEX idx_clusters_topic ON editorial.content_clusters(topic_id);

-- Nodi del contenuto (ogni pagina/articolo nel cluster)
CREATE TABLE editorial.content_nodes (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    cluster_id      UUID NOT NULL REFERENCES editorial.content_clusters(id),
    topic_id        UUID NOT NULL REFERENCES editorial.topics(id),
    parent_id       UUID REFERENCES editorial.content_nodes(id),
    slug            TEXT NOT NULL UNIQUE,
    level           SMALLINT NOT NULL,   -- 0=pillar, 1=L1, 2=L2, 3=L3
    node_type       TEXT NOT NULL,       -- pillar|satellite|support|faq|how-to|comparison|definition
    title           TEXT NOT NULL,
    meta_title      TEXT,
    meta_description TEXT,
    search_intent   TEXT,
    target_query    TEXT,
    secondary_queries TEXT[] NOT NULL DEFAULT '{}',
    entity_targets  TEXT[] NOT NULL DEFAULT '{}',
    schema_markup   TEXT[] NOT NULL DEFAULT '{}',
    priority        SMALLINT NOT NULL DEFAULT 5,
    hitl_status     TEXT NOT NULL DEFAULT 'draft',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ
);
CREATE INDEX idx_content_nodes_cluster ON editorial.content_nodes(cluster_id);
CREATE INDEX idx_content_nodes_slug ON editorial.content_nodes(slug);
CREATE INDEX idx_content_nodes_hitl ON editorial.content_nodes(hitl_status);
CREATE INDEX idx_content_nodes_parent ON editorial.content_nodes(parent_id);

-- Articoli generati
CREATE TABLE editorial.articles (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    node_id         UUID NOT NULL REFERENCES editorial.content_nodes(id),
    topic_id        UUID NOT NULL REFERENCES editorial.topics(id),
    run_id          UUID NOT NULL REFERENCES editorial.topic_runs(id),
    version         INTEGER NOT NULL DEFAULT 1,
    status          TEXT NOT NULL DEFAULT 'draft',
    word_count      INTEGER,
    reading_time_min SMALLINT,
    hero_answer     TEXT,
    intro           TEXT,
    conclusion      TEXT,
    key_takeaways   JSONB NOT NULL DEFAULT '[]',
    author_note     TEXT,
    review_note     TEXT,
    last_updated    DATE,
    content_hash    TEXT,
    embedding       vector(1536),
    metadata        JSONB NOT NULL DEFAULT '{}',
    published_at    TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ
);
CREATE INDEX idx_articles_node ON editorial.articles(node_id);
CREATE INDEX idx_articles_status ON editorial.articles(status);
CREATE INDEX idx_articles_published ON editorial.articles(published_at DESC);
CREATE INDEX idx_articles_embedding ON editorial.articles
    USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100);

-- Sezioni degli articoli (modulare)
CREATE TABLE editorial.article_sections (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    article_id  UUID NOT NULL REFERENCES editorial.articles(id) ON DELETE CASCADE,
    position    SMALLINT NOT NULL,
    heading     TEXT,
    heading_level SMALLINT,  -- 2=H2, 3=H3
    content     TEXT NOT NULL,
    section_type TEXT NOT NULL DEFAULT 'text',  -- text|table|pros_cons|comparison|code
    metadata    JSONB NOT NULL DEFAULT '{}',
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_sections_article ON editorial.article_sections(article_id, position);

-- FAQ degli articoli
CREATE TABLE editorial.article_faqs (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    article_id  UUID NOT NULL REFERENCES editorial.articles(id) ON DELETE CASCADE,
    question    TEXT NOT NULL,
    answer      TEXT NOT NULL,
    position    SMALLINT NOT NULL DEFAULT 0,
    paa_match   BOOLEAN NOT NULL DEFAULT FALSE,
    paa_source  TEXT,   -- URL della SERP da cui proviene la PAA
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_faqs_article ON editorial.article_faqs(article_id);

-- Claim con citation
CREATE TABLE editorial.article_claims (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    article_id  UUID NOT NULL REFERENCES editorial.articles(id) ON DELETE CASCADE,
    section_id  UUID REFERENCES editorial.article_sections(id),
    claim_text  TEXT NOT NULL,
    source_id   UUID NOT NULL REFERENCES sources.source_documents(id),
    confidence  NUMERIC(3,2) NOT NULL DEFAULT 0.80,  -- 0.00-1.00
    verified    BOOLEAN NOT NULL DEFAULT FALSE,
    verified_at TIMESTAMPTZ,
    claim_type  TEXT NOT NULL DEFAULT 'fact',  -- fact|statistic|quote|opinion
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_claims_article ON editorial.article_claims(article_id);
CREATE INDEX idx_claims_source ON editorial.article_claims(source_id);
```

---

### Schema: `seo`

```sql
-- Metadata SEO per ogni content node
CREATE TABLE seo.seo_metadata (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    node_id         UUID NOT NULL REFERENCES editorial.content_nodes(id) UNIQUE,
    canonical_url   TEXT,
    hreflang        JSONB NOT NULL DEFAULT '{}',  -- {"it": "...", "en": "..."}
    og_title        TEXT,
    og_description  TEXT,
    og_image        TEXT,
    og_type         TEXT DEFAULT 'article',
    twitter_card    TEXT DEFAULT 'summary_large_image',
    robots          TEXT DEFAULT 'index,follow',
    priority_sitemap NUMERIC(2,1) DEFAULT 0.7,
    changefreq      TEXT DEFAULT 'weekly',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ
);

-- JSON-LD blocks precompilati per render veloce
CREATE TABLE seo.jsonld_blocks (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    node_id     UUID NOT NULL REFERENCES editorial.content_nodes(id),
    schema_type TEXT NOT NULL,  -- Article|FAQPage|BreadcrumbList|Review|ItemList|DefinedTerm
    jsonld      JSONB NOT NULL,
    version     INTEGER NOT NULL DEFAULT 1,
    valid       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_jsonld_node ON seo.jsonld_blocks(node_id);
CREATE INDEX idx_jsonld_type ON seo.jsonld_blocks(schema_type);
```

---

### Schema: `editorial` — Internal Links

```sql
CREATE TABLE editorial.internal_links (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    from_node_id    UUID NOT NULL REFERENCES editorial.content_nodes(id),
    to_node_id      UUID NOT NULL REFERENCES editorial.content_nodes(id),
    anchor_text     TEXT,
    link_type       TEXT NOT NULL DEFAULT 'contextual',  -- contextual|nav|breadcrumb|related
    is_bidirectional BOOLEAN NOT NULL DEFAULT FALSE,
    status          TEXT NOT NULL DEFAULT 'planned',  -- planned|active|broken|canonical
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (from_node_id, to_node_id, link_type)
);
CREATE INDEX idx_links_from ON editorial.internal_links(from_node_id);
CREATE INDEX idx_links_to ON editorial.internal_links(to_node_id);
```

---

### Schema: `review`

```sql
CREATE TABLE review.hitl_reviews (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    article_id      UUID NOT NULL REFERENCES editorial.articles(id),
    reviewer_id     UUID,   -- NULL se non ancora assegnato
    reviewer_type   TEXT,   -- human|ai_assisted
    status          TEXT NOT NULL DEFAULT 'pending_review',
    -- draft|pending_review|approved|rejected|needs_revision|
    -- published|scheduled_update|deprecated|merged
    priority        TEXT NOT NULL DEFAULT 'normal',  -- urgent|high|normal|low
    sla_deadline    TIMESTAMPTZ,
    risk_level      TEXT NOT NULL DEFAULT 'LOW',    -- LOW|MEDIUM|HIGH|YMYL
    review_notes    TEXT,
    checklist       JSONB NOT NULL DEFAULT '{}',    -- checkpoints EEAT
    requires_expert BOOLEAN NOT NULL DEFAULT FALSE,
    expert_domain   TEXT,   -- 'medical'|'legal'|'financial'
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ,
    resolved_at     TIMESTAMPTZ
);
CREATE INDEX idx_reviews_article ON review.hitl_reviews(article_id);
CREATE INDEX idx_reviews_status ON review.hitl_reviews(status);
CREATE INDEX idx_reviews_sla ON review.hitl_reviews(sla_deadline) WHERE status IN ('pending_review');
```

---

### Schema: `publishing`

```sql
CREATE TABLE publishing.publishing_queue (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    article_id      UUID NOT NULL REFERENCES editorial.articles(id),
    action          TEXT NOT NULL,  -- publish|update|unpublish|schedule
    scheduled_for   TIMESTAMPTZ,
    executed_at     TIMESTAMPTZ,
    status          TEXT NOT NULL DEFAULT 'queued',  -- queued|processing|done|failed
    retry_count     SMALLINT NOT NULL DEFAULT 0,
    error_details   TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_queue_status ON publishing.publishing_queue(status, scheduled_for);

CREATE TABLE publishing.cache_invalidations (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    scope           TEXT NOT NULL,  -- article|cluster|vertical|topic|language
    scope_id        TEXT NOT NULL,  -- UUID o codice verticale/lingua
    reason          TEXT NOT NULL,
    cache_keys      TEXT[] NOT NULL DEFAULT '{}',
    executed_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_invalidations_scope ON publishing.cache_invalidations(scope, scope_id);

CREATE TABLE publishing.performance_snapshots (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    node_id         UUID NOT NULL REFERENCES editorial.content_nodes(id),
    snapshot_date   DATE NOT NULL DEFAULT CURRENT_DATE,
    organic_clicks  INTEGER DEFAULT 0,
    impressions     INTEGER DEFAULT 0,
    avg_position    NUMERIC(4,1),
    ctr             NUMERIC(5,4),
    bounce_rate     NUMERIC(5,4),
    avg_time_sec    INTEGER,
    conversions     INTEGER DEFAULT 0,
    data_source     TEXT NOT NULL DEFAULT 'gsc',  -- gsc|ga4|internal
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (node_id, snapshot_date, data_source)
);
CREATE INDEX idx_snapshots_node ON publishing.performance_snapshots(node_id, snapshot_date DESC);
```

---

### Schema: `history`

```sql
CREATE TABLE history.audit_log (
    id              BIGSERIAL PRIMARY KEY,
    entity_type     TEXT NOT NULL,
    entity_id       UUID NOT NULL,
    operation       TEXT NOT NULL,  -- INSERT|UPDATE|DELETE|STATUS_CHANGE|PUBLISH
    changed_by      UUID,           -- user_id o agent_id
    changed_by_type TEXT NOT NULL DEFAULT 'agent',  -- human|agent
    old_values      JSONB,
    new_values      JSONB,
    diff_keys       TEXT[] NOT NULL DEFAULT '{}',
    content_hash    TEXT,
    ip_address      INET,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_audit_entity ON history.audit_log(entity_type, entity_id);
CREATE INDEX idx_audit_created ON history.audit_log(created_at DESC);
-- Partitioned by month per performance su milioni di righe
-- CREATE TABLE history.audit_log PARTITION BY RANGE (created_at);
```

---

## 5. REDIS KEY STRATEGY

### 5.1 Struttura chiavi

```
{scope}:{vertical}:{locale}:{entity_type}:{id}:{variant}

Esempi:
page:tech:it:article:uuid-123:html
page:tech:it:article:uuid-123:meta
cluster:tech:it:cluster:uuid-456:tree
wiki:tech:it:topic:best-laptop:v3
links:tech:it:node:uuid-789:internal
jsonld:tech:it:node:uuid-789:FAQPage
```

### 5.2 TTL per tipo

| Tipo | TTL | Invalidazione |
|------|-----|---------------|
| `page:*:html` | 1h (YMYL: 15m) | Pub/Sub su `invalidate:page` |
| `page:*:meta` | 6h | Pub/Sub su `invalidate:page` |
| `cluster:*:tree` | 24h | On cluster update |
| `wiki:*` | 12h | On wiki rebuild |
| `links:*:internal` | 6h | On link graph update |
| `jsonld:*` | 24h | On SEO metadata update |
| `search:*` | 30m | No manual invalidation |
| `eeat:*` | 24h | On review status change |

### 5.3 Invalidazione selettiva

```
Channel: invalidate:article   → payload: {article_id, reason}
Channel: invalidate:cluster   → payload: {cluster_id, vertical_id}
Channel: invalidate:vertical  → payload: {vertical_id, locale}
Channel: invalidate:topic     → payload: {topic_id}
Channel: invalidate:entity    → payload: {entity_id} → invalida tutti gli articoli che la usano
Channel: invalidate:language  → payload: {locale} → invalida tutto per quella lingua
```

```csharp
// RedisInvalidationService.cs
public async Task InvalidateArticleAsync(Guid articleId)
{
    var keys = await _redis.SetMembersAsync($"article_keys:{articleId}");
    await _redis.KeyDeleteAsync(keys.Select(k => (RedisKey)k.ToString()).ToArray());
    await _redis.PublishAsync("invalidate:article", 
        JsonSerializer.Serialize(new { article_id = articleId, reason = "update" }));
}
```

### 5.4 Refresh automatico (Background Worker AGXIOME)

```csharp
// CacheRefreshWorker : BackgroundRunWorker
// Ogni 30 min: controlla chiavi con TTL < 5min → pre-fetch e refresh prima della scadenza
// Evita cache stampede: usa distributed lock (SET NX EX 30) prima del refresh
```

---

## 6. MODELLI JSON

### 6.1 Modello JSON: TopicWiki

```json
{
  "topic_id": "uuid",
  "topic_name": "Best Laptop 2025",
  "version": 3,
  "generated_at": "2025-05-22T10:00:00Z",
  "run_id": "uuid",
  "definition": {
    "short": "Un laptop (o notebook) è un computer portatile...",
    "extended": "...",
    "related_terms": ["notebook", "ultrabook", "Chromebook"]
  },
  "context": {
    "market_size": "USD 101B (2024, IDC)",
    "growth_rate": "3.2% CAGR 2024-2029",
    "key_trends": ["AI-powered chips", "OLED displays", "slim form factors"],
    "geographic_relevance": ["IT", "EU", "US"]
  },
  "timeline": [
    { "year": 1981, "event": "Primo laptop commerciale: Osborne 1" },
    { "year": 2024, "event": "Apple M3 Pro lancia la nuova era ARM su laptop" }
  ],
  "primary_entities": [
    { "name": "Apple MacBook Pro M3", "type": "product", "relevance": 0.95 },
    { "name": "Intel Core Ultra", "type": "product", "relevance": 0.85 }
  ],
  "secondary_entities": [
    { "name": "Thunderbolt 5", "type": "concept", "relevance": 0.70 }
  ],
  "semantic_relations": [
    { "from": "Apple MacBook Pro M3", "relation": "competes_with", "to": "Dell XPS 15" }
  ],
  "authoritative_sources": [
    { "url": "https://www.idc.com/...", "type": "study", "authority_score": 0.95 }
  ],
  "data_points": [
    { "value": "101B USD", "unit": "market_cap", "year": 2024, "source": "IDC" }
  ],
  "controversies": [
    { "topic": "right to repair", "summary": "...", "sides": ["pro", "con"] }
  ],
  "competitor_gaps": [
    "Nessun competitor copre il confronto tra laptop AI e laptop tradizionali per uso professionale",
    "Gap su dati di durata batteria in condizioni reali vs benchmark dichiarato"
  ],
  "editorial_angles": [
    { "angle": "AI-readiness per professionisti", "priority": 1, "target_audience": "professional" },
    { "angle": "Rapporto qualità-prezzo fascia 800-1200€", "priority": 2 }
  ],
  "factual_risks": [
    { "risk": "Prezzi in listino cambiano settimanalmente", "mitigation": "disclaimer data + fonte ufficiale" }
  ],
  "reviewer_notes": [
    "Verificare dati batteria con test indipendenti prima di pubblicare claims numerici"
  ]
}
```

---

### 6.2 Modello JSON: ContentNode

```json
{
  "node_id": "uuid",
  "article_id": "uuid",
  "slug": "best-laptops-video-editing-2025",
  "version": 1,
  "status": "approved",
  "hero_answer": "I migliori laptop per video editing nel 2025 sono il MacBook Pro M3 Pro (per macOS), il Dell XPS 15 (per Windows) e l'ASUS ProArt Studiobook 16 (per chi usa DaVinci Resolve su Windows). Tutti offrono display calibrati, RAM ≥32GB e GPU dedicata.",
  "intro": {
    "text": "...",
    "hook_type": "question",
    "cta_implicit": "scroll to comparison"
  },
  "sections": [
    {
      "position": 1,
      "heading": "Criteri di scelta per un laptop da video editing",
      "heading_level": 2,
      "content": "...",
      "type": "text",
      "entities_mentioned": ["RAM", "GPU", "display color gamut"],
      "claims": [
        { "text": "DaVinci Resolve richiede minimo 16GB RAM per 4K", "source_id": "uuid", "confidence": 0.93 }
      ]
    },
    {
      "position": 2,
      "heading": "Confronto: i 5 migliori laptop per video editing",
      "heading_level": 2,
      "content": "",
      "type": "table",
      "table_data": {
        "headers": ["Modello", "CPU", "RAM", "GPU", "Display", "Prezzo"],
        "rows": [
          ["MacBook Pro M3 Pro", "M3 Pro", "18GB", "18-core GPU", "Liquid Retina XDR", "2.499€"]
        ]
      }
    }
  ],
  "faqs": [
    {
      "question": "Quanta RAM serve per il video editing?",
      "answer": "Per editing 1080p è sufficiente 16GB. Per 4K si consiglia minimo 32GB.",
      "paa_match": true,
      "position": 1
    }
  ],
  "key_takeaways": [
    "MacBook Pro M3 Pro è il migliore in assoluto per stabilità e performance",
    "Per Windows, Dell XPS 15 offre il miglior display per la calibrazione colori"
  ],
  "pros_cons": {
    "macbook_pro_m3": {
      "pros": ["Performance CPU/GPU ottimale", "Display eccellente"],
      "cons": ["Prezzo elevato", "Non upgrade RAM/storage"]
    }
  },
  "citations": [
    { "position": 5, "source_id": "uuid", "url": "https://...", "anchor": "Benchmark Puget Systems" }
  ],
  "author_note": "Testato personalmente su DaVinci Resolve 18.6 con footage RED 6K.",
  "review_note": "Approvato da tech_reviewer_01. Prezzi verificati al 15/05/2025.",
  "last_updated": "2025-05-15",
  "word_count": 3240,
  "reading_time_min": 14
}
```

---

### 6.3 Modello JSON: SEO Metadata

```json
{
  "node_id": "uuid",
  "canonical_url": "https://site.it/tech/laptop/video-editing-2025",
  "hreflang": {
    "it": "https://site.it/tech/laptop/video-editing-2025",
    "en": "https://site.com/tech/laptop/video-editing-2025"
  },
  "og": {
    "title": "Migliori Laptop Video Editing 2025: Testati e Classificati",
    "description": "Abbiamo testato 18 laptop per video editing. Ecco i migliori per budget, performance e qualità display.",
    "image": "https://cdn.site.it/images/laptop-video-editing-og.jpg",
    "type": "article"
  },
  "twitter_card": {
    "card": "summary_large_image",
    "title": "Migliori Laptop Video Editing 2025",
    "description": "18 laptop testati. I migliori per ogni budget.",
    "image": "https://cdn.site.it/images/laptop-video-editing-tw.jpg"
  },
  "meta_title": "Migliori Laptop per Video Editing 2025 | Testati e Classificati",
  "meta_description": "Abbiamo testato 18 laptop per video editing professionale. Scopri i migliori per 4K, DaVinci Resolve, Premiere Pro e budget limitati.",
  "robots": "index,follow",
  "sitemap": {
    "priority": 0.8,
    "changefreq": "monthly"
  },
  "paa_blocks": [
    {
      "question": "Qual è il miglior laptop per video editing professionale nel 2025?",
      "answer_snippet": "Il MacBook Pro M3 Pro è il miglior laptop per video editing professionale nel 2025 grazie al chip M3 Pro, display Liquid Retina XDR e fino a 128GB di memoria unificata."
    }
  ],
  "sge_answer_block": {
    "direct_answer": "I migliori laptop per video editing 2025 sono: MacBook Pro M3 Pro (macOS), Dell XPS 15 (Windows), ASUS ProArt Studiobook 16 (DaVinci Resolve). Tutti hanno RAM ≥18GB e GPU dedicata.",
    "followup_queries": ["MacBook Pro M3 Pro prezzo", "Dell XPS 15 specifiche", "laptop 4K editing budget"],
    "sources": ["uuid-source-1", "uuid-source-2"]
  },
  "jsonld_blocks": [
    { "type": "Article", "block_id": "uuid" },
    { "type": "FAQPage", "block_id": "uuid" },
    { "type": "BreadcrumbList", "block_id": "uuid" },
    { "type": "ItemList", "block_id": "uuid" }
  ]
}
```

---

### 6.4 Modello JSON: HITL Review

```json
{
  "review_id": "uuid",
  "article_id": "uuid",
  "article_slug": "best-laptops-video-editing-2025",
  "vertical": "tech",
  "risk_level": "MEDIUM",
  "hitl_required": true,
  "status": "pending_review",
  "priority": "normal",
  "sla_deadline": "2025-05-24T10:00:00Z",
  "eeat_assessment": {
    "expertise_level": "senior",
    "experience_signals": ["first_person_testing", "benchmark_data_cited"],
    "authoritativeness": {
      "author_authority_score": 0.78,
      "citations_external_count": 12,
      "authoritative_sources_ratio": 0.83
    },
    "trustworthiness": {
      "sources_count": 12,
      "claim_verification_rate": 0.91,
      "disclaimer_present": false,
      "commercial_disclosure": false
    }
  },
  "checklist": {
    "sources_verified": true,
    "claims_sourced": true,
    "no_duplicate_content": true,
    "canonical_set": true,
    "internal_links_checked": false,
    "images_alt_text": false,
    "jsonld_valid": true,
    "disclaimer_added": false,
    "price_disclaimer": false
  },
  "blocking_issues": [
    "Manca disclaimer su prezzi soggetti a variazione",
    "Internal links non verificati (3 link potenzialmente broken)"
  ],
  "reviewer_notes": "",
  "requires_expert": false,
  "created_at": "2025-05-22T08:00:00Z",
  "updated_at": "2025-05-22T08:00:00Z"
}
```

---

## 7. REGOLE OPERATIVE

### 7.1 Regole Internal Linking

```
REGOLA IL-01: Ogni articolo deve avere ≥3 link interni in uscita verso pagine dello stesso cluster
REGOLA IL-02: Ogni pillar page deve essere raggiungibile da tutti i nodi L1 del suo cluster
REGOLA IL-03: L'anchor text non deve essere generico ("clicca qui", "leggi qui", "scopri di più"). Deve essere semanticamente rilevante per la pagina destinazione: può essere la keyword target esatta, una variante sinonimica, una frase nominale di 2-4 parole o una porzione di frase contestuale che rappresenta il topic principale della destinazione (es. "come calcolare la rata del mutuo" è preferibile a "mutuo" come anchor verso una pagina sul calcolo della rata)
REGOLA IL-04: Non creare più di 1 link interno verso la stessa pagina per articolo (eccezione: footer/nav)
REGOLA IL-05: Link bidirezionali obbligatori tra nodi allo stesso livello quando hanno search_intent complementare
REGOLA IL-06: Nessun orphan page: ogni nodo deve avere ≥1 link in ingresso
REGOLA IL-07: Non linkare da pagine L3 a pillar page direttamente senza passare per L1/L2 (gerarchia SEO)
REGOLA IL-08: I link di "aggiornamento" (A aggiorna B) devono usare rel="canonical" o redirect 301 + link esplicito
REGOLA IL-09: Canonical checking: prima di creare link, verificare che la pagina destinazione non sia canonica di un'altra
REGOLA IL-10: Link verso competitor sono ammessi solo come fonti esplicite (rel="nofollow sponsored") mai come navigazione
```

### 7.2 Regole EEAT

```
REGOLA EEAT-01: Ogni articolo deve avere un author con expertise_tags coerenti con il topic
REGOLA EEAT-02: Finance/Health/Legal richiedono reviewer con expert_domain = 'financial'/'medical'/'legal'
REGOLA EEAT-03: Ogni claim numerico deve avere source_id. Confidence < 0.70 → nota "dato non verificato"
REGOLA EEAT-04: Articoli con claim medici devono avere disclaimer: "non costituisce parere medico"
REGOLA EEAT-05: Articoli con claim finanziari devono avere disclaimer: "non costituisce consulenza finanziaria"
REGOLA EEAT-06: Articoli con claim legali devono avere disclaimer: "non costituisce parere legale"
REGOLA EEAT-07: last_updated deve essere aggiornato ad ogni modifica significativa (>5% del contenuto)
REGOLA EEAT-08: sources_count ≥ 5 per articoli L0/L1. sources_count ≥ 3 per L2. sources_count ≥ 1 per L3
REGOLA EEAT-09: authority_score delle fonti deve essere ≥ 0.60 per almeno il 70% delle fonti
REGOLA EEAT-10: Conflitti di interesse devono essere dichiarati (es: contenuto sponsorizzato, affiliazioni)
```

### 7.3 Regole PAA

```
REGOLA PAA-01: Ogni articolo L0/L1 deve avere ≥5 FAQ PAA-ready
REGOLA PAA-02: La risposta PAA deve essere ≤50 parole, diretta, senza jargon eccessivo
REGOLA PAA-03: La risposta PAA deve contenere la keyword della domanda (keyword in answer)
REGOLA PAA-04: Ogni risposta PAA deve essere verificabile con almeno 1 fonte
REGOLA PAA-05: PAA questions devono essere estratte da analisi SERP reale, non inventate
REGOLA PAA-06: FAQPage JSON-LD deve contenere tutte le PAA questions dell'articolo
REGOLA PAA-07: Risposte PAA contraddittorie rispetto al competitor dominante → evidenziare differenza con fonte
REGOLA PAA-08: Aggiornare le PAA ad ogni refresh del topic (le domande cambiano nel tempo)
```

### 7.4 Regole SGE/AI Overview

```
REGOLA SGE-01: Ogni articolo L0/L1 deve avere un hero_answer ≤150 parole
REGOLA SGE-02: Il hero_answer deve rispondere direttamente alla target_query senza intro lunghe
REGOLA SGE-03: Il hero_answer deve contenere le entità chiave del topic
REGOLA SGE-04: Strutturare le risposte con liste puntate quando possibile (Google AI Overview preferisce liste)
REGOLA SGE-05: Citare la fonte nel hero_answer: "Secondo [fonte autorevole], ..."
REGOLA SGE-06: Ogni sezione principale deve aprirsi con una risposta diretta (inverted pyramid)
REGOLA SGE-07: Includere dati numerici verificati nelle prime 200 parole
REGOLA SGE-08: Evitare linguaggio vago ("potrebbe", "forse", "in alcuni casi") nel hero_answer
REGOLA SGE-09: sge_answer_block in seo_metadata deve elencare le followup_queries previste
REGOLA SGE-10: Articoli YMYL → hero_answer deve citare expert o istituzione autorevole nella prima frase
```

### 7.5 Regole Anti-Duplicazione e Anti-Cannibalizzazione

```
REGOLA DEDUP-01: Prima di creare un ContentNode, similarity search su embedding degli slug/title esistenti
                 Threshold cosine > 0.85 → alert, non creare automaticamente
REGOLA DEDUP-02: Due pagine con target_query semanticamente equivalente (cosine > 0.90) → merge o canonical
REGOLA DEDUP-03: Ogni nuovo articolo viene confrontato con il corpus via IVectorStore.SearchAsync
                 Se score > 0.88 con articolo esistente → review manuale obbligatoria
REGOLA DEDUP-04: Stesso slug non può esistere per la stessa verticale + locale (UNIQUE constraint DB)
REGOLA DEDUP-05: Cannibalizzazione organica: se 2 articoli rankano per la stessa query → 1 diventa canonical
REGOLA DEDUP-06: content_hash verificato a ogni aggiornamento: bloccare save se hash identico (no-op)
REGOLA DEDUP-07: Topic merging: se due topic hanno cosine > 0.92 su embedding → merge con redirect 301
REGOLA DEDUP-08: ClusterPlanner verifica che ogni query_target sia unica nel cluster prima di assegnarla
```

---

## 8. PIANO GENERAZIONE CONTENUTI MULTILIVELLO

### 8.1 Sequenza per cluster

```
FASE 1: Pillar Page (L0)
  - Prima da generare
  - word_count target: 4.000-8.000 parole
  - schema: Article + FAQPage + BreadcrumbList
  - hero_answer obbligatorio
  - ≥10 FAQ PAA-ready
  - HITL: sempre pending_review

FASE 2: L1 Satellite Articles (3-6 per cluster)
  - Generati in batch dopo approvazione L0
  - word_count target: 2.500-5.000 parole
  - Ognuno deve linkare a L0 e a ≥2 L2

FASE 3: L2 Supporting Articles (6-15 per cluster)
  - Generati dopo approvazione degli L1 correlati
  - word_count target: 1.500-3.000 parole
  - Altamente specifici per search intent

FASE 4: L3 FAQ/How-to/Definition (15-50 per cluster)
  - Generati in batch automatizzato
  - word_count target: 500-1.500 parole
  - Schema: FAQPage / HowTo / DefinedTerm
  - HITL: auto-approve se risk_level = LOW
```

### 8.2 Throttling e priorità

```
Generazione parallela: max 4 articoli in parallelo (BudgetAwareExecutor.MaxConcurrency = 4)
Token budget per articolo L0: 80.000 token (incluse fonti RAG)
Token budget per articolo L1: 40.000 token
Token budget per articolo L2: 20.000 token
Token budget per articolo L3: 8.000 token
```

---

## 9. PIANO AGGIORNAMENTO CONTENUTI

### 9.1 Trigger di aggiornamento

| Trigger | Azione | SLA |
|---------|--------|-----|
| TrendScore cala > 30% in 30gg | Scheduled refresh | 7 giorni |
| FreshnessScore < 40 | Scheduled refresh | 14 giorni |
| Competitor pubblica articolo su stessa query | Competitor alert + review | 48 ore |
| Entity collegata aggiornata (es: nuova legge) | Entity-triggered refresh | 24 ore |
| Fonte citata non raggiungibile (404) | Source update + review | 24 ore |
| Google algo update (segnale esterno) | Full cluster review | 7 giorni |
| Performance calo > 30% CTR in 14gg | Performance-triggered review | 48 ore |
| HITL reviewer richiede aggiornamento | Manual revision | SLA umano |

### 9.2 Differenziale update

```
Aggiornamento PARZIALE: solo le sezioni cambiate (section-level diff)
  → Aggiorna article_sections, rigenera JSON-LD correlato
  → Invalida solo le cache page dell'articolo
  → Incrementa version, aggiorna last_updated
  → Nuova voce in history.audit_log

Aggiornamento COMPLETO: rigenerazione totale
  → Nuovo topic_run con run_type = 'refresh'
  → Confronto diff con versione precedente
  → HITL review se diff > 30% del contenuto
  → Incrementa version, invalida cache cluster
```

---

## 10. PIANO MONITORAGGIO PERFORMANCE

### 10.1 Metriche chiave per nodo

```
Organiche:      clicks, impressions, avg_position, CTR (via GSC API)
Engagement:     bounce_rate, avg_time_on_page, scroll_depth (via GA4)
Conversioni:    lead, acquisti, newsletter signup (via GA4 events)
SERP Features:  featured_snippet_captured (bool), paa_captured (bool), rich_snippet (bool)
```

### 10.2 Soglie di alert

| Metrica | Alert GIALLO | Alert ROSSO |
|---------|-------------|-------------|
| avg_position | > 15 | > 30 |
| CTR | < 1.5% | < 0.5% |
| bounce_rate | > 75% | > 90% |
| avg_time_sec | < 90s | < 45s |
| impressions WoW | calo > 20% | calo > 40% |

### 10.3 Dashboard metriche AGXIOME

```csharp
// PerformanceMonitorAgent (BackgroundRunWorker, every 24h)
// 1. Fetch GSC data via SearchKnowledgeToolkit.GoogleSearchConsole
// 2. Fetch GA4 data via DataAnalysisToolkit.GoogleAnalytics
// 3. Save snapshots in publishing.performance_snapshots
// 4. Trigger alerts via CommunicationToolkit.Slack se soglie superate
// 5. Trigger scheduled_update su articoli underperforming
```

---

## 11. ESEMPI CONCRETI PER VERTICALE

### 11.1 Verticale Tech — Topic: "Migliori Laptop 2025"

```
Verticale: tech | Locale: it | YMYL: false

TopicRun → TrendScanner output:
  search_volume_30d: 45.200
  trend_index: 78/100
  velocity: +12% MoM
  competitor_domains: [notebook-italia.it, bestproducts.com, rtings.com]

TopicEvaluator output:
  priority_score: 82/100
  TrendScore: 78, CommercialValueScore: 91 (CPC €1.20), CompetitionScore: 65

ClusterPlan:
  L0: "Migliori Laptop 2025: Guida Completa" (slug: migliori-laptop-2025)
  L1: "Migliori Laptop Studenti 2025", "Migliori Laptop Gaming 2025",
      "Migliori Laptop Video Editing 2025", "Migliori Laptop Professionali 2025"
  L2: "MacBook Pro M3 Pro Recensione", "Dell XPS 15 Recensione",
      "MacBook Air M3 vs Dell XPS 13", "Laptop Gaming Budget 2025"
  L3: "Cos'è un ultrabook", "RAM laptop: quanta serve",
      "Come calibrare display laptop", "SSD vs HDD su laptop"

Internal Linking:
  migliori-laptop-2025 ↔ migliori-laptop-studenti-2025 (bidirezionale)
  macbook-pro-m3-recensione → migliori-laptop-video-editing-2025 (contestuale)
  cos-e-un-ultrabook → migliori-laptop-2025 (navigazionale verso pillar)
```

---

### 11.2 Verticale Finance — Topic: "Mutuo Prima Casa 2025"

```
Verticale: finance | Locale: it | YMYL: true

EEATGovernance:
  risk_level: YMYL
  hitl_required: true
  expert_domain: financial
  disclaimer: "Le informazioni non costituiscono consulenza finanziaria. Consultare un professionista abilitato."

TopicEvaluator output:
  RiskScore: 85/100 (altamente sensibile, cambia con tassi BCE)
  FreshnessScore: 40/100 (dati cambiano mensilmente)
  → scheduled_update ogni 14 giorni

ClusterPlan:
  L0: "Mutuo Prima Casa 2025: Guida Completa" (slug: mutuo-prima-casa-2025)
  L1: "Tassi Mutuo Fisso vs Variabile 2025", "Requisiti Mutuo Prima Casa",
      "Agevolazioni Giovani Under 36 Mutuo 2025"
  L2: "Come Calcolare Rata Mutuo", "Confronto Banche Mutuo 2025",
      "Surroga Mutuo Conviene 2025"
  L3: "Cos'è il Taeg Mutuo", "Piano Ammortamento Francese",
      "Come Funziona la Fideiussione"

HITLWorkflow:
  Tutti gli articoli → mandatory human review (YMYL)
  Reviewer con financial_license richiesto per L0/L1
  SLA: 12h per articoli con tasso BCE aggiornato

Refresh trigger:
  → BCE riunione (ogni 6-8 settimane): entity_triggered_refresh su tutti gli articoli tassi
  → Auto-refresh ogni 14gg per FreshnessScore
```

---

### 11.3 Verticale Health — Topic: "Vitamina D Carenza"

```
Verticale: health | Locale: it | YMYL: true

EEATGovernance:
  risk_level: YMYL
  expert_domain: medical
  reviewer_type: human (medico specialista)
  disclaimer: "Le informazioni sono a scopo divulgativo. Non sostituiscono il parere del medico."
  claim_verification_rate_required: ≥ 0.95

SerpAnalyzer:
  Gap identificato dai competitor: nessuno copre "carenza vitamina D in gravidanza con sintomi atipici"
  Angolo editoriale differenziante → L1 "Carenza Vitamina D in Gravidanza: Sintomi e Rischi"

Sources strategy:
  Solo fonti: PubMed, ISS, WHO, Ministero della Salute, riviste peer-reviewed
  authority_score minimo accettato: 0.80
  Vietato citare: blog, forum, siti senza byline medico verificato

SGE Answer Block:
  hero_answer: "La carenza di vitamina D si manifesta con affaticamento, dolori muscolari,
  fragilità ossea e umore basso. Secondo l'Istituto Superiore di Sanità, il 75% degli italiani
  ha livelli insufficienti (<30 ng/mL). La diagnosi avviene con esame del sangue (25-OH vitamina D)."
  sources: [ISS, PubMed PMID-37...]

ClusterPlan:
  L0: "Carenza Vitamina D: Sintomi, Cause e Come Rimediare"
  L1: "Vitamina D: Dosaggio Giornaliero Raccomandato", "Carenza Vitamina D Bambini",
      "Carenza Vitamina D in Gravidanza"
  L2: "Vitamina D2 vs D3: Differenze", "Alimenti Ricchi di Vitamina D",
      "Integratori Vitamina D: Quale Scegliere"
  L3: "Cos'è la Vitamina D", "Esame 25-OH Vitamina D: Come Interpretarlo",
      "Vitamina D e Calcio: Il Legame"
```


---

### 11.4 Verticale Legal — Topic: "Licenziamento Illegittimo 2025"

```
Verticale: legal | Locale: it | YMYL: true

EEATGovernance:
  risk_level: YMYL
  expert_domain: legal
  reviewer_type: human (avvocato abilitato)
  disclaimer: "Le informazioni hanno scopo puramente informativo e non costituiscono parere legale.
               Consultare un avvocato per la propria fattispecie."
  claim_verification_rate_required: ≥ 0.95

Sources strategy:
  Solo fonti: Gazzetta Ufficiale, Corte di Cassazione (sentenze), Ministero del Lavoro,
               INPS, riviste giuridiche peer-reviewed (Il Foro Italiano, Diritto & Giustizia)
  authority_score minimo accettato: 0.85
  Vietato citare: blog legali senza byline avvocato, forum (es. Avvocato.it user posts)
  Ogni norma citata → riferimento esatto (Art. X, L. Y/ZZZZ o D.Lgs. Y/ZZZZ)

TopicEvaluator output:
  RiskScore: 90/100 (YMYL massimo, impatto diretto su tutele individuali)
  FreshnessScore: 55/100 (norme cambiano con Decreti; aggiornamento ogni 30gg)
  CommercialValueScore: 72/100 (CPC alto: avvocati e lavoratori)
  priority_score: 78/100

ClusterPlan:
  L0: "Licenziamento Illegittimo: Cosa Fare e Come Tutelarsi" (slug: licenziamento-illegittimo)
  L1: "Licenziamento per Giustificato Motivo Soggettivo: Quando è Illegittimo",
      "Licenziamento Discriminatorio: Tutele e Risarcimento",
      "Impugnare il Licenziamento: Tempi, Modalità e Costi",
      "Reintegra vs Indennizzo: Differenze dopo il Jobs Act"
  L2: "Art. 18 Statuto dei Lavoratori: Cosa Prevede Ancora",
      "Tutela Crescente (D.Lgs. 23/2015): Come si Calcola l'Indennizzo",
      "Conciliazione Sindacale: Quando Conviene",
      "Tentativo Obbligatorio di Conciliazione: Guida Pratica"
  L3: "Cos'è il Giustificato Motivo Soggettivo",
      "Differenza tra Licenziamento Nullo e Annullabile",
      "Termine di 60 Giorni per Impugnare il Licenziamento",
      "Come Leggere la Lettera di Licenziamento"

HITLWorkflow:
  Tutti gli articoli → mandatory human review (YMYL)
  Reviewer: avvocato specializzato in diritto del lavoro
  SLA: 12h per articoli con riferimento a nuovi decreti o sentenze recenti
  Checklist specifica:
    - ogni norma citata con estremi corretti (articolo + legge + anno)
    - ogni sentenza con numero + sezione Cassazione + anno
    - disclaimer presente in prima schermata (above the fold)
    - nessun consiglio operativo senza disclaimer

Refresh trigger:
  → Pubblicazione nuovo Decreto Legislativo lavoro: entity_triggered_refresh
  → Sentenza Cassazione sezioni unite su topic correlato: entity_triggered_refresh
  → Auto-refresh ogni 30gg per FreshnessScore

SGE Answer Block:
  hero_answer: "Il licenziamento illegittimo è quello privo di giusta causa, giustificato motivo
  soggettivo o oggettivo. Secondo la Corte di Cassazione, il lavoratore ha 60 giorni per
  impugnarlo (Art. 6 L. 604/1966). Le tutele variano: reintegra (Art. 18 Statuto) o
  indennizzo crescente (D.Lgs. 23/2015) a seconda della data di assunzione."
  sources: [Cassazione Civ. Sez. Lav., L. 604/1966, D.Lgs. 23/2015]

Internal Linking:
  licenziamento-illegittimo ↔ impugnare-licenziamento-tempi-modalita (bidirezionale L0↔L1)
  tutela-crescente-calcolo-indennizzo → reintegra-vs-indennizzo-differenze (L2→L1)
  cos-e-giustificato-motivo-soggettivo → licenziamento-giustificato-motivo-soggettivo (L3→L1)
```

---

### 11.5 Verticale Real Estate — Topic: "Comprare Casa 2025"

```
Verticale: real_estate | Locale: it | YMYL: true

EEATGovernance:
  risk_level: HIGH (non YMYL puro, ma impatto finanziario elevato)
  hitl_required: true
  expert_domain: financial (per sezioni mutuo) + legal_lite (per sezioni contratto)
  disclaimer: "I dati sui prezzi immobiliari sono indicativi. Per valutazioni precise
               rivolgersi a un agente immobiliare abilitato o notaio."

TopicEvaluator output:
  search_volume_30d: 38.500
  trend_index: 71/100
  velocity: +8% MoM (mercato immobiliare in ripresa post-rialzo tassi)
  CommercialValueScore: 88/100 (CPC altissimo: agenzie, mutui, notai)
  ClusterDepthScore: 92/100 (topic con centinaia di sotto-query)
  priority_score: 84/100

Sources strategy:
  Fonti accettate: Nomisma, Scenari Immobiliari, Agenzia delle Entrate (OMI),
                   Banca d'Italia (ABI dati mutui), FIAIP, Confedilizia
  authority_score minimo: 0.70
  Citare prezzi al m² con fonte OMI + trimestre di riferimento obbligatorio

ClusterPlan:
  L0: "Comprare Casa nel 2025: Guida Completa Passo per Passo"
      (slug: comprare-casa-2025-guida-completa)
      word_count target: 7.000-9.000 parole
  L1: "Come Scegliere il Mutuo per la Prima Casa nel 2025",
      "Spese Acquisto Casa: Tutte le Tasse e i Costi",
      "Compromesso di Vendita: Cos'è e Come Funziona",
      "Agevolazioni Prima Casa 2025: Chi ne ha Diritto",
      "Come Valutare un Immobile: Strumenti e Metodi"
  L2: "Rogito Notarile: Cosa Succede il Giorno dell'Atto",
      "Proposta di Acquisto: Come Scriverla e Cosa Controllare",
      "Caparra Confirmatoria vs Caparra Penitenziale: Differenze",
      "Ispezione Ipotecaria e Visura Catastale: Come Farle",
      "Bonus Casa 2025: Detrazioni Disponibili",
      "Acquisto Casa all'Asta: Guida Pratica 2025",
      "Box Auto e Cantina: Come si Acquistano con la Casa"
  L3: "Cos'è la Rendita Catastale",
      "IMU Prima Casa: Si Paga?",
      "Differenza tra Preliminare e Compromesso",
      "Cos'è il Mutuo a Tasso Fisso",
      "Cosa Controlla il Notaio Prima del Rogito",
      "Quanto Costa il Notaio per un Atto di Compravendita",
      "Visura Ipotecaria: Come Richiederla Online"

HITLWorkflow:
  L0, L1 con dati finanziari → pending_review (SLA 24h)
  L2/L3 informativi → auto-approve se risk_level = MEDIUM e claim_verification_rate ≥ 0.90
  Reviewer: agente immobiliare FIAIP o consulente creditizio per sezioni mutuo

Refresh trigger:
  → Riunione BCE (ogni 6-8 settimane): entity_triggered_refresh su articoli mutuo
  → Pubblicazione dati OMI Agenzia Entrate (trimestrale): prezzi/m² aggiornati
  → Nuova legge su agevolazioni prima casa: entity_triggered_refresh immediato
  → FreshnessScore < 50: scheduled_refresh ogni 30gg (mercato volatile)

SGE Answer Block:
  hero_answer: "Comprare casa nel 2025 richiede: scelta dell'immobile, proposta d'acquisto,
  compromesso con caparra (tipicamente 10-20% del prezzo), richiesta mutuo (se necessario)
  e rogito notarile. Le spese accessorie ammontano in media al 10-15% del prezzo (IVA/imposta
  registro, notaio, agenzia). Secondo l'OMI Agenzia Entrate, il prezzo medio nelle grandi città
  è di 3.200 €/m² (Q4 2024)."
  sources: [OMI Q4 2024, Nomisma Osservatorio Immobiliare 2025, ABI]

Internal Linking:
  comprare-casa-2025-guida ↔ come-scegliere-mutuo-prima-casa (bidirezionale L0↔L1)
  comprare-casa-2025-guida ↔ spese-acquisto-casa-tasse-costi (bidirezionale L0↔L1)
  compromesso-vendita → proposta-di-acquisto (L1→L2, relazione sequenziale processo)
  rogito-notarile → compromesso-vendita (L2→L1 reverse: prerequisito)
  cos-e-rendita-catastale → spese-acquisto-casa (L3→L1 navigazionale verso pillar L1)
  imu-prima-casa → agevolazioni-prima-casa-2025 (L3→L1 topico correlato)

Performance targets:
  avg_position target: ≤ 8 per query "comprare casa 2025" entro 6 mesi
  CTR target: ≥ 2.5% (verticale high-intent)
  Conversion KPI: richieste preventivo mutuo, contatti agenzia (GA4 events)
```

---

## 12. COMPONENTI AGXIOME NECESSARI

### 12.1 Core (già esistenti)

| Componente | Utilizzo nel sistema editoriale |
|------------|--------------------------------|
| `HeavySwarm` | TopicEvaluator, TopicWikiBuilder, ContentGenerator, EEATGovernance |
| `GrokSwarm` | SerpAnalyzer, ClusterPlanner, ContentGenerator per nodo |
| `ForestSwarm` | TrendScanner, HITLWorkflow, PublishingRuntime |
| `AgxiomeDagWorkflow` | `EditorialRunPipeline` end-to-end |
| `BackgroundRunWorker` | PerformanceMonitorAgent, CacheRefreshWorker |
| `CancellationManager` | Annullamento run lunghi |
| `IVectorStore` (pgvector) | SemanticGraphBuilder, anti-dedup similarity |
| `IEmbedder` (OllamaMxbai) | Embedding entità, articoli, topic |
| `IChunker` (Semantic) | Chunking fonti per RAG TopicWikiBuilder |
| `IReranker` (LLM/Cohere) | Reranking risultati SERP + fonti |
| `ToolResultCache` | Cache tool calls SERP (TTL 1h) |
| `ToolCallLimiter` | Rate limiting SerpAPI, Google Trends |
| `ToolkitPolicyEnforcer` | Whitelist tool per verticale YMYL |
| `SemanticToolSelector` | Selezione toolkit ottimale per action type |
| `BudgetAwareExecutor` | Token budget per run e per articolo |
| `IAgentStorageBridge` | Persistenza stato agent su PostgreSQL |
| `ResponseCache` | Cache risposte LLM per query identiche |
| `TokenCounter` | Tracking costo per topic_run |
| `AuditTrail` (Security) | Ogni write auditato in history.audit_log |
| `RateLimiting` (Security) | Protezione endpoint publishing da burst |
| `RedisSessionManager` | Session state tra step pipeline |

### 12.2 Toolkits necessari

| Toolkit | Azioni richieste |
|---------|----------------|
| `SearchKnowledgeToolkits.SerpApiToolkit` | SERP analysis, PAA extraction, related queries |
| `SearchKnowledgeToolkits.TavilyToolkit` | Deep web research per TopicWikiBuilder |
| `WebSocialToolkits.WebBrowserToolkit` | Crawling articoli competitor |
| `WebSocialToolkits.RedditToolkit` | Trend scanning Reddit |
| `DataAnalysisToolkits.SqlToolkit` | Query analytics su performance_snapshots |
| `ProductivityToolkits.JiraToolkit` | HITL task creation per review team |
| `CommunicationToolkits.SlackToolkit` | Alert performance + HITL notifications |
| `FileStorageToolkits.S3Toolkit` | Storage media (immagini, file) |

### 12.3 Nuovi componenti da sviluppare

| Componente | Progetto | Descrizione |
|------------|----------|-------------|
| `EditorialRunPipeline` | `AGXIOME.Editorial` | DAG workflow completo |
| `TrendScannerAgent` | `AGXIOME.Editorial` | ForestSwarm multi-source |
| `SerpAnalyzerAgent` | `AGXIOME.Editorial` | GrokSwarm SERP extraction |
| `TopicWikiBuilderAgent` | `AGXIOME.Editorial` | HeavySwarm wiki construction |
| `SemanticGraphBuilderAgent` | `AGXIOME.Editorial` | Graph nodes + pgvector |
| `ClusterPlannerAgent` | `AGXIOME.Editorial` | GrokSwarm cluster planning |
| `ContentGeneratorAgent` | `AGXIOME.Editorial` | GrokSwarm per-node generation |
| `SEOOptimizerAgent` | `AGXIOME.Editorial` | JSON-LD, OpenGraph, hreflang |
| `EEATGovernanceAgent` | `AGXIOME.Editorial` | HeavySwarm EEAT scoring |
| `HITLWorkflowAgent` | `AGXIOME.Editorial` | ForestSwarm routing review |
| `PublishingRuntimeAgent` | `AGXIOME.Editorial` | Publish + cache + index |
| `PerformanceMonitorAgent` | `AGXIOME.Editorial` | BackgroundRunWorker 24h |
| `RedisInvalidationService` | `AGXIOME.Editorial` | Pub/sub cache invalidation |
| `EditorialDbContext` | `AGXIOME.Editorial` | EF Core / Dapper per schemi |
| `SerpApiToolkit` | `AGXIOME.Toolkits` | Se non esiste: nuovo toolkit |

---

## 13. STRUTTURA PROGETTO .NET

```
src/
  AGXIOME.Editorial/
    Agents/
      TrendScannerAgent.cs
      TopicEvaluatorAgent.cs
      SerpAnalyzerAgent.cs
      QueryExpansionAgent.cs
      TopicWikiBuilderAgent.cs
      SemanticGraphBuilderAgent.cs
      ClusterPlannerAgent.cs
      ContentGeneratorAgent.cs
      SEOOptimizerAgent.cs
      EEATGovernanceAgent.cs
      HITLWorkflowAgent.cs
      PublishingRuntimeAgent.cs
      PerformanceMonitorAgent.cs
    Pipelines/
      EditorialRunPipeline.cs
    Models/
      TopicWikiJson.cs
      ContentNodeJson.cs
      SeoMetadataJson.cs
      HitlReviewJson.cs
      ClusterPlanJson.cs
    Services/
      RedisInvalidationService.cs
      TopicSimilarityService.cs
      ClaimVerificationService.cs
      ContentHashService.cs
    Data/
      EditorialDbContext.cs
      Migrations/
    Extensions/
      EditorialServiceExtensions.cs  ← AddEditorialStack(IServiceCollection)
    AGXIOME.Editorial.csproj

apps/
  agxioma-editorial/
    Program.cs
    appsettings.json    ← verticals config, HITL SLA, budget limits
    Controllers/
      EditorialController.cs   ← /api/editorial/run, /api/editorial/status
      PublishingController.cs  ← /api/publish/{slug}
      HITLController.cs        ← /api/hitl/review, /api/hitl/approve
    Rendering/
      DynamicPageMiddleware.cs ← DB → Redis → render
```

### 13.1 DI Registration

```csharp
// EditorialServiceExtensions.cs
public static IServiceCollection AddEditorialStack(
    this IServiceCollection services,
    IConfiguration config)
{
    // AGXIOME core
    services.AddAgxiomeCore(config);
    services.AddToolkitOrchestration();
    services.AddAgxiomeSecurity(config);

    // Storage
    services.AddPgVectorStore(config.GetConnectionString("Editorial"));
    services.AddRedisCache(config["Redis:ConnectionString"]);
    services.AddOllamaEmbedder("mxbai-embed-large");

    // Editorial agents
    services.AddScoped<TrendScannerAgent>();
    services.AddScoped<TopicEvaluatorAgent>();
    services.AddScoped<SerpAnalyzerAgent>();
    services.AddScoped<QueryExpansionAgent>();
    services.AddScoped<TopicWikiBuilderAgent>();
    services.AddScoped<SemanticGraphBuilderAgent>();
    services.AddScoped<ClusterPlannerAgent>();
    services.AddScoped<ContentGeneratorAgent>();
    services.AddScoped<SEOOptimizerAgent>();
    services.AddScoped<EEATGovernanceAgent>();
    services.AddScoped<HITLWorkflowAgent>();
    services.AddScoped<PublishingRuntimeAgent>();
    services.AddSingleton<EditorialRunPipeline>();

    // Services
    services.AddSingleton<RedisInvalidationService>();
    services.AddScoped<TopicSimilarityService>();
    services.AddScoped<ClaimVerificationService>();

    // Background workers
    services.AddHostedService<PerformanceMonitorAgent>();
    services.AddHostedService<CacheRefreshWorker>();

    return services;
}
```

---

---

## 14. DECISION & SIMULATION LAYER

> **Obiettivo**: trasformare il sistema da "macchina editoriale" a "runtime editoriale intelligente". Nessun topic genera contenuto senza superare un gate decisionale esplicito. Nessun run parte senza una simulazione di costo/valore.

---

### 14.1 Topic Decision Gate

Il `TopicDecisionGate` è un **nodo bloccante** inserito nel DAG tra `topic_eval` e `serp_analyze`. Se un topic non supera le soglie minime, il run si interrompe con `status = rejected_by_gate` e il motivo viene loggato.

#### Soglie per verticale

```csharp
// TopicDecisionGateConfig.cs
public record GateThresholds(
    double MinPriorityScore,
    double MinClusterDepthScore,
    double MaxCompetitionScore,   // inverso: <soglia = concorrenza alta, blocca
    double MaxRiskScore,          // se > soglia → review obbligatoria, non blocco
    double MinMonetizationScore
);

// appsettings.json → per verticale
"GateThresholds": {
  "tech":         { "MinPriority": 50, "MinClusterDepth": 40, "MaxCompetition": 85, "MaxRisk": 70, "MinMonetization": 30 },
  "finance":      { "MinPriority": 60, "MinClusterDepth": 50, "MaxCompetition": 80, "MaxRisk": 100, "MinMonetization": 50 },
  "health":       { "MinPriority": 55, "MinClusterDepth": 45, "MaxCompetition": 80, "MaxRisk": 100, "MinMonetization": 25 },
  "legal":        { "MinPriority": 55, "MinClusterDepth": 40, "MaxCompetition": 80, "MaxRisk": 100, "MinMonetization": 30 },
  "real_estate":  { "MinPriority": 55, "MinClusterDepth": 45, "MaxCompetition": 82, "MaxRisk": 90,  "MinMonetization": 45 }
}
```

#### Esito gate

| Esito | Condizione | Azione |
|-------|-----------|--------|
| `PASS` | Tutte le soglie superate | Run continua con `serp_analyze` |
| `HOLD` | Soglie parzialmente mancate | Topic salvato con `status = on_hold`, rianalizzato in 30gg |
| `REJECT` | Soglie critiche non superate | `topic_runs.status = rejected_by_gate`, email alert |
| `ESCALATE` | `RiskScore > MaxRisk` | Run sospeso, creato task HITL per valutazione umana |

```csharp
// TopicDecisionGateAgent.cs
public class TopicDecisionGateAgent : SingleAgent
{
    public async Task<GateResult> EvaluateAsync(TopicScoreCard scoreCard, GateThresholds thresholds)
    {
        var failures = new List<string>();

        if (scoreCard.PriorityScore < thresholds.MinPriorityScore)
            failures.Add($"priority_score {scoreCard.PriorityScore} < {thresholds.MinPriorityScore}");
        if (scoreCard.ClusterDepthScore < thresholds.MinClusterDepthScore)
            failures.Add($"cluster_depth_score {scoreCard.ClusterDepthScore} < {thresholds.MinClusterDepthScore}");
        if (scoreCard.CompetitionScore > thresholds.MaxCompetitionScore)
            failures.Add($"competition_score {scoreCard.CompetitionScore} > {thresholds.MaxCompetitionScore}");
        if (scoreCard.MonetizationScore < thresholds.MinMonetizationScore)
            failures.Add($"monetization_score {scoreCard.MonetizationScore} < {thresholds.MinMonetizationScore}");

        if (scoreCard.RiskScore > thresholds.MaxRiskScore)
            return GateResult.Escalate(scoreCard, "Risk level requires human decision");

        return failures.Count == 0
            ? GateResult.Pass(scoreCard)
            : failures.Count <= 1
                ? GateResult.Hold(scoreCard, failures)
                : GateResult.Reject(scoreCard, failures);
    }
}
```

#### Schema DB aggiuntivo

```sql
-- Risultato del gate per ogni run
CREATE TABLE editorial.topic_gate_results (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    run_id          UUID NOT NULL REFERENCES editorial.topic_runs(id),
    topic_id        UUID NOT NULL REFERENCES editorial.topics(id),
    gate_outcome    TEXT NOT NULL,  -- PASS|HOLD|REJECT|ESCALATE
    score_snapshot  JSONB NOT NULL, -- snapshot completo dei score al momento del gate
    failures        JSONB NOT NULL DEFAULT '[]',  -- [{field, value, threshold}]
    override_by     UUID,           -- user_id se un umano ha overridden il gate
    override_reason TEXT,
    evaluated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_gate_results_topic ON editorial.topic_gate_results(topic_id, evaluated_at DESC);
CREATE INDEX idx_gate_results_outcome ON editorial.topic_gate_results(gate_outcome);
```

---

### 14.2 Editorial Simulation (`pre-flight`)

Prima di lanciare un run reale su 20-50 pagine, il sistema esegue una **simulazione dry-run** che stima costi, traffico, rischi e valore senza chiamare LLM generativi né scrivere sul DB di produzione.

#### Flusso simulazione

```
[INPUT: vertical_id + topic + locale + cluster_config]
         │
         ▼
┌─────────────────────────┐
│  SimulationAgent        │ ← SingleAgent (DeepSeek/GPT-4o)
│  (dry-run mode)         │   Usa solo: TrendScanner (read), SERP cache, DB esistente
└────────────┬────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────┐
│  SimulationReport                                        │
│                                                         │
│  cluster_preview:     struttura cluster stimata          │
│  estimated_pages:     conteggio nodi L0/L1/L2/L3         │
│  traffic_potential:   clicks stimati a 6 mesi (MLM)      │
│  cannibalization_risk: conflitti rilevati nel grafo      │
│  cost_estimate:       token + API costs (USD)            │
│  value_estimate:      revenue proxy (CPC × clicks stim.) │
│  review_burden:       ore HITL stimate                   │
│  risk_summary:        risk_level distribution             │
│  recommendation:      PROCEED | REVIEW | ABORT           │
└─────────────────────────────────────────────────────────┘
```

#### Modello JSON SimulationReport

```json
{
  "simulation_id": "uuid",
  "topic": "Mutuo Prima Casa 2025",
  "vertical": "finance",
  "locale": "it",
  "simulated_at": "2025-05-22T10:00:00Z",

  "cluster_preview": {
    "estimated_l0": 1,
    "estimated_l1": 5,
    "estimated_l2": 8,
    "estimated_l3": 20,
    "total_pages": 34
  },

  "traffic_potential": {
    "model": "position_ctr_model_v1",
    "avg_position_estimate": 12,
    "monthly_impressions_6m": 48000,
    "monthly_clicks_6m": 720,
    "confidence": "medium"
  },

  "cannibalization_risk": {
    "conflicts_detected": 3,
    "conflicts": [
      { "existing_slug": "mutuo-2024-guida", "similarity_score": 0.87, "action": "review" }
    ]
  },

  "cost_estimate": {
    "llm_tokens_total": 2800000,
    "llm_cost_usd": 4.20,
    "serp_api_calls": 340,
    "serp_cost_usd": 1.70,
    "embedding_calls": 1200,
    "embedding_cost_usd": 0.12,
    "total_cost_usd": 6.02
  },

  "value_estimate": {
    "avg_cpc_topic": 1.35,
    "clicks_6m_estimate": 4320,
    "revenue_proxy_6m_usd": 5832,
    "roi_estimate": "96x over cost",
    "payback_days_estimate": 45
  },

  "review_burden": {
    "ymyl_articles": 6,
    "high_risk_articles": 12,
    "estimated_review_hours": 14,
    "expert_reviews_required": 6
  },

  "risk_summary": {
    "LOW": 20,
    "MEDIUM": 8,
    "HIGH": 4,
    "YMYL": 2
  },

  "recommendation": "PROCEED",
  "recommendation_notes": "ROI atteso elevato (96x). 3 conflitti di cannibalizzazione da risolvere prima del run. 6 articoli YMYL richiedono revisore finanziario."
}
```

#### Schema DB aggiuntivo

```sql
CREATE TABLE editorial.simulation_reports (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    topic_id        UUID NOT NULL REFERENCES editorial.topics(id),
    vertical_id     UUID NOT NULL REFERENCES editorial.verticals(id),
    locale          TEXT NOT NULL DEFAULT 'it',
    report          JSONB NOT NULL,
    recommendation  TEXT NOT NULL,  -- PROCEED|REVIEW|ABORT
    approved_by     UUID,           -- user_id se approvato dall'operatore
    approved_at     TIMESTAMPTZ,
    run_id          UUID REFERENCES editorial.topic_runs(id),  -- run generato da questa simulazione
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_simulations_topic ON editorial.simulation_reports(topic_id, created_at DESC);
```

---

### 14.3 Cost & Token Budget Controller

Estende il `BudgetAwareExecutor` già presente in AGXIOME core con tracking esplicito per dimensione editoriale.

```sql
-- Tracking costi per granularità editoriale
CREATE TABLE editorial.cost_tracking (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    run_id          UUID NOT NULL REFERENCES editorial.topic_runs(id),
    node_id         UUID REFERENCES editorial.content_nodes(id),
    article_id      UUID REFERENCES editorial.articles(id),
    scope           TEXT NOT NULL,  -- topic|cluster|article|page|run
    provider        TEXT NOT NULL,  -- deepseek|openai|ollama|serp_api|tavily
    operation       TEXT NOT NULL,  -- generate|embed|search|rerank|analyze
    tokens_in       INTEGER NOT NULL DEFAULT 0,
    tokens_out      INTEGER NOT NULL DEFAULT 0,
    cost_usd        NUMERIC(10,6) NOT NULL DEFAULT 0,
    duration_ms     INTEGER,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_cost_run ON editorial.cost_tracking(run_id);
CREATE INDEX idx_cost_node ON editorial.cost_tracking(node_id);

-- Vista aggregata per run
CREATE VIEW editorial.cost_summary_by_run AS
SELECT
    run_id,
    scope,
    SUM(tokens_in + tokens_out)     AS total_tokens,
    SUM(cost_usd)                   AS total_cost_usd,
    COUNT(*)                        AS operations,
    AVG(duration_ms)                AS avg_duration_ms
FROM editorial.cost_tracking
GROUP BY run_id, scope;

-- Vista costo per articolo pubblicato
CREATE VIEW editorial.cost_per_published_page AS
SELECT
    cn.slug,
    cn.level,
    t.name AS topic,
    v.name AS vertical,
    SUM(ct.cost_usd)                AS total_cost_usd,
    MAX(ps.organic_clicks)          AS clicks_last_30d,
    CASE WHEN SUM(ct.cost_usd) > 0
         THEN (MAX(ps.organic_clicks) * 0.01) / SUM(ct.cost_usd)  -- proxy ROI
         ELSE NULL END              AS roi_proxy
FROM editorial.content_nodes cn
JOIN editorial.topics t ON cn.topic_id = t.id
JOIN editorial.verticals v ON t.vertical_id = v.id
LEFT JOIN editorial.cost_tracking ct ON ct.node_id = cn.id
LEFT JOIN publishing.performance_snapshots ps ON ps.node_id = cn.id
    AND ps.snapshot_date >= CURRENT_DATE - INTERVAL '30 days'
GROUP BY cn.id, cn.slug, cn.level, t.name, v.name;
```

---

## 15. GOVERNANCE LIFECYCLE

> **Obiettivo**: garantire che il contenuto pubblicato rimanga accurato, attuale, non duplicato e di qualità crescente nel tempo. La governance non è un evento puntuale ma un ciclo continuo.

---

### 15.1 Content Quality Score (CQS)

Ogni articolo ha un `ContentQualityScore` persistente, calcolato dopo la pubblicazione e ricalcolato ad ogni trigger significativo.

```sql
CREATE TABLE editorial.content_quality_scores (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    article_id      UUID NOT NULL REFERENCES editorial.articles(id),
    node_id         UUID NOT NULL REFERENCES editorial.content_nodes(id),
    version         INTEGER NOT NULL,
    -- sotto-score (0.00-1.00)
    factual_score       NUMERIC(4,3) NOT NULL DEFAULT 0,  -- claim verification rate
    source_score        NUMERIC(4,3) NOT NULL DEFAULT 0,  -- avg authority_score fonti
    originality_score   NUMERIC(4,3) NOT NULL DEFAULT 0,  -- 1 - similarity vs corpus
    depth_score         NUMERIC(4,3) NOT NULL DEFAULT 0,  -- word_count + sections + sources
    freshness_score     NUMERIC(4,3) NOT NULL DEFAULT 0,  -- età contenuto vs FreshnessDecay
    internal_link_score NUMERIC(4,3) NOT NULL DEFAULT 0,  -- links_in + links_out vs target
    eeat_score          NUMERIC(4,3) NOT NULL DEFAULT 0,  -- da EEATRecord
    -- score composito
    cqs_total           NUMERIC(4,3) NOT NULL DEFAULT 0,
    -- classificazione
    cqs_tier            TEXT NOT NULL DEFAULT 'C',  -- A|B|C|D|F
    -- trigger per azione
    recommended_action  TEXT,  -- keep|refresh|merge|canonicalize|noindex|delete_redirect
    computed_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (article_id, version)
);
CREATE INDEX idx_cqs_article ON editorial.content_quality_scores(article_id, computed_at DESC);
CREATE INDEX idx_cqs_tier ON editorial.content_quality_scores(cqs_tier);
CREATE INDEX idx_cqs_action ON editorial.content_quality_scores(recommended_action);
```

#### Formula CQS composito

```
cqs_total =
    factual_score       × 0.25   +
    source_score        × 0.20   +
    originality_score   × 0.15   +
    depth_score         × 0.15   +
    freshness_score     × 0.10   +
    internal_link_score × 0.10   +
    eeat_score          × 0.05

Tier mapping:
  A: cqs_total >= 0.85
  B: cqs_total >= 0.70
  C: cqs_total >= 0.55
  D: cqs_total >= 0.40
  F: cqs_total < 0.40
```

#### CQSComputeAgent (BackgroundRunWorker — ogni 24h)

```csharp
// Ricalcola CQS per articoli che hanno avuto:
// - update di performance (nuovi clicks/impressioni)
// - nuovi claim verificati/smentiti
// - fonti broken
// - refresh di entità collegate
// - aging (FreshnessDecay ogni 7 gg)
```

---

### 15.2 Pruning & Content Decay

#### Stati del ciclo di vita di un articolo

```
draft → pending_review → approved → published
  │                                     │
  └──────────────────────────────────────┼──→ scheduled_update
                                         │         │
                                         │         ▼
                                         │    needs_revision ─→ published (after fix)
                                         │
                                         ├──→ merged         (canonical su altra pagina)
                                         ├──→ canonicalized  (rel=canonical verso altra URL)
                                         ├──→ noindexed      (index,noindex via robots)
                                         ├──→ deprecated     (conservato, non indicizzato)
                                         └──→ deleted_redirect (301 verso sostituto)
```

#### Regole di decay automatico

| Condizione | CQS Tier | Azione automatica | Trigger umano opz. |
|---|---|---|---|
| `organic_clicks_90d = 0` AND `impressions_90d < 100` | D/F | → `noindex` candidate | revisione prima di eseguire |
| `freshness_score < 0.25` AND `cqs_total < 0.50` | D/F | → `scheduled_refresh` | — |
| Similarity > 0.88 con articolo più performante | qualsiasi | → `merge` candidate | HITL obbligatorio |
| `source_score < 0.40` (troppe fonti broken/outdated) | D | → `needs_revision` | — |
| `factual_score < 0.50` (claim contradetti da nuove fonti) | D/F | → `HITL review urgente` | obbligatorio |
| `cqs_total < 0.30` per 60+ giorni consecutivi | F | → `delete_redirect` candidate | HITL obbligatorio |

```sql
-- Tracciamento delle azioni di pruning proposte
CREATE TABLE editorial.pruning_actions (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    node_id         UUID NOT NULL REFERENCES editorial.content_nodes(id),
    article_id      UUID NOT NULL REFERENCES editorial.articles(id),
    proposed_action TEXT NOT NULL,  -- keep|refresh|merge|canonicalize|noindex|delete_redirect
    reason          TEXT NOT NULL,
    cqs_snapshot    JSONB NOT NULL,
    approved_by     UUID,
    approved_at     TIMESTAMPTZ,
    executed_at     TIMESTAMPTZ,
    status          TEXT NOT NULL DEFAULT 'proposed',  -- proposed|approved|rejected|executed
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_pruning_node ON editorial.pruning_actions(node_id);
CREATE INDEX idx_pruning_status ON editorial.pruning_actions(status);
```

---

### 15.3 Cannibalization Governance a livello Cluster

Le regole `DEDUP-*` esistenti operano a livello di singolo articolo. Serve un layer che controlli i conflitti **tra cluster diversi**, soprattutto durante la pianificazione di nuovi topic.

```sql
-- Matrice dei conflitti tra cluster
CREATE TABLE editorial.cluster_conflict_matrix (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    cluster_a_id    UUID NOT NULL REFERENCES editorial.content_clusters(id),
    cluster_b_id    UUID NOT NULL REFERENCES editorial.content_clusters(id),
    conflict_type   TEXT NOT NULL,  -- query_overlap|entity_overlap|intent_overlap|structural_dup
    overlap_score   NUMERIC(4,3) NOT NULL,  -- 0-1, calcolato su embedding
    conflicting_queries TEXT[] NOT NULL DEFAULT '{}',
    resolution      TEXT,  -- NULL=unresolved|merge_clusters|canonical|noindex|keep_both
    resolved_by     UUID,
    resolved_at     TIMESTAMPTZ,
    detected_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (cluster_a_id, cluster_b_id)
);
CREATE INDEX idx_conflicts_cluster ON editorial.cluster_conflict_matrix(cluster_a_id);
CREATE INDEX idx_conflicts_score ON editorial.cluster_conflict_matrix(overlap_score DESC);
```

#### ClusterConflictScanAgent (BackgroundRunWorker — ogni volta che un cluster viene creato o aggiornato)

```csharp
// Per ogni nuovo cluster:
// 1. Embedding del pillar_slug + top 5 target_queries
// 2. Similarity search vs tutti i cluster esistenti stessa verticale
// 3. Se overlap_score > 0.75 → inserisce record in cluster_conflict_matrix
// 4. Se overlap_score > 0.88 → blocca il cluster con status = 'conflict_hold'
// 5. Notifica via Slack + crea task HITL per risoluzione
```

---

### 15.4 Source Reliability Memory

Ogni fonte accumula una reputazione nel tempo basata su accuracy storica, frequenza di aggiornamento e conflitti rilevati.

```sql
-- Memoria di affidabilità per ogni fonte
CREATE TABLE sources.source_reliability (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_id       UUID NOT NULL REFERENCES sources.source_documents(id) UNIQUE,
    -- metriche cumulative
    total_claims_cited      INTEGER NOT NULL DEFAULT 0,
    claims_verified         INTEGER NOT NULL DEFAULT 0,
    claims_contradicted     INTEGER NOT NULL DEFAULT 0,
    claims_outdated         INTEGER NOT NULL DEFAULT 0,
    avg_update_lag_days     NUMERIC(6,1),  -- giorni medi tra evento e aggiornamento fonte
    citation_frequency      INTEGER NOT NULL DEFAULT 0,  -- quante volte citata totale
    -- authority over time
    authority_score_current NUMERIC(4,2) NOT NULL DEFAULT 0,
    authority_score_6m_ago  NUMERIC(4,2),
    authority_score_12m_ago NUMERIC(4,2),
    -- classificazione
    reliability_tier        TEXT NOT NULL DEFAULT 'unknown',  -- S|A|B|C|D|blacklisted
    blacklisted             BOOLEAN NOT NULL DEFAULT FALSE,
    blacklist_reason        TEXT,
    -- update
    last_evaluated          TIMESTAMPTZ,
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_reliability_tier ON sources.source_reliability(reliability_tier);
CREATE INDEX idx_reliability_source ON sources.source_reliability(source_id);

-- Storico degli eventi di affidabilità (time-series)
CREATE TABLE sources.source_reliability_events (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_id       UUID NOT NULL REFERENCES sources.source_documents(id),
    event_type      TEXT NOT NULL,  -- claim_verified|claim_contradicted|claim_outdated|authority_change|broken_link|content_removed
    article_id      UUID REFERENCES editorial.articles(id),
    claim_id        UUID REFERENCES editorial.article_claims(id),
    details         JSONB NOT NULL DEFAULT '{}',
    impact_score    NUMERIC(3,2) NOT NULL DEFAULT 0,  -- -1.00 a +1.00
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_reliability_events_source ON sources.source_reliability_events(source_id, created_at DESC);
```

#### Formula affidabilità

```
reliability_score =
    (claims_verified / total_claims_cited)         × 0.40  +
    (1 - claims_contradicted / total_claims_cited) × 0.30  +
    (1 - avg_update_lag_days / 30).clamp(0,1)      × 0.20  +
    authority_score_current                         × 0.10

Tier mapping:
  S: >= 0.90 (fonti istituzionali top: PubMed, ISS, Cassazione)
  A: >= 0.80
  B: >= 0.70
  C: >= 0.55  ← minimo accettato per YMYL
  D: >= 0.40  ← non usare per YMYL
  blacklisted: >= 3 claim contradetti confermati
```

#### Regola aggiuntiva per claim

```
REGOLA EEAT-11: Usare solo fonti con reliability_tier >= B per articoli L0/L1
REGOLA EEAT-12: Per articoli YMYL usare solo reliability_tier >= C (mai D o blacklisted)
REGOLA EEAT-13: Se una fonte viene blacklistata, tutti gli articoli che la citano
                entrano automaticamente in needs_revision con priorità alta
```

---

### 15.5 HITL Checklist Verticali

Le checklist di review sono specifiche per verticale, non generiche. Il sistema carica la checklist corretta in base a `(vertical_id, risk_level)`.

```sql
-- Checklist per verticale
CREATE TABLE review.vertical_checklists (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    vertical_id     UUID NOT NULL REFERENCES editorial.verticals(id),
    risk_level      TEXT NOT NULL,  -- LOW|MEDIUM|HIGH|YMYL
    checklist_items JSONB NOT NULL, -- array di {id, label, required, category}
    version         INTEGER NOT NULL DEFAULT 1,
    active          BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (vertical_id, risk_level, version)
);
```

#### Esempio: checklist Legal YMYL

```json
{
  "vertical": "legal",
  "risk_level": "YMYL",
  "version": 1,
  "items": [
    { "id": "L01", "category": "normativa",    "required": true,  "label": "Ogni norma citata ha riferimento esatto (Art. + L./D.Lgs. + anno)" },
    { "id": "L02", "category": "normativa",    "required": true,  "label": "Nessuna norma abrogata citata come vigente" },
    { "id": "L03", "category": "normativa",    "required": true,  "label": "Se legge recente (<6 mesi), verificata su Gazzetta Ufficiale" },
    { "id": "L04", "category": "giurisprudenza","required": false, "label": "Sentenze citate con numero + sezione + anno (es: Cass. Civ. Sez. Lav. n. 12345/2024)" },
    { "id": "L05", "category": "disclaimer",   "required": true,  "label": "Disclaimer 'non costituisce parere legale' presente above the fold" },
    { "id": "L06", "category": "disclaimer",   "required": true,  "label": "Nessun consiglio operativo senza disclaimer esplicito" },
    { "id": "L07", "category": "fonti",        "required": true,  "label": "Tutte le fonti con reliability_tier >= C" },
    { "id": "L08", "category": "fonti",        "required": true,  "label": "Nessuna fonte blog/forum senza byline avvocato verificato" },
    { "id": "L09", "category": "reviewer",     "required": true,  "label": "Reviewer è avvocato abilitato con specializzazione coerente" },
    { "id": "L10", "category": "reviewer",     "required": false, "label": "Secondo reviewer per articoli con valore commerciale elevato (CPC > €3)" },
    { "id": "L11", "category": "contenuto",    "required": true,  "label": "Claim_verification_rate >= 0.95 (check automatico pre-review)" },
    { "id": "L12", "category": "contenuto",    "required": false, "label": "Aggiornamento esplicito con data se legge modificata negli ultimi 30gg" }
  ]
}
```

#### Esempio: checklist Health YMYL

```json
{
  "vertical": "health",
  "risk_level": "YMYL",
  "version": 1,
  "items": [
    { "id": "H01", "category": "fonti",       "required": true,  "label": "Fonti scientifiche: PubMed PMID o DOI citato esplicitamente" },
    { "id": "H02", "category": "fonti",       "required": true,  "label": "Studi citati: campione adeguato (n > 100 per claims forti)" },
    { "id": "H03", "category": "fonti",       "required": true,  "label": "Nessuna fonte: blog, forum, social senza peer-review" },
    { "id": "H04", "category": "claim",       "required": true,  "label": "Claim numerici (dosaggi, valori normali) verificati su fonti ISS/WHO/EMA" },
    { "id": "H05", "category": "claim",       "required": true,  "label": "Claim_verification_rate >= 0.95" },
    { "id": "H06", "category": "disclaimer",  "required": true,  "label": "Disclaimer medico presente above the fold e in conclusione" },
    { "id": "H07", "category": "disclaimer",  "required": true,  "label": "Nessuna indicazione terapeutica senza 'consultare il medico'" },
    { "id": "H08", "category": "reviewer",    "required": true,  "label": "Reviewer: medico specialista in area coerente col topic" },
    { "id": "H09", "category": "contenuto",   "required": false, "label": "Controindicazioni e effetti avversi inclusi (se topic farmaci/integratori)" },
    { "id": "H10", "category": "contenuto",   "required": true,  "label": "Nessuna promessa di cura o guarigione nel titolo/hero_answer" }
  ]
}
```

#### Esempio: checklist Finance YMYL

```json
{
  "vertical": "finance",
  "risk_level": "YMYL",
  "version": 1,
  "items": [
    { "id": "F01", "category": "dati",        "required": true,  "label": "Tassi/rendimenti con data di rilevazione e fonte (es: BCE, ABI, Banca d'Italia)" },
    { "id": "F02", "category": "dati",        "required": true,  "label": "Nessun dato economico privo di fonte e data" },
    { "id": "F03", "category": "disclaimer",  "required": true,  "label": "Disclaimer 'non costituisce consulenza finanziaria' above the fold" },
    { "id": "F04", "category": "disclaimer",  "required": true,  "label": "Conflitti di interesse dichiarati (es: affiliazioni bancarie)" },
    { "id": "F05", "category": "freshness",   "required": true,  "label": "Se contiene tassi BCE/mutui: data aggiornamento <= 14 giorni" },
    { "id": "F06", "category": "reviewer",    "required": true,  "label": "Reviewer ha abilitazione consulenza finanziaria o qualifica equivalente" },
    { "id": "F07", "category": "claim",       "required": true,  "label": "Proiezioni future con scenario base + worst case (nessuna promessa di rendimento)" },
    { "id": "F08", "category": "contenuto",   "required": false, "label": "Sezione 'Cosa fare ora': solo informativa, rimanda a professionista abilitato" }
  ]
}
```

---

## 16. FRONTEND MANAGEMENT CONSOLE

> **Risposta alla domanda**: Sì, il frontend è **assolutamente necessario**. Il backend da solo è un motore senza cruscotto. La console di gestione è ciò che rende il sistema operabile da un team editoriale reale senza toccare codice o database direttamente.

---

### 16.1 Perché è necessario

| Operazione | Senza frontend | Con frontend |
|---|---|---|
| Avviare un editorial run | API call manuale + JSON | Click + form configurato |
| Approvare/rifiutare HITL | SQL UPDATE diretto | Queue interface con preview |
| Monitorare costi per run | Query SQL su cost_tracking | Dashboard real-time |
| Gestire pruning di 30 articoli | Script manuale | Bulk action con preview |
| Schedulare aggiornamenti | Cron configurato a mano | Calendar drag & drop |
| Vedere conflitti di cannibalizzazione | Join query 4 tabelle | Visual cluster conflict map |
| Rivedere un articolo YMYL | JSON grezzo da API | Editor strutturato con checklist |

---

### 16.2 Stack tecnologico

**Scelta**: **Next.js 15 (App Router) + TypeScript** — API backend ASP.NET Core esistente.

Motivazioni:
- Il backend è già esposto come REST API: il frontend è un consumer puro
- Next.js SSR è ideale per dashboard dati con refresh continuo
- Ecosistema React ricco per editor, tabelle, drag & drop, grafici
- Deployment indipendente dal backend .NET

**Librerie chiave**:

| Area | Libreria |
|------|---------|
| UI components | shadcn/ui + Radix UI |
| Tabelle dati | TanStack Table v8 |
| Grafici performance | Recharts |
| Editor contenuto | TipTap (per review HITL) |
| Form management | React Hook Form + Zod |
| State management | Zustand |
| Real-time | SSE (Server-Sent Events da ASP.NET Core) o SignalR |
| Auth | NextAuth.js + JWT dal backend |
| Query/cache | TanStack Query (React Query) |

---

### 16.3 Moduli della Console

#### 16.3.1 Topic Queue & Run Manager

**Funzionalità**:
- Lista topics per verticale con `priority_score`, `status`, `last_run_date`
- Filtri: verticale / stato / range priority / YMYL flag
- **Avvia run**: form configurazione (locale, run_type, budget cap, simulation first toggle)
- **Risultato simulazione**: modal con `SimulationReport` — confirm/abort
- **Run status monitor**: progress in tempo reale via SSE (step corrente, token usati, costo live)
- **Run history**: tabella storico con costi, durate, articoli generati

```
┌─────────────────────────────────────────────────────────────────┐
│  TOPIC QUEUE                              [+ Nuovo Topic]       │
│  Verticale: [Finance ▼]  Stato: [Tutti ▼]  Priority: [>60 ▼]   │
├──────────────────────────┬───────┬───────┬──────────┬──────────┤
│ Topic                    │ Prio  │ Stato │ Last Run │ Azione   │
├──────────────────────────┼───────┼───────┼──────────┼──────────┤
│ Mutuo Prima Casa 2025    │ 82 ●  │ ready │ 3gg fa   │ [Run ▶]  │
│ Pensione Anticipata      │ 71 ●  │ hold  │ 12gg fa  │ [Review] │
│ ETF Migliori 2025        │ 65 ○  │ ready │ mai      │ [Run ▶]  │
└──────────────────────────┴───────┴───────┴──────────┴──────────┘
```

---

#### 16.3.2 HITL Review Interface

**Funzionalità**:
- Queue di review con SLA countdown, risk_level badge, priorità
- Filtri per: verticale, risk_level, reviewer assegnato, SLA status
- **Articolo in review**: layout 3 colonne
  - Sinistra: metadata articolo + score CQS + EEATRecord
  - Centro: preview articolo renderizzato (TipTap readonly o edit mode)
  - Destra: checklist verticale interattiva + note reviewer
- **Azioni**: Approve / Reject / Request Revision / Assign to Expert
- **Bulk actions**: approve batch articoli L3 LOW risk

```
┌────────────────────────────────────────────────────────────────────┐
│  HITL QUEUE                    Pending: 14   Urgente: 3   SLA: ⚠   │
├────────┬──────────────┬────────┬───────┬────────────┬─────────────┤
│ SLA    │ Articolo     │ Verti. │ Risk  │ Reviewer   │ Azione      │
├────────┼──────────────┼────────┼───────┼────────────┼─────────────┤
│ 2h ⚠   │ Vitamina D.. │ Health │ YMYL  │ Dr. Rossi  │ [Review →]  │
│ 8h     │ Licenz. Ill. │ Legal  │ YMYL  │ non asseg. │ [Assign]    │
│ 22h    │ Mutuo 2025   │ Finan. │ HIGH  │ M. Bianchi │ [Review →]  │
└────────┴──────────────┴────────┴───────┴────────────┴─────────────┘
```

---

#### 16.3.3 Content Calendar & Scheduling

**Funzionalità**:
- Vista calendario (settimana/mese) degli articoli scheduled
- Drag & drop per spostare scheduled_for
- Vista per verticale con color coding
- Trigger manuali: "Forza refresh per evento" (es: riunione BCE)
- Batch scheduling: "Pubblica tutti gli L3 approvati del cluster X entro venerdì"
- Alert visivo se SLA HITL non rispettato o se scheduled_for è passato senza publish

---

#### 16.3.4 Performance Dashboard

**Funzionalità**:
- Overview verticale: clicks, impressioni, CTR, avg_position aggregati
- Drill-down per cluster → per articolo
- Grafici trend: WoW, MoM per ogni metrica
- Heatmap posizioni SERP (articoli per avg_position bucket)
- Alert panel: articoli in alert GIALLO/ROSSO
- CQS distribution chart (A/B/C/D/F per verticale)
- **Underperformer panel**: articoli con CQS <= D + azioni rapide (refresh/noindex/merge)

---

#### 16.3.5 Cluster & Content Map

**Funzionalità**:
- Visualizzazione grafica del cluster (tree view o force-directed graph)
- Node colors: status (draft/published/deprecated), CQS tier
- Edge colors: link type (contextual/nav/breadcrumb)
- Click su nodo: side panel con metadata + CQS + preview
- Alert visivo per: orphan pages, broken links, conflitti di cannibalizzazione
- **Conflict overlay**: evidenzia i nodi in conflitto tra cluster diversi

---

#### 16.3.6 Source & Entity Manager

**Funzionalità**:
- Lista fonti con `reliability_tier`, `authority_score`, citation count
- Storico affidabilità: grafico authority_score nel tempo
- **Blacklist management**: motivo, date, articoli impattati
- Entity browser: tutte le entità estratte con tipo, verticale, articoli che le usano
- **Merge entity**: unire entità duplicate (es: "MacBook Pro M3" e "Apple MacBook Pro M3 2024")

---

#### 16.3.7 Cost & Budget Monitor

**Funzionalità**:
- Dashboard costi real-time per run in corso
- Riepilogo mensile: costo per verticale, per provider LLM, per operazione
- **Tabella `cost_per_published_page`**: costo per articolo + ROI proxy
- Budget caps: configurare `max_cost_usd_per_run` per verticale
- Alert: run che supera il 80% del budget → notifica (non blocco)

---

### 16.4 Struttura progetto Frontend

```
apps/
  agxioma-editorial-console/         ← Next.js 15 App Router
    src/
      app/
        (dashboard)/
          topics/
            page.tsx                  ← Topic Queue
            [id]/run/page.tsx         ← Avvia run + simulation report
          hitl/
            page.tsx                  ← Review queue
            [reviewId]/page.tsx       ← Review articolo
          calendar/
            page.tsx                  ← Content calendar
          performance/
            page.tsx                  ← Performance dashboard
            [vertical]/page.tsx
          clusters/
            page.tsx
            [clusterId]/page.tsx      ← Cluster map
          sources/
            page.tsx                  ← Source & entity manager
          costs/
            page.tsx                  ← Cost & budget monitor
        api/
          [...proxy]/route.ts         ← Proxy verso AGXIOME Editorial API
      components/
        hitl/
          ChecklistPanel.tsx
          ArticlePreview.tsx
          ReviewActions.tsx
        clusters/
          ClusterTreeView.tsx
          ClusterGraph.tsx
        performance/
          CqsDistributionChart.tsx
          SerpHeatmap.tsx
        shared/
          RiskBadge.tsx
          CqsTierBadge.tsx
          SlaCountdown.tsx
      lib/
        api/
          editorial.ts                ← client API typed (fetch + React Query hooks)
          hitl.ts
          performance.ts
          costs.ts
        sse/
          useRunProgress.ts           ← SSE hook per run in corso
      types/
        editorial.ts                  ← TypeScript types speculari ai modelli JSON backend
    package.json
    next.config.ts
    tsconfig.json
```

---

### 16.5 Endpoint API backend necessari (aggiuntivi)

Oltre a quelli già previsti in §13, la console richiede questi endpoint aggiuntivi:

```
# Topic & Run
GET    /api/editorial/topics?vertical=&status=&minPriority=
POST   /api/editorial/topics/{id}/simulate
POST   /api/editorial/topics/{id}/run
GET    /api/editorial/runs/{runId}/stream              ← SSE progress
GET    /api/editorial/runs/{runId}/cost-summary

# HITL
GET    /api/hitl/queue?vertical=&riskLevel=&reviewer=
GET    /api/hitl/{reviewId}
PUT    /api/hitl/{reviewId}/approve
PUT    /api/hitl/{reviewId}/reject
PUT    /api/hitl/{reviewId}/request-revision
PUT    /api/hitl/{reviewId}/assign

# Content
GET    /api/clusters/{clusterId}/tree
GET    /api/content-nodes/{nodeId}
GET    /api/articles/{articleId}/cqs
POST   /api/pruning/propose
PUT    /api/pruning/{actionId}/approve

# Sources
GET    /api/sources?tier=&vertical=
GET    /api/sources/{id}/reliability-history
PUT    /api/sources/{id}/blacklist
POST   /api/entities/merge

# Performance
GET    /api/performance/overview?vertical=&period=
GET    /api/performance/underperformers?vertical=&cqsTier=
GET    /api/costs/monthly-summary?vertical=
GET    /api/costs/per-page?vertical=
```

---

### 16.6 Struttura progetto aggiornata (.NET + Frontend)

```
src/
  AGXIOME.Editorial/                  ← Backend library (invariato §13)
  AGXIOME.Editorial.Api/              ← ASP.NET Core API project (nuovo)
    Controllers/
      EditorialController.cs
      HitlController.cs
      ClusterController.cs
      SourceController.cs
      PerformanceController.cs
      CostController.cs
    Hubs/
      RunProgressHub.cs               ← SignalR hub per SSE run progress
    AGXIOME.Editorial.Api.csproj

apps/
  agxioma-editorial-backend/          ← Host ASP.NET Core (porta 5010)
    Program.cs
    appsettings.json
  agxioma-editorial-console/          ← Next.js 15 (porta 3000)
    ...                               ← (vedi §16.4)
```

---

*Documento aggiornato il 22 Maggio 2026 — v2.0*  
*Aggiunte: §14 Decision & Simulation Layer, §15 Governance Lifecycle, §16 Frontend Management Console*  
*Basato su AGXIOME v1.0 — audit codebase 21 Maggio 2026*  
*Sprint di riferimento: MASTER_TASK_LIST.md Sprint 1-7*
