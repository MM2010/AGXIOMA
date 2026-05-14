# P03 — Tool System

## File: `ToolRegistry.cs`

Namespace: `AGXIOME.Core.Tools`

## Classi

### `ToolRegistry : IToolRegistry`

```csharp
public sealed class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, IAgxiomeTool> _global = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _perAgent = new();

    public void Register<T>() where T : IAgxiomeTool
    {
        var instance = Activator.CreateInstance<T>();
        _global[instance.Name] = instance;
    }

    public void Register(IAgxiomeTool tool) => _global[tool.Name] = tool;
    public void Unregister(string name) => _global.TryRemove(name, out _);

    public void RegisterForAgent(string agentId, string toolName)
    {
        _perAgent.AddOrUpdate(agentId,
            _ => new HashSet<string> { toolName },
            (_, set) => { set.Add(toolName); return set; });
    }

    public IAgxiomeTool? GetTool(string name)
        => _global.GetValueOrDefault(name);

    public IReadOnlyList<IAgxiomeTool> GetAgentTools(string agentId)
    {
        if (!_perAgent.TryGetValue(agentId, out var names))
            return Array.Empty<IAgxiomeTool>();
        return names.Select(n => _global.GetValueOrDefault(n))
                    .Where(t => t != null).Select(t => t!).ToList();
    }
}
```

### `AgxiomeToolBase` (tool factory)

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
        // Reflection su proprietà con [ToolParameter]
        // → { name, description, parameters: { type: "object", properties, required } }
    }
}
```

### ToolContext / ToolResult

```csharp
public record ToolContext(
    string AgentId,
    string RunId,
    string? TenantId = null,
    Dictionary<string, object>? Parameters = null,
    TimeSpan? Timeout = null,
    ISessionState? SessionState = null,
    IDependencyProvider? Dependencies = null
);

public record ToolResult(
    bool Success,
    object? Data,
    string? ErrorMessage = null,
    TimeSpan? ExecutionTime = null,
    IReadOnlyList<Image>? Images = null,
    IReadOnlyList<Video>? Videos = null,
    IReadOnlyList<Audio>? Audios = null
);
```

### `ToolParameterAttribute`

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class ToolParameterAttribute : Attribute
{
    public bool Required { get; set; }
    public string Description { get; set; } = "";
    public object? DefaultValue { get; set; }
    public string? Type { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
}
```

## Callable factory (Agno compat)

```csharp
// tools può essere:
// - List<IAgxiomeTool>
// - Func<ToolContext, List<IAgxiomeTool>>  // Callable factory
// La factory è chiamata a ogni run, cacheable per user_id/session_id

public class CallableToolFactory
{
    private readonly Func<ToolContext, List<IAgxiomeTool>> _factory;
    private readonly MemoryCache _cache;

    public List<IAgxiomeTool> Resolve(ToolContext ctx)
    {
        // Cache key: custom key function > user_id > session_id
        // Se cache_callables=false → sempre risolto
        // clear_callable_cache() per invalidare
    }
}
```

## Built-in tools

| Classe | Name | Categoria |
|--------|------|-----------|
| `WebSearchTool` | `web_search` | web |
| `WebScrapeTool` | `web_scrape` | web |
| `HttpRequestTool` | `http_request` | web |
| `DatabaseQueryTool` | `db_query` | data |
| `FileSystemTool` | `file_system` | code |
| `CodeExecutionTool` | `code_exec` | code |
| `GitTool` | `git` | code |
| `KnowledgeSearchTool` | `knowledge_search` | data |
| `EmailTool` | `email` | communication |

## MCP Tool adapter

Vedi `P18-XLINK.md` per dettagli MCP.

## Test

| Test | Descrizione |
|------|------------|
| Register + GetTool | Restituisce istanza |
| Tool schema generato | JSON valido con parametri |
| Execute tool returns result | Data non null |
| Callable factory resolve per ruolo | Tool diversi per role |
| Tool con HITL pause | Richiede conferma |
