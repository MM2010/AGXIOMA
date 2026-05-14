---
title: SemanticKernelBridge
category: entity
tags: [bridge, semantic-kernel, adapter, plugin]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SemanticKernelBridge

## Summary
The `SemanticKernelBridge` is an in-process adapter that wraps Microsoft Semantic Kernel plugins as AGXIOME-callable capabilities. It uses a thin, interface-based adapter (`ISemanticKernelAdapter`) to avoid a hard dependency on the Semantic Kernel NuGet package, enabling optional integration. The bridge provides a simple `InvokePluginAsync` method that translates AGXIOME agent calls into SK function invocations, and includes a `NullSemanticKernelAdapter` for testing without a live SK runtime.

## Key Details
- Defined in namespace `AGXIOME.XLink.Bridges` within `AGXIOME.XLink/Bridges/SemanticKernelBridge.cs`.
- Two core interfaces: `ISemanticKernelAdapter` (abstraction over SK kernel) and `ISemanticKernelBridge` (AGXIOME service interface).
- `SemanticKernelBridge` implements `ISemanticKernelBridge` by delegating to an injected `ISemanticKernelAdapter`.
- `IsAvailable` property checks if the adapter is ready.
- `InvokePluginAsync` returns an `AgentBridgeResult` with success/failure, result, error message, and elapsed time.
- `NullSemanticKernelAdapter` provides deterministic outputs for unit testing without SK.
- Pattern supports lazy loading of SK adapter assembly at runtime, maintaining separation of concerns.

## Cross-References
- [[AgentBridgeResult]] — the return type used by `InvokePluginAsync`.
- [[XLink]] — the parent namespace under which this bridge operates.

## Source Files
- `AGXIOME.XLink/Bridges/SemanticKernelBridge.cs` (ingested 2026-05-14)