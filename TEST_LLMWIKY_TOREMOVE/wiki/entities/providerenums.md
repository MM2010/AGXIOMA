---
title: Provider Enums
category: entity
tags: [enums, providers, type definitions]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Provider Enums

## Summary
Central collection of enum types in the AGXIOME framework that define all supported providers, storage backends, vector databases, embedders, document readers, chunkers, rerankers, toolkits, authentication schemes, RBAC roles, and background job types. These enums eliminate hardcoded strings from routing and configuration code, ensuring type safety and discoverability across the system.

## Key Details
- **LLMProviderType** (60+ members): LLM/chat model providers including OpenAI, Anthropic, Gemini, Ollama, and specialized providers like TogetherAI, FireworksAI, AWSBedrock, HuggingFace, etc.
- **StorageProviderType** (30+ members): Database and cache providers: PostgreSQL, MySQL, MongoDB, Redis, SQLite, CosmosDB, Firestore, DuckDB, etc.
- **VectorDbProviderType** (30+ members): Vector storage providers: PgVector, Pinecone, Qdrant, ChromaDB, FAISS, HNSWLib, etc.
- **EmbedderType** (25 members): Text embedding models: OpenAISmall, Cohere, Mistral, OnnxBertMiniLM, OllamaNomic, etc.
- **DocumentReaderType** (28 members): Document format readers (Pdf, Docx, Website, YouTubeTranscript) and external sources (Firecrawl, AWS S3, Notion).
- **ChunkerType** (12 members): Text chunking strategies: Fixed, Heading, Recursive, Semantic, Agentic, Code, etc.
- **RerankerType** (8 members): Reranking models: CohereEnglish, JinaV2, VoyageRerank2, BgeOnnx, etc.
- **ToolkitCategory** (12 members): Functional categories for toolkits: Communication, DataAnalysis, AiMl, Enterprise, etc.
- **AuthSchemeType & RBAC enums**: Authentication (Jwt, ApiKey, OAuth2) and role-based access control (Admin, Operator, Developer, ReadOnly) with resource-level actions.
- **BackgroundJobType & BackgroundJobStatus**: Runtime job types (RunExecution, SessionCleanup, KnowledgeIngest) and states (Queued, Running, Completed, Failed, Cancelled).

## Cross-References
- [[entities/agent]] — Uses LLMProviderType and VectorDbProviderType for agent configuration.
- [[entities/workflow]] — References RbacAction and BackgroundJobType.
- [[entities/knowledgebase]] — Uses DocumentReaderType, ChunkerType, EmbedderType.
- [[concepts/routing]] — These enums replace string-based routing logic.

## Source Files
- `AGXIOME.Shared/Models/ProviderEnums.cs` (ingested 2026-05-14)