---
title: Agent Template Examples
category: entity
tags: [dynamic-agent, templates, examples, few-shot, ISimpleDynamicAgent]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Agent Template Examples

## Summary
The static class `AgentTemplateExamples` provides three complete few-shot examples of dynamic agents that implement `ISimpleDynamicAgent` and are decorated with the `[AgxiomeAgent]` attribute. These examples are embedded in the system prompt of `AgentCodeGenerator` to guide an LLM toward the correct coding style when generating new dynamic agents.

## Key Details
- Located in namespace `AGXIOME.DynamicRuntime.Templates`.
- Contains three constant string fields (`Example1_TextTransform`, `Example2_JsonValidator`, `Example3_SentimentClassifier`), each holding a complete C# class definition.
- All examples follow the same pattern: sealed class implementing `ISimpleDynamicAgent`, with public properties `AgentId` and `Description`, and a `RunAsync(string, CancellationToken)` method.
- Each example is annotated with the `[AgxiomeAgent("id", "description")]` attribute.
- Example 1 (`TextReverser`) inverts words and converts them to uppercase.
- Example 2 (`JsonValidator`) validates JSON input and reports root node type.
- Example 3 (`RuleSentiment`) performs simple lexical sentiment classification.
- The `AllExamples` property returns all three examples concatenated as a single multi-block string, intended for LLM system prompts.

## Cross-References
- [[AgentCodeGenerator]] — consumes these examples in its system prompt.
- [[ISimpleDynamicAgent]] — the interface all examples implement.
- [[AgxiomeAgentAttribute]] — the attribute used for agent metadata.
- [[DynamicRuntime]] — the runtime context where these agents are executed.

## Source Files
- `AGXIOME.DynamicRuntime/Templates/AgentTemplateExamples.cs` (ingested 2026-05-14)