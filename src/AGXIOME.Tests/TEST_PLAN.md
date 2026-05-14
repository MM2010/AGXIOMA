# AGXIOME — Test Plan Completo

Obiettivo: **infallibilità**. Ogni modulo testato da ogni angolazione.
Copertura: unit → integration → property-based → chaos → replay.

---

## 1. AGXIOME.Shared — Record, Enum, Interfacce

### Test di base
| # | Test | Cosa verifica |
|---|------|-------------|
| 1.1 | Record immutabili | `with { }` crea nuova istanza, originale invariato |
| 1.2 | Record equality | Due record con stessi valori sono `==` |
| 1.3 | Enum valori unici | Nessun enum ha valori duplicati |
| 1.4 | Interfacce marcatori | Tutte le interfacce hanno implementazione concreta nell'assembly |

### Test subdoli
| # | Test | Cosa verifica |
|---|------|-------------|
| 1.5 | Record con null in campi stringa | Non throwa NullReferenceException |
| 1.6 | Enum serialization roundtrip | `Enum.Parse` e `ToString` tornano allo stesso valore |
| 1.7 | Record con default CancellationToken | Default `default` non causa throw |

---

## 2. AGXIOME.Core — AgxiomeAgent

### Test di base
| # | Test | Cosa verifica |
|---|------|-------------|
| 2.1 | Creazione agente minimale | Status = Idle, Id non null |
| 2.2 | RunAsync con engine test | Response.Content = "Test response" |
| 2.3 | RunAsync completato | Status = Completed |
| 2.4 | RunAsync max iterations | Status = Error dopo `MaxIterations` tool call loop |
| 2.5 | Tool registrato viene chiamato | ToolName matcha response |
| 2.6 | Stream produce chunks | `RunStreamAsync` yield almeno 1 chunk prima di IsFinal |

### Test di contorno (boundary)
| # | Test | Cosa verifica |
|---|------|-------------|
| 2.7 | Prompt vuoto string.Empty | Non throwa, ritorna response |
| 2.8 | Prompt null | `ArgumentNullException` |
| 2.9 | MaxIterations = 0 | Iterazione infinita bloccata → Completed dopo 1 ciclo |
| 2.10 | MaxIterations = int.MaxValue | Non crasha |
| 2.11 | Timeout = 0ms | Task cancellato |
| 2.12 | Timeout = -1ms | `ArgumentException` |
| 2.13 | CancellationToken già cancellato | `TaskCanceledException` |
| 2.14 | Dispose chiamato durante RunAsync | `ObjectDisposedException` |
| 2.15 | Multipla RunAsync concorrente | Solo 1 eseguita, altre in coda o rifiutate |

### Test subdoli (edge)
| # | Test | Cosa verifica |
|---|------|-------------|
| 2.16 | RunAsync dopo errore | Status = Error, seconda RunAsync fallisce subito |
| 2.17 | Tool call loop con tool che ritorna errore | Errore loggato, loop continua |
| 2.18 | Tool call loop con tool che throwa eccezione | Loop non si blocca, next iterazione |
| 2.19 | Guardrail blocca input → Status = Error, Content contiene "Guardrail" |
| 2.20 | Guardrail su output → Response filtrata |
| 2.21 | Pre-hook modifica input → input arriva modificato all'engine |
| 2.22 | Post-hook modifica output → response finale modificata |
| 2.23 | Background hook non blocca response | Response ritorna prima di hook |
| 2.24 | Multiple tool call in un solo LLM response (parallel_tool_calls) | Tutti eseguiti |
| 2.25 | Engine throwa eccezione → retry 3x, poi fallback provider |
| 2.26 | Sessione state persiste tra run | `SessionState.Get()` dopo seconda RunAsync |
| 2.27 | Learning attivo → ProcessAsync chiamato dopo run |

---

## 3. AGXIOME.Core — ToolRegistry

### Test di base
| # | Test | Cosa verifica |
|---|------|-------------|
| 3.1 | Register + GetTool | Tool tornato correttamente |
| 3.2 | RegisterFunction<T> | Tool creato via reflection |
| 3.3 | Unregister tool | Non più ottenibile |
| 3.4 | Per-agent tool isolation | agent A ha tool X, agent B no |

### Test subdoli
| # | Test | Cosa verifica |
|---|------|-------------|
| 3.5 | Register stesso nome 2x | Ultima registrazione vince |
| 3.6 | GetTool nome inesistente | Return null, non throw |
| 3.7 | FindByCategory vuota | Lista vuota |
| 3.8 | ToJsonSchema con parametri complessi | Schema JSON valido |
| 3.9 | ToJsonSchema senza parametri | Schema con `properties: {}` e `required: []` |
| 3.10 | GetToolSchemas con callable factory | Factory eseguita, tools risolti per user_id |

