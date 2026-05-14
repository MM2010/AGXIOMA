# AGXIOMA LLMWiki — Schema & Operating Guide

> This document is the schema for the AGXIOMA LLMWiki.
> Based on the Karpathy LLM Wiki pattern (https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f).
> The LLM agent reads this on every session to understand the wiki structure and workflows.

---

## Purpose

This wiki is a **persistent, compounding knowledge base** of the AGXIOMA project.
Instead of re-deriving knowledge from raw source files at query time (RAG),
the LLM incrementally **builds and maintains structured wiki pages** that capture
entities, concepts, patterns, cross-references, and contradictions.

The wiki is the compiled artifact. Sources are immutable. The LLM writes the wiki.

---

## Three Layers

| Layer | Location | Owner | Role |
|---|---|---|---|
| **Raw sources** | `src/`, `apps/`, `docs/`, `PLAN/` | Immutable | Ground truth — never modified |
| **Wiki** | `wiki/` | LLM | Summaries, entities, concepts, cross-refs |
| **Schema** | `WIKI_SCHEMA.md` | Human + LLM | This file — conventions, workflows |

---

## Wiki Directory Structure

```
wiki/
├── index.md          ← content catalog (LLM updates on every ingest)
├── log.md            ← append-only chronological log
├── overview.md       ← project overview, high-level architecture
├── entities/         ← one page per key class, interface, module, component
│   ├── AgxiomeBuilder.md
│   ├── IAgxiomeAgent.md
│   ├── KnowledgeBase.md
│   └── ...
├── concepts/         ← architectural patterns, workflows, design decisions
│   ├── RAG-pipeline.md
│   ├── swarm-patterns.md
│   ├── tool-calling.md
│   └── ...
└── sources/          ← per-file summaries (one page = one source file)
    ├── AGXIOME.Core.md
    ├── AGXIOME.Storage.md
    └── ...
```

---

## Page Format Convention

Every wiki page follows this frontmatter + structure:

```markdown
---
title: <Page Title>
category: entity|concept|source|overview
tags: [tag1, tag2]
source_count: <number of source files this page reflects>
created_at: <ISO date>
updated_at: <ISO date>
---

# <Page Title>

## Summary
One paragraph synthesis.

## Key Details
Bullet points of important facts.

## Cross-References
- [[RelatedPage]] — why it's related
- [[AnotherPage]] — relationship

## Source Files
- `path/to/source.cs` (ingested YYYY-MM-DD)
```

---

## index.md Format

```markdown
# AGXIOMA Wiki Index

_Last updated: <date>. Total pages: N._

## Overview
- [[overview]] — Project overview and architecture

## Entities (N)
- [[entities/AgxiomeBuilder]] — Fluent builder for agents and swarms
- ...

## Concepts (N)
- [[concepts/RAG-pipeline]] — RAG ingestion and retrieval workflow
- ...

## Sources (N)
- [[sources/AGXIOME.Core]] — Core agent runtime, builder, patterns
- ...
```

---

## log.md Format (append-only)

Each entry starts with a consistent prefix for grep-ability:
```
## [YYYY-MM-DD HH:MM] <op_type> | <title>
```

Example entries:
```markdown
## [2026-05-14 10:30] ingest | AGXIOME.Core — 47 files processed, 12 wiki pages created/updated
## [2026-05-14 10:45] query | "What is AgxiomeBuilder?" — 3 pages retrieved
## [2026-05-14 11:00] lint | Health check — 2 orphan pages found, 1 stale claim flagged
```

---

## Operations

### Ingest
When asked to ingest source files:
1. Read the source file content
2. Identify: what is this file? (class, interface, config, docs, etc.)
3. Extract key entities, their purpose, their relationships
4. Write or update the relevant wiki page(s)
5. Update `index.md` catalog entry
6. Append ingest entry to `log.md`
7. One source may touch multiple wiki pages (e.g., a file defining 3 classes → 3 entity pages)

### Query
When answering questions:
1. Read `index.md` to find relevant pages
2. Load the relevant pages
3. Synthesize an answer with citations (`[[PageName]]`)
4. If the wiki is insufficient, note which sources might fill the gap
5. Good answers can be filed back as new wiki pages

### Lint
When running a health check:
1. Check for orphan pages (no inbound links from index.md)
2. Check for stale claims (pages not updated after related source changes)
3. Check for missing cross-references (concept mentioned but no page)
4. Check for contradictions between pages
5. Report findings and suggest new pages or investigations

---

## LLM Prompting Notes

- Always maintain the frontmatter format
- Use `[[WikiLink]]` syntax for cross-references (Obsidian-compatible)
- Keep summaries concise (1-2 paragraphs max)
- When a new source contradicts an existing page, note the contradiction explicitly
- The wiki should get richer with every ingest — never delete pages, only update them
- Prefer updating existing pages over creating new ones when in doubt

---

## Technology Stack (for reference)

| Component | Technology |
|---|---|
| Vector store | PostgreSQL + pgvector (`LLMWikiTest` DB) |
| Embeddings | Ollama `mxbai-embed-large` (1024 dims) |
| LLM | DeepSeek `deepseek-v4-flash` |
| Session cache | Redis `localhost:6379` |
| Framework | AGXIOMA (.NET 10) |
