---
title: GenerateAgentTool
category: entity
tags: [tool, agent, generation, codegen, LLM]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# GenerateAgentTool

## Summary
`GenerateAgentTool` is a sealed tool class within the AGXIOME framework that orchestrates the dynamic creation of a new C# agent via LLM-based code synthesis. It accepts a natural language description and optional example input/output, invokes the `AgentCodeGenerator` to produce the agent artifact, and returns the generated agent's ID, execution time, and storage path. This tool enables runtime agent generation from user prompts.

## Key Details
- **Name**: `"generate_agent"`  
- **Description**: "Generates a new dynamic C# agent from a natural language description using LLM code synthesis."  
- **Categories**: `["dynamic", "codegen", "agent"]`  
- **Required Parameters**: `userId` (string), `description` (string)  
- **Optional Parameters**: `example_input`, `example_output` (strings)  
- **Inherits**: `AgxiomeFunction`  
- **Primary Dependency**: `AgentCodeGenerator` (injected via constructor)  
- **Execution**: Asynchronous (`Task<ToolResult>`), respects cancellation token  
- **Error Handling**: Returns `ToolResult(false, null, errors)` on missing parameters or generation failure  
- **Success Output**: JSON object with `agentId`, `elapsedMs` (long milliseconds), and `savedFilePath`  
- **Parameter Extraction**: Uses private static helper `GetParam` to safely extract string values from the parameter dictionary  

## Cross-References
- [[AgentCodeGenerator]] — core service invoked to perform the actual code synthesis
- [[AgentGenerationRequest]] — input model for agent generation parameters
- [[AgxiomeFunction]] — base class for all tools in the framework
- [[ToolContext]] — execution context supplied to each tool
- [[ToolResult]] — standardized result type returned by tools

## Source Files
- `AGXIOME.Core/Tools/GenerateAgentTool.cs` (ingested 2026-05-14)