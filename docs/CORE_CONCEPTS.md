 # Core Concepts — AGXIOME

Agenti, Tools, Teams, Workflows, Session, Context, Learning.

---

## Agenti

Un agente AGXIOME è un ciclo LLM che può chiamare strumenti, con guardrail, hook, e memoria.

```csharp
var agent = AgentFactory.Create("research-agent", cfg =>
{
    cfg.SystemPrompt = "Sei un assistente di ricerca.";
    cfg.MaxIterations = 15;
    cfg.ReasoningEngine = new LLMWIKYConfig { Provider = "OpenAI", Model = "gpt-4o" };
});

agent.RegisterFunction<WebSearchTool>();
agent.RegisterFunction<KnowledgeSearchTool>();
```

### Ciclo di vita

```
CreateAgent → Config → RegisterTools → RunAsync(Tool Loop) → Completed | Error
                              │
                         EnableLearning
                              │
                         AddGuardrail
                              │
                         AddHook
```

### Esecuzione interna

```
RunAsync:
  1. Guardrail pre (PII, injection, moderation)
  2. Pre-hooks (audit, validazione)
  3. LLM loop:
     a. Inferenza LLMWIKY
     b. Se tool call → execute → append result → loop
     c. Se response → final
  4. Guardrail post (content validation)
  5. Post-hooks (persist, telemetry)
  6. Background hooks (eval asincrono)
  7. Learning (auto-save memoria)
```

### Token di cancellazione

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var result = await agent.RunAsync(request, cts.Token);
```

---

## Tools

### Definire un tool

```csharp
public sealed class WeatherTool : AgxiomeFunction
{
    public override string Name => "weather";
    public override string Description => "Get weather for a city";

    [ToolParameter(Required = true, Description = "City name")]
    public string City { get; set; } = "";

    [ToolParameter(Description = "Unit: celsius/fahrenheit", DefaultValue = "celsius")]
    public string Unit { get; set; } = "celsius";

    public override Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        // ctx.SessionState → accesso allo stato sessione
        // ctx.Dependencies → dependency injection
        return Task.FromResult(new ToolResult(true, $"{City}: 22°{Unit[0].ToString().ToUpper()}"));
    }
}

agent.RegisterFunction<WeatherTool>();
```

### Tool Context

```csharp
public record ToolContext(
    string AgentId,
    string RunId,
    string? TenantId = null,
    Dictionary<string, object>? Parameters = null,
    TimeSpan? Timeout = null,
    ISessionState? SessionState = null,    // stato sessione
    IDependencyProvider? Dependencies = null // DI
);
```

### Callable Factory

I tool possono essere risolti a runtime per user/sessione:

```csharp
var agent = AgentFactory.Create("adaptive-agent", cfg => { ... });
agent.RegisterTool((ToolContext ctx) =>
{
    var role = ctx.SessionState?.Get<string>("role");
    return role == "admin"
        ? new List<IAgxiomeTool> { new AdminTool(), new SearchTool() }
        : new List<IAgxiomeTool> { new SearchTool() };
});
```

### MCP Tools

```csharp
var mcpTool = new MCPToolAdapter(
    new MCPClientOptions("github", MCPTransport.Stdio, Command: "npx", Args: ["@modelcontextprotocol/github"]),
    "search_repos",
    schema
);
agent.RegisterTool(mcpTool);
```

---

## Teams

I team orchestrano più agenti. 4 modalità:

| Modalità | Comportamento |
|----------|--------------|
| **Coordinate** (default) | Leader decompose, delega, sintetizza |
| **Route** | Routing a un singolo specialista |
| **Broadcast** | Stesso task a tutti, sintesi |
| **Tasks** | Loop task fino a completamento |

```csharp
var leader = AgentFactory.CreateTestAgent("coordinator");
var team = new AgxiomeTeam("research", new TeamConfig { Name = "research" }, leader);

