---
title: AgentRearrangeTests
category: entity
tags: [tests, flow-parser, agent-rearrange]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentRearrangeTests

## Summary
The `AgentRearrangeTests` class contains a suite of unit tests for the `FlowParser` component, which parses flow definition strings into stages of agents. These tests validate correct handling of single-agent, sequential (`->`), parallel (`,`), mixed flows, empty strings, and complex agent pipelines. They ensure the parser correctly decomposes human-readable flow syntax into structured stage lists used by the framework's agent orchestration.

## Key Details
- **Test class**: `AgentRearrangeTests` in `AGXIOME.Tests`
- **Method tested**: `FlowParser.Parse(string flow)` returning `List<List<string>>`
- **Test cases**:
  - `FlowParser_SingleAgent_ReturnsOneStage` – verifies a single agent string produces one stage with one agent.
  - `FlowParser_Sequential_ReturnsOrderedStages` – validates sequential arrows create multiple stages in order.
  - `FlowParser_Parallel_ReturnsParallelStage` – confirms comma-separated agents are grouped into a single parallel stage.
  - `FlowParser_MultipleParallel_ReturnsCorrectStages` – tests flows with parallel groups in sequential stages.
  - `FlowParser_Empty_Throws` – ensures empty or whitespace strings raise `ArgumentException`.
  - `FlowParser_ComplexFlow_ParsesCorrectly` – validates a real-world pattern (researcher → writer, editor → publisher).
- **Assertions**: Use xUnit's `Assert.Single`, `Assert.Equal`, `Assert.Throws<ArgumentException>`

## Cross-References
- [[FlowParser]] — the core parsing logic tested by this class
- [[AgentRearrangeWorkflow]] — workflow abstraction that likely consumes parsed stages

## Source Files
- `AGXIOME.Tests/AgentRearrangeTests.cs` (ingested 2026-05-14)