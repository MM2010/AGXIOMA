---
goal: AGXIOME F8 Tools Infrastructure PLAN_DETAILS
version: 1.0
date_created: 2026-05-16
last_updated: 2026-05-16
owner: AGXIOME implementation track
status: Planned
tags: [feature, planning, tools, infrastructure, testing, f8]
---

# Introduction

Questo documento definisce il `PLAN_DETAILS` della sezione F8 Tools Infrastructure. La sezione non va trattata come greenfield: nel repo esistono gia' implementazioni e test per cache, limiter, stop rules, filtri, callable factory e metriche. Il lavoro corretto e' quindi di hardening, integrazione e completamento, non di riscrittura.

## 1. Requirements & Constraints

- `REQ-001`: F8 deve restare allineata ai file core esistenti in `AGXIOME.Core/Tools`.
- `REQ-002`: i componenti F8 devono essere thread-safe, deterministici e senza dipendenze di rete nel path caldo.
- `REQ-003`: dove non esiste un provider esterno reale, i test devono usare oggetti reali di produzione e concorrenza reale, non mock gratuiti.
- `REQ-004`: l'integrazione con `AgxiomeAgent` e con i toolkit deve restare backward-compatible.
- `TST-001`: ogni task TI-01..TI-06 deve avere test funzionali, offensivi, aggressivi e di osservabilita'.
- `TST-002`: i test devono coprire sia comportamento isolato sia integrazione minima nei call-site gia' presenti.
- `SEC-001`: niente leak di payload sensibili o argomenti tool in log/metriche non sanificate.
- `PERF-001`: evitare regressioni di allocazione e lock contention nei componenti in-memory.

## 2. Sezione Corrente Nel Repo

- `AGXIOME.Core/Tools/ToolResultCache.cs`
- `AGXIOME.Core/Tools/ToolCallLimiter.cs`
- `AGXIOME.Core/Tools/StopAfterToolEvaluator.cs`
- `AGXIOME.Core/Tools/ToolFilter.cs`
- `AGXIOME.Core/Tools/CallableFactory.cs`
- `AGXIOME.Core/Tools/ToolMetricsCollector.cs`
- `AGXIOME.Core/Agent/AgxiomeAgent.cs`
- `AGXIOME.Tests/F8ToolsInfrastructureTests.cs`

## 3. Decisione Di Sequenza

1. `TI-01 Tool Result Cache`
2. `TI-02 Tool Call Limit`
3. `TI-03 Stop After Tool`
4. `TI-04 Include/Exclude Tools`
5. `TI-05 Callable Factory`
6. `TI-06 Tool Metrics`
7. integrazione finale con `AgxiomeAgent`

## 4. Shared Baseline F8

- `TASK-001`: verificare contratti pubblici e interfacce di tutti i componenti F8.
- `TASK-002`: verificare thread-safety reale con test concorrenti sui componenti in-memory.
- `TASK-003`: consolidare `F8ToolsInfrastructureTests.cs` come suite canonica della sezione.
- `TASK-004`: aggiungere almeno un test di integrazione minima con `AgxiomeAgent` per i componenti che influenzano il run control.
- `TASK-005`: definire il confine tra uso di oggetti reali e uso accettabile di stub/mocks solo per shape esterne.

## 5. Task Details

### TI-01 - Tool Result Cache

- Goal: chiudere `ToolResultCache` come cache TTL deterministica e sicura per uso runtime.
- Production files: `AGXIOME.Core/Tools/ToolResultCache.cs`.
- Test files: `AGXIOME.Tests/F8ToolsInfrastructureTests.cs`.
- SubTASK-01: verificare chiave deterministica con argomenti ordinati e hash stabile.
- SubTASK-02: verificare sweep TTL, lazy eviction e reset statistiche.
- SubTASK-03: verificare invalidazione per singolo tool e invalidazione globale.
- SubTASK-04: verificare eviction policy `Lru` e `TtlFirst` sotto saturazione.
- Functional tests: set/get, invalidate, stats snapshot, reset stats.
- Offensive tests: tool name vuoto, result nullo, parametri non serializzabili.
- Aggressive tests: concorrenza alta su set/get/invalidate, saturazione `_maxEntries`, race con timer di sweep.
- Observability tests: hit/miss coerenti, nessun leak di payload raw nelle metriche di cache.
- Real provider strategy: oggetto reale in-memory, timer reale, concorrenza reale.
- Exit criteria: cache deterministica, thread-safe e integrabile senza regressioni.

### TI-02 - Tool Call Limit

- Goal: chiudere `ToolCallLimiter` come guardrail autorevole per il run.
- Production files: `AGXIOME.Core/Tools/ToolCallLimiter.cs`, `AGXIOME.Core/Agent/AgxiomeAgent.cs`.
- Test files: `AGXIOME.Tests/F8ToolsInfrastructureTests.cs`.
- SubTASK-01: verificare limiti totali, consecutivi e per-tool.
- SubTASK-02: verificare reset, remove e snapshot coerente dello stato run.
- SubTASK-03: verificare integrazione nei punti di decisione dell'agente.
- Functional tests: check allowed, record success, record error, reset.
- Offensive tests: `runId` vuoto, `toolName` vuoto, limiti a zero e limiti estremi.
- Aggressive tests: piu' thread che registrano successo/errore sullo stesso run, piu' run concorrenti.
- Observability tests: reason string coerente e non rumorosa.
- Real provider strategy: stato reale `ConcurrentDictionary`, nessun mock necessario.
- Exit criteria: blocco deterministico dei run fuori soglia e statistiche coerenti.

