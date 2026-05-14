# 12 — Migration Roadmap

## Scopo

Transizione strutturata verso AGXIOME v1.0. ADR, NFR budget, rollout strategy.
Basato su roadmap v5.0 Server-Authoritative Architecture.

## Migration roadmap

| Step | Task | Modulo | Effort | Priority | Dipendenze |
|------|------|--------|--------|----------|-----------|
| 1 | Shared Models project | `AGXIOME.Shared` | 2-3h | CRITICAL | Nessuna |
| 2 | Core Agent engine | `AGXIOME.Core` | 4-6h | CRITICAL | Step 1 |
| 3 | LLMWIKY engine | `AGXIOME.LLMWIKY` | 4-6h | CRITICAL | Step 2 |
| 4 | X-Link transport | `AGXIOME.XLink` | 3-4h | CRITICAL | Step 1 |
| 5 | PostgreSQL storage | `AGXIOME.Storage.Postgres` | 3-4h | CRITICAL | Step 1 |
| 6 | Redis cache layer | `AGXIOME.Storage.Redis` | 2-3h | HIGH | Step 1 |
| 7 | Multi-provider storage | `AGXIOME.Storage.*` | 4-6h | HIGH | Step 5 |
| 8 | Agent Teams | `AGXIOME.Teams` | 4-6h | HIGH | Step 2 |
| 9 | RAG Knowledge | `AGXIOME.Knowledge` | 4-6h | HIGH | Step 5 |
| 10 | Workflow engine | `AGXIOME.Workflows` | 4-6h | HIGH | Step 2 |
| 11 | SignalR realtime | `AGXIOME.Realtime` | 3-4h | HIGH | Step 2 |
| 12 | Security layer | `AGXIOME.Security` | 4-6h | CRITICAL | Step 3 |
| 13 | Monitoring/Observability | `AGXIOME.Telemetry` | 2-3h | HIGH | Step 1 |
| 14 | Test suite | `tests/*` | 6-8h | HIGH | Steps 2-12 |
| 15 | ADR + docs | `docs/*` | 2-3h | MEDIUM | Steps 1-14 |

## ADR template

```markdown
# ADR-NNN: [Titolo decisione]

## Stato
[Proposed | Accepted | Deprecated | Superseded]

## Contesto
[Problema architetturale, forze in gioco, vincoli]

## Opzioni
- **A**: [Descrizione] — [Pro/Contro]
- **B**: [Descrizione] — [Pro/Contro]
- **C**: [Descrizione] — [Pro/Contro]

## Decisione
Opzione **X**: [Motivazione]

## Conseguenze
- Positive: [impatto positivo]
- Negative: [tradeoff accettati]
- Neutral: [cambiamenti organizzativi]

## Compliance
- [ ] Design Gate
- [ ] Security Gate
- [ ] Scalability Gate
- [ ] Reliability Gate
- [ ] Operability Gate
- [ ] Release Gate
```

## NFR Budget

| Categoria | Metrica | Target | Misura |
|-----------|---------|--------|--------|
| **Latenza** | Agent run (no tool) p95 | < 500 ms | OpenTelemetry |
| | Agent run (1 tool) p95 | < 2 s | OpenTelemetry |
| | RAG search p95 (10K docs) | < 50 ms | OpenTelemetry |
| | RAG full cycle p95 | < 600 ms | OpenTelemetry |
| | X-Link delivery p95 (same instance) | < 1 ms | OpenTelemetry |
| | Workflow execution p95 (5 step) | < 5 s | OpenTelemetry |
| | SignalR delivery p95 | < 50 ms | OpenTelemetry |
| **Disponibilità** | Agent execution | 99.9% | Uptime monitor |
| | Storage layer | 99.99% | DB uptime |
| | LLMWIKY provider | 99.5% | Provider SLA |
| | Real-time delivery | 99.9% | Connection uptime |
| **Affidabilità** | Message delivery guarantee | At-least-once | Test replay |
| | Duplicate detection | < 0.01% | Redis dedup |
| | Reconnect success rate | > 99% | Connection stats |
| **Sicurezza** | Auth failures before block | 5/min/IP | Rate-limit counter |
| | Crypto key rotation frequency | 90 days | Scheduled job |
| | Audit log completeness | 100% | Required events |
| **Scalabilità** | Max agents per instance | 100 | Load test |
| | Max concurrent connections | 10K per instance | SignalR perf |
| | Max RAG QPS (100K docs) | 500 | Benchmark |
| **Operabilità** | Deployment rollback time | < 5 min | CI/CD pipeline |
| | Incident detection (critical) | < 1 min | Alert latency |
| | Log query p95 | < 2 s | Seq/ELK perf |

## Error budget

| SLO | Error budget (30d) | Consumo per incident |
|-----|-------------------|---------------------|
| Agent execution 99.9% | 43 min downtime | +10 min per outage |
| Storage 99.99% | 4.3 min downtime | +1 min per outage |
| Realtime delivery 99.9% | 43 min downtime | +5 min per degradation |

## Rollback strategy

| Trigger | Azione | Tempo |
|---------|--------|-------|
| Agent error rate > 10% per 5 min | `git revert <commit>` + deploy | < 5 min |
| Storage write latency > 5s p95 | Spegni feature flag, usa cache | < 2 min |
| Security breach | Kill switch: blocca tutte le connessioni | < 1 min |
| Rate-limit falso positivo | Disable rate-limit via config | < 1 min |

## Rollout strategy (feature flags)

```csharp
public static class FeatureFlags
{
    public const string AgentTeams = "agxiome.teams.enabled";
    public const string RAGSearch = "agxiome.rag.enabled";
    public const string WorkflowEngine = "agxiome.workflows.enabled";
    public const string XLinkCompression = "agxiome.xlink.compression";
    
    // Kill switches
    public const string KillSwitchAll = "agxiome.kill.all";
    public const string KillSwitchAgents = "agxiome.kill.agents";
}

// Fasi rollout:
// Phase 1: Canary (5% traffic) → 24h monitor
// Phase 2: Regional (30% traffic) → 48h monitor
// Phase 3: Global (100% traffic) → permanente
```

## Ownership map

| Flusso critico | Service owner | Incident owner | Approval owner |
|---------------|--------------|---------------|---------------|
| Agent execution | Core team | SRE | Architect |
| LLM inference | ML/AI team | Core team | Architect |
| Storage/CQRS | Data team | DBA/SRE | Architect |
| Realtime/SignalR | Platform team | SRE | Architect |
| Security/Auth | Security team | Security | CISO |
| RAG/Knowledge | ML/AI team | Core team | Architect |
| X-Link transport | Platform team | Core team | Architect |

## Artifacts da generare

| Artifact | Template | Quando |
|----------|----------|--------|
| ADR per ogni modulo | `.github/architecture/ADR_TEMPLATE.md` | Pre-implementazione |
| Threat model | `.github/architecture/THREAT_MODEL_TEMPLATE.md` | Pre-security review |
| Review checklist | `.github/architecture/REVIEW_CHECKLIST.md` | Pre-release |
| Runbook operativo | `docs/runbooks/` | Post-implementazione |
