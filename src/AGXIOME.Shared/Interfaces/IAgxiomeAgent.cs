using AGXIOME.Shared.Models;
using AGXIOME.Shared.Config;

namespace AGXIOME.Shared.Interfaces;

/// <summary>
/// Core agent contract defining the full lifecycle of an AGXIOME agent.
/// Agents are the fundamental execution unit: they accept requests, run LLM-backed
/// reasoning loops with tool use, and return structured responses.
/// </summary>
/// <remarks>
/// <para><b>Lifecycle</b>: Create → Configure (tools/hooks/guardrails/schemas) → Run/Stream → Dispose.</para>
/// <para><b>Thread safety</b>: Implementations must support concurrent <see cref="RunAsync(AgentRequest,CancellationToken)"/>
/// and <see cref="RunStreamAsync"/> calls on the same instance. Configuration methods (Register*, Add*, Use*)
/// should be called during setup, not during active runs.</para>
/// <para><b>Disposal</b>: Always call <see cref="IDisposable.Dispose"/>. Implementations must cancel
/// pending runs and release resources (memory, HTTP clients, caches, embeddings).</para>
/// <para><b>Errors</b>: Methods throw <see cref="OperationCanceledException"/> on token signal.
/// Infrastructure failures surface as <c>AgxiomeException</c>. Input validation failures throw
/// <see cref="ArgumentException"/>.</para>
/// </remarks>
/// <example>
/// <code>
/// var agent = new AgxiomeAgent("my-agent", llmProvider);
/// agent.RegisterTool(new WebSearchTool());
/// agent.AddGuardrail(new PiiDetectionGuardrail());
/// agent.EnableLearning();
/// var response = await agent.RunAsync(new AgentRequest("Capital of France?"));
/// Console.WriteLine(response.Content); // "Paris"
/// </code>
/// </example>
public interface IAgxiomeAgent : IDisposable
{
    /// <summary>Unique immutable agent identifier.</summary>
    string Id { get; }

    /// <summary>Current execution status. See <see cref="AgentStatus"/>.</summary>
    AgentStatus Status { get; }

    /// <summary>In-memory agent memory for conversation history and state.</summary>
    IAgentMemory Memory { get; }

    /// <summary>Optional learning machine. When non-null, the agent auto-logs interactions
    /// and builds a knowledge profile across sessions.</summary>
    ILearningMachine? Learning { get; }

    /// <summary>Session identifier. Set when bound to <see cref="ISessionManager"/>.</summary>
    string? SessionId { get; }

    /// <summary>Mutable session state shared across runs in the same session.</summary>
    ISessionState SessionState { get; }

    /// <summary>Runs the agent through the full pipeline and returns a typed response.</summary>
    /// <exception cref="OperationCanceledException">Token signaled.</exception>
    /// <exception cref="ArgumentException">Request is null or prompt is empty.</exception>
    Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken ct = default);

    /// <summary>Runs the agent and deserializes output into <typeparamref name="T"/>.
    /// Requires <see cref="SetOutputSchema{T}"/> to be called first.</summary>
    /// <exception cref="InvalidOperationException">No output schema for <typeparamref name="T"/>.</exception>
    Task<T> RunAsync<T>(AgentRequest request, CancellationToken ct = default);

    /// <summary>Streams LLM output chunks as they are generated. Use <c>await foreach</c>.</summary>
    IAsyncEnumerable<AgentChunk> RunStreamAsync(AgentRequest request, CancellationToken ct = default);

    /// <summary>Streams structured events (tool calls, guardrails, hooks, result).</summary>
    IAsyncEnumerable<RunOutputEvent> RunEventStreamAsync(AgentRequest request, CancellationToken ct = default);

    /// <summary>Resumes a run paused for HITL requirements.</summary>
    Task<AgentResponse> ContinueRunAsync(string runId, IReadOnlyList<HITLRequirement> requirements, CancellationToken ct = default);

    /// <summary>Registers a typed function as an LLM-callable tool.</summary>
    void RegisterFunction<T>() where T : AgxiomeFunction;

    /// <summary>Registers a pre-built tool instance.</summary>
    void RegisterTool(IAgxiomeTool tool);

    /// <summary>Adds middleware wrapping each run invocation.</summary>
    void UseMiddleware(IAgentMiddleware middleware);

    /// <summary>Adds an agent lifecycle hook.</summary>
    void AddHook(IAgentHook hook);

    /// <summary>Adds a tool-level hook (before/after tool execution).</summary>
    void AddToolHook(IToolHook toolHook);

    /// <summary>Adds an input/output guardrail.</summary>
    void AddGuardrail(IGuardrail guardrail);

    /// <summary>Configures a structured JSON output schema.</summary>
    void SetOutputSchema<T>() where T : class;

    /// <summary>Configures a structured input validation schema.</summary>
    void SetInputSchema<T>() where T : class;

    /// <summary>Requires human confirmation for a specific function type.</summary>
    void RequireConfirmationFor<T>() where T : AgxiomeFunction;

    /// <summary>Enables the learning subsystem with optional configuration.</summary>
    void EnableLearning(LearningConfig? config = null);
}
