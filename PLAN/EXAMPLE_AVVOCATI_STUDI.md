# 🏛️ AGXIOMA — Studio Legale: Piano Progetto Pilota

## Versione
**v1.0** — 19 Maggio 2026  
**Stato**: Pianificazione

---

## TL;DR

Creare `apps/agxioma-legal-email/` — un sistema che:
1. Riceve email simulate via API
2. Le cataloga per ramo del diritto tramite `ForestSwarm`
3. Esegue query explosion multi-prospettiva con `HeavySwarm`
4. Interroga i codici civil/penale via `WikiQueryEngine` (embedding pgvector + rerank)
5. Genera bozze di risposta con citazioni articoli di legge
6. Sottopone tutto a HITL (Human-in-the-Loop) di un avvocato
7. Invia la risposta e salva a database con followup schedulato

Frontend React a 2 colonne con timeline real-time (SignalR).  
Docker Compose per PostgreSQL pgvector + Redis.  
Embedding su GPU via Ollama host (`mxbai-embed-large`).  
LLM DeepSeek (`deepseek-v4-flash`).

---

## Architettura

```
┌──────────────┐     ┌─────────────────────────────────────────────────────┐
│   FE React   │────▶│               Backend .NET 10                       │
│  (port 3001) │     │                                                     │
│              │     │  POST /api/email/ingest                             │
│  1. Inbox    │     │       │                                             │
│  2. Timeline │     │       ▼                                             │
│     (DX)     │     │  ┌──────────────────┐                               │
│  3. HITL     │     │  │EmailIngestion    │  Parsifica, validazione       │
│  4. Query    │     │  │Handler           │  salva DB                     │
│              │     │  └────────┬─────────┘                               │
│              │     │           ▼                                         │
│  SignalR ◀───│─────│──┐  ┌──────────────────┐                            │
│              │     │  │  │Categorization    │  ForestSwarm.FindBestMatch │
│              │     │  │  │Phase             │  → tree civile/penale/…   │
│              │     │  │  └────────┬─────────┘                            │
│              │     │  │           ▼                                     │
│              │     │  │  ┌──────────────────┐                            │
│              │     │  │  │Query Explosion   │  HeavySwarm → 5 query     │
│              │     │  │  │Phase             │  multi-prospettiva        │
│              │     │  │  └────────┬─────────┘                            │
│              │     │  │           ▼                                     │
│              │     │  │  ┌──────────────────┐                            │
│              │     │  │  │RAG Retrieval     │  WikiQueryEngine           │
│              │     │  │  │Phase             │  embed→pgvector→rerank    │
│              │     │  │  └────────┬─────────┘                            │
│              │     │  │           ▼                                     │
│              │     │  │  ┌──────────────────┐                            │
│              │     │  │  │Response Draft    │  LLM → bozza con           │
│              │     │  │  │Phase             │  citazioni [[art-X]]     │
│              │     │  │  └────────┬─────────┘                            │
│              │     │  │           ▼                                     │
│              │     │  │  ┌──────────────────┐                            │
│              │     │  │  │HITL Phase        │  Pausa per review         │
│              │     │  │  │ (HITLSwarm)      │  avvocato                 │
│              │     │  │  └────────┬─────────┘                            │
│              │     │  │           ▼                                     │
│              │     │  │  ┌──────────────────┐                            │
│              │     │  │  │Followup Phase    │  Invio email + DB         │
│              │     │  │  │                  │  + followup schedulato    │
│              │     │  └─────────────────────┘                            │
└──────────────┘     └─────────────────────────────────────────────────────┘

                                ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                         Infrastruttura Docker                            │
│                                                                          │
│  ┌─────────────────────┐  ┌──────────────┐  ┌──────────────────────┐    │
│  │ PostgreSQL pgvector  │  │    Redis      │  │  Ollama (host)       │    │
│  │ port 5433            │  │ port 6380     │  │  mxbai-embed-large   │    │
│  │ agxiome_legal DB     │  │ cache 24h TTL │  │  GPU (NVIDIA)        │    │
│  └─────────────────────┘  └──────────────┘  └──────────────────────┘    │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## Componenti Riutilizzati (nessun nuovo codice core)

| Componente | Libreria | Ruolo |
|---|---|---|
| `LLMWIKYEngine` | `AGXIOME.LLMWIKY` | Engine LLM DeepSeek |
| `OllamaMxbaiEmbedder` | `AGXIOME.Storage` | Embedding locale GPU via Ollama (`mxbai-embed-large`, 1024 dim) |
| `WikiIngester` | `AGXIOME.LLMWIKY` | Ingest codici civil/penale in pgvector |
| `WikiQueryEngine` | `AGXIOME.LLMWIKY` | Retrieval semantico + citazioni `[[slug]]` |
| `WikiDatabase` | `AGXIOME.LLMWIKY` | PostgreSQL pgvector + Redis cache |
| `ForestSwarm` | `AGXIOME.Core` | Routing keyword → specialista legale |
| `HeavySwarm` | `AGXIOME.Core` | Query explosion + ricerca parallela |
| `HITLSwarm` | `AGXIOME.Core` | Checkpoint HITL per review avvocato |
| `AgxiomeFunction` | `AGXIOME.Shared` | Classe base tool legali |
| `LlmReranker` | `AGXIOME.Storage` | Reranking via LLM dei risultati pgvector |
| `SignalR Hubs` | `AGXIOME.Realtime` | Colonna real-time DX WebSocket |
| React + Vite FE | `apps/agxioma-support-hub/frontend` | Pattern UI tab + pannelli |
| Docker Compose pattern | `docker-compose.f4-postgres.yml` | PostgreSQL pgvector + Redis |
| BackofficeRuntime pattern | `apps/agxioma-support-hub/BackofficeRuntime.cs` | Lifecycle run/ticket/survey |

---

## Stato Attuale (già fatto)

- [x] Scraping **1.200 articoli Codice Civile** → `data/codice-civile/`
- [x] Scraping **1.076 articoli Codice Penale** → `data/codice-penale/`
- [x] `data/` in `.gitignore`
- [x] Script `scripts/scrape-codici-full.ps1`
- [x] Pattern `AgxiomeServiceBuilder.UseModel()` disponibile (da fix precedente)
- [x] Pattern error handling 502/503 e frontend errori strutturati

---

## Steps Implementativi

### Fase 1 — Scaffolding App (monolitica)

| Step | Azione | Est. |
|---|---|---|
| 1.1 | Creare `apps/agxioma-legal-email/` con csproj, appsettings.json, Dockerfile | 30min |
| 1.2 | Aggiungere `docker-compose.legal-email.yml` (postgres pgvector + redis + be + fe) | 30min |
| 1.3 | Creare frontend React `frontend/` con layout 2 colonne | 2h |
| 1.4 | Creare `Tools/`: `SearchLegalCodeTool`, `DraftResponseTool`, `LookupPrecedentTool`, `EstimateCostTool` | 1h |
| 1.5 | Seed SQL tabelle: `legal_emails`, `legal_cases`, `legal_reviews`, `ingestion_state` | 20min |

### Fase 2 — Wiki Ingestion (integrata nell'app, pre-flight)

> **Decisione architetturale**: l'ingestion dei codici NON è un processo separato.  
> L'app `apps/agxioma-legal-email/` contiene al suo interno il motore di ingestion completo
> (WikiIngester, OllamaMxbaiEmbedder, WikiDatabase) e lo esegue all'avvio se necessario.

| Step | Azione | Est. |
|---|---|---|
| 2.1 | Creare `LegalCodeIngestionService.cs` — servizio hosted (IHostedService) che all'avvio: | 30min |
| | • Controlla tabella `ingestion_state` per ultimo timestamp + hash dei file | |
| | • Se `data/codice-civile/` o `data/codice-penale/` hanno file nuovi/modificati → ri-ingesta | |
| | • Altrimenti salta (flag "up-to-date" sul DB) | |
| | • Durante ingestion: chunk → embed (OllamaMxbaiEmbedder) → store in pgvector | |
| | • Salva timestamp completamento in `ingestion_state` | |
| 2.2 | Registrare `LegalCodeIngestionService` come hosted service in DI | 5min |
| 2.3 | Esporre `GET /api/legal/ingestion-status` per FE — mostra stato e progresso | 10min |
| 2.4 | Tabella `ingestion_state`: `(id, code_type TEXT, status TEXT, file_hash TEXT, chunk_count INT, completed_at TIMESTAMPTZ)` | 10min |

**Flusso startup:**
```
App avviata
  ├── PostgreSQL/Redis check (docker dipendenza)
  ├── Legge ingestion_state
  │   ├── Nessun record → FULL INGEST (chunk + embed + store)
  │   ├── Record exists, file invariati → SKIP (flag up-to-date)
  │   └── Record exists, file cambiati → RE-INGEST
  ├── Pronta per ricevere email
  └── GET /api/legal/ingestion-status → { status, chunkCount, completedAt }
