---  
title: AgentRearrange  
category: entity  
tags: [workflow, DAG, flow-parser, einsum]  
source_count: 1  
created_at: 2026-05-14  
updated_at: 2026-05-14  
---  

# AgentRearrange  

## Summary  
`AgentRearrange` is an einsum-style flow parser that converts a concise string notation into an executable DAG (directed acyclic graph) workflow. It interprets sequences and parallel branches defined by the `->` and `,` operators, builds the corresponding execution stages, and constructs an `AgxiomeDagWorkflow` that can be run by the AGXIOME agent framework. This provides a declarative way to orchestrate multi-agent pipelines with minimal code.  

## Key Details  
- **Flow syntax**:  
  - `A -> B, C` — A runs first, then B and C in parallel.  
  - `A -> B -> C` — sequential execution A → B → C.  
  - `A, B -> C` — A and B in parallel, then C.  
- Constructor requires a non-null flow string and a list of `IAgxiomeAgent` agents, keyed by `Id`.  
- `BuildDagFromFlow()` parses the flow using `FlowParser`, creates an `AgxiomeDagWorkflow`, adds nodes (agents or function nodes for missing agents), edges, parallel branches, and barrier nodes.  
- `FlowParser.Parse()` splits the flow string by `->` to get stages, then by `,` within each stage to get parallel agent names. Returns `List<List<string>>`.  
- Missing agents are handled by adding a function node that returns an error message.  
- Parallel stages are wrapped in a branch node (`parallel_...`) and followed by a barrier node (`barrier_...`) to synchronize before the next stage.  

## Cross-References  
- [[AgxiomeDagWorkflow]] — the DAG structure built by `AgentRearrange`.  
- [[IAgxiomeAgent]] — interface for agents recognized by the framework.  
- [[NodeType]] — used to label nodes as Agent or Function in the DAG.  

## Source Files  
- `AGXIOME.Core/Workflows/AgentRearrange.cs` (ingested 2026-05-14)