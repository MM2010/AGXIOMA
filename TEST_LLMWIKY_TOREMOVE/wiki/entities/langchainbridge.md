---
title: LangChain Bridge
category: entity
tags: [bridge, langchain, agent, http, xlink, integration]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# LangChain Bridge

## Summary
The LangChain Bridge is an HTTP-based integration that allows AGXIOME to invoke LangChain-compatible agent chains as remote capabilities. It avoids a direct LangChain SDK dependency by using a JSON-RPC–style HTTP protocol, making the framework bridge lightweight and loosely coupled. The bridge exposes two main operations: running a named chain with input parameters and listing available chains from the remote endpoint.

## Key Details
- Defined by the `ILangChainBridge` interface with `IsAvailable`, `RunChainAsync`, and `ListChainsAsync`.
- Implementation class `LangChainBridge` uses `HttpClient` to communicate with a configurable endpoint.
- Endpoint URL is read from environment variable `LANGCHAIN_ENDPOINT` (default `null`).
- `RunChainAsync` sends a POST request to `{endpoint}/run` with a JSON body containing `chain` name and `inputs`.
- `ListChainsAsync` sends a GET request to `{endpoint}/chains` and returns a list of chain names.
- All calls return an `AgentBridgeResult` record that encapsulates `Success`, `Content`, `Error`, `FrameworkName`, and `LatencyMs`.
- Errors and exceptions are caught and reported in the result, never thrown.
- Supports cancellation via `CancellationToken`.
- Designed for the MU-08 architecture module — LangChain agent is a remote capability endpoint.

## Cross-References
- [[AgentBridgeResult]] — shared result type used by all framework bridges.
- [[XLink]] — overarching integration layer for external agent frameworks.

## Source Files
- `AGXIOME.XLink/Bridges/LangChainBridge.cs` (ingested 2026-05-14)