```

### Fase 3 — Pipeline Agenti

| Step | Azione | Est. |
|---|---|---|
| 3.1 | `Program.cs`: bootstrap provider DeepSeek + OllamaMxbaiEmbedder + WikiDatabase + WikiQueryEngine | 30min |
| 3.2 | `ForestSwarm("legal-router")` con 5 trees: civile, penale, amministrativo, lavoro, fallback | 20min |
| 3.3 | `HeavySwarm("legal-researcher")` con 3 worker + question/synthesis agent | 20min |
| 3.4 | Agente `response-drafter` per generazione bozza | 15min |
| 3.5 | Wrapper `HITLSwarm` per pausa review avvocato | 15min |
| 3.6 | `PipelineOrchestrator.cs` — macchina a stati che coordina le 7 fasi | 1h |

### Fase 4 — Endpoints API

| Step | Endpoint | Est. |
|---|---|---|
| 4.1 | `POST /api/email/ingest` — riceve email, avvia pipeline | 20min |
| 4.2 | `GET /api/email/{id}/pipeline` — stato pipeline real-time | 15min |
| 4.3 | `POST /api/review/approve` — HITL approvazione bozza | 15min |
| 4.4 | `POST /api/review/reject` — HITL rigetto con note | 15min |
| 4.5 | `GET /api/legal/query?q=...` — query diretta codici (strumento avvocato) | 15min |
| 4.6 | `GET /api/dashboard` — stats globali | 10min |
| 4.7 | `GET /api/cases` — lista casi con stato | 10min |
| 4.8 | `GET /api/legal/ingestion-status` — stato ingestion codici | 5min |

### Fase 5 — Pipeline Esecuzione (7 fasi)

| Step | Fase | Descrizione |
|---|---|---|
| 5.1 | **INGESTED** — `EmailIngestionHandler`: parsifica mittente/oggetto/corpo, salva `legal_emails` | 30min |
| 5.2 | **CATEGORIZED** — `ForestSwarm.FindBestMatch()` → determina albero (civile/penale/lavoro/…) | 15min |
| 5.3 | **QUERY_EXPLODED** — `HeavySwarm` genera 5 query (normativa, giurisprudenza, dottrina, prassi, comparata) | 20min |
| 5.4 | **RAG_COMPLETED** — embed → pgvector search → dedup slug → `LlmReranker` | 20min |
| 5.5 | **ANALYSIS_DONE** — LLM analizza risultati + cita articoli con `[[slug]]` | 15min |
| 5.6 | **DRAFT_READY** — genera bozza email di risposta con link spiegazione | 15min |
| 5.7 | **HITL_PENDING** — `HITLSwarm` pausa, notifica avvocato via FE | 10min |
| 5.8 | **REPLY_SENT / FOLLOWUP_DONE** — su approvazione: invia email + DB + followup schedulato | 20min |

### Fase 6 — Frontend

| Step | Pannello | Est. |
|---|---|---|
| 6.1 | `EmailInboxPanel.tsx` — inbox email con stato pipeline | 45min |
| 6.2 | `PipelineTimelinePanel.tsx` — colonna DX real-time (SignalR) | 45min |
| 6.3 | `HITLReviewPanel.tsx` — avvocato vede bozza, approva/rigetta con note | 30min |
| 6.4 | `LegalQueryPanel.tsx` — query diretta ai codici | 30min |
| 6.5 | `DashboardPanel.tsx` — stats globali | 20min |
| 6.6 | `IngestionStatusPanel.tsx` — mostra stato ingestion codici (progress bar startup) | 15min |
| 6.7 | SignalR Hub backend `PipelineHub.cs` — eventi real-time per fase | 30min |
| 6.8 | `App.tsx` — layout 2 colonne con routing | 20min |

### Fase 7 — Docker e Test Demo

| Step | Azione | Est. |
|---|---|---|
| 7.1 | `docker-compose.legal-email.yml` completo: postgres + redis + be + fe | 20min |
| 7.2 | Script `test-pilot.ps1` per invio email demo + verifica pipeline | 20min |
| 7.3 | Test manuale: invio email → pipeline → HITL → approvazione → risposta | 30min |

---

## Macchina a Stati Pipeline

```
INGESTED ──▶ CATEGORIZED ──▶ QUERY_EXPLODED ──▶ RAG_COMPLETED
                                                      │
                                                      ▼
                                              ANALYSIS_DONE
                                                      │
                                                      ▼
                                              DRAFT_READY
                                                      │
                                                      ▼
                                              HITL_PENDING
                                             ┌────┴────┐
                                             ▼         ▼
                                       APPROVED   REJECTED
                                             │         │
                                             ▼         ▼
                                       REPLY_SENT  AWAITING_REVISION
                                             │
                                             ▼
                                       FOLLOWUP_DONE
