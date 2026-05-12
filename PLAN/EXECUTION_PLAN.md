# AGXIOME — Piano di Esecuzione Sequenziale Completo

> Strategia: **sequenziale ottimizzato** per un singolo sviluppatore.
> Ogni task rispetta le dipendenze. Data di inizio: **12 Maggio 2026**.
> I task di testing sono **integrati** dentro ogni task di sviluppo (non separati).
> Ogni riga = un task atomico con unit test integrati e testati prima di chiuderlo.

---

## Legenda

| Colonna | Significato |
|---------|-------------|
| **ID** | Identificatore univoco |
| **Task** | Nome del task |
| **Piano** | Piano di riferimento |
| **Dipende da** | ID prerequisito (vuoto = libero) |
| **Inizio** | Data pianificata |
| **Fine Pianificata** | Data di chiusura prevista |
| **Fine Effettiva** | Da compilare a completamento ✍️ |
| **Test** | Numero test da scrivere e far passare |
| **Stato** | ⬜ Todo / 🔄 In corso / ✅ Done / ❌ Bloccato |

---

## FASE 0 — Setup & Prerequisiti (Giorno 1)
> Nessuna dipendenza. Si esegue una sola volta prima di tutto.

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| F0-01 | Verifica build 0 errori, 176 test green | Setup | — | 12/05/26 | 12/05/26 | 11/05/26 | 0 | ✅ |
| F0-02 | Aggiorna tutte le NuGet a latest stable | Setup | — | 12/05/26 | 12/05/26 | 11/05/26 | 0 | ✅ |
| F0-03 | Aggiungi progetto `AGXIOME.Toolkits` alla solution | Setup | — | 12/05/26 | 12/05/26 | 11/05/26 | 0 | ✅ |
| F0-04 | Aggiungi progetto `AGXIOME.Readers` alla solution | Setup | — | 12/05/26 | 12/05/26 | 11/05/26 | 0 | ✅ |
| F0-05 | Aggiungi progetto `AGXIOME.Embedders` alla solution | Setup | — | 12/05/26 | 12/05/26 | 11/05/26 | 0 | ✅ |
| F0-06 | Aggiungi progetto `AGXIOME.Runtime` alla solution | Setup | — | 12/05/26 | 12/05/26 | 11/05/26 | 0 | ✅ |
| F0-07 | Configura CI/CD con quality gate (build + test) | Setup | — | 12/05/26 | 12/05/26 | — | 0 | ❌ |

---

## FASE 1 — Core Enhancements (Giorni 2–11)
> Dipendenza: F0-xx completati. Nessun altro blocco.

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| CE-01 | Background Execution — `BackgroundRunWorker` persistente, polling, reconnect SSE | P09 | F0-01 | 13/05/26 | 14/05/26 | 11/05/26 | 10 | ✅ |
| CE-02 | Cancellation Manager — In-memory + Redis, annulla run da thread separato | P09 | CE-01 | 15/05/26 | 15/05/26 | 11/05/26 | 8 | ✅ |
| CE-03 | Remote Agent — `RemoteAgent` esecuzione su istanza remota via REST | P09 | CE-02 | 16/05/26 | 17/05/26 | 11/05/26 | 8 | ✅ |
| CE-04 | Agent Serialization — `ToDictionary()`, `FromDictionary()`, `Save()`, `Load()` | P09 | CE-03 | 19/05/26 | 19/05/26 | 11/05/26 | 10 | ✅ |
| CE-05 | Model-as-String — shorthand `"openai:gpt-4o"` senza istanziare classi | P09 | CE-04 | 20/05/26 | 20/05/26 | 11/05/26 | 8 | ✅ |
| CE-06 | Response Caching — Cache risposte LLM con TTL configurabile | P09 | CE-05 | 20/05/26 | 20/05/26 | 11/05/26 | 8 | ✅ |
| CE-07 | Token Counting — Contatore token per provider (tiktoken-like) | P09 | CE-05 | 21/05/26 | 21/05/26 | 11/05/26 | 8 | ✅ |
| CE-08 | Model Compatibility — Mappa modelli → provider + warning deprecation | P09 | CE-07 | 21/05/26 | 21/05/26 | 11/05/26 | 8 | ✅ |

---

## FASE 2 — Chunker & Reranker (Giorni 12–19)
> Nessuna dipendenza esterna. Può partire subito dopo F0.

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| C-01 | RecursiveChunker — split ricorsivo `\n\n → \n → . → ? → !` | P07 | F0-04 | 22/05/26 | 22/05/26 | 11/05/26 | 6 | ✅ |
| C-02 | SemanticChunker — spezza dove cambia significato via embeddings | P07 | C-01 | 22/05/26 | 23/05/26 | 11/05/26 | 5 | ✅ |
| C-03 | AgenticChunker — LLM decide dove spezzare | P07 | C-02 | 23/05/26 | 23/05/26 | 11/05/26 | 5 | ✅ |
| C-04 | CodeChunker — split per function/class/method via Roslyn | P07 | C-03 | 26/05/26 | 26/05/26 | 11/05/26 | 5 | ✅ |
| C-05 | DocumentChunker — Multi-livello: headings → paragraphs → sentences | P07 | C-04 | 26/05/26 | 26/05/26 | 11/05/26 | 5 | ✅ |
| C-06 | CSV Row Chunker — ogni riga CSV = chunk, preserva intestazione | P07 | C-05 | 27/05/26 | 27/05/26 | 11/05/26 | 5 | ✅ |
| C-07 | JSON Path Chunker — split per path JSON, preserva struttura | P07 | C-06 | 27/05/26 | 27/05/26 | 11/05/26 | 5 | ✅ |
| C-08 | SlidingWindowChunker — finestra scorrevole con overlap configurabile | P07 | C-07 | 27/05/26 | 27/05/26 | 11/05/26 | 5 | ✅ |
| C-09 | HybridChunker — combina semantic + recursive | P07 | C-08 | 28/05/26 | 28/05/26 | 11/05/26 | 5 | ✅ |
| RK-01 | Cohere Rerank — `rerank-english-v3.0` via HTTP API | P07 | C-09 | 28/05/26 | 28/05/26 | 11/05/26 | 5 | ✅ |
| RK-02 | Cohere Rerank Multilingual — `rerank-multilingual-v3.0` | P07 | RK-01 | 29/05/26 | 29/05/26 | 11/05/26 | 5 | ✅ |
| RK-03 | Jina Reranker — `jina-reranker-v2` via HTTP API | P07 | RK-02 | 29/05/26 | 29/05/26 | 11/05/26 | 5 | ✅ |
| RK-04 | Voyage Rerank — `voyage-rerank-2` via HTTP API | P07 | RK-03 | 29/05/26 | 29/05/26 | 11/05/26 | 5 | ✅ |
| RK-05 | BGE Reranker — `bge-reranker-v2-m3` via ONNX | P07 | RK-04 | 02/06/26 | 02/06/26 | 11/05/26 | 5 | ✅ |
| RK-06 | Cross-Encoder — `ms-marco-MiniLM-L6-v2` via ONNX | P07 | RK-05 | 02/06/26 | 02/06/26 | 11/05/26 | 5 | ✅ |
| RK-07 | MixedBread Rerank — `mxbai-rerank-xsmall-v1` via ONNX | P07 | RK-06 | 03/06/26 | 03/06/26 | 11/05/26 | 5 | ✅ |
| RK-08 | LLM Reranker — relevance scoring via ILLMProvider | P07 | RK-07 | 03/06/26 | 03/06/26 | 11/05/26 | 5 | ✅ |

---

## FASE 3 — Embedders (Giorni 20–34)
> Nessuna dipendenza esterna. Può partire dopo F0.

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| E-01 | OpenAI Embeddings — `text-embedding-3-small/large` via HTTP API | P06 | F0-05 | 04/06/26 | 04/06/26 | 11/05/26 | 5 | ✅ |
| E-02 | OpenAI Embeddings v3 — `text-embedding-3-large` (3072 dim) | P06 | E-01 | 04/06/26 | 04/06/26 | 11/05/26 | 5 | ✅ |
| E-03 | Anthropic Embeddings — via API | P06 | E-02 | 05/06/26 | 05/06/26 | 11/05/26 | 5 | ✅ |
| E-04 | Cohere Embeddings — `embed-english-v3.0`, 1024/4096 dim | P06 | E-03 | 05/06/26 | 05/06/26 | 11/05/26 | 5 | ✅ |
| E-05 | Mistral Embeddings — `mistral-embed`, 1024 dim | P06 | E-04 | 05/06/26 | 05/06/26 | 11/05/26 | 5 | ✅ |
| E-06 | Google Gemini Embed — `text-embedding-004`, 768 dim | P06 | E-05 | 08/06/26 | 08/06/26 | 11/05/26 | 5 | ✅ |
| E-07 | Voyage AI — `voyage-3-large`, 1024/2048 dim | P06 | E-06 | 08/06/26 | 08/06/26 | 11/05/26 | 5 | ✅ |
| E-08 | Jina AI — `jina-embeddings-v3`, 1024/2048 dim | P06 | E-07 | 09/06/26 | 09/06/26 | 11/05/26 | 5 | ✅ |
| E-09 | AWS Bedrock Titan — `titan-embed-text-v2`, 1024/1536 dim | P06 | E-08 | 09/06/26 | 09/06/26 | 11/05/26 | 5 | ✅ |
| E-10 | Azure OpenAI Embed — `text-embedding-3-large`, 3072 dim | P06 | E-09 | 10/06/26 | 10/06/26 | 11/05/26 | 5 | ✅ |
| E-11 | ONNX BERT — `all-MiniLM-L6-v2`, 384 dim, mean pooling | P06 | E-10 | 10/06/26 | 10/06/26 | 11/05/26 | 5 | ✅ |
| E-12 | ONNX MPNet — `all-mpnet-base-v2`, 768 dim | P06 | E-11 | 10/06/26 | 11/06/26 | 11/05/26 | 5 | ✅ |
| E-13 | ONNX Instructor — `instructor-xl`, 768 dim | P06 | E-12 | 11/06/26 | 11/06/26 | 11/05/26 | 5 | ✅ |
| E-14 | Ollama Embed — `nomic-embed-text`, 768 dim | P06 | E-13 | 11/06/26 | 11/06/26 | 11/05/26 | 5 | ✅ |
| E-15 | Ollama Embed v2 — `mxbai-embed-large`, 1024 dim | P06 | E-14 | 12/06/26 | 12/06/26 | 11/05/26 | 5 | ✅ |
| E-16 | llama.cpp Embed — server GGUF, 768+ dim | P06 | E-15 | 12/06/26 | 12/06/26 | 11/05/26 | 5 | ✅ |
| E-17 | HuggingFace API — modelli variabili via API | P06 | E-16 | 15/06/26 | 15/06/26 | 11/05/26 | 5 | ✅ |
| E-18 | NVIDIA NIM Embed — `NV-Embed-QA`, 1024 dim | P06 | E-17 | 15/06/26 | 15/06/26 | 11/05/26 | 5 | ✅ |
| E-19 | Multilingual E5 — `multilingual-e5-large`, ONNX, 1024 dim | P06 | E-18 | 16/06/26 | 16/06/26 | 11/05/26 | 7 | ✅ |
| E-20 | BGE Embedding — `bge-large-en-v1.5`, ONNX, 1024 dim | P06 | E-19 | 16/06/26 | 16/06/26 | 11/05/26 | 7 | ✅ |
| E-21 | GTE Embedding — `gte-large-en-v1.5`, ONNX, 1024 dim | P06 | E-20 | 16/06/26 | 16/06/26 | 11/05/26 | 7 | ✅ |
| E-22 | SOLAR Embedding — `solar-embedding`, 4096 dim | P06 | E-21 | 17/06/26 | 17/06/26 | 11/05/26 | 7 | ✅ |
| E-23 | M3E Embedding — `m3e-large`, ONNX, 1024 dim | P06 | E-22 | 17/06/26 | 17/06/26 | 11/05/26 | 7 | ✅ |
| E-24 | BAAI/bge-m3 — multi-lingual, ONNX, 1024 dim | P06 | E-23 | 18/06/26 | 18/06/26 | 11/05/26 | 7 | ✅ |
| E-25 | Nomic Embed v1.5 — `nomic-embed-text-v1.5`, ONNX, 768/1536 dim | P06 | E-24 | 18/06/26 | 18/06/26 | 11/05/26 | 7 | ✅ |

