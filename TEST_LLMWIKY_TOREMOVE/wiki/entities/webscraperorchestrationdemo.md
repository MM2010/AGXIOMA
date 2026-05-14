---
title: WebScraperOrchestrationDemo
category: entity
tags: [web scraping, orchestration, multi-agent, deepseek, demo]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# WebScraperOrchestrationDemo

## Summary
The `WebScraperOrchestrationDemo` is a multi-agent web scraping demonstration within the AGXIOME framework. It launches four parallel worker agents, each targeting a different search engine (Google, DuckDuckGo, Bing, Yahoo), to fetch top-10 results for a user-provided query. Workers strip HTML, save results to local files, and report completion via an in-memory channel. A DeepSeek-backed orchestrator agent then reads all worker reports, validates the saved files for content quality and HTML-stripping correctness, and produces a final audit summary. This demo illustrates the asynchronous parallelism, communication channels, and LLM-driven orchestration capabilities of the AGXIOME agent system.

## Key Details
- **Architecture**: `WebWorkerAgent` instances run in parallel for each engine; their reports are collected via a `Channel<WorkerReport>`. The orchestrator is an [[AgxiomeAgent]] powered by a DeepSeek LLM.
- **Usage**: `agxiome demo scraper [--query "..."] [--out ./output]` (requires `DEEPSEEK_API_KEY` environment variable).
- **Error handling**: Failed workers produce an error file and are flagged; the orchestrator can detect partial or blocked responses.
- **Local validation**: A deterministic audit checks that output files contain no HTML tags and have meaningful content, independent of the LLM.
- **Engine behavior**: DuckDuckGo is noted as the most "scraper-friendly" for local development; Google/Bing/Yahoo may return blocked responses.
- **Output**: Files named `{engine}-results.txt` are written to a timestamped run directory under `--out`.
- **Exit code**: Returns `0` on success, `1` if the API key is missing, `2` if local validation fails.

## Cross-References
- [[AgxiomeAgent]] — the base agent class used for both workers and the orchestrator.
- [[DeepSeekProvider]] — the LLM provider that backs the orchestrator agent.
- [[Channel]] — the concurrency primitive used for worker-to-orchestrator report passing.

## Source Files
- `AGXIOME.CLI/Demo/WebScraperOrchestrationDemo.cs` (ingested 2026-05-14)