```

---

## Albero ForestSwarm `legal-router`

```
legal-router (ForestSwarm)
├── 🌳 civile
│   ├── civilista         (contratti, locazioni, successioni, proprietà, responsabilità)
│   ├── family-lawyer     (affidamento, separazione, adozione, divorzio)
│   └── real-estate       (compravendita, usucapione, confini, immissioni)
├── 🌳 penale
│   ├── penalista         (reati comuni: furto, rapina, truffa, lesioni)
│   ├── white-collar      (reati societari, bancarotta, falso in bilancio)
│   └── criminal-defense  (difesa penale, misure cautelari, impugnazioni)
├── 🌳 amministrativo
│   ├── admin-lawyer      (appalti, concessioni, edilizia, urbanistica)
│   └── tax-lawyer        (tributi, cartelle esattoriali, contenzioso)
├── 🌳 lavoro
│   ├── labor-lawyer      (licenziamenti, contratti, infortuni, mobbing)
│   └── union-lawyer      (sindacale, scioperi, vertenze)
└── 🌳 fallback
    └── generalista       (tutto non classificato altrove)
```

---

## Struttura Directory App

```
apps/agxioma-legal-email/
├── AgxiomaLegalEmail.csproj
├── appsettings.json
├── Program.cs
├── Dockerfile
├── Tools/
│   ├── SearchLegalCodeTool.cs      — query WikiQueryEngine per codici
│   ├── LookupPrecedentTool.cs      — cerca casi simili in archivio
│   ├── DraftResponseTool.cs        — genera bozza email con template
│   └── EstimateCostTool.cs         — stima onorari basata su complessità
├── Handlers/
│   ├── EmailIngestionHandler.cs    — parsifica email + salva DB
│   └── PipelineOrchestrator.cs     — coordina le 7 fasi (macchina a stati)
├── Models/
│   ├── LegalEmail.cs               — email ricevuta
│   ├── LegalCase.cs                — caso legale
│   ├── PipelineState.cs            — enum stati pipeline
│   └── LegalReview.cs              — review avvocato
├── Hubs/
│   └── PipelineHub.cs              — SignalR real-time
├── frontend/
│   ├── package.json
│   ├── vite.config.ts
│   ├── src/
│   │   ├── App.tsx                 — layout 2 colonne
│   │   ├── EmailInboxPanel.tsx     — inbox sinistra
│   │   ├── PipelineTimelinePanel.tsx — colonna DX real-time
│   │   ├── HITLReviewPanel.tsx     — review avvocato
│   │   ├── LegalQueryPanel.tsx     — query codici
│   │   ├── DashboardPanel.tsx      — stats
│   │   ├── api.ts                  — client SDK
│   │   └── index.css               — stili
│   └── index.html
```

---

## Configurazione Appsettings

```json
{
  "Agent": { "Provider": "deepseek", "Model": "deepseek-v4-flash" },
  "Legal": {
    "CivilCodePath": "data/codice-civile",
    "PenalCodePath": "data/codice-penale",
    "EmbedModel": "mxbai-embed-large",
    "OllamaBaseUrl": "http://host.docker.internal:11434",
    "TopK": 8,
    "MinScore": 0.45,
    "RerankTopK": 5,
    "Workers": 3
  },
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5433;Database=agxiome_legal;Username=agxiome;Password=agxiome_legal_pw",
    "Redis": "localhost:6380"
  }
}
```

---

## Docker Compose

```yaml
services:
  legal-postgres:
    image: pgvector/pgvector:pg17
    container_name: agxioma-legal-postgres
    environment:
      POSTGRES_DB: agxiome_legal
      POSTGRES_USER: agxiome
      POSTGRES_PASSWORD: agxiome_legal_pw
    ports: ["5433:5432"]
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U agxiome -d agxiome_legal"]
      interval: 5s
      timeout: 5s
      retries: 20
    volumes:
      - legal-postgres-data:/var/lib/postgresql/data

  legal-redis:
    image: redis:7-alpine
    container_name: agxioma-legal-redis
    command: ["redis-server", "--appendonly", "yes"]
    ports: ["6380:6379"]
    volumes:
      - legal-redis-data:/data

  legal-backend:
    build:
      context: .
      dockerfile: apps/agxioma-legal-email/Dockerfile
    container_name: agxioma-legal-backend
    ports: ["5013:8080"]
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      DEEPSEEK_API_KEY: ${DEEPSEEK_API_KEY}
      Legal__OllamaBaseUrl: http://host.docker.internal:11434
    depends_on:
      legal-postgres: { condition: service_healthy }
      legal-redis: { condition: service_started }

  legal-frontend:
    build:
      context: apps/agxioma-legal-email/frontend
    container_name: agxioma-legal-frontend
    ports: ["3001:80"]
    depends_on: [legal-backend]