---

## 4. AGXIOME.Core — Workflow

### Test di base
| # | Test | Cosa verifica |
|---|------|-------------|
| 4.1 | Sequential execution | Steps in ordine |
| 4.2 | Conditional skip | Step non eseguito se condizione false |
| 4.3 | Conditional execute | Step eseguito se condizione true |
| 4.4 | Parallel execution | Tutti gli step eseguiti |
| 4.5 | Loop max iterations | Stop dopo N |
| 4.6 | Router seleziona route | Step giusto eseguito |

### Test subdoli
| # | Test | Cosa verifica |
|---|------|-------------|
| 4.7 | DAG con ciclo (A→B, B→A) | Rilevato, workflow fallisce o loop rilevato |
| 4.8 | Step throwa eccezione | Workflow non si blocca, step = Failed |
| 4.9 | Parallel con N=100 step | Tutti completati, timeout non scatta |
| 4.10 | Loop infinito (no max, no end condition) | Bloccato da max_iterations global |
| 4.11 | Router route inesistente | Step saltato con messaggio warning |
| 4.12 | CEL condition con input vuoto | Non throwa |
| 4.13 | CEL condition sintassi errata | Non throwa, return false |
| 4.14 | Workflow annidato 3 livelli | Tutti gli step eseguiti in ordine |
| 4.15 | HITL step pausa e resume | Pausa, attende, riprende |

---

## 5. AGXIOME.Core — Teams

### Test di base
| # | Test | Cosa verifica |
|---|------|-------------|
| 5.1 | Coordinate mode | Task distribuiti, synth finale |
| 5.2 | Route mode | Single specialist risponde |
| 5.3 | Broadcast mode | Tutti i membri chiamati |
| 5.4 | Tasks mode | Loop fino a max_iterations |
| 5.5 | AddAgent con ruolo | Agente ha role settato |

### Test subdoli
| # | Test | Cosa verifica |
|---|------|-------------|
| 5.6 | Team senza membri | Esegue senza errori (leader only) |
| 5.7 | Member fallisce, partial failure true | Risultato parziale, non throw |
| 5.8 | Member fallisce, partial failure false | Throw |
| 5.9 | Nested team (team dentro team) | Sub-team esegue |
| 5.10 | Model inheritance | Member senza model usa team.model |
| 5.11 | Timeout globale scatta | TeamResponse con status error |
| 5.12 | Callable members factory | Factory chiamata a ogni run |

---

## 6. AGXIOME.LLMWIKY — OpenAI Provider

### Test di base
| # | Test | Cosa verifica |
|---|------|-------------|
| 6.1 | OpenAI response parse | Content estratto correttamente |
| 6.2 | OpenAI tool call parse | ToolName, ToolCallId, ToolArguments estratti |
| 6.3 | OpenAI streaming | Chunk flusso |
| 6.4 | OpenAI embedding | Dimensioni corrette |

### Test subdoli (con mock HTTP)
| # | Test | Cosa verifica |
|---|------|-------------|
| 6.5 | HTTP 429 rate limit → retry + backoff | Retry dopo delay |
| 6.6 | HTTP 500 server error → fallback provider | Secondo provider chiamato |
| 6.7 | HTTP 401 auth error → NO retry | Fallisce subito |
| 6.8 | Response JSON malformato | JsonException gestito |
| 6.9 | Stream interrotto a metà | Chunk parziali, non throwa |
| 6.10 | Response con 0 tool call | Nessun tool, solo text |
| 6.11 | Response con 5+ tool call in parallelo | Tutti estratti |
| 6.12 | embedding di stringa vuota | Vettore 0-dim o eccezione gestita |
| 6.13 | embedding batch da 100 testi | Tutti processati |

---

## 7. AGXIOME.LLMWIKY — Anthropic Provider

### Test di base
| # | Test | Cosa verifica |
|---|------|-------------|
| 7.1 | Anthropic response parse | Content dal content_block type=text |
| 7.2 | Anthropic tool_use parse | ToolName da content_block type=tool_use |
| 7.3 | Anthropic streaming | SSE event content_block_delta |

### Test subdoli
| # | Test | Cosa verifica |
|---|------|-------------|
| 7.4 | Anthropic system prompt nel campo system | Non nel messages array |
| 7.5 | Anthropic tool format | `input_schema`, non `parameters` |
| 7.6 | Streaming event ordering | content_block_start → delta → message_stop |
| 7.7 | Embedding → NotSupportedException | Throw gestito |

