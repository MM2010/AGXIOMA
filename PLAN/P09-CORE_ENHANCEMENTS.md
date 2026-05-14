# P09 — Core Enhancements

> **Priority**: 🔴 P0 | **Dipende da**: — | **Usato da**: Tutti
> **Target**: Background execution, cancellation manager, remote agent, serialization

---

## Task

| # | Task | Descrizione | Effort |
|---|------|-------------|--------|
| CE-01 | **Background Execution** | `BackgroundRunWorker` persistente con polling, reconnect, resume SSE | 2gg |
| CE-02 | **Cancellation Manager** | In-memory + Redis. Annulla run da thread separato | 1gg |
| CE-03 | **Remote Agent** | `RemoteAgent` che esegue su istanza remota via REST | 2gg |
| CE-04 | **Agent Serialization** | `ToDictionary()`, `FromDictionary()`, `Save()`, `Load()` con DB | 1gg |
| CE-05 | **Model-as-String** | `"openai:gpt-4o"` shorthand senza classi | 1gg |
| CE-06 | **Response Caching** | Cache LLM risposte in sviluppo (TTL configurabile) | 1gg |
| CE-07 | **Token Counting** | Contatore token per provider (tiktoken-like) | 1gg |
| CE-08 | **Model Compatibility** | Mappa modelli → provider + warning obsoleto | 1gg |

**Totale task: 8 | File: ~15 | Test: ~80**
