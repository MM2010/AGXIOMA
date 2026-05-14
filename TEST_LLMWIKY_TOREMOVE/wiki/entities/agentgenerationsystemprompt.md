---
title: AgentGenerationSystemPrompt
category: entity
tags: [prompt-generation, agent-generation, system-prompt, dynamic-runtime]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentGenerationSystemPrompt

## Summary
A static class that constructs the system prompt used to instruct an LLM to generate a C# dynamic agent for the AGXIOMA framework. It enforces a strict output format, restricts allowed/blocked namespaces, specifies the required interface and attribute, and includes few-shot examples from `AgentTemplateExamples`. This prompt ensures that LLM-generated agents are safe, instantiable, and follow framework conventions.

## Key Details
- `Build(string[]? additionalAllowed = null)` returns the complete system prompt string.
- Two built-in namespace lists: `DefaultAllowedNamespaces` (e.g., `System`, `System.Text.Json`, `AGXIOME.DynamicRuntime.Bridge`) and `DefaultBlockedNamespaces` (e.g., `System.IO`, `System.Reflection`, `unsafe`).
- Optional additional allowed namespaces can be appended via `additionalAllowed` parameter.
- Requires generated classes to implement `ISimpleDynamicAgent` and be decorated with `[AgxiomeAgent(...)]`.
- Prompts include descriptions of `AgentServices` static class for LLM and HTTP calls (`AskAsync`, `GetAsync`, `PostJsonAsync`).
- Instructions demand a single `csharp` code block with no extra text, kebab-case agent IDs, no constructor parameters, and no unsafe code.
- Few-shot examples are pulled from `AgentTemplateExamples.AllExamples`.

## Cross-References
- [[ISimpleDynamicAgent]] — the interface that generated agents must implement.
- [[AgentServices]] — the bridge providing built-in LLM and HTTP capabilities.
- [[AgentTemplateExamples]] — the source of few-shot agent templates included in the prompt.

## Source Files
- `AGXIOME.DynamicRuntime/Generation/Prompts/AgentGenerationSystemPrompt.cs` (ingested 2026-05-14)