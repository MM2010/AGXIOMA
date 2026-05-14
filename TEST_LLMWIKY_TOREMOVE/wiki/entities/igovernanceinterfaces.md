---
title: IGovernanceInterfaces
category: entity
tags: [governance, interfaces, hooks, guardrails, approval, middleware]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# IGovernanceInterfaces

## Summary
The `IGovernanceInterfaces` collection defines the core governance contracts for the AGXIOMA agent framework. It includes interfaces for guardrail validation (`IGuardrail`), agent lifecycle hooks (`IAgentHook`), tool execution hooks (`IToolHook`), middleware pipelines (`IAgentMiddleware`), and approval workflows (`IApprovalManager`). Together, they provide a structured, extensible mechanism to enforce policies, observe behaviour, and control execution across agent and tool interactions.

## Key Details
- `IGuardrail` – Validates content against a defined rule (e.g., safety, compliance). Exposes `Name`, `Type` ([[GuardrailType]]), `Severity` ([[GuardrailSeverity]]), and `ValidateAsync(...)` returning [[GuardrailResult]].
- `IAgentHook` – Hooks into agent lifecycle points ([[HookPoint]]). Supports ordered execution (`Order`), background execution (`RunInBackground`), and async processing via `ExecuteAsync(...)`.
- `IToolHook` – Before/after hooks for tool execution. Provides `BeforeExecuteAsync` and `AfterExecuteAsync` with [[ToolContext]] and [[ToolResult]].
- `IAgentMiddleware` – Middleware pattern for agent pipelines. `InvokeAsync(AgentContext, Func<Task> next)` allows interception and chaining.
- `IApprovalManager` – Manages approval requests for tool invocations. Includes `RequestApprovalAsync`, `ResolveApprovalAsync`, and `ListPendingAsync`.
- All interfaces use `CancellationToken` to support cancellation.
- These interfaces reside in the `AGXIOME.Shared.Interfaces` namespace and are implemented by governance components.

## Cross-References
- [[GuardrailType]] – Enum for guardrail classification
- [[GuardrailSeverity]] – Enum for severity levels
- [[GuardrailResult]] – Result type from guardrail validation
- [[HookPoint]] – Enum of lifecycle hook points
- [[HookContext]] – Context passed to agent hooks
- [[AgentContext]] – Context for middleware and agent execution
- [[ToolContext]] – Context for tool before/after hooks
- [[ToolResult]] – Result object from tool execution
- [[ApprovalRequest]] – Model for approval requests
- [[ApprovalConfig]] – Configuration for approval rules

## Source Files
- `AGXIOME.Shared/Interfaces/IGovernanceInterfaces.cs` (ingested 2026-05-14)