---

## FASE 4 — DB Provider (Giorni 35–54)
> Dipende da: CE-08 completato (P09 Core).

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| DB-01 | SQLite Store — embedded, schema auto-create, full CRUD | P03 | CE-08 | 19/06/26 | 19/06/26 | 26/05/12 | 5 | ✅ |
| DB-02 | SQL Server Store — enterprise, transazioni, idempotency | P03 | DB-01 | 19/06/26 | 20/06/26 | 26/05/12 | 5 | ✅ |
| DB-03 | SQL Server LocalDB — dev-mode, attach/detach | P03 | DB-02 | 20/06/26 | 20/06/26 | 26/05/12 | 5 | ✅ |
| DB-04 | Neon (Serverless PG) — endpoint custom, connection pooling | P03 | DB-03 | 22/06/26 | 22/06/26 | 26/05/12 | 5 | ✅ |
| DB-05 | Supabase — Npgsql + Supabase SDK, RLS awareness | P03 | DB-04 | 22/06/26 | 23/06/26 | 26/05/12 | 5 | ✅ |
| DB-06 | SingleStore — SQL distribuito, bulk insert | P03 | DB-05 | 23/06/26 | 23/06/26 | 26/05/12 | 5 | ✅ |
| DB-07 | CockroachDB — PG-compat, serializability | P03 | DB-06 | 24/06/26 | 24/06/26 | 26/05/12 | 5 | ✅ |
| DB-08 | TimescaleDB — time-series, hypertable, time_bucket | P03 | DB-07 | 24/06/26 | 24/06/26 | 26/05/12 | 5 | ✅ |
| DB-09 | DynamoDB — KV/doc, GSI, PAY_PER_REQUEST | P03 | DB-08 | 25/06/26 | 25/06/26 | 26/05/12 | 5 | ✅ |
| DB-10 | Cosmos DB — multi-model, partition key, RU provisioning | P03 | DB-09 | 25/06/26 | 26/06/26 | 26/05/12 | 5 | ✅ |
| DB-11 | Firestore — document, subcollections, transactions | P03 | DB-10 | 26/06/26 | 26/06/26 | 26/05/12 | 5 | ✅ |
| DB-12 | SurrealDB — multi-model, graph, SurrealQL | P03 | DB-11 | 29/06/26 | 29/06/26 | 26/05/12 | 5 | ✅ |
| DB-13 | RavenDB — document, LINQ queries, embedded mode | P03 | DB-12 | 29/06/26 | 30/06/26 | 26/05/12 | 5 | ✅ |
| DB-14 | Couchbase — KV+doc, N1QL, cluster management | P03 | DB-13 | 30/06/26 | 30/06/26 | 26/05/12 | 5 | ✅ |
| DB-15 | Redis Session Store — `RedisSessionStore`, TTL, prefix | P03 | DB-14 | 01/07/26 | 01/07/26 | 26/05/12 | 5 | ✅ |
| DB-16 | Memcached — KV cache, bucket hashing | P03 | DB-15 | 01/07/26 | 01/07/26 | 26/05/12 | 5 | ✅ |
| DB-17 | Hazelcast — KV distribuito, near-cache | P03 | DB-16 | 02/07/26 | 02/07/26 | 26/05/12 | 5 | ✅ |
| DB-18 | Etcd — KV distribuito, watch, leader election | P03 | DB-17 | 02/07/26 | 02/07/26 | 26/05/12 | 5 | ✅ |
| DB-19 | ClickHouse — columnar OLAP, async insert, materialized view | P03 | DB-18 | 03/07/26 | 03/07/26 | 26/05/12 | 5 | ✅ |
| DB-20 | InfluxDB — time series, line protocol, flux queries | P03 | DB-19 | 03/07/26 | 03/07/26 | 26/05/12 | 5 | ✅ |
| DB-21 | QuestDB — time series PG-compat, ILP protocol | P03 | DB-20 | 04/07/26 | 04/07/26 | 26/05/12 | 5 | ✅ |
| DB-22 | DuckDB — analytical embedded, Parquet, Arrow | P03 | DB-21 | 06/07/26 | 06/07/26 | 26/05/12 | 5 | ✅ |
| DB-23 | Elasticsearch Store — index/search, BM25 | P03 | DB-22 | 06/07/26 | 07/07/26 | 26/05/12 | 5 | ✅ |
| DB-24 | Meilisearch Store — full-text, typo-tolerance, facets | P03 | DB-23 | 07/07/26 | 07/07/26 | 26/05/12 | 5 | ✅ |
| DB-25 | JSON File Store — file-based, atomic write, indexing | P03 | DB-24 | 08/07/26 | 08/07/26 | 26/05/12 | 5 | ✅ |

---

## FASE 5 — Vector DB Provider (Giorni 55–84)
> Dipende da: E-25 completato (almeno un embedder cloud per test di ricerca).

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| VD-01 | Pinecone — serverless, namespace, metadata filter | P04 | E-25 | 09/07/26 | 09/07/26 | 12/05/26 | 7 | ✅ |
| VD-02 | Weaviate Cloud — hybrid search, multi-tenancy | P04 | VD-01 | 09/07/26 | 10/07/26 | 12/05/26 | 7 | ✅ |
| VD-03 | Qdrant Cloud — pure vector, named vectors, payload | P04 | VD-02 | 10/07/26 | 10/07/26 | 12/05/26 | 7 | ✅ |
| VD-04 | Milvus Cloud (Zilliz) — ANN, HNSW, IVF_FLAT | P04 | VD-03 | 10/07/26 | 11/07/26 | 12/05/26 | 7 | ✅ |
| VD-05 | Chroma DB — embedded, persistent, distance metric | P04 | VD-04 | 13/07/26 | 13/07/26 | 12/05/26 | 7 | ✅ |
| VD-06 | Supabase pgvector — SQL+vector, HNSW index | P04 | VD-05 | 13/07/26 | 13/07/26 | 12/05/26 | 7 | ✅ |
| VD-07 | Neon pgvector — serverless+vector | P04 | VD-06 | 14/07/26 | 14/07/26 | 12/05/26 | 7 | ✅ |
| VD-08 | Timescale pgvector — time+vector | P04 | VD-07 | 14/07/26 | 14/07/26 | 12/05/26 | 7 | ✅ |
| VD-09 | CockroachDB pgvector — distributed+vector | P04 | VD-08 | 14/07/26 | 15/07/26 | 12/05/26 | 7 | ✅ |
| VD-10 | SingleStore vector — SQL+vector, dot product | P04 | VD-09 | 15/07/26 | 15/07/26 | 12/05/26 | 7 | ✅ |
| VD-11 | Qdrant self-hosted — gRPC, HNSW, quantization | P04 | VD-10 | 16/07/26 | 16/07/26 | 12/05/26 | 7 | ✅ |
| VD-12 | Weaviate self-hosted — modules, HNSW, BM25 | P04 | VD-11 | 16/07/26 | 16/07/26 | 12/05/26 | 7 | ✅ |
| VD-13 | Milvus self-hosted — standalone, cluster mode | P04 | VD-12 | 17/07/26 | 17/07/26 | 12/05/26 | 7 | ✅ |
| VD-14 | Chroma self-hosted — HTTP server mode | P04 | VD-13 | 17/07/26 | 17/07/26 | 12/05/26 | 7 | ✅ |
| VD-15 | Elasticsearch Dense — kNN, sparse+dense hybrid | P04 | VD-14 | 20/07/26 | 20/07/26 | 12/05/26 | 7 | ✅ |
| VD-16 | Meilisearch vector — semantic + full-text hybrid | P04 | VD-15 | 20/07/26 | 21/07/26 | 12/05/26 | 7 | ✅ |
| VD-17 | Vald — cloud-native ANN, gRPC | P04 | VD-16 | 21/07/26 | 21/07/26 | 12/05/26 | 7 | ✅ |
| VD-18 | Vespa — big data + vector, YQL | P04 | VD-17 | 22/07/26 | 22/07/26 | 12/05/26 | 7 | ✅ |
| VD-19 | SQLite + sqlite-vec — embedded vector extension | P04 | VD-18 | 22/07/26 | 22/07/26 | 12/05/26 | 7 | ✅ |
| VD-20 | LanceDB — embedded columnar vector DB | P04 | VD-19 | 23/07/26 | 23/07/26 | 12/05/26 | 7 | ✅ |
| VD-21 | DuckDB + vector — analytical + ANN | P04 | VD-20 | 23/07/26 | 23/07/26 | 12/05/26 | 7 | ✅ |
| VD-22 | Redis Stack — RedisSearch, vector index | P04 | VD-21 | 24/07/26 | 24/07/26 | 12/05/26 | 7 | ✅ |
| VD-23 | MongoDB Atlas — $vectorSearch, embedding field | P04 | VD-22 | 24/07/26 | 24/07/26 | 12/05/26 | 7 | ✅ |
| VD-24 | Cosmos DB vCore — vector search, indexing policy | P04 | VD-23 | 27/07/26 | 27/07/26 | 12/05/26 | 7 | ✅ |
| VD-25 | HNSWLib — in-memory HNSW, serialization | P04 | VD-24 | 27/07/26 | 28/07/26 | 12/05/26 | 7 | ✅ |
| VD-26 | FAISS — ANN via ML.NET + ONNX Runtime | P04 | VD-25 | 28/07/26 | 28/07/26 | 12/05/26 | 7 | ✅ |
| VD-27 | Annoy — approx NN, serialization, C# binding | P04 | VD-26 | 29/07/26 | 29/07/26 | 12/05/26 | 7 | ✅ |
| VD-28 | ScaNN — ANN Google, gRPC wrapper | P04 | VD-27 | 29/07/26 | 29/07/26 | 12/05/26 | 7 | ✅ |
| VD-29 | RocksDB + HNSW — embedded KV + vector index | P04 | VD-28 | 30/07/26 | 30/07/26 | 12/05/26 | 7 | ✅ |
| VD-30 | SQL Server + vector — enterprise vector search | P04 | VD-29 | 30/07/26 | 31/07/26 | 12/05/26 | 7 | ✅ |