### TI-03 - Stop After Tool

- Goal: chiudere `StopAfterToolEvaluator` come regola di terminazione chiara e prevedibile.
- Production files: `AGXIOME.Core/Tools/StopAfterToolEvaluator.cs`, `AGXIOME.Core/Agent/AgxiomeAgent.cs`.
- Test files: `AGXIOME.Tests/F8ToolsInfrastructureTests.cs`.
- SubTASK-01: verificare tutte le modalita' `AfterAnyCall`, `AfterSuccess`, `AfterError`, `Never`.
- SubTASK-02: verificare add/remove rule e comportamento globale.
- SubTASK-03: verificare integrazione con flusso agente dopo tool call reali.
- Functional tests: evaluate con successo/errore, remove, no-rule.
- Offensive tests: tool name nullo/vuoto, regole duplicate, stato globale sporco tra test.
- Aggressive tests: accessi concorrenti a rule set globale.
- Observability tests: motivazione stop esposta in modo utile ma non verboso.
- Real provider strategy: oggetto reale, nessun mock.
- Exit criteria: terminazione tool-driven controllabile senza side effect nascosti.

### TI-04 - Include/Exclude Tools

- Goal: chiudere `ToolFilter` come motore di allowlist/blocklist per scope.
- Production files: `AGXIOME.Core/Tools/ToolFilter.cs`.
- Test files: `AGXIOME.Tests/F8ToolsInfrastructureTests.cs`.
- SubTASK-01: verificare regole per scope multiple e retrieval coerente.
- SubTASK-02: verificare precedence del filtro per allowlist e blocklist.
- SubTASK-03: verificare comportamento con set vuoti e scope mancanti.
- Functional tests: set filter, get filter, get all filters, evaluate allow/block.
- Offensive tests: `scopeId` vuoto, nomi tool nulli, set inconsistenti.
- Aggressive tests: update concorrente di filtri su piu' scope.
- Observability tests: nessuna esposizione indebita di liste tool sensibili.
- Real provider strategy: stato reale concorrente, nessun provider esterno.
- Exit criteria: filtri stabili, leggibili e applicabili a sessioni reali.

### TI-05 - Callable Factory

- Goal: chiudere `CallableFactory` come bridge stabile verso formati callable LLM, in particolare OpenAI function calling.
- Production files: `AGXIOME.Core/Tools/CallableFactory.cs`, `AGXIOME.Shared/Interfaces/IAgxiomeTool.cs`.
- Test files: `AGXIOME.Tests/F8ToolsInfrastructureTests.cs`.
- SubTASK-01: verificare serializzazione schema tool in formato OpenAI.
- SubTASK-02: verificare parse dei tool call in ingresso.
- SubTASK-03: verificare compatibilita' con tool array e deduplicazione logica.
- Functional tests: create callable, build tools array, parse call payload.
- Offensive tests: payload malformed, JSON mancante, arguments invalidi.
- Aggressive tests: parsing ripetuto ad alto volume, tool definitions multiple concorrenti.
- Observability tests: error messages utili su parsing ma senza dump integrale superfluo.
- Real provider strategy: oggetti tool reali o stub minimi strutturali solo per la shape del contratto.
- Exit criteria: schema e parsing affidabili per provider OpenAI-compatible.

### TI-06 - Tool Metrics

- Goal: chiudere `ToolMetricsCollector` come raccolta di count, latency ed error rate.
- Production files: `AGXIOME.Core/Tools/ToolMetricsCollector.cs`.
- Test files: `AGXIOME.Tests/F8ToolsInfrastructureTests.cs`.
- SubTASK-01: verificare record di successi, errori e latenze.
- SubTASK-02: verificare aggregazioni per tool e snapshot coerenti.
- SubTASK-03: verificare reset/cleanup e non esplosione della cardinalita'.
- Functional tests: record success, record error, snapshot metrics.
- Offensive tests: tool name vuoto, latency negative, input null.
- Aggressive tests: altissima frequenza di record su piu' tool in parallelo.
- Observability tests: metriche pseudonimizzate dove il nome tool deriva da input utente.
- Real provider strategy: collector reale in-memory, orologio reale.
- Exit criteria: metriche affidabili e sicure per debug e governance.

## 6. Testing Strategy

- `TEST-001`: ogni task TI deve avere test funzionali diretti sul componente reale.
- `TEST-002`: ogni task TI deve avere almeno un test offensivo su input o stato invalido.
- `TEST-003`: ogni task TI deve avere almeno un test aggressivo di concorrenza o saturazione.
- `TEST-004`: la suite F8 deve includere almeno un test di integrazione minima con `AgxiomeAgent`.
- `TEST-005`: niente dipendenze di rete nei test F8; il realismo qui e' dato da oggetti, thread, timer e strutture reali.

## 7. Risks & Assumptions

- `RISK-001`: alcune implementazioni F8 usano stato globale o timer, quindi i test possono contaminarsi se non isolati bene.
- `RISK-002`: l'uso di `ConcurrentDictionary` non garantisce da solo correttezza semantica sotto race complesse.
- `ASSUMPTION-001`: la suite `F8ToolsInfrastructureTests.cs` resta il punto centrale di validazione.
- `ASSUMPTION-002`: non e' richiesto introdurre un provider distribuito della cache in questa sezione; quello e' hardening futuro.

## 8. Next Execution Order

1. baseline shared F8
2. TI-01 cache
3. TI-02 limiter
4. TI-03 stop rules
5. TI-04 filters
6. TI-05 callable factory
7. TI-06 metrics
8. integrazione finale agente
