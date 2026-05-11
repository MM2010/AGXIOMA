# Getting Started — AGXIOME

Crea il tuo primo agente AI in 5 minuti.

---

## Prerequisiti

- .NET 10 SDK
- (Opzionale) API key per provider LLM (OpenAI, Anthropic, Ollama, etc.)

## 1. Crea un progetto

```bash
dotnet new console -n MioAgente
cd MioAgente
dotnet add reference /path/to/AGXIOME.Core
```

## 2. Crea un agente con engine di test

Nessuna API key necessaria. `CreateTestAgent` usa un engine fittizio.

```csharp
using AGXIOME.Core.Agent;
using AGXIOME.Shared.Models;

var agent = AgentFactory.CreateTestAgent("demo");
var result = await agent.RunAsync(new AgentRequest("Ciao!"));

Console.WriteLine(result.Status);  // Completed
Console.WriteLine(result.Content); // "Test response"
```

## 3. Connessione a un LLM reale

### OpenAI

```csharp
using AGXIOME.Core.Agent;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Models;

var agent = AgentFactory.Create("openia-assistente", cfg =>
{
    cfg.SystemPrompt = "Sei un assistente utile e gentile.";
    cfg.ReasoningEngine = new LLMWIKYConfig
    {
        Provider = "OpenAI",
        Model = "gpt-4o-mini",
        ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    };
});

var result = await agent.RunAsync(
    new AgentRequest("Quali sono le ultime scoperte sull'AI?"));
Console.WriteLine(result.Content);
```

### Ollama (locale)

```csharp
cfg.ReasoningEngine = new LLMWIKYConfig
{
    Provider = "Ollama",
    Model = "llama3.2",
    Endpoint = "http://localhost:11434"
};
```

### Anthropic Claude

```csharp
cfg.ReasoningEngine = new LLMWIKYConfig
{
    Provider = "Anthropic",
    Model = "claude-sonnet-4-20250514",
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
};
```

## 4. Aggiungi strumenti (Tools)

```csharp
public class WeatherTool : AgxiomeFunction
{
    public override string Name => "get_weather";
    public override string Description => "Ottieni il meteo per una città";

    [ToolParameter(Required = true, Description = "Nome della città")]
    public string City { get; set; } = "";

    public override Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
        => Task.FromResult(new ToolResult(true, $"Il meteo a {City} è soleggiato."));
}

// L'agente LLM decide quando chiamare get_weather
agent.RegisterFunction<WeatherTool>();
```

## 5. Streaming

```csharp
await foreach (var chunk in agent.RunStreamAsync(new AgentRequest("Racconta una storia")))
{
    Console.Write(chunk.Content);
    if (chunk.IsFinal) Console.WriteLine();
}
```

## 6. Errori e resilience

Tutti gli errori restituiscono messaggi user-friendly, mai stack trace:

```csharp
using AGXIOME.Shared.Errors;

var result = await agent.RunAsync(new AgentRequest("test"));
if (result.Status == AgentStatus.Error)
{
    Console.WriteLine(result.Content);
    // Output: [ERR-0101] Esecuzione dell'agente fallita
    // (nessun file path, nessuna riga di codice)
}
```

## Prossimi passi

| Guida | Contenuto |
|-------|-----------|
| [Core Concepts](CORE_CONCEPTS.md) | Agenti, Tools, Teams, Workflows |
| [LLM Providers](PROVIDERS.md) | Tutti i provider, configurazione, costi |
| [Error Handling](ERROR_HANDLING.md) | Codici errore, resilience, circuit breaker |
| [Logging](LOGGING.md) | Provider logger, per-modulo |
| [Storage & RAG](STORAGE.md) | PostgreSQL, RAG, knowledge base |
| [Testing](TESTING.md) | Strategia e copertura test |