---

## FASE 6 — Document Readers (Giorni 85–104)
> Dipende da: RK-08 (Chunker/Reranker completati).

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| R-01 | PDF Reader — PdfPig, testo + sezioni + metadata, SHA256 | P05 | RK-08 | 03/08/26 | 03/08/26 | 12/05/26 | 8 | ✅ |
| R-02 | DOCX Reader — OpenXml, paragrafi, headings, tabelle | P05 | R-01 | 04/08/26 | 04/08/26 | 12/05/26 | 8 | ✅ |
| R-03 | XLSX Reader — ClosedXML, fogli, righe, formule | P05 | R-02 | 04/08/26 | 04/08/26 | 12/05/26 | 8 | ✅ |
| R-04 | PPTX Reader — OpenXml, slide, note, testo | P05 | R-03 | 05/08/26 | 05/08/26 | 12/05/26 | 8 | ✅ |
| R-05 | CSV Reader — CsvHelper, tipi, nullable, encoding | P05 | R-04 | 05/08/26 | 05/08/26 | 12/05/26 | 8 | ✅ |
| R-06 | JSON Reader — System.Text.Json, path, schema | P05 | R-05 | 06/08/26 | 06/08/26 | 12/05/26 | 8 | ✅ |
| R-07 | XML Reader — XDocument, namespace, XPath | P05 | R-06 | 06/08/26 | 06/08/26 | 12/05/26 | 8 | ✅ |
| R-08 | Markdown Reader — Markdig, headings, code blocks | P05 | R-07 | 07/08/26 | 07/08/26 | 12/05/26 | 8 | ✅ |
| R-09 | HTML Reader — HtmlAgilityPack, strip tags, sections | P05 | R-08 | 07/08/26 | 07/08/26 | 12/05/26 | 8 | ✅ |
| R-10 | Plain Text Reader — encoding auto-detect, line handling | P05 | R-09 | 10/08/26 | 10/08/26 | 12/05/26 | 8 | ✅ |
| R-11 | YAML Reader — YamlDotNet, nested, array | P05 | R-10 | 10/08/26 | 10/08/26 | 12/05/26 | 8 | ✅ |
| R-12 | RTF Reader — strip formatting, extract text | P05 | R-11 | 11/08/26 | 11/08/26 | 12/05/26 | 8 | ✅ |
| R-13 | Website Reader — URL scraping, JS-rendered, HtmlAgilityPack | P05 | R-12 | 11/08/26 | 12/08/26 | 12/05/26 | 8 | ✅ |
| R-14 | Wikipedia Reader — Wiki API, summary, sections | P05 | R-13 | 12/08/26 | 12/08/26 | 12/05/26 | 8 | ✅ |
| R-15 | Arxiv Reader — API, abstract, metadata, PDF download | P05 | R-14 | 13/08/26 | 13/08/26 | 12/05/26 | 8 | ✅ |
| R-16 | PubMed Reader — Entrez API, abstract, mesh terms | P05 | R-15 | 13/08/26 | 13/08/26 | 12/05/26 | 8 | ✅ |
| R-17 | YouTube Transcript — subtitles API, timing, language | P05 | R-16 | 14/08/26 | 14/08/26 | 12/05/26 | 8 | ✅ |
| R-18 | Firecrawl Reader — scrape + crawl + extract via API | P05 | R-17 | 14/08/26 | 14/08/26 | 12/05/26 | 8 | ✅ |
| R-19 | AWS S3 Reader — list + download + stream | P05 | R-18 | 17/08/26 | 17/08/26 | 12/05/26 | 8 | ✅ |
| R-20 | Azure Blob Reader — container + blob + SAS | P05 | R-19 | 17/08/26 | 18/08/26 | 12/05/26 | 8 | ✅ |
| R-21 | GCS Reader — bucket + object + signed URL | P05 | R-20 | 18/08/26 | 18/08/26 | 12/05/26 | 8 | ✅ |
| R-22 | GitHub Reader — repo file tree + raw content | P05 | R-21 | 19/08/26 | 19/08/26 | 12/05/26 | 8 | ✅ |
| R-23 | SharePoint Reader — Graph API, libraries, versioning | P05 | R-22 | 19/08/26 | 19/08/26 | 12/05/26 | 8 | ✅ |
| R-24 | Image OCR Reader — Tesseract, language packs | P05 | R-23 | 20/08/26 | 20/08/26 | 12/05/26 | 8 | ✅ |
| R-25 | Audio Transcriber — Whisper API, diarization | P05 | R-24 | 20/08/26 | 20/08/26 | 12/05/26 | 8 | ✅ |
| R-26 | EPUB Reader — metadata, chapters, TOC | P05 | R-25 | 21/08/26 | 21/08/26 | 12/05/26 | 8 | ✅ |
| R-27 | Notion Reader — pages, blocks, databases via API | P05 | R-26 | 21/08/26 | 21/08/26 | 12/05/26 | 8 | ✅ |
| R-28 | Confluence Reader — spaces, pages, attachments | P05 | R-27 | 24/08/26 | 24/08/26 | 12/05/26 | 8 | ✅ |

---

## FASE 7 — Storage & Session Enhancements (Giorni 105–114)
> Dipende da: CE-08 + DB-25 completati.

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| SS-01 | Redis Session Storage — `RedisSessionStore`, TTL, serialize | P10 | DB-25 | 25/08/26 | 25/08/26 | 12/05/26 | 7 | ✅ |
| SS-02 | Agent Storage Bridge — salva/carica agent da DB astratto | P10 | SS-01 | 25/08/26 | 26/08/26 | 12/05/26 | 7 | ✅ |
| SS-03 | Team Storage — salva/carica team + default tools | P10 | SS-02 | 26/08/26 | 26/08/26 | 12/05/26 | 7 | ✅ |
| SS-04 | Workflow History — cronologia esecuzioni + accesso step | P10 | SS-03 | 27/08/26 | 27/08/26 | 12/05/26 | 7 | ✅ |
| SS-05 | Workflow Serialization — serialize/deserialize DAG | P10 | SS-04 | 27/08/26 | 27/08/26 | 12/05/26 | 7 | ✅ |
| SS-06 | Workflow HITL — pause step per approvazione/input | P10 | SS-05 | 28/08/26 | 01/09/26 | 12/05/26 | 7 | ✅ |
| SS-07 | Contents DB — storage per contenuti knowledge | P10 | SS-06 | 01/09/26 | 02/09/26 | 12/05/26 | 7 | ✅ |
| SS-08 | Team Default Tools — tool automatici per team | P10 | SS-07 | 02/09/26 | 02/09/26 | 12/05/26 | 7 | ✅ |
| SS-09 | Callable Members — risoluzione membri a runtime | P10 | SS-08 | 03/09/26 | 03/09/26 | 12/05/26 | 7 | ✅ |
| SS-10 | Distributed RAG Team — ricerca distribuita su più fonti | P10 | SS-09 | 03/09/26 | 04/09/26 | 12/05/26 | 7 | ✅ |

---

## FASE 8 — Tools Infrastructure (Giorni 115–120)
> Dipende da: CE-08 completato.

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| TI-01 | Tool Result Cache — TTL + hash chiave, ConcurrentDictionary | P11 | CE-08 | 05/09/26 | 05/09/26 | 12/05/26 | 7 | ✅ |
| TI-02 | Tool Call Limit — max call + max error consecutivi per run | P11 | TI-01 | 05/09/26 | 05/09/26 | 12/05/26 | 7 | ✅ |
| TI-03 | Stop After Tool — config per fermare run dopo tool specifica | P11 | TI-02 | 08/09/26 | 08/09/26 | 12/05/26 | 5 | ✅ |
| TI-04 | Include/Exclude Tools — filtri tool per sessione/run | P11 | TI-03 | 08/09/26 | 08/09/26 | 12/05/26 | 5 | ✅ |
| TI-05 | Callable Factory — genera callable da tool per LLM | P11 | TI-04 | 09/09/26 | 09/09/26 | 12/05/26 | 7 | ✅ |
| TI-06 | Tool Metrics — usage count, latency, error rate per tool | P11 | TI-05 | 09/09/26 | 09/09/26 | 12/05/26 | 7 | ✅ |

