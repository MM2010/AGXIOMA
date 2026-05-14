---
title: AgentGenerationRequest
category: entity
tags: [generation, dynamic-runtime, llm, record]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentGenerationRequest

## Summary
`AgentGenerationRequest` is an immutable data record used to parameterize agent code generation via a Large Language Model (LLM). It encapsulates the user's intent, optional example pairs, and namespace constraints that guide the generated agent's behavior and scope.

## Key Details
- Defined as a `sealed record` in namespace `AGXIOME.DynamicRuntime.Generation`.
- Contains four properties: `UserId`, `Description`, optional `ExampleInput`, optional `ExampleOutput`, and optional `AllowedNamespaces`.
- `UserId` and `Description` are required; `ExampleInput`/`ExampleOutput` can provide few-shot examples for the LLM.
- `AllowedNamespaces` restricts the agent's generated code to specific namespaces.
- Designed for consumption by the agent generation pipeline in the dynamic runtime.

## Cross-References
- [[DynamicRuntime]] — the runtime environment where this request is processed and agent code is generated.
- [[Agent]] — the entity that may be created or evolved using this request.
- [[LlmGenerationService]] — the service that receives this request to produce agent code.

## Source Files
- `AGXIOME.DynamicRuntime/Generation/AgentGenerationRequest.cs` (ingested 2026-05-14)