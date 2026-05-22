# Changelog

All notable changes to AGXIOME are documented in this file.

Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) — [Semantic Versioning](https://semver.org/spec/v2.0.0.html)

---

## [1.0.0] — 2026-05-22

### Added

#### Core Runtime
- `IAgentRuntime` / `AgentRuntime` — unified agent execution engine with streaming SSE
- `IAgentRepository` / `InMemoryAgentRepository` — agent lifecycle storage (CRUD + search)
- `AgentRunnerService` — hosted service for persistent agent sessions
- `ResilientExecutor` — automatic retry, circuit breaker, fallback chain
- Structured logging via `IAgentLogger` with Serilog JSON sinks

#### LLM Providers (50+)
- **Big Labs**: OpenAI (GPT-4o, o1, o3, GPT-4.1), Anthropic (Claude Sonnet 4, Haiku 3.5), Google Gemini (2.0 Flash, 1.5 Pro), Azure OpenAI, AWS Bedrock, Azure AI Foundry
- **Independent**: DeepSeek, Mistral AI, xAI Grok, Cohere, Perplexity, Groq, Together AI, Cerebras, Fireworks AI, Sambanova
- **Meta-routers**: OpenRouter, LiteLLM
- **Asian providers**: Moonshot/Kimi, Zhipu, Baichuan, ByteDance, Alibaba Qwen, Baidu Ernie, Tencent Hunyuan, 01.AI
- **Local / Self-hosted**: Ollama, llama.cpp, LM Studio, LocalAI, GPT4All, KoboldCPP, vLLM, TGI, text-gen-webui
- **Custom**: any OpenAI-compatible endpoint
- `ModelCompatibilityMap` — static model-string → provider lookup with status (Supported/Preview/Deprecated)
- `ProviderCostTracker` — per-provider token cost estimation

#### Vector / Embedding Stores (30+)
- In-memory: `HNSWLibStore`, `InMemoryVectorStore`
- Cloud: Pinecone, Weaviate, Qdrant, Chroma, Milvus, Azure AI Search, AWS OpenSearch, Google Vertex AI Matching Engine
- SQL-backed: PostgreSQL pgvector, SQLite VSS, MySQL InnoDB vector
- Time-series: InfluxDB, TimescaleDB
- Graph: Neo4j, Amazon Neptune
- Hybrid: Elasticsearch, OpenSearch, Redis Search, Typesense
- HNSW/FAISS: local high-performance indexes

#### Embedders
- `OllamaMxbaiEmbedder`, `OllamaNoMicEmbedder` — local Ollama models
- OpenAI `text-embedding-3-small/large`, `text-embedding-ada-002`
- Cohere, Voyage, HuggingFace, Mistral embedders
- BGE, E5, GTE, Jina, NomicEmbed via API or ONNX

#### Rerankers (9 types)
- Cohere Rerank English / Multilingual
- Jina Reranker V2
- Voyage Rerank 2
- BGE ONNX (`BgeOnnxReranker`) — local ONNX embedding-based approximation
- BGE API (`BgeApiReranker`) — HTTP API (InfinityEmbeddings / text-embeddings-inference compatible)
- CrossEncoder ONNX
- MixedBread ONNX
- LLM Reranker — prompt-based reranking via any LLM provider

#### RAG Pipeline
- `RagPipeline` — ingest → chunk → embed → store → query → rerank → generate
- `ChunkingStrategies` — fixed, sentence, semantic, overlap, recursive
- `DocumentReaders` — PDF, DOCX, HTML, Markdown, plain text, CSV, JSON
- `HybridSearch` — BM25 sparse + dense vector fusion (RRF)
- Source citation in responses

#### Toolkits (12 categories, 45+ toolkits)
- **Communication**: Slack, Discord, Telegram, WhatsApp, Email, SMS, Teams
- **Data Analysis**: DuckDB, PostgreSQL, MySQL, SQLite, ClickHouse, Spark, Pandas-like
- **Developer**: GitHub, GitLab, Bitbucket, Jira, Linear
- **Productivity**: Google Calendar, Google Drive, Notion, Todoist, Airtable
- **Media**: YouTube, Twitter/X, LinkedIn, Reddit, Instagram
- **Finance**: Yahoo Finance, Alpha Vantage, Stripe, Coinbase
- **Security**: OWASP scanner, secrets detection, CVE lookup, SBOM generator
- **File**: local filesystem read/write/search, archive, hash
- **Search**: Google, Bing, DuckDuckGo, Brave, Tavily, SerpAPI
- **Cloud**: AWS S3/EC2/Lambda, Azure Blob/Functions, GCP Storage
- **Monitoring**: Prometheus, Grafana, PagerDuty, Datadog
- **System**: shell exec, process monitor, cron, environment

#### Toolkit Orchestration Layer
- `CapabilityRegistry` — manifests with semantic descriptions for all toolkit actions
- `ToolRouter` — goal → toolkit routing with 30+ fallback chains and known aliases
- `SemanticToolSelector` — embedding-based semantic matching for tool selection
- `BudgetAwareExecutor` — execution with token/cost budget enforcement
- `ToolkitPolicyEnforcer` — RBAC policy enforcement on toolkit execution
- `InMemoryPolicyProvider` — in-process policy store for testing and development

#### Multi-Agent Architectures
- `HeavySwarm` — parallel specialist agents → synthesis (research pipelines)
- `GrokSwarm` — captain + workers → structured output
- `ForestSwarm` — keyword-based routing to specialized agents
- Agent memory: in-process, Redis, PostgreSQL backends

#### Security
- `ISecurityAuditor` / `SecurityAuditService` — runtime audit log with RBAC event tracking
- `ISecretManager` — abstraction over Azure Key Vault, AWS Secrets Manager, HashiCorp Vault, env vars
- Input/output sanitization, prompt injection detection hooks
- PII detection pipeline

#### Storage
- `IKnowledgeStore` / `KnowledgeStoreBase` — unified CRUD + semantic search interface
- `IConversationStore` — conversation history with pluggable backends
- `IAgentStateStore` — durable agent state (checkpoint / restore)
- Backends: InMemory, Redis, PostgreSQL, SQLite, MongoDB, CosmosDB

#### LLMWiki (Ingestion Engine)
- `WikiIngester` — parallel batch ingestion (configurable parallelism)
- `NormalizeContent()` — blank line collapse, CRLF normalization
- `ChunkText()` — fixed-size + overlap chunking with infinite-loop guard
- Configurable: `EmbedMaxChars`, `IngestParallelism`, `SourceExcludes`, file size guard (500 KB)
- Backed by PostgreSQL pgvector + OllamaMxbaiEmbedder; 395/395 files @ 131 MB RAM stable

#### Infrastructure
- Docker Compose stacks: full, F4-postgres, F4-test-stack, legal-email, support-hub-enterprise
- Helm chart (`helm/agxiome/`)
- GitHub Actions CI: build + test on ubuntu-latest + windows-latest (.NET 10)
- GitHub Actions docker-publish workflow
- Health check endpoints on all hosted services

### Tests
- 2946 unit/integration tests passing (0 fail, ~25 skip) as of 2026-05-22
- xUnit test suites: F01–F16 covering core, providers, storage, toolkits, orchestration
- `F15_ToolkitOrchestrationTests` — 52+ tests for CapabilityRegistry, ToolRouter, BudgetAwareExecutor, SemanticToolSelector, ToolkitPolicyEnforcer
- `F16_ToolkitTests` — toolkit registry and communication toolkit tests

---

## [0.9.0] — 2026-04-15 _(pre-release)_

- Initial internal release
- Core runtime + OpenAI/Anthropic/DeepSeek providers
- HNSWLib in-memory vector store
- Basic RAG pipeline
- HeavySwarm / GrokSwarm / ForestSwarm multi-agent patterns
- Docker Compose stack

---

[1.0.0]: https://github.com/your-org/agxiome/releases/tag/v1.0.0
[0.9.0]: https://github.com/your-org/agxiome/releases/tag/v0.9.0