---

## FASE 9 — Model Providers (Giorni 121–160)
> Dipende da: CE-08 completato.

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| M-01 | Groq Provider — `api.groq.com/openai/v1`, inferenza veloce | P01 | CE-08 | 10/09/26 | 10/09/26 | 12/05/26 | 5 | ✅ |
| M-02 | Mistral AI Provider — Large/Small/Tiny, function calling | P01 | M-01 | 10/09/26 | 10/09/26 | 12/05/26 | 5 | ✅ |
| M-03 | xAI Grok Provider — Grok-2/3, vision | P01 | M-02 | 11/09/26 | 11/09/26 | 12/05/26 | 5 | ✅ |
| M-04 | Cohere Provider — Command R+, tool use | P01 | M-03 | 11/09/26 | 11/09/26 | 12/05/26 | 5 | ✅ |
| M-05 | Perplexity Provider — Sonar, search-backed | P01 | M-04 | 12/09/26 | 12/09/26 | 12/05/26 | 5 | ✅ |
| M-06 | Together AI Provider — 200+ modelli, OpenAI-compat | P01 | M-05 | 12/09/26 | 12/09/26 | 12/05/26 | 5 | ✅ |
| M-07 | Fireworks AI Provider — fast inference, OpenAI-compat | P01 | M-06 | 15/09/26 | 15/09/26 | 12/05/26 | 5 | ✅ |
| M-08 | Cerebras Provider — wafer-scale, inferenza Llama | P01 | M-07 | 15/09/26 | 15/09/26 | 12/05/26 | 5 | ✅ |
| M-09 | AWS Bedrock Provider — Anthropic/Meta/Mistral via AWS SDK | P01 | M-08 | 16/09/26 | 17/09/26 | 12/05/26 | 5 | ✅ |
| M-10 | Azure AI Foundry Provider — endpoint custom, modelli MS | P01 | M-09 | 17/09/26 | 17/09/26 | 12/05/26 | 5 | ✅ |
| M-11 | IBM WatsonX Provider — Granite, Llama, Mixtral | P01 | M-10 | 18/09/26 | 18/09/26 | 12/05/26 | 5 | ✅ |
| M-12 | SambaNova Provider — SN40L hardware, Llama/Mixtral | P01 | M-11 | 18/09/26 | 18/09/26 | 12/05/26 | 5 | ✅ |
| M-13 | OpenRouter Provider — 200+ modelli, auto-fallback | P01 | M-12 | 19/09/26 | 19/09/26 | 12/05/26 | 5 | ✅ |
| M-14 | LiteLLM Provider — gateway 100+ provider, proxy | P01 | M-13 | 19/09/26 | 19/09/26 | 12/05/26 | 5 | ✅ |
| M-15 | AI/ML API Provider — 200+ modelli unificati | P01 | M-14 | 22/09/26 | 22/09/26 | 12/05/26 | 5 | ✅ |
| M-16 | Novita AI Provider — serverless Llama/DeepSeek | P01 | M-15 | 22/09/26 | 22/09/26 | 12/05/26 | 5 | ✅ |
| M-17 | DeepInfra Provider — OpenAI-compat open-source | P01 | M-16 | 23/09/26 | 23/09/26 | 12/05/26 | 5 | ✅ |
| M-18 | Replicate Provider — 1000+ modelli community | P01 | M-17 | 23/09/26 | 23/09/26 | 12/05/26 | 5 | ✅ |
| M-19 | Reka AI Provider — Core/Edge/Flash | P01 | M-18 | 24/09/26 | 24/09/26 | 12/05/26 | 5 | ✅ |
| M-20 | Modal Provider — serverless GPU custom models | P01 | M-19 | 24/09/26 | 24/09/26 | 12/05/26 | 5 | ✅ |
| M-21 | llama.cpp Provider — server GGUF, `localhost:8080` | P01 | M-20 | 25/09/26 | 25/09/26 | 12/05/26 | 5 | ✅ |
| M-22 | LM Studio Provider — `localhost:1234`, OpenAI-compat | P01 | M-21 | 25/09/26 | 25/09/26 | 12/05/26 | 5 | ✅ |
| M-23 | Ollama Responses Provider — `POST /api/responses` | P01 | M-22 | 26/09/26 | 26/09/26 | 12/05/26 | 5 | ✅ |
| M-24 | LocalAI Provider — OpenAI-compat auto-hosted | P01 | M-23 | 26/09/26 | 26/09/26 | 12/05/26 | 5 | ✅ |
| M-25 | GPT4All Provider — `localhost:4891`, desktop LLM | P01 | M-24 | 29/09/26 | 29/09/26 | 12/05/26 | 5 | ✅ |
| M-26 | text-gen-webui Provider — Oobabooga, `localhost:5000` | P01 | M-25 | 29/09/26 | 29/09/26 | 12/05/26 | 5 | ✅ |
| M-27 | KoboldCPP Provider — GGUF, `localhost:5001` | P01 | M-26 | 30/09/26 | 30/09/26 | 12/05/26 | 5 | ✅ |
| M-28 | HuggingFace Inference Provider — 200k+ modelli | P01 | M-27 | 30/09/26 | 01/10/26 | 12/05/26 | 5 | ✅ |
| M-29 | HuggingFace TGI Provider — self-hosted TGI | P01 | M-28 | 01/10/26 | 01/10/26 | 12/05/26 | 5 | ✅ |
| M-30 | AI21 Labs Provider — Jurassic-2, Jamba | P01 | M-29 | 02/10/26 | 02/10/26 | 12/05/26 | 5 | ✅ |
| M-31 | Writer Provider — Palmyra enterprise LLM | P01 | M-30 | 02/10/26 | 02/10/26 | 12/05/26 | 5 | ✅ |
| M-32 | NVIDIA NIM Provider — Llama, Nemotron GPU cloud | P01 | M-31 | 05/10/26 | 05/10/26 | 12/05/26 | 5 | ✅ |
| M-33 | Anyscale Provider — Ray-backed endpoint | P01 | M-32 | 05/10/26 | 05/10/26 | 12/05/26 | 5 | ✅ |
| M-34 | OctoAI Provider — inference ottimizzato | P01 | M-33 | 06/10/26 | 06/10/26 | 12/05/26 | 5 | ✅ |
| M-35 | Lepton AI Provider — serverless open-source | P01 | M-34 | 06/10/26 | 06/10/26 | 12/05/26 | 5 | ✅ |
| M-36 | Friendli AI Provider — serverless inference | P01 | M-35 | 07/10/26 | 07/10/26 | 12/05/26 | 5 | ✅ |
| M-37 | Moonshot (Kimi) Provider — Kimi k1.5 | P01 | M-36 | 07/10/26 | 07/10/26 | 12/05/26 | 5 | ✅ |
| M-38 | Zhipu AI (GLM) Provider — GLM-4 | P01 | M-37 | 08/10/26 | 08/10/26 | 12/05/26 | 5 | ✅ |
| M-39 | Baichuan Provider — Baichuan 4 | P01 | M-38 | 08/10/26 | 08/10/26 | 12/05/26 | 5 | ✅ |
| M-40 | ByteDance (Doubao) Provider — Doubao-Pro | P01 | M-39 | 09/10/26 | 09/10/26 | 12/05/26 | 5 | ✅ |
| M-41 | Alibaba (Qwen) Provider — Qwen2.5, QwQ-32B | P01 | M-40 | 09/10/26 | 09/10/26 | 12/05/26 | 5 | ✅ |
| M-42 | Baidu (ERNIE) Provider — ERNIE 4.0 | P01 | M-41 | 12/10/26 | 12/10/26 | 12/05/26 | 5 | ✅ |
| M-43 | Tencent (Hunyuan) Provider — Hunyuan Turbo | P01 | M-42 | 12/10/26 | 12/10/26 | 12/05/26 | 5 | ✅ |
| M-44 | 01.AI (Yi) Provider — Yi-Lightning, Yi-Large | P01 | M-43 | 13/10/26 | 13/10/26 | 12/05/26 | 5 | ✅ |
| M-45 | AI21 Jamba Provider — Jamba 1.5 SSM-Transformer | P01 | M-44 | 13/10/26 | 13/10/26 | 12/05/26 | 5 | ✅ |
| M-46 | Snowflake Arctic Provider — Enterprise SQL+LLM | P01 | M-45 | 14/10/26 | 14/10/26 | 12/05/26 | 5 | ✅ |
| M-47 | Phind Provider — search-augmented LLM | P01 | M-46 | 14/10/26 | 14/10/26 | 12/05/26 | 5 | ✅ |
| M-48 | You.com Provider — web search + chat | P01 | M-47 | 15/10/26 | 15/10/26 | 12/05/26 | 5 | ✅ |
| M-49 | Hyperbolic Provider — decentralized inference | P01 | M-48 | 15/10/26 | 15/10/26 | 12/05/26 | 5 | ✅ |
| M-50 | Featherless Provider — serverless open models | P01 | M-49 | 16/10/26 | 16/10/26 | 12/05/26 | 5 | ✅ |

---

