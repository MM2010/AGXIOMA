# AGXIOME — API Reference v0.0.1-beta

## Core Agent

| Interface | Description |
|-----------|-------------|
| [`IAgxiomeAgent`](src/AGXIOME.Shared/Interfaces/IAgxiomeAgent.cs) | Core agent: run, stream, tools, hooks, guardrails, learning |
| [`IAgentMemory`](src/AGXIOME.Shared/Interfaces/IAgentMemory.cs) | Conversation history + key-value state |
| [`ISemanticMemory`](src/AGXIOME.Shared/Interfaces/IAgentMemory.cs) | Vector-based semantic memory search |
| [`IAgentContext`](src/AGXIOME.Shared/Interfaces/IRemainingInterfaces.cs) | Pipeline context: system prompt, instructions, few-shot |

## Learning & Knowledge

| Interface | Description |
|-----------|-------------|
| [`ILearningMachine`](src/AGXIOME.Shared/Interfaces/ILearningKnowledgeScheduler.cs) | User memory, profiles, entity facts, session context, decisions |
| [`IKnowledgeBase`](src/AGXIOME.Shared/Interfaces/ILearningKnowledgeScheduler.cs) | Document ingestion + semantic search (RAG) |
| [`ICultureSystem`](src/AGXIOME.Shared/Interfaces/ICultureSystem.cs) | Organizational knowledge shared across agents |

## Structured Output

| Interface | Description |
|-----------|-------------|
| [`IOutputParser`](src/AGXIOME.Shared/Interfaces/IStructuredOutput.cs) | JSON deserializer for LLM responses |
| [`IResponseModel`](src/AGXIOME.Shared/Interfaces/IStructuredOutput.cs) | Natural language formatter from typed output |
| `AgentOutputSchema` | JSON Schema for structured LLM output |
| `AgentInputSchema` | JSON Schema for input validation |

## Teams & Workflows

| Interface | Description |
|-----------|-------------|
| [`IAgxiomeTeam`](src/AGXIOME.Shared/Interfaces/IAgxiomeTeam.cs) | Multi-agent team (Router, Swarm, Pipeline, Supervisor) |
| [`IAgxiomeWorkflow`](src/AGXIOME.Shared/Interfaces/IAgxiomeWorkflow.cs) | DAG-based workflow engine |
| `TeamTopology` | Router / Swarm / Pipeline / Supervisor |
| `TeamMode` | Coordinate / Route / Broadcast |

## Tools & Functions

| Interface | Description |
|-----------|-------------|
| [`IAgxiomeTool`](src/AGXIOME.Shared/Interfaces/IAgxiomeTool.cs) | Base tool contract |
| `AgxiomeFunction` | LLM-callable function with auto JSON Schema |
| `ToolParameterAttribute` | Parameter metadata for schema generation |
| [`IToolRegistry`](src/AGXIOME.Shared/Interfaces/IToolRegistry.cs) | Central tool registry |

## Skills System

| Interface | Description |
|-----------|-------------|
| [`IAgxiomeSkill`](src/AGXIOME.Shared/Interfaces/IRemainingInterfaces.cs) | Self-contained skill package (SKILL.md + scripts + references) |
| [`ISkillManager`](src/AGXIOME.Shared/Interfaces/IRemainingInterfaces.cs) | Skill orchestrator: load, register, bind to agents |
| `SkillPackage` | On-disk skill package metadata |
| `SkillManifest` | Remote registry manifest |

## Governance: Guardrails, Hooks, HITL

| Interface | Description |
|-----------|-------------|
| [`IGuardrail`](src/AGXIOME.Shared/Interfaces/IGovernanceInterfaces.cs) | Input/output content validation |
| [`IAgentHook`](src/AGXIOME.Shared/Interfaces/IGovernanceInterfaces.cs) | Lifecycle hooks (BeforeRun, AfterRun, OnError) |
| [`IToolHook`](src/AGXIOME.Shared/Interfaces/IGovernanceInterfaces.cs) | Pre/post tool execution hooks |
| [`IApprovalManager`](src/AGXIOME.Shared/Interfaces/IGovernanceInterfaces.cs) | Human-in-the-Loop approval workflow |
| `GuardrailType` | Input / Output / Both |
| `GuardrailSeverity` | Block / Warn / Transform |
| `HookPoint` | BeforeRun, AfterRun, BeforeToolCall, AfterToolCall, OnError, OnStreamChunk |

## XLink Transport

