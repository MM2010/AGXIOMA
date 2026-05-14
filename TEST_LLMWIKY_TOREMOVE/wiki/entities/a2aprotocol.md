---
title: A2A Protocol
category: entity
tags: [a2a, agent-to-agent, json-rpc, protocol]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# A2A Protocol

## Summary
The Agent-to-Agent (A2A) protocol implementation enables AGXIOME agents to communicate with any A2A-compatible agent server using JSON-RPC. It follows the Google ADK A2A standard and provides both a client (`A2AClient`) for sending messages and a server handler (`A2AServerHandler`) that wraps an `IAgxiomeAgent` to accept incoming requests.

## Key Details
- **Client side**: `A2AClient.SendMessageAsync()` sends a user message to a remote agent URL and returns an `A2AResponse`.
- **Server side**: `A2AServerHandler.ProcessAsync()` deserializes an incoming JSON-RPC request, invokes the wrapped `IAgxiomeAgent`, and returns a properly formatted response.
- **Protocol**: Uses JSON-RPC 2.0 with method `"tasks/send"`.
- **Message model**: `A2AMessage` with `Role` (user/agent) and `Parts` (list of `A2APart` containing text or type).
- **Session support**: Optional `sessionId` parameter allows multi-turn conversations.
- **Error handling**: Server returns standard JSON-RPC error responses for invalid requests.
- **Namespace**: `AGXIOME.Interfaces.A2A`, source file `AGXIOME.Interfaces/A2A/A2AProtocol.cs`.

## Cross-References
- [[AgxiomeAgent]] — the `IAgxiomeAgent` interface that is wrapped by `A2AServerHandler`
- [[A2ARequest]] — JSON-RPC request record used by the protocol
- [[A2AResponse]] — JSON-RPC response record with result or error

## Source Files
- `AGXIOME.Interfaces/A2A/A2AProtocol.cs` (ingested 2026-05-14)