---
title: WorkflowSerializer
category: entity
tags: [serialization, workflow, DAG, storage]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# WorkflowSerializer

## Summary
The `WorkflowSerializer` is a stateless utility that provides deterministic JSON serialization and deserialization of workflow DAG definitions (nodes + edges). It uses pure data records—`WorkflowDefinition`, `WorkflowStepDescriptor`, and `WorkflowTransitionDescriptor`—so that handler closures are never serialized, keeping the wire format safe and portable. The serializer also includes helper methods (`SaveAsync`, `FromEntry`) for round‑tripping workflow definitions to an `ICommandStore`.

## Key Details
- **Records**: `WorkflowStepDescriptor` (node), `WorkflowTransitionDescriptor` (edge), `WorkflowDefinition` (full DAG), and `WorkflowDefinitionEntry` (persistence DTO).
- **Serialization options**: Uses `System.Text.Json` with `JsonIgnoreCondition.WhenWritingNull` and `JsonStringEnumConverter` for enum‑first step types.
- **No closure serialization**: Step handler logic is never embedded in JSON—only a `HandlerRef` string identifier is stored.
- **Condition handling**: `ConditionRef` on transitions is a lookup key for a server‑side registry; condition logic is never deserialized or executed.
- **Helper methods**:
  - `Serialize(WorkflowDefinition)` → JSON string.
  - `Deserialize(string json)` → `WorkflowDefinition`.
  - `SaveAsync(WorkflowDefinition, ICommandStore, CancellationToken)` persists as a `WorkflowDefinitionEntry`.
  - `FromEntry(WorkflowDefinitionEntry?)` loads and deserializes a stored definition (returns `null` if entry is `null`).
- **Deterministic IDs**: Uses `Guid.CreateVersion7()` for `WorkflowDefinition.Id` by default.
- **Immutable data**: All public properties are `init`‑only; collections are `IReadOnlyList`.

## Cross-References
- [[ICommandStore]] — the storage abstraction used by `SaveAsync` and `FromEntry`.
- [[WorkflowDefinition]] — the core DAG model that this serializer handles.

## Source Files
- `AGXIOME.Core/Storage/WorkflowSerializer.cs` (ingested 2026-05-14)