# 13 — Tool System

## Scopo

Sistema completo per registrazione, discovery, injection ed esecuzione di tool.
Ogni tool = `IAgxiomeTool` eseguibile con parametri tipizzati, audit e timeout.
Tool possono essere attribuiti a singoli agenti o globali.

## Architettura Tool

```
                    ┌──────────────────────┐
                    │   Tool Registry      │
                    │   (globale + per-agent)│
                    └──────────┬───────────┘
                               │
          ┌────────────────────┼────────────────────┐
          │                    │                    │
          ▼                    ▼                    ▼
┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐
│  Built-in Tools  │ │  Custom Tools    │ │  Remote Tools    │
│  (AGXIOME.Tools) │ │  (user-defined)  │ │  (gRPC/HTTP)     │
└──────────────────┘ └──────────────────┘ └──────────────────┘
```

## Contratti

```csharp
// ── Tool marker ──
public interface IAgxiomeTool
{
    string Name { get; }
    string Description { get; }
    string? Version { get; }              // SemVer opzionale
    IReadOnlyList<string> Categories { get; }  // "web", "data", "code", "system"
}

// ── Tool eseguibile ──
public interface IAgxiomeExecutableTool : IAgxiomeTool
{
    Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct);
    JsonObject ToJsonSchema();  // Auto-generato per LLM function calling
}

// ── Tool contesto ──
public record ToolContext(
    string AgentId,
    string RunId,
    string? TenantId = null,
    Dictionary<string, object>? Parameters = null,
    TimeSpan? Timeout = null
);

// ── Tool result ──
public record ToolResult(
    bool Success,
    object? Data,
    string? ErrorMessage = null,
    TimeSpan? ExecutionTime = null,
    ToolMetadata? Metadata = null
);

public record ToolMetadata(
    string ToolName,
    string? Version,
    long BytesProcessed = 0,
    int ItemsProcessed = 0
);

// ── Tool parametro (attributo per reflection) ──
[AttributeUsage(AttributeTargets.Property)]
public class ToolParameterAttribute : Attribute
{
    public bool Required { get; set; } = false;
    public string Description { get; set; } = "";
    public object? DefaultValue { get; set; }
    public string? Type { get; set; }      // "string", "number", "integer", "boolean", "array"
    public string? Pattern { get; set; }   // Regex validation
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
}
```

## Tool Registry

```csharp
public interface IToolRegistry
{
    // Globale
    void Register<T>() where T : IAgxiomeTool;
    void Register(IAgxiomeTool instance);
    void Unregister(string toolName);
    
    // Per-agente
    void RegisterForAgent(string agentId, string toolName);
    void UnregisterFromAgent(string agentId, string toolName);

    // Discovery
    IAgxiomeTool? GetTool(string toolName);
    IReadOnlyList<IAgxiomeTool> GetAgentTools(string agentId);
    IReadOnlyList<IAgxiomeTool> GetAllTools();
    IReadOnlyList<IAgxiomeTool> FindByCategory(string category);

    // Schema (per LLM function calling)
    IReadOnlyList<JsonObject> GetToolSchemas(string agentId);
}
```

## Tool execution pipeline

```
ExecuteAsync:
  1. Validate: parametri richiesti presenti, tipi corretti
  2. Authorize: agente ha permesso per questo tool?
  3. Audit: log execution start
  4. Timeout: start timer (default 30s)
  5. Execute: tool logic
  6. On success: format result, audit complete
  7. On failure: retry? (se transient), log error
  8. Return ToolResult
```

## Built-in tools (AGXIOME.Tools)

