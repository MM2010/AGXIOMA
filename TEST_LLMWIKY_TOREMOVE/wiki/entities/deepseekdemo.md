---
title: Deepseekdemo
category: entity
tags: [demo, deepseek, interactive, cli]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Deepseekdemo

## Summary
The `DeepSeekDemo` class provides an interactive console session that uses DeepSeek as the LLM provider within the AGXIOME framework. It runs a ReAct-style loop (Think → Act → Respond) with optional streaming output, enabling users to chat with a DeepSeek-powered agent directly from the command line. This demo showcases the integration of the [[LLMWIKYEngine]] with the DeepSeek provider and serves as a reference for building custom interactive agents.

## Key Details
- **Entry point**: `RunAsync(string[] args, CancellationToken ct)` called from the CLI dispatcher when the verb is `demo`.
- **Environment variable**: Requires `DEEPSEEK_API_KEY`; optionally `DEEPSEEK_MODEL` (default: `deepseek-chat`).
- **CLI arguments**: `--model` (model name), `--system` (custom system prompt), `--stream` (enable token-by-token streaming).
- **Default system prompt**: "You are AGXIOME Assistant, a helpful and concise AI agent."
- **Agent configuration**: Uses `AgentFactory.CreateWithEngine` with a custom `LLMWIKYEngine` configured for DeepSeek (OpenAI‑compatible endpoint).
- **Interactive REPL commands**: `/clear` resets conversation history; `/history` prints all exchanges; `exit`/`quit`/`/bye` end the session.
- **Streaming mode**: When `--stream` is provided, tokens are printed as they arrive via `agent.RunStreamAsync`.
- **Blocking mode**: Returns full response and displays token count, inference count, and duration metadata.
- **Error handling**: Catches missing API key (exit code 1), authentication errors (exit code 2), and general exceptions.
- **Logging**: Suppresses AGXIOME internals below `Warning` to keep the console clean.

## Cross-References
- [[LLMWIKYEngine]] — the engine that abstracts DeepSeek and other providers; used here to build the agent.
- [[AgentFactory]] — factory method `CreateWithEngine` used to instantiate the demo agent.
- [[LLMWIKYConfig]] — configuration object passed to the engine; sets provider, model, API key, and retry parameters.
- [[CLIDispatcher]] — dispatcher that routes the `demo deepseek` command to this class.
- [[DeepSeek]] — the LLM provider referenced in the demo.

## Source Files
- `AGXIOME.CLI/Demo/DeepSeekDemo.cs` (ingested 2026-05-14)