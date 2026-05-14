---
title: DynamicOrchestratorDemo
category: entity
tags: [dynamic-orchestration, agent, llm, deepseek, redis, worker]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# DynamicOrchestratorDemo

## Summary
`DynamicOrchestratorDemo` is a CLI demo within the AGXIOMA framework that showcases runtime agent instantiation driven by LLM-based planning. Given a user goal phrase, an orchestrator (powered by DeepSeek) decomposes the goal into a JSON task plan, stores each task in Redis, spawns one worker agent per task, executes them with tool access, and finally synthesizes the collected context into a coherent result. The demo illustrates end-to-end dynamic orchestration with Redis-based state management and autonomous tool-use by workers.

## Key Details
- **Flow**: User enters a goal → Orchestrator (DeepSeek) returns a JSON task plan → Tasks registered in Redis under `agxiome:tasks:<runId>:<taskId>` → Workers instantiated at runtime per task → Each worker executes, updates Redis status/result → Orchestrator waits for all tasks, reads context from Redis, and prints a timeline.
- **Redis fallback**: If Redis is unavailable, an in-memory `TaskStore` is used instead.
- **Worker Tool Guide**: Each worker receives a predefined system prompt (overriding the planner’s) that defines three tools: `http_fetch`, `web_search`, and `file_save`. Workers are instructed to use these tools, avoid claiming no internet access, and call `file_save` before final answer.
- **Orchestrator Planning**: Uses a strict JSON schema (`PlanSchema`) for the LLM to return a list of tasks with `id`, `agentName`, `agentRole`, `systemPrompt`, `taskPrompt`, and `dependsOn`.
- **Configuration**: Requires `DEEPSEEK_API_KEY` environment variable; accepts `--redis` and `--model` CLI arguments (defaults: `localhost:6379` and `deepseek-chat`).
- **Run ID & Session**: A unique run ID (short guid) is generated per `agxiome demo orchestrator` execution; each interactive session gets a session ID for logging.
- **TTL**: All Redis task keys expire after 3600 seconds (1 hour).

## Cross-References
- [[LLMWIKYConfig]] — Configuration used to create the DeepSeek engine for orchestration and worker agents.
- [[AgentFactory]] — Factory that configures and creates agent instances for workers.
- [[TaskStore]] — In-memory/Redis-backed store for task state used by the demo.

## Source Files
- `AGXIOME.CLI/Demo/DynamicOrchestratorDemo.cs` (ingested 2026-05-14)