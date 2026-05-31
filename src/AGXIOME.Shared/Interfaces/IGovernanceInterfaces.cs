using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

/// <summary>
/// Guardrail contract for validating agent input and output content.
/// Guardrails run synchronously in the agent pipeline: input guardrails before LLM inference,
/// output guardrails after. They can block, warn, or transform content.
/// </summary>
/// <remarks>
/// <para><b>Types</b>: <see cref="GuardrailType.Input"/> checks incoming user prompts.
/// <see cref="GuardrailType.Output"/> checks the LLM response before it reaches the user.</para>
/// <para><b>Severity</b>: <see cref="GuardrailSeverity.Block"/> rejects the content outright.
/// <see cref="GuardrailSeverity.Warn"/> allows it but flags it.
/// <see cref="GuardrailSeverity.Transform"/> modifies the content (e.g., redacts PII).</para>
/// <para><b>Performance</b>: Implementations must be fast (&lt;50ms). Heavy validation should be
/// delegated to an async background evaluator.</para>
/// </remarks>
public interface IGuardrail
{
    /// <summary>Unique name for observability and configuration.</summary>
    string Name { get; }

    /// <summary>Whether this guardrail checks input, output, or both.</summary>
    GuardrailType Type { get; }

    /// <summary>Action to take when the guardrail is triggered.</summary>
    GuardrailSeverity Severity { get; }

    /// <summary>
    /// Validates content against the guardrail rules.
    /// </summary>
    /// <param name="content">The text to validate.</param>
    /// <param name="ctx">Context including agent ID, session ID, and request metadata.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating pass/fail/transform with an optional message.</returns>
    Task<GuardrailResult> ValidateAsync(string content, GuardrailContext ctx, CancellationToken ct = default);
}

/// <summary>
/// Agent lifecycle hook. Fires at specific points in the agent execution pipeline.
/// Supports ordered execution and background (fire-and-forget) mode.
/// </summary>
/// <remarks>
/// <para><b>Hook Points</b>: See <see cref="HookPoint"/> for the available lifecycle stages
/// (BeforeRun, AfterRun, BeforeToolCall, AfterToolCall, OnError, OnStreamChunk).</para>
/// <para><b>Ordering</b>: Hooks execute in ascending <see cref="Order"/> within the same <see cref="HookPoint"/>.</para>
/// <para><b>Background</b>: When <see cref="RunInBackground"/> is true, the hook is dispatched
/// via <c>Task.Run</c> and does not block the agent pipeline. Exceptions are logged, not propagated.</para>
/// </remarks>
public interface IAgentHook
{
    /// <summary>Unique name for observability.</summary>
    string Name { get; }

    /// <summary>When in the agent lifecycle this hook executes.</summary>
    HookPoint Point { get; }

    /// <summary>Execution order (lower = earlier). Default 0.</summary>
    int Order { get; }

    /// <summary>If true, the hook runs on a background thread and does not block the agent.</summary>
    bool RunInBackground { get; }

    /// <summary>Invokes the hook logic with the current pipeline context.</summary>
    Task ExecuteAsync(HookContext context, CancellationToken ct = default);
}

/// <summary>
/// Tool-level hook that fires immediately before and after each tool execution.
/// Unlike <see cref="IAgentHook"/>, these are always synchronous (blocking) because
/// they may need to modify tool inputs or inspect outputs before the agent continues.
/// </summary>
public interface IToolHook
{
    /// <summary>
    /// Called before the tool executes. Can modify the tool context (inputs, parameters).
    /// </summary>
    Task BeforeExecuteAsync(ToolContext ctx, CancellationToken ct = default);

    /// <summary>
    /// Called after the tool executes. Can inspect or modify the result.
    /// </summary>
    Task AfterExecuteAsync(ToolContext ctx, ToolResult result, CancellationToken ct = default);
}

/// <summary>
/// Agent middleware — wraps the entire agent execution pipeline.
/// Follows the ASP.NET Core middleware pattern: each middleware calls <c>next()</c>
/// to invoke the next middleware in the chain, then inspects/modifies the result.
/// </summary>
/// <example>
/// <code>
/// public class TimingMiddleware : IAgentMiddleware
/// {
///     public async Task InvokeAsync(AgentContext context, Func&lt;Task&gt; next)
///     {
///         var sw = Stopwatch.StartNew();
///         await next();
///         Console.WriteLine($"Agent run took {sw.ElapsedMilliseconds}ms");
///     }
/// }
/// </code>
/// </example>
public interface IAgentMiddleware
{
    /// <summary>
    /// Invokes the middleware. Call <paramref name="next"/> to continue the pipeline.
    /// </summary>
    /// <param name="context">The current agent execution context.</param>
    /// <param name="next">The next middleware or the actual agent run.</param>
    Task InvokeAsync(AgentContext context, Func<Task> next);
}

/// <summary>
/// Manages the Human-in-the-Loop (HITL) approval workflow.
/// When an agent tool requires confirmation, an approval request is created.
/// A human (or admin agent) resolves it, and the agent resumes.
/// </summary>
public interface IApprovalManager
{
    /// <summary>
    /// Creates an approval request for a tool that requires human confirmation.
    /// The agent pauses until <see cref="ResolveApprovalAsync"/> is called.
    /// </summary>
    /// <param name="agentId">The agent requesting approval.</param>
    /// <param name="toolName">The tool that requires confirmation.</param>
    /// <param name="payload">The tool parameters being proposed.</param>
    /// <param name="config">Approval configuration (TTL, required approvers).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created approval request with a unique ID.</returns>
    Task<ApprovalRequest> RequestApprovalAsync(string agentId, string toolName, object payload, ApprovalConfig config, CancellationToken ct = default);

    /// <summary>
    /// Resolves (approves or rejects) a pending approval request.
    /// </summary>
    /// <param name="approvalId">The approval request ID.</param>
    /// <param name="approved">True to approve, false to reject.</param>
    /// <param name="notes">Optional notes explaining the decision.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolution result.</returns>
    Task<ApprovalResolution> ResolveApprovalAsync(string approvalId, bool approved, string? notes = null, CancellationToken ct = default);

    /// <summary>
    /// Lists all pending (not yet resolved) approval requests, optionally filtered by agent.
    /// </summary>
    Task<IReadOnlyList<ApprovalRequest>> ListPendingAsync(string? agentId = null, CancellationToken ct = default);
}
