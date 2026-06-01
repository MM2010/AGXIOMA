# Team Topologies

> AGXIOME provides four team topologies for multi-agent coordination.

## Topology Comparison

| Topology | Pattern | Best For | Routing |
|----------|---------|----------|---------|
| **Router** | Single best agent per query | Simple classification | LLM decides |
| **Swarm** | All agents run in parallel | Research, brainstorming | Merge results |
| **Pipeline** | Sequential agent chain | Multi-step workflows | Fixed order |
| **Supervisor** | Delegates to sub-agents | Complex task decomposition | LLM supervisor |

## ForestSwarm — Domain-Based Routing

Routes each request to the best "tree" (domain), then to the best agent within that tree using **keyword matching**.

```
User message ──→ ForestSwarm
                   ├── Tree("billing")    [keywords: invoice, payment, refund]
                   ├── Tree("technical")  [keywords: error, api, crash]
                   ├── Tree("sales")      [keywords: plan, pricing, trial]
                   └── Tree("general")    [fallback]
```

```csharp
.AddForestSwarm("support", forest =>
{
    forest.AddTree("billing")
        .AddAgent("billing-specialist", "You handle payments.")
        .WithTool(new GetInvoiceStatusTool())
        .WithKeywords("invoice", "payment", "refund");

    forest.AddTree("technical")
        .AddAgent("tech-specialist", "You handle API errors.")
        .WithTool(new GetSystemStatusTool())
        .WithKeywords("error", "crash", "timeout");
});
```

**Response includes routing metadata**: `SelectedAgentId`, `SelectedTreeName`, `MatchedKeywords`, `Duration`.

## GrokSwarm — Captain + Specialists

A **Captain** decomposes complex tasks into sub-questions. **Specialists** answer their assigned questions in parallel. The Captain **synthesizes** the final report.

```
Task ──→ Captain (decomposes)
           ├── Specialist A (bugs & security)
           ├── Specialist B (performance & logic)
           └── Specialist C (architecture review)
         ← Captain (synthesizes findings → report)
```

```csharp
var captain = AgxiomeBuilder.Agent("captain")
    .WithSystemPrompt("Decompose the task. Synthesize findings into a report.")
    .Build();

var harper = AgxiomeBuilder.Agent("harper")
    .WithSystemPrompt("Find bugs, null-refs, security vulnerabilities.")
    .Build();

var grok = new GrokSwarm(captain, new[] { harper, benjamin, lucas });
var result = await grok.RunAsync("Review this code: " + code);
```

## HeavySwarm — Parallel Research + Synthesis

Generates research questions, dispatches to workers in parallel, and synthesizes a structured report.

```
Topic ──→ Phase 1: Generate 5 questions
              │
         Phase 2: Parallel research (workers answer questions)
              │
         Phase 3: Synthesis → Report (Executive Summary, Findings, Risks, Conclusion)
```

```csharp
.AddHeavySwarm("researcher", heavy =>
{
    heavy.WithWorkers(4)
        .WithParallelResearch()
        .WithQuestionPrompt("Generate 5 focused research questions about: ...")
        .WithSynthesisPrompt(
            "Synthesize into:\n1. Executive Summary\n2. Key Findings\n" +
            "3. Risk Factors\n4. Opportunities\n5. Conclusion");
});
```

## Multi-Agent Team — Role-Based

Fixed role agents collaborating. Each agent has a specific role and system prompt.

```csharp
var team = new AgxiomeTeam("dev-team", TeamTopology.Router, TeamMode.Coordinate);

team.AddAgent("pm", "Product Manager", cfg =>
    cfg.WithSystemPrompt("You are a PM. Prioritize features based on impact."));

team.AddAgent("architect", "Software Architect", cfg =>
    cfg.WithSystemPrompt("You design system architecture. Consider scalability."));

team.AddAgent("developer", "Developer", cfg =>
    cfg.WithSystemPrompt("You implement features. Write clean, tested code."));
```

## Next

- [ForestSwarm Deep Dive](forest-swarm.md)
- [GrokSwarm Deep Dive](grok-swarm.md)
- [HeavySwarm Deep Dive](heavy-swarm.md)
- [Cookbook: Teams](../../cookbook/03-teams/)