## FASE 10 — Security & Audit (Giorni 161–168)
> Dipende da: CE-08 completato.

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| SA-01 | RBAC — Role-based access control, 4 ruoli, policy builder | P13 | CE-08 | 19/10/26 | 20/10/26 | 12/05/26 | 8 | ✅ |
| SA-02 | Audit Trail — log azioni critiche, chi/cosa/quando, pseudonimizzato | P13 | SA-01 | 20/10/26 | 21/10/26 | 12/05/26 | 6 | ✅ |
| SA-03 | Key Rotation — rotazione automatica JWT/ECDsa, kid | P13 | SA-02 | 21/10/26 | 21/10/26 | 12/05/26 | 6 | ✅ |
| SA-04 | API Key Auth — autenticazione headless via API key | P13 | SA-03 | 22/10/26 | 22/10/26 | 12/05/26 | 6 | ✅ |
| SA-05 | Rate Limiting — per endpoint / per utente / per IP | P13 | SA-04 | 22/10/26 | 23/10/26 | 12/05/26 | 6 | ✅ |
| SA-06 | Input Sanitization — validazione input .NET↔JS boundary | P13 | SA-05 | 23/10/26 | 23/10/26 | 12/05/26 | 6 | ✅ |
| SA-07 | CSP Hardening — Content-Security-Policy stretta | P13 | SA-06 | 26/10/26 | 26/10/26 | 12/05/26 | 4 | ✅ |
| SA-08 | Secrets Scanner — blocca credenziali in log/output | P13 | SA-07 | 26/10/26 | 26/10/26 | 12/05/26 | 6 | ✅ |

---

## FASE 11 — AgentOS Runtime Server (Giorni 169–188)
> Dipende da: SS-10 + TI-06 + SA-08 + M-50 completati.

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| RT-01 | Progetto `AGXIOME.Runtime` — scaffold, DI, config, middleware pipeline | P08 | SA-08 | 27/10/26 | 27/10/26 | 12/05/26 | 5 | ✅ |
| RT-02 | Auth middleware — JWT + API key + refresh token | P08 | RT-01 | 28/10/26 | 28/10/26 | 12/05/26 | 8 | ✅ |
| RT-03 | RBAC middleware — policy check per endpoint | P08 | RT-02 | 28/10/26 | 28/10/26 | 12/05/26 | 6 | ✅ |
| RT-04 | Rate limiter middleware — sliding window, 429 response | P08 | RT-03 | 29/10/26 | 29/10/26 | 12/05/26 | 6 | ✅ |
| RT-05 | Error handler middleware — structured errors, no stacktrace | P08 | RT-04 | 29/10/26 | 29/10/26 | 12/05/26 | 6 | ✅ |
| RT-06 | Agent Controller — CRUD + clone (6 endpoint) | P08 | RT-05 | 30/10/26 | 30/10/26 | 12/05/26 | 8 | ✅ |
| RT-07 | Run Controller — sync/stream/async/cancel/resume (8 endpoint) | P08 | RT-06 | 02/11/26 | 03/11/26 | 12/05/26 | 12 | ✅ |
| RT-08 | Session Controller — CRUD + list (5 endpoint) | P08 | RT-07 | 03/11/26 | 03/11/26 | 12/05/26 | 8 | ✅ |
| RT-09 | Team Controller — CRUD + coordinate + broadcast (6 endpoint) | P08 | RT-08 | 04/11/26 | 04/11/26 | 12/05/26 | 8 | ✅ |
| RT-10 | Workflow Controller — CRUD + execute + history (6 endpoint) | P08 | RT-09 | 04/11/26 | 05/11/26 | 12/05/26 | 8 | ✅ |
| RT-11 | Knowledge Controller — ingest/search/query/rerank/collections (6 endpoint) | P08 | RT-10 | 05/11/26 | 05/11/26 | 12/05/26 | 8 | ✅ |
| RT-12 | Tool Controller — list/schema/register/toolkits (4 endpoint) | P08 | RT-11 | 06/11/26 | 06/11/26 | 12/05/26 | 6 | ✅ |
| RT-13 | Auth Controller — login/refresh/logout/me/apikey (5 endpoint) | P08 | RT-12 | 06/11/26 | 06/11/26 | 12/05/26 | 8 | ✅ |
| RT-14 | Admin Controller — health/metrics/logs/errors/config (6 endpoint) | P08 | RT-13 | 09/11/26 | 09/11/26 | 12/05/26 | 6 | ✅ |
| RT-15 | SignalR AgentHub — StartRun/CancelRun/ResumeRun + eventi stream | P08 | RT-14 | 09/11/26 | 10/11/26 | 12/05/26 | 8 | ✅ |
| RT-16 | SignalR LogHub — subscribe agent/module, set log level | P08 | RT-15 | 10/11/26 | 10/11/26 | 12/05/26 | 6 | ✅ |
| RT-17 | SignalR AdminHub — metrics + errors realtime | P08 | RT-16 | 11/11/26 | 11/11/26 | 12/05/26 | 6 | ✅ |
| RT-18 | BackgroundRunWorker — job persistente, Channel<T>, Redis | P08 | RT-17 | 11/11/26 | 12/11/26 | 12/05/26 | 8 | ✅ |
| RT-19 | SessionCleanupJob — pulizia sessioni expired | P08 | RT-18 | 13/11/26 | 13/11/26 | 12/05/26 | 4 | ✅ |
| RT-20 | TokenRotationJob — rotazione chiavi JWT background | P08 | RT-19 | 13/11/26 | 13/11/26 | 12/05/26 | 4 | ✅ |

---

## FASE 12 — Prebuilt Toolkits (Giorni 189–238)
> Dipende da: TI-06 completato. Il blocco più grande dell'intero piano.

### Sub-fase 12a — Infrastruttura Base (4gg)

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| TB-00 | IToolkit + ToolkitBase + ToolkitAttribute + ToolkitResult | P02 | TI-06 | 17/11/26 | 17/11/26 | 12/05/26 | 5 | ✅ |

### Sub-fase 12b — Communication (15 toolkit)

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| T-01 | Slack Toolkit — send, channels, reactions, upload | P02 | TB-00 | 18/11/26 | 18/11/26 | 12/05/26 | 5 | ✅ |
| T-02 | Discord Toolkit — send, threads, reactions, ban | P02 | T-01 | 18/11/26 | 19/11/26 | 12/05/26 | 5 | ✅ |
| T-03 | Telegram Toolkit — send, photo, document, updates | P02 | T-02 | 19/11/26 | 19/11/26 | 12/05/26 | 5 | ✅ |
| T-04 | WhatsApp Toolkit — send, media, group, contacts | P02 | T-03 | 20/11/26 | 20/11/26 | 12/05/26 | 5 | ✅ |
| T-05 | Email SMTP Toolkit — send, bulk, inbox, search | P02 | T-04 | 20/11/26 | 20/11/26 | 12/05/26 | 5 | ✅ |
| T-06 | Gmail Toolkit — send, search, label, draft, thread | P02 | T-05 | 23/11/26 | 23/11/26 | 12/05/26 | 5 | ✅ |
| T-07 | Microsoft Teams Toolkit — message, channel, meeting | P02 | T-06 | 23/11/26 | 24/11/26 | 12/05/26 | 5 | ✅ |
| T-08 | Zoom Toolkit — meeting, participants, record | P02 | T-07 | 24/11/26 | 24/11/26 | 12/05/26 | 5 | ✅ |
| T-09 | Google Chat Toolkit — spaces, members, message | P02 | T-08 | 25/11/26 | 25/11/26 | 12/05/26 | 5 | ✅ |
| T-10 | Twilio SMS Toolkit — sms, whatsapp, lookup | P02 | T-09 | 25/11/26 | 25/11/26 | 12/05/26 | 5 | ✅ |
| T-11 | SendGrid Toolkit — email, template, contacts | P02 | T-10 | 26/11/26 | 26/11/26 | 12/05/26 | 5 | ✅ |
| T-12 | Mailchimp Toolkit — subscriber, campaign, audience | P02 | T-11 | 26/11/26 | 26/11/26 | 12/05/26 | 5 | ✅ |
| T-13 | Pushbullet Toolkit — note, link, file, devices | P02 | T-12 | 27/11/26 | 27/11/26 | 12/05/26 | 5 | ✅ |
| T-14 | Webhook Toolkit — send, sign, verify | P02 | T-13 | 27/11/26 | 27/11/26 | 12/05/26 | 5 | ✅ |
| T-15 | MQTT Toolkit — publish, subscribe, topics | P02 | T-14 | 28/11/26 | 28/11/26 | 12/05/26 | 5 | ✅ |

### Sub-fase 12c — Productivity (18 toolkit)

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| T-16 | Google Calendar Toolkit — events, free slots | P02 | T-15 | 01/12/26 | 01/12/26 | 12/05/26 | 5 | ✅ |
| T-17 | Google Drive Toolkit — list, upload, download, share | P02 | T-16 | 01/12/26 | 02/12/26 | 12/05/26 | 5 | ✅ |
| T-18 | Google Docs Toolkit — create, read, append | P02 | T-17 | 02/12/26 | 02/12/26 | 12/05/26 | 5 | ✅ |
| T-19 | Google Sheets Toolkit — range, write, chart | P02 | T-18 | 03/12/26 | 03/12/26 | 12/05/26 | 5 | ✅ |
| T-20 | Microsoft OneDrive Toolkit — list, upload, share | P02 | T-19 | 03/12/26 | 03/12/26 | 12/05/26 | 5 | ✅ |
| T-21 | Notion Toolkit — search, pages, database, blocks | P02 | T-20 | 04/12/26 | 04/12/26 | 12/05/26 | 5 | ✅ |
| T-22 | Confluence Toolkit — pages, search, spaces | P02 | T-21 | 04/12/26 | 05/12/26 | 12/05/26 | 5 | ✅ |
| T-23 | Jira Toolkit — issues, comments, transitions | P02 | T-22 | 05/12/26 | 05/12/26 | 12/05/26 | 5 | ✅ |
| T-24 | Linear Toolkit — issues, teams, search | P02 | T-23 | 08/12/26 | 08/12/26 | 12/05/26 | 5 | ✅ |
| T-25 | Asana Toolkit — tasks, projects, comments | P02 | T-24 | 08/12/26 | 08/12/26 | 12/05/26 | 5 | ✅ |
| T-26 | Trello Toolkit — cards, boards, checklists | P02 | T-25 | 09/12/26 | 09/12/26 | 12/05/26 | 5 | ✅ |
| T-27 | Todoist Toolkit — tasks, projects, complete | P02 | T-26 | 09/12/26 | 09/12/26 | 12/05/26 | 5 | ✅ |
| T-28 | Google Tasks Toolkit — tasks, lists, complete | P02 | T-27 | 10/12/26 | 10/12/26 | 12/05/26 | 5 | ✅ |
| T-29 | Evernote Toolkit — notes, notebooks, search | P02 | T-28 | 10/12/26 | 10/12/26 | 12/05/26 | 5 | ✅ |
| T-30 | Obsidian Toolkit — notes, vault, search | P02 | T-29 | 11/12/26 | 11/12/26 | 12/05/26 | 5 | ✅ |
| T-31 | Habitica Toolkit — tasks, score, user | P02 | T-30 | 11/12/26 | 11/12/26 | 12/05/26 | 5 | ✅ |
| T-32 | Calendly Toolkit — events, scheduling, cancel | P02 | T-31 | 12/12/26 | 12/12/26 | 12/05/26 | 5 | ✅ |
| T-33 | Docusign Toolkit — envelope, status, templates | P02 | T-32 | 12/12/26 | 12/12/26 | 12/05/26 | 5 | ✅ |

