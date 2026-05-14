# P12 — CLI, UI & Multi-Framework

> **Priority**: 🟢 P2 | **Dipende da**: P08 (Runtime)
> **Target**: CLI interattiva, Control Plane UI, bridge multi-framework

---

## Task

| # | Task | Descrizione | Effort |
|---|------|-------------|--------|
| MU-01 | **CLI — `agxiome run`** | Esegui agent/team da terminale | 1gg |
| MU-02 | **CLI — `agxiome list`** | Lista agent/team/workflow | 0.5gg |
| MU-03 | **CLI — `agxiome session`** | Gestione sessioni | 0.5gg |
| MU-04 | **CLI — `agxiome admin`** | Health check, metrics, logs | 0.5gg |
| MU-05 | **CLI — `agxiome config`** | Get/set config runtime | 0.5gg |
| MU-06 | **Control Plane UI** | Web dashboard per agent/team/run | 5gg |
| MU-07 | **Approval UI** | UI per approvare HITL requests | 2gg |
| MU-08 | **LangChain Bridge** | Wrapper per eseguire LangChain agents | 1gg |
| MU-09 | **Semantic Kernel Bridge** | Wrapper per SK plugins | 1gg |
| MU-10 | **Claude SDK Bridge** | Bridge per Anthropic API diretto | 1gg |

### Pattern: CLI

```
> agxiome run agent my-agent "Cosa è AGXIOME?"
  ✓ Run started (id: run_abc123)
  ▶ AGXIOME è un framework open-source per agenti AI...
  
> agxiome run agent my-agent "Spiega" --stream
  AGXIOME è un framework...
  
> agxiome list agents
  ┌────────────┬──────────┬──────────┐
  │ Name       │ Model    │ Sessions │
  ├────────────┼──────────┼──────────┤
  │ my-agent   │ gpt-4o   │ 12       │
  │ helper     │ claude-4 │ 5        │
  └────────────┴──────────┴──────────┘
```

**Totale task: 10 | File: ~20 | Test: ~40**
