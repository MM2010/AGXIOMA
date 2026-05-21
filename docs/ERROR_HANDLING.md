# Error Handling — AGXIOME

Sistema strutturato di errori con codici categorizzati, messaggi user-friendly, e resilience automatica.

---

## Principio

**Lo stack trace non è mai esposto all'utente.**

```
AgxiomeException
├── UserMessage      → "Servizio AI non disponibile." (mostrato all'utente)
├── TechnicalMessage → "LLM call failed: timeout after 30s" (solo log)
├── ErrorCode        → LLM_ProviderUnavailable (codice macchina)
├── Severity         → Error / Warning / Critical
├── IsRetryable      → true/false
└── StackTrace       → disponibile solo in .StackTrace, non in .ToString()
```

### Output utente

```
[ERR-0101] Esecuzione dell'agente fallita
```

### Output log (interno)

```
[ERR-0101] Agent 'agent-1' run failed: TimeoutException: LLM did not respond...
   at AGXIOME.Core.Agent.AgxiomeAgent.RunAsync(...)
   at AGXIOME.LlmWiky.LLMWIKYEngine.InferAsync(...)
```

## Codici errore

| Codice | Nome | Causa | Retry |
|--------|------|-------|-------|
| ERR-0000 | Unknown | Errore sconosciuto | ❌ |
| ERR-0001 | InternalError | Errore interno framework | ❌ |
| ERR-0006 | OperationCancelled | Operazione annullata | ❌ |
| ERR-0100 | AE_InitFailed | Inizializzazione agente fallita | ❌ |
| ERR-0101 | AE_RunFailed | Esecuzione agente fallita | ❌ |
| ERR-0103 | AE_MaxIterationsReached | Massimo tentativi raggiunto | ❌ |
| ERR-0107 | AE_EngineNotConfigured | Engine LLM non configurato | ❌ |
| ERR-0200 | LLM_ProviderUnavailable | Provider LLM non disponibile | ✅ |
| ERR-0201 | LLM_RateLimited | Rate limit (429) superato | ✅ |
| ERR-0202 | LLM_ServerError | Errore server provider (500/502/503) | ✅ |
| ERR-0203 | LLM_Timeout | Richiesta LLM scaduta | ✅ |
| ERR-0204 | LLM_AuthenticationFailed | Autenticazione fallita (401/403) | ❌ |
| ERR-0205 | LLM_InvalidResponse | Risposta LLM non valida | ✅ |
| ERR-0206 | LLM_ContextWindowExceeded | Contesto troppo lungo | ❌ |
| ERR-0209 | LLM_FallbackAllExhausted | Tutti i provider fallback falliti | ❌ |
| ERR-0300 | TL_NotFound | Tool non trovato | ❌ |
| ERR-0301 | TL_ExecutionFailed | Esecuzione tool fallita | ✅ |
| ERR-0303 | TL_InvalidArguments | Argomenti tool non validi | ❌ |
| ERR-0400 | DB_ConnectionFailed | Connessione DB fallita | ✅ |
| ERR-0401 | DB_QueryFailed | Query DB fallita | ✅ |
| ERR-0403 | DB_DeadlockDetected | Deadlock DB rilevato | ✅ |
| ERR-0408 | DB_CircuitBreakerOpen | Circuito DB aperto | ❌ |
| ERR-0600 | XL_ChecksumMismatch | Checksum X-Link non valido | ✅ |
| ERR-0800 | SE_AuthenticationFailed | Autenticazione fallita | ❌ |
| ERR-0806 | SE_RateLimitExceeded | Rate limit superato | ❌ |
| ERR-0900 | GR_InputBlocked | Input bloccato da guardrail | ❌ |
| ERR-0902 | GR_PIIDetected | PII rilevato | ❌ |
| ERR-0903 | GR_PromptInjectionDetected | Prompt injection rilevato | ❌ |
| ERR-1000 | HT_RequirementTimeout | Richiesta HITL scaduta | ❌ |
| ERR-1205 | NW_BadGateway | Bad gateway (502) | ✅ |
| ERR-1206 | NW_ServiceUnavailable | Servizio non disponibile (503) | ✅ |
| ERR-1207 | NW_GatewayTimeout | Gateway timeout (504) | ✅ |

### Factory methods

```csharp
// Rate limit
throw AgxiomeException.RateLimited("OpenAI", TimeSpan.FromSeconds(30));

// Errore DB
throw AgxiomeException.DatabaseError("INSERT", "unique constraint violation");

// Errore di rete
throw AgxiomeException.NetworkError("api.openai.com", 502);

// Autenticazione
throw AgxiomeException.AuthFailed("Anthropic");
```

## Resilience

### Retry policy

```csharp
var policy = new RetryPolicy
{
    MaxRetries = 3,
    BaseDelay = TimeSpan.FromMilliseconds(200),
    ExponentialBackoff = true,
    JitterFactor = 0.2    // ±20% random
};
```

### Circuit breaker

```csharp
var breaker = new CircuitBreaker(
    failureThreshold: 5,     // dopo 5 fallimenti
    resetTimeout: TimeSpan.FromSeconds(30)  // riprova dopo 30s
);
breaker.RecordFailure();
if (breaker.IsOpen) { /* salta operazione, usa fallback */ }
breaker.RecordSuccess(); // resetta contatore
```

### ResilientExecutor

```csharp
var executor = new ResilientExecutor(policy, breaker, "MyComponent");

var result = await executor.ExecuteAsync("db-query",
    operation: async ct => await QueryDatabase(ct),
    fallback:  async ct => await QueryCache(ct)   // fallback opzionale
);

if (result.Success)
    UseData(result.Data!);
else
    Log.Error("Fallito dopo {Attempts} tentativi: {Error}", result.Attempts, result.ErrorMessage);
```

### Tipi di errore gestiti

| Errore | Classificazione | Comportamento |
|--------|----------------|--------------|
| 429 Too Many Requests | RateLimited | Retry con backoff |
| 500 Internal Server Error | Transient | Retry |
| 502 Bad Gateway | ServerError | Retry |
| 503 Service Unavailable | ServerError | Retry → circuit breaker |
| 504 Gateway Timeout | ServerError | Retry |
| TimeoutException | Transient | Retry |
| SocketException | ConnectionLost | Retry → circuit breaker |
| InvalidDataException | DataCorruption | Retry |
| InvalidOperationException | Unknown | Nessun retry |

### Catturare errori

```csharp
try { await agent.RunAsync(request); }
catch (AgxiomeException ex)
{
    Console.WriteLine($"Errore: {ex.UserMessage}");
    Log.Error(ex.TechnicalMessage);
    
    if (ex.IsRetryable)
        await Task.Delay(1000);
        retry();
}
```
