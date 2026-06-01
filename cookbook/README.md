# AGXIOME Cookbook

> Ready-to-run examples organized by topic. Each recipe links to the full demo app or code sample.

---

## 🚀 Getting Started

| Recipe | Description | Demo App |
|--------|-------------|----------|
| [Single Agent](01-single-agent/README.md) | Create your first AI agent | [`agxioma-single-agent`](../apps/agxioma-single-agent/) |
| [Install from NuGet](01-single-agent/nuget.md) | `dotnet add package AGXIOME.Core --prerelease` | — |
| [Use DeepSeek](01-single-agent/deepseek.md) | Configure DeepSeek as LLM provider | — |

## 🤖 Agents & Tools

| Recipe | Description | Demo App |
|--------|-------------|----------|
| [Tool Agent](02-tools/README.md) | Agent with 5+ tools (search, file system, API) | [`agxioma-tool-agent`](../apps/agxioma-tool-agent/) |
| [Streaming Agent](02-tools/streaming.md) | SSE streaming with `RunStreamAsync` | [`agxioma-single-agent`](../apps/agxioma-single-agent/) |
| [Structured Output](02-tools/structured-output.md) | Typed JSON responses from LLMs | — |
| [Guardrails & HITL](02-tools/guardrails.md) | Input validation + human-in-the-loop | — |

## 🏗 Teams & Swarms

| Recipe | Description | Demo App |
|--------|-------------|----------|
| [ForestSwarm Routing](03-teams/forest-swarm.md) | Domain-based agent routing | [`agxioma-team`](../apps/agxioma-team/) |
| [GrokSwarm Code Review](03-teams/grok-swarm.md) | Captain + Specialists pattern | [`agxioma-code-reviewer`](../apps/agxioma-code-reviewer/) |
| [HeavySwarm Research](03-teams/heavy-swarm.md) | Parallel research + synthesis | [`agxioma-orchestrator`](../apps/agxioma-orchestrator/) |
| [Multi-Agent Team](03-teams/multi-agent.md) | Role-based team collaboration | [`agxioma-multi-agent-team`](../apps/agxioma-multi-agent-team/) |
| [Hybrid Routing](03-teams/hybrid-routing.md) | Keyword + semantic routing | [`agxioma-hybrid-routing`](../apps/agxioma-hybrid-routing/) |

## 📚 RAG & Knowledge

| Recipe | Description | Demo App |
|--------|-------------|----------|
| [RAG Pipeline](04-rag/README.md) | Chunk → Embed → Search → Generate | [`agxioma-rag-pipeline`](../apps/agxioma-rag-pipeline/) |
| [LLM Wiki](04-rag/llm-wiki.md) | Internal wiki with RAG search | [`agxioma-llm-wiki`](../apps/agxioma-llm-wiki/) |
| [Culture System](04-rag/culture.md) | Shared organizational knowledge | — |
| [Learning Machine](04-rag/learning.md) | Auto-learn user preferences | — |

## 🔧 Domain-Specific

| Recipe | Description | Demo App |
|--------|-------------|----------|
| [Stock Tracker](05-domains/stock-tracker.md) | Financial analysis with ForestSwarm | [`agxioma-stock-tracker`](../apps/agxioma-stock-tracker/) |
| [Support Hub](05-domains/support-hub.md) | Customer support with ticket tools | [`agxioma-support-hub`](../apps/agxioma-support-hub/) |
| [Legal Email Pipeline](05-domains/legal-email.md) | GDPR classification + SignalR | [`agxioma-legal-email`](../apps/agxioma-legal-email/) |
| [Incident Pipeline](05-domains/incident-pipeline.md) | Automated incident response | [`agxioma-incident-pipeline`](../apps/agxioma-incident-pipeline/) |
| [Research Agent](05-domains/research-agent.md) | Multi-phase web research | [`agxioma-research-agent`](../apps/agxioma-research-agent/) |

## 🧪 Advanced

| Recipe | Description | Demo App |
|--------|-------------|----------|
| [Dynamic Skill Lab](06-advanced/dynamic-skills.md) | Runtime skill compilation + sandbox | [`agxioma-dynamic-skill-lab`](../apps/agxioma-dynamic-skill-lab/) |
| [Agentic Builder](06-advanced/agentic-builder.md) | LLM generates complete projects | [`agxioma-agentic-builder`](../apps/agxioma-agentic-builder/) |
| [OCR Service](06-advanced/ocr-service.md) | Tesseract OCR as AGXIOME tool | [`agxioma-ocr-service`](../apps/agxioma-ocr-service/) |

---

## 📖 Reference

- [API Reference](../docs/API.md)
- [Provider Audit](../docs/PROVIDER_TOOLKIT_AUDIT.md)
- [All Demos](../docs/DEMO_IMPLEMENTATIONS_EXAMPLES.md)
- [Website](https://agxiome.com)
