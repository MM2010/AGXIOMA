---
title: AgentServices
category: entity
tags: [service-locator, static-bridge, runtime-code, LLM, HTTP, generated-code]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentServices

## Summary
`AgentServices` is a static service locator that provides runtime-compiled agent code with direct, stateless access to core infrastructure services — specifically an `ILLMProvider` for AI completions and a shared `HttpClient` for HTTP operations. It is designed to circumvent the impossibility of constructor injection for dynamically instantiated types (`Activator.CreateInstance`). Initialization occurs once at application startup via `Initialize()`, after which generated code can call `AgentServices.Llm`, `AgentServices.Http`, or the convenience methods `AskAsync`, `GetAsync`, and `PostJsonAsync` without any dependency wiring.

## Key Details
- Static class in namespace `AGXIOME.DynamicRuntime.Bridge`.
- Exposes two static properties: `Llm` (type `ILLMProvider`) and `Http` (type `HttpClient`).
- Both properties throw `InvalidOperationException` if accessed before `Initialize()` is called.
- `Initialize(ILLMProvider llm, HttpClient? http = null)` sets the static references; an `HttpClient` is created if null is passed.
- Concurrent access to pooled `HttpClient` is safe by design; `ILLMProvider` thread safety depends on the implementation.
- `AskAsync(...)` is a synchronous-style shortcut that builds an `LLMCompletionRequest` and returns the response content as a string.
- `GetAsync(url, ct)` and `PostJsonAsync(url, payload, ct)` wrap `HttpClient` calls and return response body as string.
- The class is intended for use by code generated at runtime (e.g., via Roslyn compilation) where constructor injection is not feasible.
- Initialization is expected to be orchestrated by `DynamicRuntimeServiceExtensions` (likely at host startup).

## Cross-References
- [[ILLMProvider]] — the interface for LLM completions injected into `AgentServices`.
- [[LLMCompletionRequest]] — the model used in `AskAsync` to encapsulate the request.
- [[DynamicRuntimeServiceExtensions]] — the extension method class that calls `Initialize()` and wires the services.
- [[DynamicRuntime]] — the overarching component that produces and executes runtime code relying on `AgentServices`.

## Source Files
- `AGXIOME.DynamicRuntime/Bridge/AgentServices.cs` (ingested 2026-05-14)