| Interface | Description |
|-----------|-------------|
| [`IXLinkRouter`](src/AGXIOME.Shared/Interfaces/IRemainingInterfaces.cs) | Typed message routing with compression, checksum, encryption |
| `XLinkEnvelope` | Message envelope with HMAC integrity |
| `XLinkOptions` | Compression (Gzip), signature, checksum, retry |

## Realtime (SignalR)

| Class | Description |
|-------|-------------|
| `SignalRCompletionNotifier` | Channel<T>-based async completion events |
| `RealtimeGroupChatSwarm` | Group-based agent communication |

## LLM Providers

| Interface | Description |
|-----------|-------------|
| [`ILLMProvider`](src/AGXIOME.Shared/Interfaces/ILLMProvider.cs) | Unified LLM completion + embedding API |
| [`ILLMProviderFactory`](src/AGXIOME.Shared/Interfaces/ILLMProvider.cs) | Provider creation from config |
| `LLMProviderType` | 50+ provider enum: OpenAI, Anthropic, Ollama, local, regional |
| `LLMProviderCapabilities` | Streaming, embeddings, function calling, vision |

## LLMWIKY Engine

| Interface | Description |
|-----------|-------------|
| [`ILLMWIKYEngine`](src/AGXIOME.Shared/Interfaces/ILLMWIKYEngine.cs) | Inference, embedding, function calling orchestration |

## Storage (CQRS)

| Interface | Description |
|-----------|-------------|
| [`ICommandStore`](src/AGXIOME.Shared/Interfaces/IStorageInterfaces.cs) | CQRS command-side: Save, Update, Delete |
| `IQueryStore<T>` | CQRS query-side: GetById, Query, QueryPaged |
| [`IUnitOfWork`](src/AGXIOME.Shared/Interfaces/IStorageInterfaces.cs) | Transactional boundary |
| `StorageProviderType` | 25 providers: PostgreSQL, SQLite, Redis, MongoDB, etc. |

## Vector Stores

| Type | Description |
|------|-------------|
| `VectorDbProviderType` | 30 providers: Pinecone, Qdrant, Milvus, pgvector, etc. |
| `VectorRecord` | Vector + metadata for upsert/search |
| `VectorFilter` | Metadata-based pre-filtering |

## Embedders & Chunkers

| Interface | Description |
|-----------|-------------|
| [`IEmbedder`](src/AGXIOME.Shared/Interfaces/IProviderInterfaces.cs) | Single-text + batch embedding |
| [`IChunker`](src/AGXIOME.Shared/Interfaces/IProviderInterfaces.cs) | Document chunking with overlap |
| [`IReranker`](src/AGXIOME.Shared/Interfaces/IProviderInterfaces.cs) | Relevance re-ranking |
| `EmbedderType` | 25 embedder models |
| `ChunkerType` | 12 chunking strategies |
| `RerankerType` | 8 reranker models |

## Session Management

| Interface | Description |
|-----------|-------------|
| [`ISessionManager`](src/AGXIOME.Shared/Interfaces/ISessionManager.cs) | Session lifecycle: create, persist, resume |
| [`ISessionState`](src/AGXIOME.Shared/Interfaces/ISessionManager.cs) | Mutable key-value session state |

## Telemetry

| Class | Description |
|-------|-------------|
| `AgxiomeMetrics` | Meter "AGXIOME.Core": 7 counters, 7 histograms, 2 UpDownCounters |
| `OrchestratorMetrics` | Meter "AGXIOME.Orchestration": 4 counters |
| `TelemetryConfig` | OpenTelemetry configuration + DI extension |

## Security

| Interface | Description |
|-----------|-------------|
| [`IAgxiomeCrypto`](src/AGXIOME.Shared/Interfaces/IRemainingInterfaces.cs) | ECDSA signing + HMAC integrity |
| [`IAuditLogger`](src/AGXIOME.Shared/Interfaces/IRemainingInterfaces.cs) | Pseudonymized audit trail |
| `RbacRole` | Admin, Operator, Developer, ReadOnly |
| `RbacAction` | Read, Write, Execute, Delete, Admin |

## Configuration

| Record | Description |
|--------|-------------|
| `LearningConfig` | Learning modes, optimization, max memories |
| `LLMWIKYConfig` | LLM provider configuration |
| `AgentConfig` | Agent configuration (model, tools, guardrails) |
| `GovernanceConfig` | HITL, audit hooks, background evals |

---

> Full source with XML docs: [`src/AGXIOME.Shared/Interfaces/`](../src/AGXIOME.Shared/Interfaces/)