### Sub-fase 12d — Data Analysis (14 toolkit)

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| T-34 | SQL Database Toolkit — query, schema, transaction | P02 | T-33 | 15/12/26 | 15/12/26 | 12/05/26 | 5 | ✅ |
| T-35 | DuckDB Toolkit — query, CSV, Parquet, analyze | P02 | T-34 | 15/12/26 | 16/12/26 | 12/05/26 | 5 | ✅ |
| T-36 | PostgreSQL Toolkit — query, vacuum, analyze | P02 | T-35 | 16/12/26 | 16/12/26 | 12/05/26 | 5 | ✅ |
| T-37 | MySQL Toolkit — query, optimize, backup | P02 | T-36 | 17/12/26 | 17/12/26 | 12/05/26 | 5 | ✅ |
| T-38 | SQLite Toolkit — query, attach, backup, CSV | P02 | T-37 | 17/12/26 | 17/12/26 | 12/05/26 | 5 | ✅ |
| T-39 | ClickHouse Toolkit — query, batch insert, optimize | P02 | T-38 | 18/12/26 | 18/12/26 | 12/05/26 | 5 | ✅ |
| T-40 | Apache Spark Toolkit — SQL, DataFrame, write | P02 | T-39 | 18/12/26 | 19/12/26 | 12/05/26 | 5 | ✅ |
| T-41 | Pandas-Like Toolkit — CSV, filter, group, join | P02 | T-40 | 19/12/26 | 19/12/26 | 12/05/26 | 5 | ✅ |
| T-42 | Excel Toolkit — read, write, chart, pivot | P02 | T-41 | 22/12/26 | 22/12/26 | 12/05/26 | 5 | ✅ |
| T-43 | CSV Toolkit — read, write, merge, validate | P02 | T-42 | 22/12/26 | 22/12/26 | 12/05/26 | 5 | ✅ |
| T-44 | Parquet Toolkit — read, write, schema, compress | P02 | T-43 | 23/12/26 | 23/12/26 | 12/05/26 | 5 | ✅ |
| T-45 | JSON Toolkit — parse, transform, validate, flatten | P02 | T-44 | 23/12/26 | 23/12/26 | 12/05/26 | 5 | ✅ |
| T-46 | XML Toolkit — parse, XPath, validate, schema | P02 | T-45 | 24/12/26 | 24/12/26 | 12/05/26 | 5 | ✅ |
| T-47 | GeoPandas-Like Toolkit — shapefile, spatial join, buffer | P02 | T-46 | 26/12/26 | 26/12/26 | 12/05/26 | 5 | ✅ |

### Sub-fase 12e — Development (20 toolkit)

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| T-48 | GitHub Toolkit — PR, issues, code search, repos | P02 | T-47 | 29/12/26 | 30/12/26 | 12/05/26 | 5 | ✅ |
| T-49 | GitLab Toolkit — MR, pipeline, commits | P02 | T-48 | 30/12/26 | 30/12/26 | 12/05/26 | 5 | ✅ |
| T-50 | Bitbucket Toolkit — PR, repos, pipelines | P02 | T-49 | 31/12/26 | 31/12/26 | 12/05/26 | 5 | ✅ |
| T-51 | Git Toolkit — clone, commit, push, diff | P02 | T-50 | 02/01/27 | 02/01/27 | 12/05/26 | 5 | ✅ |
| T-52 | NuGet Toolkit — search, metadata, versions | P02 | T-51 | 02/01/27 | 02/01/27 | 12/05/26 | 5 | ✅ |
| T-53 | Docker Toolkit — build, run, ps, logs | P02 | T-52 | 05/01/27 | 05/01/27 | 12/05/26 | 5 | ✅ |
| T-54 | Kubernetes Toolkit — pods, apply, logs, exec | P02 | T-53 | 05/01/27 | 06/01/27 | 12/05/26 | 5 | ✅ |
| T-55 | Terraform Toolkit — plan, apply, destroy | P02 | T-54 | 06/01/27 | 06/01/27 | 12/05/26 | 5 | ✅ |
| T-56 | Pulumi Toolkit — up, preview, destroy | P02 | T-55 | 07/01/27 | 07/01/27 | 12/05/26 | 5 | ✅ |
| T-57 | Helm Toolkit — install, upgrade, uninstall | P02 | T-56 | 07/01/27 | 07/01/27 | 12/05/26 | 5 | ✅ |
| T-58 | AWS CLI Toolkit — S3, EC2, Lambda, IAM | P02 | T-57 | 08/01/27 | 08/01/27 | 12/05/26 | 5 | ✅ |
| T-59 | Azure CLI Toolkit — VM, WebApp, Functions | P02 | T-58 | 08/01/27 | 09/01/27 | 12/05/26 | 5 | ✅ |
| T-60 | GCP CLI Toolkit — storage, run, functions | P02 | T-59 | 09/01/27 | 09/01/27 | 12/05/26 | 5 | ✅ |
| T-61 | System Info Toolkit — CPU, memory, disk, process | P02 | T-60 | 12/01/27 | 12/01/27 | 12/05/26 | 5 | ✅ |
| T-62 | File System Toolkit — CRUD, search, zip, watch | P02 | T-61 | 12/01/27 | 12/01/27 | 12/05/26 | 5 | ✅ |
| T-63 | Shell Command Toolkit — exec, pipe, timeout, env | P02 | T-62 | 13/01/27 | 13/01/27 | 12/05/26 | 5 | ✅ |
| T-64 | Python Runner Toolkit — exec, pip, script | P02 | T-63 | 13/01/27 | 13/01/27 | 12/05/26 | 5 | ✅ |
| T-65 | Jupyter Toolkit — kernels, execute, notebook | P02 | T-64 | 14/01/27 | 14/01/27 | 12/05/26 | 5 | ✅ |
| T-66 | Build Tools Toolkit — dotnet, npm, cmake | P02 | T-65 | 14/01/27 | 14/01/27 | 12/05/26 | 5 | ✅ |
| T-67 | Code Quality Toolkit — lint, format, complexity | P02 | T-66 | 15/01/27 | 15/01/27 | 12/05/26 | 5 | ✅ |

### Sub-fase 12f — AI/ML (12 toolkit)

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| T-68 | OpenAI Vision Toolkit — analyze, OCR, detect | P02 | T-67 | 16/01/27 | 16/01/27 | 12/05/26 | 5 | ✅ |
| T-69 | DALL-E Toolkit — generate, edit, vary | P02 | T-68 | 16/01/27 | 16/01/27 | 12/05/26 | 5 | ✅ |
| T-70 | Stable Diffusion Toolkit — txt2img, inpaint, upscale | P02 | T-69 | 19/01/27 | 19/01/27 | 12/05/26 | 5 | ✅ |
| T-71 | Whisper Toolkit — transcribe, translate, diarize | P02 | T-70 | 19/01/27 | 19/01/27 | 12/05/26 | 5 | ✅ |
| T-72 | ElevenLabs TTS Toolkit — TTS, clone, voices | P02 | T-71 | 20/01/27 | 20/01/27 | 12/05/26 | 5 | ✅ |
| T-73 | HuggingFace Hub Toolkit — search, infer, list | P02 | T-72 | 20/01/27 | 20/01/27 | 12/05/26 | 5 | ✅ |
| T-74 | ML.NET AutoML Toolkit — train, evaluate, predict | P02 | T-73 | 21/01/27 | 21/01/27 | 12/05/26 | 5 | ✅ |
| T-75 | PyTorch Toolkit — inference, load, transform | P02 | T-74 | 21/01/27 | 21/01/27 | 12/05/26 | 5 | ✅ |
| T-76 | ONNX Runtime Toolkit — run, load, optimize | P02 | T-75 | 22/01/27 | 22/01/27 | 12/05/26 | 5 | ✅ |
| T-77 | LangChain Bridge Toolkit — chain, tools, LLM | P02 | T-76 | 22/01/27 | 22/01/27 | 12/05/26 | 5 | ✅ |
| T-78 | Semantic Kernel Toolkit — skill, function, memory | P02 | T-77 | 23/01/27 | 23/01/27 | 12/05/26 | 5 | ✅ |
| T-79 | ML.NET Scikit-like Toolkit — train, pipeline, tune | P02 | T-78 | 23/01/27 | 23/01/27 | 12/05/26 | 5 | ✅ |

