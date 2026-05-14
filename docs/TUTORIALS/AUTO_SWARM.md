# AutoSwarm — Zero-Configuration Swarm Orchestration

AGXIOME's `AutoSwarm` automatically analyzes your goal and builds the optimal swarm topology — no manual configuration needed.

## Basic Usage

```csharp
using AGXIOME.Core;

// Simple auto-swarm with keyword detection
var swarm = AgxiomeBuilder.Swarm()
    .AddAgent("researcher", b => b.UseDeepSeek().WithSystemPrompt("You research thoroughly."))
    .AddAgent("analyst", b => b.UseDeepSeek().WithSystemPrompt("You analyze data."))
    .AddAgent("writer", b => b.UseDeepSeek().WithSystemPrompt("You write clearly."))
    .AutoSwarm("Research the topic, analyze findings, and produce a report")
    .Build();
```

## LLM-Driven AutoSwarm

```csharp
using AGXIOME.Core.Patterns;
using AGXIOME.LLMWIKY;

var engine = new LLMWIKYEngine(new LLMWIKYConfig { Model = "gpt-4o" });
var autoBuilder = new AutoSwarmBuilder(engine, "Build a customer support system with routing, escalation, and FAQ handling");

// Returns the optimal swarm topology
var swarm = await autoBuilder.BuildAsync();
```

## Pattern Selection

AutoSwarm automatically selects the best pattern based on keywords:
- **MajorityVoting**: "vote", "consensus", "best answer"
- **GroupChat**: "debate", "discuss", "conversation"
- **PlannerWorker**: "plan", "execute", "workflow", "task"
- **MixtureOfAgents**: "synthesize", "combine", "aggregate"
- **Hierarchical**: default for 2+ agents