volumes:
  legal-postgres-data:
  legal-redis-data:
```

---

## Strumenti Locali Embedding/Reranking

| Strumento | Modello | Dove | Dimensione |
|---|---|---|---|
| **Embedding** | `mxbai-embed-large` | Ollama su GPU host (`http://host.docker.internal:11434`) | 1024 dim |
| **Reranking** | `LlmReranker` via DeepSeek | Backend LLM | N/A |

Tutto funzionante via `OllamaMxbaiEmbedder` + `LlmReranker` già presenti in `AGXIOME.Storage`.

---

## Potenziamenti (oltre baseline)

| Feature | Meccanismo | Priorità |
|---|---|---|
| **Query explosion multi-prospettiva** | HeavySwarm → 5 query (normativa/giurisprudenza/dottrina/prassi/comparata) | Alta |
| **Reranking ibrido** | pgvector cosine-sim → dedup slug → LlmReranker finale | Alta |
| **Colonna DX real-time** | SignalR `PipelineHub` → FE con timing per fase | Media |
| **HITL con note avvocato** | Endpoint reject con `reviewNotes` → bozza revisionabile | Media |
| **Precedenti (casi simili)** | `LookupPrecedentTool` su `legal_cases` storiche con embedding | Media |
| **Stima onorari** | `EstimateCostTool`: LLM valuta complessità → fascia di costo | Bassa |
| **Followup automatico** | Schedulato per N giorni dopo reply: check se cliente ha risposto | Bassa |
| **GDPR/privacy check** | Guardrail su contenuto email prima di salvare | Media |
| **Sentiment/urgenza** | LLM analizza tono → priorità (bassa/media/alta/urgente) | Bassa |
| **Notifiche multi-canale** | Email + Telegram (toolkit già esistente in `AGXIOME.Toolkits`) | Bassa |