---

## 8. AGXIOME.LLMWIKY — Ollama Provider

### Test di base
| # | Test | Cosa verifica |
|---|------|-------------|
| 8.1 | Ollama response parse | Content da message.content |
| 8.2 | Ollama streaming | NDJSON parsing |
| 8.3 | Ollama embedding | Vettore da /api/embeddings |

### Test subdoli
| # | Test | Cosa verifica |
|---|------|-------------|
| 8.4 | Ollama done:false senza content | Skip, wait next line |
| 8.5 | Ollama server non raggiungibile | HttpRequestException gestito |
| 8.6 | provider fallback: Ollama → OpenAI | Fallback funziona |

---

## 9. AGXIOME.LLMWIKY — Provider Factory & Fallback

| # | Test | Cosa verifica |
|---|------|-------------|
| 9.1 | Factory create OpenAI | Provider tipo OpenAI |
| 9.2 | Factory create Ollama | Provider tipo Ollama |
| 9.3 | Factory unknown provider | ArgumentException |
| 9.4 | Fallback: primario fallisce → secondario | Secondo provider chiamato |
| 9.5 | Fallback: entrambi falliscono | AggregateException |
| 9.6 | CostTracker OpenAI > 0 | Costo calcolato |
| 9.7 | CostTracker Ollama = 0 | Gratuito |
| 9.8 | CostTracker provider sconosciuto = 0 | 0 cost |

---

## 10. AGXIOME.Storage — PostgresStore & Outbox

| # | Test | Cosa verifica |
|---|------|-------------|
| 10.1 | SaveAsync + GetByIdAsync (con TestContainer) | Entity match |
| 10.2 | UpdateAsync | Valore aggiornato |
| 10.3 | DeleteAsync | Non più presente |
| 10.4 | UoW Commit | Dati persistiti |
| 10.5 | UoW Rollback | Dati non persistiti |
| 10.6 | Outbox write + poller processa | Evento processato |
| 10.7 | Redis cache hit | Valore tornato |
| 10.8 | Redis cache miss | Null tornato |
| 10.9 | Redis TTL scaduto | Valore non più presente |
| 10.10 | Connessione DB stringa vuota | ArgumentException |
| 10.11 | SQL injection attempt | Dapper parametrizza, non esegue |
| 10.12 | INSERT con caratteri speciali (Unicode, emoji) | Salvato e letto correttamente |
| 10.13 | Invalidation pattern | Chiavi cancellate |

---

## 11. AGXIOME.Storage — RAG / KnowledgeBase

| # | Test | Cosa verifica |
|---|------|-------------|
| 11.1 | Ingest + Search | Chunk retrieved |
| 11.2 | Documento vuoto | Nessun chunk, nessun errore |
| 11.3 | Chunker: fixed size testo piccolo (< chunk) | 1 chunk |
| 11.4 | Chunker: fixed size testo grande (> chunk) | N chunk |
| 11.5 | Chunker: markdown headings | Split corretto per ## |
| 11.6 | Chunker: sentence split | Frase intera in un chunk |
| 11.7 | Search con MinScore=0.0 | Tutti i chunk tornati |
| 11.8 | Search con MinScore=1.0 | Nessun chunk tornato |
| 11.9 | Search con metadata filter | Solo chunk con metadati match |
| 11.10 | Delete documento | Chunk non più cercabili |
| 11.11 | Ingest batch 100 documenti | Tutti processati |

---

## 12. AGXIOME.XLink — Transport

| # | Test | Cosa verifica |
|---|------|-------------|
| 12.1 | Envelope serialization roundtrip | Payload invariato |
| 12.2 | HMAC compute+verify match | Verification passa |
| 12.3 | HMAC compute+verify fail (data altered) | Verification fallisce |
| 12.4 | Envelope con payload vuoto | Non crasha |
| 12.5 | Envelope con payload grande (10MB) | Non crasha |
| 12.6 | Compression Gzip attiva | Payload compresso < decompresso |
| 12.7 | Subscribe + Send | Handler riceve messaggio |
| 12.8 | Subscribe multiplo stesso topic | Tutti ricevono |
| 12.9 | Send senza subscriber | Nessun errore |

---

## 13. AGXIOME.Security — Crypto & Audit

| # | Test | Cosa verifica |
|---|------|-------------|
| 13.1 | ECDsa key pair generation | Chiavi non null, formati validi |
| 13.2 | Sign + Verify (stessa chiave) | Verifica OK |
| 13.3 | Sign + Verify (chiave diversa) | Verifica FAIL |
| 13.4 | HMAC compute + verify match | Uguali |
| 13.5 | HMAC compute + tampered data | Diversi |
| 13.6 | Audit log write + query | Evento ritrovato per tipo |
| 13.7 | Audit log write + query con filtro data | Eventi nel range |
| 13.8 | Audit log 10K eventi write | Performance non degrada |
| 13.9 | Key pair con seed fisso | Riproducibile |