| Tool | Categoria | Descrizione |
|------|-----------|-------------|
| `WebSearchTool` | web | Ricerca web con Google/Bing |
| `WebScrapeTool` | web | Estrai contenuto da URL |
| `HttpRequestTool` | web | Richieste HTTP generiche |
| `DatabaseQueryTool` | data | Esegue query SQL su DB registrato |
| `FileSystemTool` | code | Lettura/scrittura file (sandboxed) |
| `CodeExecutionTool` | code | Esegue codice in sandbox (Python/JS/C#) |
| `GitTool` | code | Operazioni Git (clone, log, diff) |
| `LintTool` | code | Analisi statica codice |
| `KnowledgeSearchTool` | data | Ricerca su Knowledge Base (RAG) |
| `EmailTool` | communication | Invio email template |
| `NotificationTool` | communication | Invio notifiche SignalR/push |
| `SerialPortTool` | hardware | Comunicazione seriale (IoT) |
| `CalendarTool` | productivity | Calendario, scheduling |
| `MathTool` | utility | Calcoli, statistica, formule |
| `TransformTool` | data | Trasformazione dati (CSV/JSON/XML) |
| `StorageTool` | data | CRUD su storage registrato |

## Tool base class (semplificata)

```csharp
public abstract class AgxiomeToolBase : IAgxiomeExecutableTool
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual string? Version => "1.0.0";
    public virtual IReadOnlyList<string> Categories => Array.Empty<string>();

    public abstract Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct);

    public JsonObject ToJsonSchema()
    {
        // Reflection-based: legge proprietà con [ToolParameter] attribute
        var schema = new JsonObject
        {
            ["name"] = Name,
            ["description"] = Description,
            ["parameters"] = GenerateParameterSchema()
        };
        return schema;
    }

    private JsonObject GenerateParameterSchema()
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var prop in GetType().GetProperties())
        {
            var attr = prop.GetCustomAttribute<ToolParameterAttribute>();
            if (attr == null) continue;

            var paramSchema = new JsonObject
            {
                ["type"] = attr.Type ?? MapType(prop.PropertyType),
                ["description"] = attr.Description
            };
            if (attr.DefaultValue != null)
                paramSchema["default"] = JsonValue.Create(attr.DefaultValue);
            if (attr.Pattern != null)
                paramSchema["pattern"] = attr.Pattern;

            properties[ToSnakeCase(prop.Name)] = paramSchema;

            if (attr.Required)
                required.Add(ToSnakeCase(prop.Name));
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required
        };
    }
}
```

## Tool example

```csharp
public sealed class WebSearchTool : AgxiomeToolBase
{
    public override string Name => "web_search";
    public override string Description => "Search the web for up-to-date information.";
    public override IReadOnlyList<string> Categories => new[] { "web", "research" };

    [ToolParameter(Required = true, Description = "The search query string")]
    public string Query { get; set; } = "";

    [ToolParameter(Description = "Number of results to return", DefaultValue = 5, MinValue = 1, MaxValue = 20)]
    public int MaxResults { get; set; } = 5;

    public override async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        try
        {
            // Logic...
            var results = await SearchEngine.SearchAsync(Query, MaxResults, ct);
            return new ToolResult(true, results, Metadata: new ToolMetadata(Name, Version));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}
```

## Tool execution policy

```csharp
public class ToolExecutionPolicy
{
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public Dictionary<string, TimeSpan> PerToolTimeout { get; set; } = new();
    public int MaxRetries { get; set; } = 2;
    public bool RetryOnTimeout { get; set; } = false;
    public bool RetryOnHttpError { get; set; } = true;
    public List<string> RetryableExceptions { get; set; } = new()
    {
        "System.TimeoutException",
        "System.Net.Http.HttpRequestException"
    };
    public bool EnableAudit { get; set; } = true;
    public bool EnableResultCaching { get; set; } = false;
    public TimeSpan? CacheDuration { get; set; } // Solo se EnableResultCaching = true
}
```

## Tool discovery

```csharp
// ── Reflection scanning ──
var tools = Assembly.GetExecutingAssembly()
    .GetTypes()
    .Where(t => t.IsAssignableTo(typeof(IAgxiomeExecutableTool))
                && !t.IsAbstract)
    .Select(t => Activator.CreateInstance(t) as IAgxiomeTool)
    .ToList();

// ── Remote tool (gRPC/HTTP proxy) ──
public class RemoteToolProxy : IAgxiomeExecutableTool
{
    public string Name { get; }
    public string Description { get; }
    public string RemoteEndpoint { get; }
    public string? AuthToken { get; }

    // Inoltra contesto a servizio remoto
    public async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var response = await _httpClient.PostAsJsonAsync(
            RemoteEndpoint, context, ct);
        return await response.Content.ReadFromJsonAsync<ToolResult>(ct);
    }
}
```

## Security tool

- Ogni tool ha `Categories` per autorizzazione granulare
- `ToolExecutionPolicy` per timeout e retry
- Audit obbligatorio per tool che modificano dati
- Sandboxing per esecuzione codice
- Rate-limit per tool (es. web_search: 10/min)

## MCP (Model Context Protocol) Integration

```csharp
// ── MCP Tool adapter ──
// Connetti tool MCP esterni come IAgxiomeTool
public class MCPToolAdapter : IAgxiomeExecutableTool
{
    public string Name { get; }
    public string Description { get; }
    public IReadOnlyList<string> Categories => new[] { "mcp" };

    private readonly MCPClientOptions _options;  // stdio, SSE, streamable HTTP
    private readonly string _toolName;
    private readonly JsonObject _inputSchema;

    public MCPToolAdapter(MCPClientOptions options, string toolName, JsonObject inputSchema)
    {
        _options = options;
        _toolName = toolName;
        _inputSchema = inputSchema;
        Name = $"{options.ServerName}_{toolName}";  // Prefix per evitare collisioni
        Description = inputSchema["description"]?.GetValue<string>() ?? "";
    }

    public async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        // Connette a MCP server (stdio/SSE/streamable HTTP)
        // Invia tool call
        // Riceve risultato
        // Mappa a ToolResult
    }

    public JsonObject ToJsonSchema()
    {
        // Usa inputSchema direttamente dal MCP server
        return new JsonObject
        {
            ["name"] = Name,
            ["description"] = Description,
            ["parameters"] = _inputSchema
        };
    }
}

// ── Multi-MCP server manager ──
public class MultiMCPTools
{
    private readonly List<MCPClientOptions> _servers;

    public IReadOnlyList<IAgxiomeExecutableTool> DiscoverTools()
    {
        var tools = new List<IAgxiomeExecutableTool>();
        foreach (var server in _servers)
        {
            var serverTools = FetchTools(server);
            tools.AddRange(serverTools);
        }
        return tools;
    }
}

// ── Transport options ──
public record MCPClientOptions(
    string ServerName,
    MCPTransport Transport,
    string? Command = null,           // Per stdio
    string[]? Args = null,            // Per stdio
    Uri? Endpoint = null,             // Per SSE / streamable HTTP
    Dictionary<string, string>? Headers = null  // Dynamic headers
);

public enum MCPTransport { Stdio, SSE, StreamableHttp }
```