---

## Albero Decisionale: Agente vs Avvocato Umano

```
ForestSwarm match score ≥ soglia?
├── SÌ → procede con agente specializzato
└── NO → routing a pool avvocati umani
         (caso raro per demo, sempre agente)
```

---

## Criteri di Verifica

1. `dotnet build apps/agxioma-legal-email/AgxiomaLegalEmail.csproj` ✅
2. `npm run build` in frontend ✅
3. `docker compose -f docker-compose.legal-email.yml up -d` → postgres healthy + redis ✅
4. `curl -X POST localhost:5013/api/legal/ingest` → codici ingeriti con embedding in pgvector ✅
5. `curl -X POST localhost:5013/api/email/ingest -d '{"sender":"cliente@test.it","subject":"Furto in appartamento","body":"...","attachments":[]}'` → stato `HITL_PENDING` ✅
6. `curl -X POST localhost:5013/api/review/approve -d '{"emailId":"...","lawyerId":"avv-001"}'` → stato `REPLY_SENT` ✅
7. FE mostra timeline DX completa con tutte le fasi e timing ✅

---

## Escluso dalla V1 (futuro)

| Feature | Motivo |
|---|---|
| OCR documenti allegati | Richiede `AGXIOME.Readers` non ancora integrato |
| Integrazione IMAP/POP3 reale | Solo API simulata in V1 |
| Fatturazione elettronica | Non pertinente al core demo |
| Firma digitale risposte | Complessità eccessiva per V1 |
| Integrazione PEC | Dipende da provider esterni |
| App mobile | Solo web in V1 |

---

## Appendice: Dati Codici Già Scraped

| Codice | Articoli | Directory |
|---|---|---|
| Codice Civile | 1.200 | `data/codice-civile/` |
| Codice Penale | 1.076 | `data/codice-penale/` |
| **Totale** | **2.276** | `data/` (escluso da git) |