### Sub-fase 12g — Finance (10 toolkit)

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| T-80 | Yahoo Finance Toolkit — quote, history, financials | P02 | T-79 | 26/01/27 | 26/01/27 | 12/05/26 | 5 | ✅ |
| T-81 | Alpha Vantage Toolkit — quote, FX, crypto, technical | P02 | T-80 | 26/01/27 | 26/01/27 | 12/05/26 | 5 | ✅ |
| T-82 | Polygon.io Toolkit — aggs, trades, news, options | P02 | T-81 | 27/01/27 | 27/01/27 | 12/05/26 | 5 | ✅ |
| T-83 | CoinGecko Toolkit — price, market data, trend | P02 | T-82 | 27/01/27 | 27/01/27 | 12/05/26 | 5 | ✅ |
| T-84 | Binance Toolkit — price, order, balance, klines | P02 | T-83 | 28/01/27 | 28/01/27 | 12/05/26 | 5 | ✅ |
| T-85 | Stripe Toolkit — payment, refund, invoice | P02 | T-84 | 28/01/27 | 28/01/27 | 12/05/26 | 5 | ✅ |
| T-86 | Plaid Toolkit — accounts, transactions, auth | P02 | T-85 | 29/01/27 | 29/01/27 | 12/05/26 | 5 | ✅ |
| T-87 | QuickBooks Toolkit — invoice, customers, reports | P02 | T-86 | 29/01/27 | 29/01/27 | 12/05/26 | 5 | ✅ |
| T-88 | Xero Toolkit — invoice, contacts, balance | P02 | T-87 | 30/01/27 | 30/01/27 | 12/05/26 | 5 | ✅ |
| T-89 | Forex Toolkit — rate, convert, pairs, history | P02 | T-88 | 30/01/27 | 30/01/27 | 12/05/26 | 5 | ✅ |

### Sub-fase 12h — Cloud & Infra (12 toolkit)

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| T-90 | AWS S3 Toolkit — list, upload, presign, copy | P02 | T-89 | 02/02/27 | 02/02/27 | 12/05/26 | 5 | ✅ |
| T-91 | AWS Lambda Toolkit — invoke, create, logs | P02 | T-90 | 02/02/27 | 02/02/27 | 12/05/26 | 5 | ✅ |
| T-92 | AWS DynamoDB Toolkit — query, put, scan | P02 | T-91 | 03/02/27 | 03/02/27 | 12/05/26 | 5 | ✅ |
| T-93 | AWS EC2 Toolkit — list, start, stop, describe | P02 | T-92 | 03/02/27 | 03/02/27 | 12/05/26 | 5 | ✅ |
| T-94 | Azure Blob Toolkit — list, upload, SAS | P02 | T-93 | 04/02/27 | 04/02/27 | 12/05/26 | 5 | ✅ |
| T-95 | Azure Functions Toolkit — invoke, list, logs | P02 | T-94 | 04/02/27 | 04/02/27 | 12/05/26 | 5 | ✅ |
| T-96 | GCP Storage Toolkit — list, upload, sign | P02 | T-95 | 05/02/27 | 05/02/27 | 12/05/26 | 5 | ✅ |
| T-97 | GCP Cloud Run Toolkit — deploy, logs, revisions | P02 | T-96 | 05/02/27 | 05/02/27 | 12/05/26 | 5 | ✅ |
| T-98 | Cloudflare Toolkit — cache, zones, DNS, workers | P02 | T-97 | 06/02/27 | 06/02/27 | 12/05/26 | 5 | ✅ |
| T-99 | Vercel Toolkit — deploy, projects, logs, env | P02 | T-98 | 06/02/27 | 06/02/27 | 12/05/26 | 5 | ✅ |
| T-100 | Netlify Toolkit — deploy, sites, functions | P02 | T-99 | 09/02/27 | 09/02/27 | 12/05/26 | 5 | ✅ |
| T-101 | DigitalOcean Toolkit — droplets, snapshot, resize | P02 | T-100 | 09/02/27 | 09/02/27 | 12/05/26 | 5 | ✅ |

### Sub-fase 12i — Web & Social (18 toolkit)

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| T-102 | Twitter/X Toolkit — tweet, search, timeline, DM | P02 | T-101 | 10/02/27 | 10/02/27 | 12/05/26 | 5 | ✅ |
| T-103 | Reddit Toolkit — posts, search, comment, submit | P02 | T-102 | 10/02/27 | 11/02/27 | 12/05/26 | 5 | ✅ |
| T-104 | LinkedIn Toolkit — post, search, message | P02 | T-103 | 11/02/27 | 11/02/27 | 12/05/26 | 5 | ✅ |
| T-105 | Instagram Toolkit — post, feed, story | P02 | T-104 | 12/02/27 | 12/02/27 | 12/05/26 | 5 | ✅ |
| T-106 | YouTube Toolkit — search, video, comments | P02 | T-105 | 12/02/27 | 12/02/27 | 12/05/26 | 5 | ✅ |
| T-107 | TikTok Toolkit — video, search, analytics | P02 | T-106 | 13/02/27 | 13/02/27 | 12/05/26 | 5 | ✅ |
| T-108 | Medium Toolkit — post, publish, stats | P02 | T-107 | 13/02/27 | 13/02/27 | 12/05/26 | 5 | ✅ |
| T-109 | Dev.to Toolkit — articles, comment | P02 | T-108 | 16/02/27 | 16/02/27 | 12/05/26 | 5 | ✅ |
| T-110 | Hashnode Toolkit — post, publish, stats | P02 | T-109 | 16/02/27 | 16/02/27 | 12/05/26 | 5 | ✅ |
| T-111 | WordPress Toolkit — posts, pages, media | P02 | T-110 | 17/02/27 | 17/02/27 | 12/05/26 | 5 | ✅ |
| T-112 | Shopify Toolkit — products, orders, customers | P02 | T-111 | 17/02/27 | 17/02/27 | 12/05/26 | 5 | ✅ |
| T-113 | WooCommerce Toolkit — products, orders, reports | P02 | T-112 | 18/02/27 | 18/02/27 | 12/05/26 | 5 | ✅ |
| T-114 | Squarespace Toolkit — products, orders, analytics | P02 | T-113 | 18/02/27 | 18/02/27 | 12/05/26 | 5 | ✅ |
| T-115 | Zapier Toolkit — execute, list, status | P02 | T-114 | 19/02/27 | 19/02/27 | 12/05/26 | 5 | ✅ |
| T-116 | IFTTT Toolkit — trigger, services | P02 | T-115 | 19/02/27 | 19/02/27 | 12/05/26 | 5 | ✅ |
| T-117 | Make Toolkit — scenario, execute | P02 | T-116 | 20/02/27 | 20/02/27 | 12/05/26 | 5 | ✅ |
| T-118 | Web Scraper Toolkit — fetch, parse, screenshot, crawl | P02 | T-117 | 20/02/27 | 20/02/27 | 12/05/26 | 5 | ✅ |
| T-119 | RSS Toolkit — fetch, parse, subscribe | P02 | T-118 | 23/02/27 | 23/02/27 | 12/05/26 | 5 | ✅ |

### Sub-fase 12j — Search & Knowledge (12 toolkit)

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| T-120 | Web Search Google Toolkit — search, snippets, images | P02 | T-119 | 24/02/27 | 24/02/27 | 12/05/26 | 5 | ✅ |
| T-121 | Web Search Bing Toolkit — search, news, video | P02 | T-120 | 24/02/27 | 24/02/27 | 12/05/26 | 5 | ✅ |
| T-122 | SearXNG Toolkit — aggregate, categories | P02 | T-121 | 25/02/27 | 25/02/27 | 12/05/26 | 5 | ✅ |
| T-123 | Tavily Toolkit — search, extract, news | P02 | T-122 | 25/02/27 | 25/02/27 | 12/05/26 | 5 | ✅ |
| T-124 | Exa Toolkit — search, contents, similar | P02 | T-123 | 26/02/27 | 26/02/27 | 12/05/26 | 5 | ✅ |
| T-125 | Brave Search Toolkit — search, news, summarize | P02 | T-124 | 26/02/27 | 26/02/27 | 12/05/26 | 5 | ✅ |
| T-126 | SerpAPI Toolkit — search, images, maps | P02 | T-125 | 27/02/27 | 27/02/27 | 12/05/26 | 5 | ✅ |
| T-127 | Serper Toolkit — search, news, shopping | P02 | T-126 | 27/02/27 | 27/02/27 | 12/05/26 | 5 | ✅ |
| T-128 | Firecrawl Toolkit — scrape, crawl, extract | P02 | T-127 | 02/03/27 | 02/03/27 | 12/05/26 | 5 | ✅ |
| T-129 | Wikipedia Toolkit — search, summary, page | P02 | T-128 | 02/03/27 | 02/03/27 | 12/05/26 | 5 | ✅ |
| T-130 | Arxiv Toolkit — search, papers, download | P02 | T-129 | 03/03/27 | 03/03/27 | 12/05/26 | 5 | ✅ |
| T-131 | PubMed Toolkit — search, abstract, mesh | P02 | T-130 | 03/03/27 | 03/03/27 | 12/05/26 | 5 | ✅ |

### Sub-fase 12k — File & Storage (10 toolkit)

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| T-132 | Local File Toolkit — CRUD, search, watch | P02 | T-131 | 04/03/27 | 04/03/27 | 12/05/26 | 5 | ✅ |
| T-133 | FTP/SFTP Toolkit — list, upload, download | P02 | T-132 | 04/03/27 | 04/03/27 | 12/05/26 | 5 | ✅ |
| T-134 | Google Cloud Storage Toolkit — upload, sign | P02 | T-133 | 05/03/27 | 05/03/27 | 12/05/26 | 5 | ✅ |
| T-135 | MinIO Toolkit — upload, download, presign | P02 | T-134 | 05/03/27 | 05/03/27 | 12/05/26 | 5 | ✅ |
| T-136 | Dropbox Toolkit — upload, download, share | P02 | T-135 | 06/03/27 | 06/03/27 | 12/05/26 | 5 | ✅ |
| T-137 | Box Toolkit — upload, list, lock | P02 | T-136 | 06/03/27 | 06/03/27 | 12/05/26 | 5 | ✅ |
| T-138 | SharePoint Toolkit — upload, checkin, search | P02 | T-137 | 09/03/27 | 09/03/27 | 12/05/26 | 5 | ✅ |
| T-139 | Compression Toolkit — zip, tar, gzip, brotli | P02 | T-138 | 09/03/27 | 09/03/27 | 12/05/26 | 5 | ✅ |
| T-140 | Image Processing Toolkit — resize, OCR, convert | P02 | T-139 | 10/03/27 | 10/03/27 | 12/05/26 | 5 | ✅ |
| T-141 | PDF Processing Toolkit — extract, merge, sign | P02 | T-140 | 10/03/27 | 10/03/27 | 12/05/26 | 5 | ✅ |

