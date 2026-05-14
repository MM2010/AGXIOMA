# Logging — AGXIOME

Logging strutturato con supporto nativo per i principali sistemi .NET.
Per-modulo log level con override via wildcard.

---

## Provider supportati

| Provider | Type | Descrizione |
|----------|------|-------------|
| Console | `"Console"` (default) | Output colorato su console |
| Seq | `"Seq"` | Structured JSON su HTTP |
| NLog | `"NLog"` | Integrazione NLog nativa |
| Graylog | `"Graylog"` | GELF su HTTP |
| Serilog | `"Serilog"` | Serilog sink |

## Configurazione

### Globale

```csharp
var factory = new AgxiomeLoggerFactory();

factory.Configure(new LoggingConfig
{
    DefaultMinimumLevel = LogLevel.Information,
    Provider = "Seq",
    Endpoint = "http://localhost:5341"
});

var agentLogger = factory.CreateLogger("AGXIOME.Core.Agent.main");
agentLogger.Information("Agent started");
```

### Per-modulo con wildcard

```csharp
factory.Configure(new LoggingConfig
{
    DefaultMinimumLevel = LogLevel.Error,       // solo errori di default
    ModuleOverrides = new()
    {
        // Debug per tutti i moduli agent
        new() { Module = "AGXIOME.Core.Agent.*", MinimumLevel = LogLevel.Debug },

        // Warning per LLM
        new() { Module = "AGXIOME.LLMWIKY*", MinimumLevel = LogLevel.Warning },

        // Silenzia Storage
        new() { Module = "AGXIOME.Storage.*", MinimumLevel = LogLevel.Exception },
    }
});
```

## Provider Seq

```csharp
new LoggingConfig
{
    Provider = "Seq",
    Endpoint = "http://localhost:5341",
    ApiKey = "your-seq-api-key"  // opzionale
}
```

Format: JSON strutturato su `POST /api/events/raw`.

## Provider Graylog

```csharp
new LoggingConfig
{
    Provider = "Graylog",
    Endpoint = "http://localhost:12201/gelf"
}
```

Format: GELF JSON su HTTP.

## Provider NLog e Serilog

I provider NLog e Serilog sono stub che delegano alla libreria nativa.
Per usarli, installa il pacchetto NuGet corrispondente e configura.

### NLog

```xml
<!-- nlog.config -->
<nlog>
  <targets>
    <target name="file" type="File" fileName="logs/agxiome.log" />
  </targets>
  <rules>
    <logger name="AGXIOME.*" minlevel="Debug" writeTo="file" />
  </rules>
</nlog>
```

```csharp
new LoggingConfig { Provider = "NLog" };
```

### Serilog

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();

new LoggingConfig { Provider = "Serilog" };
```

## Livelli di log

| Livello | Costante | Quando usare |
|---------|----------|-------------|
| Debug | `LogLevel.Debug` | Dettagli implementativi, non in produzione |
| Information | `LogLevel.Information` | Eventi importanti: agent created, run completed |
| Warning | `LogLevel.Warning` | Situazioni anomale: retry, rate-limit, fallback |
| Error | `LogLevel.Error` | Errori recuperabili: tool failed, DB timeout |
| Exception | `LogLevel.Exception` | Eccezioni non recuperabili, con stack trace |

## Integrazione nell'agente

Il logger AGXIOME è già integrato in `AgxiomeAgent`:
- Creazione agente → `Information`
- Inizio run → `Debug`
- Guardrail bloccato → `Warning`
- Tool eseguito → `Debug`
- Run completato → `Information`
- Run fallito → `Exception` (con stack trace nel log, non nell'output)

```csharp
// Output console esempio:
// [14:30:01.123] [Information ] [AGXIOME.Core.Agent.demo] Agent 'demo' created (engine=TestLLMWIKYEngine)
// [14:30:01.456] [Debug       ] [AGXIOME.Core.Agent.demo] Agent 'demo' run a1b2c3d4 started
// [14:30:02.001] [Information ] [AGXIOME.Core.Agent.demo] Run a1b2c3d4 -> Completed (542ms)
```