---

## 14. AGXIOME.Core — HITL

| # | Test | Cosa verifica |
|---|------|-------------|
| 14.1 | Create requirement pending | Status = Pending |
| 14.2 | Resolve approved | Status = Approved |
| 14.3 | Resolve rejected | Status = Rejected |
| 14.4 | GetPendingForRun | Solo requirement per quel run |
| 14.5 | Resolve inesistente | No-op, non throw |
| 14.6 | Double resolve (già approved) | Seconda resolve non cambia stato |
| 14.7 | Team member tool HITL | MemberAgentName popolato |

---

## 15. AGXIOME.Core — Session State

| # | Test | Cosa verifica |
|---|------|-------------|
| 15.1 | Set + Get valore semplice | Match |
| 15.2 | Get chiave inesistente | Default(T) |
| 15.3 | ToDictionary non modificabile dall'esterno | Copia indipendente |
| 15.4 | Set null | Get<T> per reference type = null |
| 15.5 | Set + Get type diverso | InvalidCastException |
| 15.6 | ContainsKey | true/false corretto |

---

## 16. AGXIOME.Core — Learning

| # | Test | Cosa verifica |
|---|------|-------------|
| 16.1 | ProcessAsync Always mode | Save chiamato automaticamente |
| 16.2 | ProcessAsync Agentic mode | Save NON chiamato |
| 16.3 | GetUserMemories vuoto | Lista vuota |
| 16.4 | Save + retrieve | Memory tornato |

---

## 17. AGXIOME.Core — Schedule Manager

| # | Test | Cosa verifica |
|---|------|-------------|
| 17.1 | Create + List | Schedule presente |
| 17.2 | Create con cron invalida | ArgumentException |
| 17.3 | Delete schedule | Non più in lista |
| 17.4 | Pause/Resume | Status cambiato |
| 17.5 | Trigger manuale | Run history registrato |

---

## 18. Property-based test (FsCheck)

| # | Test | Cosa verifica |
|---|------|-------------|
| 18.1 | XLink envelope roundtrip | Qualsiasi byte[] payload serializzato/deserializzato invariato |
| 18.2 | HMAC proprietà: HMAC(a) == HMAC(a), HMAC(a) != HMAC(b) | Deterministico |
| 18.3 | SessionState Set/Get | set(_, v).get(_) == v |
| 18.4 | Chunker property: testo originale ricostruibile dai chunk | Concatenazione chunk =~ originale |

---

## 19. Chaos test (Testcontainers)

| # | Test | Cosa verifica |
|---|------|-------------|
| 19.1 | PostgreSQL crash durante write | Retry + circuit breaker |
| 19.2 | Redis crash | Cache miss → fallback a DB |
| 19.3 | LLM endpoint timeout | Retry 3x, fallback provider |
| 19.4 | Network partition tra agent | Buffer messaggi, reconnect |

---

## Coverage Target

| Modulo | Coverage |
|--------|----------|
| AGXIOME.Shared | ≥ 95% |
| AGXIOME.Core (agent) | ≥ 90% |
| AGXIOME.Core (tools) | ≥ 95% |
| AGXIOME.Core (workflow) | ≥ 85% |
| AGXIOME.Core (teams) | ≥ 80% |
| AGXIOME.Core (session) | ≥ 90% |
| AGXIOME.Core (guardrails) | ≥ 95% |
| AGXIOME.Core (HITL) | ≥ 90% |
| AGXIOME.Core (learning) | ≥ 80% |
| AGXIOME.Core (hooks) | ≥ 85% |
| AGXIOME.LLMWIKY (engine) | ≥ 90% |
| AGXIOME.LLMWIKY (providers) | ≥ 85% |
| AGXIOME.Storage (CQRS) | ≥ 85% |
| AGXIOME.Storage (RAG) | ≥ 80% |
| AGXIOME.XLink | ≥ 90% |
| AGXIOME.Security | ≥ 95% |
| AGXIOME.Telemetry | ≥ 80% |

---

## Esecuzione

```
1. Unit test (19 sezioni) — mock tutto, test isolati
2. Integration test (Testcontainers) — PostgreSQL, Redis reali
3. Property-based test (FsCheck) — roundtrip, invarianti
4. Chaos test — fault injection, recovery
```

**Target totale: 200+ test.**