var webAgent = team.AddAgent("web-researcher", "Web search specialist");
webAgent.RegisterFunction<WebSearchTool>();

var dataAgent = team.AddAgent("data-analyst", "Data analysis specialist");
dataAgent.RegisterFunction<DatabaseQueryTool>();

var result = await team.ExecuteAsync("Analizza le ultime tendenze di mercato");
```

### Nested Teams

```csharp
var mainTeam = new AgxiomeTeam("main", ..., leader);
var subTeam = new AgxiomeTeam("nlp", ...); // Team come membro
```

---

## Workflows

I workflow orchestrano step in sequenza, parallelo, condizionale, o loop.

```csharp
var wf = new AgxiomeWorkflow("research-pipeline");

wf.AddStep("extract", ctx => ExtractData(ctx));
wf.AddStep("analyze", ctx => AnalyzeData(ctx));
wf.AddStep("report",  ctx => GenerateReport(ctx));

wf.AddTransition("extract", "analyze");
wf.AddTransition("analyze", "report");

var result = await wf.ExecuteAsync(new WorkflowContext());
```

### Step types

| Type | Descrizione |
|------|-------------|
| `FunctionWorkflowStep` | Esegue funzione C# |
| `AgentWorkflowStep` | Esegue un agente |
| `ConditionalBranch` | If/else su condizione |
| `ParallelStep` | Fan-out N step |
| `LoopStep` | Iterazione fino a condizione |
| `RouterStep` | Selezione route dinamica |

### CEL Conditions

```csharp
wf.AddTransition("gate", "urgent-path",
    ctx => new CELConditionEvaluator().Evaluate("input.Contains(\"urgent\")", ctx, lastOutput));
```

---

## Session & State

Le sessioni persistono lo stato tra run:

```csharp
var agent = AgentFactory.CreateTestAgent("todo-agent");
agent.SessionState.Set("shopping_list", new List<string>());

await agent.RunAsync(new AgentRequest("Aggiungi latte alla lista"));
// SessionState.Get<List<string>>("shopping_list") → ["latte"]
```

---

## Context Engineering

```csharp
public class MyDependencyProvider : IDependencyProvider
{
    public T GetDependency<T>() where T : class => /* ... */;
}

var resolver = new DynamicInstructionResolver();
var instructions = resolver.Resolve(
    "Current user: {user_name}", agent.SessionState);
// → "Current user: Alice"
```

---

## Guardrails

```csharp
agent.AddGuardrail(new PIIDetectionGuardrail());      // blocca SSN, email, CC
agent.AddGuardrail(new PromptInjectionGuardrail());     // blocca injection
agent.AddGuardrail(new OpenAIModerationGuardrail());   // moderation API
```

---

## Hooks

```csharp
agent.AddHook(new DelegateHook("audit", HookPoint.PostRun,
    async (ctx, ct) => await LogAudit(ctx)));
```

---

## HITL (Human-in-the-Loop)

```csharp
[RequiresConfirmation(Message = "Confermi l'invio dell'email?")]
public sealed class SendEmailTool : AgxiomeFunction { /* ... */ }
// L'esecuzione si mette in pausa fino a conferma utente
```

---

## Learning

```csharp
agent.EnableLearning(new LearningConfig
{
    UserMemoryMode = LearningMode.Always,
    UserProfileMode = LearningMode.Agentic
});
```

### Learning stores

| Store | Descrizione |
|-------|-------------|
| User Memory | Osservazioni non strutturate |
| User Profile | Campi strutturati (nome, preferenze) |
| Entity Memory | Fatti su entità esterne |
| Session Context | Riepilogo sessione |
| Learned Knowledge | Insight cross-user |
| Decision Log | Decisioni con ragionamento |

---

## Skills

```csharp
var skillManager = new SkillManager();
var reviewer = new CodeReviewSkill();
skillManager.RegisterSkill(reviewer);
skillManager.ApplySkillToAgent(agent, "code-review");
```