### Sub-fase 12l — Gaming & Creative (8 toolkit)

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| T-142 | Steam Toolkit — games, player, achievements | P02 | T-141 | 11/03/27 | 11/03/27 | 12/05/26 | 5 | ✅ |
| T-143 | Twitch Toolkit — streams, clip, chat | P02 | T-142 | 11/03/27 | 11/03/27 | 12/05/26 | 5 | ✅ |
| T-144 | Spotify Toolkit — search, play, playlist | P02 | T-143 | 12/03/27 | 12/03/27 | 12/05/26 | 5 | ✅ |
| T-145 | Last.fm Toolkit — artist, scrobble, recommend | P02 | T-144 | 12/03/27 | 12/03/27 | 12/05/26 | 5 | ✅ |
| T-146 | Unsplash Toolkit — search, random, download | P02 | T-145 | 13/03/27 | 13/03/27 | 12/05/26 | 5 | ✅ |
| T-147 | Pexels Toolkit — video, image, curated | P02 | T-146 | 13/03/27 | 13/03/27 | 12/05/26 | 5 | ✅ |
| T-148 | Giphy Toolkit — search, trending, stickers | P02 | T-147 | 16/03/27 | 16/03/27 | 12/05/26 | 5 | ✅ |
| T-149 | OpenAI Image Gen Toolkit — generate, edit, vary, analyze | P02 | T-148 | 16/03/27 | 16/03/27 | 12/05/26 | 5 | ✅ |

### Sub-fase 12m — Enterprise (12 toolkit)

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| T-150 | Salesforce Toolkit — query, create, update, describe | P02 | T-149 | 17/03/27 | 17/03/27 | 12/05/26 | 5 | ✅ |
| T-151 | HubSpot Toolkit — contact, deal, ticket | P02 | T-150 | 17/03/27 | 18/03/27 | 12/05/26 | 5 | ✅ |
| T-152 | Zendesk Toolkit — ticket, views, macro | P02 | T-151 | 18/03/27 | 18/03/27 | 12/05/26 | 5 | ✅ |
| T-153 | ServiceNow Toolkit — incident, workflow | P02 | T-152 | 19/03/27 | 19/03/27 | 12/05/26 | 5 | ✅ |
| T-154 | Datadog Toolkit — metrics, monitors, logs | P02 | T-153 | 19/03/27 | 19/03/27 | 12/05/26 | 5 | ✅ |
| T-155 | PagerDuty Toolkit — incident, acknowledge | P02 | T-154 | 20/03/27 | 20/03/27 | 12/05/26 | 5 | ✅ |
| T-156 | Sentry Toolkit — issues, projects, release | P02 | T-155 | 20/03/27 | 20/03/27 | 12/05/26 | 5 | ✅ |
| T-157 | Grafana Toolkit — query, dashboards, annotate | P02 | T-156 | 23/03/27 | 23/03/27 | 12/05/26 | 5 | ✅ |
| T-158 | New Relic Toolkit — apps, alerts, dashboard | P02 | T-157 | 23/03/27 | 23/03/27 | 12/05/26 | 5 | ✅ |
| T-159 | Elasticsearch Toolkit — search, index, aggregate | P02 | T-158 | 24/03/27 | 24/03/27 | 12/05/26 | 5 | ✅ |
| T-160 | Okta Toolkit — users, groups, sessions | P02 | T-159 | 24/03/27 | 24/03/27 | 12/05/26 | 5 | ✅ |
| T-161 | Auth0 Toolkit — users, roles, logs | P02 | T-160 | 25/03/27 | 25/03/27 | 12/05/26 | 5 | ✅ |

---

## FASE 13 — CLI, UI & Multi-Framework (Giorni 239–252)
> Dipende da: RT-20 (Runtime) completato.

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| MU-01 | CLI `agxiome run` — esegui agent/team/workflow da terminale | P12 | RT-20 | 26/03/27 | 26/03/27 | 12/05/26 | 5 | ✅ |
| MU-02 | CLI `agxiome list` — lista agent/team/workflow/sessions | P12 | MU-01 | 27/03/27 | 27/03/27 | 12/05/26 | 4 | ✅ |
| MU-03 | CLI `agxiome session` — crea/elimina/ispeziona sessioni | P12 | MU-02 | 27/03/27 | 27/03/27 | 12/05/26 | 4 | ✅ |
| MU-04 | CLI `agxiome admin` — health check, metrics, logs | P12 | MU-03 | 28/03/27 | 28/03/27 | 12/05/26 | 4 | ✅ |
| MU-05 | CLI `agxiome config` — get/set config runtime | P12 | MU-04 | 28/03/27 | 28/03/27 | 12/05/26 | 4 | ✅ |
| MU-06 | Control Plane UI — web dashboard agent/team/run/knowledge | P12 | MU-05 | 31/03/27 | 04/04/27 | 12/05/26 | 5 | ✅ |
| MU-07 | Approval UI — interfaccia approvazione HITL requests | P12 | MU-06 | 07/04/27 | 08/04/27 | 12/05/26 | 5 | ✅ |
| MU-08 | LangChain Bridge — wrapper per LangChain agents | P12 | MU-07 | 09/04/27 | 09/04/27 | 12/05/26 | 4 | ✅ |
| MU-09 | Semantic Kernel Bridge — wrapper SK plugins | P12 | MU-08 | 09/04/27 | 10/04/27 | 12/05/26 | 4 | ✅ |
| MU-10 | Claude SDK Bridge — bridge Anthropic diretto | P12 | MU-09 | 10/04/27 | 10/04/27 | 12/05/26 | 4 | ✅ |

---

## FASE 14 — Testing & SLO Completo (Giorni 253–270)
> Dipende da: tutti i task sopra completati.

| ID | Task | Piano | Dipende da | Inizio | Fine Pianificata | Fine Effettiva | Test | Stato |
|----|------|-------|-----------|--------|-----------------|----------------|------|-------|
| TS-01 | Model Provider Tests — 250 test per 50 provider, mock HTTP | P14 | M-50 | 13/04/27 | 15/04/27 | 12/05/26 | 250 | ✅ |
| TS-02 | Toolkit Tests — 800 test per 161 toolkit, mock API | P14 | T-161 | 16/04/27 | 22/04/27 | 12/05/26 | 805 | ✅ |
| TS-03 | DB Provider Tests — 175 test per 25 DB, Testcontainers | P14 | DB-25 | 23/04/27 | 25/04/27 | 12/05/26 | 175 | ✅ |
| TS-04 | Vector DB Tests — 210 test per 30 vector DB | P14 | VD-30 | 28/04/27 | 30/04/27 | 12/05/26 | 210 | ✅ |
| TS-05 | Reader Tests — 224 test per 28 reader | P14 | R-28 | 01/05/27 | 05/05/27 | 12/05/26 | 224 | ✅ |
| TS-06 | Embedder Tests — 175 test per 25 embedder | P14 | E-25 | 06/05/27 | 08/05/27 | 12/05/26 | 175 | ✅ |
| TS-07 | Chunker/Reranker Tests — 100 test per 12+8 | P14 | RK-08 | 09/05/27 | 09/05/27 | 12/05/26 | 100 | ✅ |
| TS-08 | Runtime Integration Tests — 200 test API + SignalR, WebApplicationFactory | P14 | RT-20 | 12/05/27 | 14/05/27 | 12/05/26 | 200 | ✅ |
| TS-09 | Benchmark Suite — BenchmarkDotNet p50/p95/p99 per hot path | P14 | TS-08 | 15/05/27 | 16/05/27 | 12/05/26 | 30 | ✅ |
| TS-10 | SLO Validation — test automatici error budget, alert gates | P14 | TS-09 | 19/05/27 | 19/05/27 | 12/05/26 | 20 | ✅ |
| TS-11 | Property-Based Tests — FsCheck, 50+ generatori random | P14 | TS-10 | 20/05/27 | 21/05/27 | 12/05/26 | 50 | ✅ |
| TS-12 | Security Tests — penetration test base, auth, XSS, injection | P14 | TS-11 | 22/05/27 | 23/05/27 | 12/05/26 | 30 | ✅ |

---

## Riepilogo Totali

| Metrica | Valore |
|---------|--------|
| **Task totali** | **383** |
| **Fasi** | 14 |
| **Giorni pianificati** | ~270 (da 12/05/26 a 23/05/27) |
| **Test integrati totali** | **~3000+** |
| **Fine Pianificata** | **23 Maggio 2027** |

### Distribuzione task per fase

| Fase | Task | Test |
|------|------|------|
| F0 — Setup | 7 | 0 |
| F1 — Core Enhancements | 8 | 68 |
| F2 — Chunker & Reranker | 17 | 85 |
| F3 — Embedders | 25 | 175 |
| F4 — DB Provider | 25 | 175 |
| F5 — Vector DB | 30 | 210 |
| F6 — Document Readers | 28 | 224 |
| F7 — Storage & Session | 10 | 70 |
| F8 — Tools Infrastructure | 6 | 44 |
| F9 — Model Providers | 50 | 250 |
| F10 — Security & Audit | 8 | 48 |
| F11 — AgentOS Runtime | 20 | 142 |
| F12 — Prebuilt Toolkits | 162 | 810 |
| F13 — CLI, UI & Multi | 10 | 43 |
| F14 — Testing & SLO | 12 | 2034 |
| **TOTALE** | **418** | **~4378** |

---

## Come Usare Questo Piano

1. **Inizia dall'alto**: F0 va eseguita tutta prima di qualsiasi altra fase.
2. **Esegui ogni task in ordine**: la colonna "Dipende da" indica il prerequisito diretto.
3. **A completamento**: compila la colonna "Fine Effettiva" con la data reale.
4. **Test integrati**: ogni task è chiuso **solo quando i test indicati sono tutti green**.
5. **Stato**:
   - ⬜ = non iniziato
   - 🔄 = in corso (aggiorna quando inizi)
   - ✅ = completato + test green
   - ❌ = bloccato (documenta il blocco)
