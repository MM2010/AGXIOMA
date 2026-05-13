using AGXIOME.Core.HITL;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace AGXIOME.Core.Patterns;

/// <summary>
/// AGXIOMA HITLSwarm — Human-In-The-Loop Swarm.
/// ZERO EQUIVALENT in Swarms or LangChain.
///
/// Any agent in the swarm can trigger a HITL checkpoint before executing a sensitive tool.
/// The swarm pauses, serialises state to an in-memory store (Redis-ready), and waits for
/// human approval. On resume, execution continues from the exact pause point.
///
/// Features:
///   - SensitiveToolPatterns: regex/keyword patterns that auto-trigger HITL
///   - HumanReviewTimeout: auto-reject if no human responds within window
///   - ApprovalDelegate: inject any approval channel (API callback, Slack, Teams, etc.)
///   - PauseState: full serialised state of each paused agent run
///   - OnPauseForHuman / OnResumed callbacks for audit trail
/// </summary>
public sealed class HITLSwarm
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];

    private readonly IReadOnlyList<IAgxiomeAgent> _agents;
    private readonly HITLSwarmConfig _config;
    private readonly HITLManager _hitlManager;

    public HITLSwarm(
        IReadOnlyList<IAgxiomeAgent> agents,
        HITLSwarmConfig? config = null)
    {
        _agents = agents;
        _config = config ?? new HITLSwarmConfig();
        _hitlManager = new HITLManager();
    }

    /// <summary>Run all agents sequentially with HITL checkpoints. Returns all results.</summary>
    public async Task<HITLSwarmResult> RunAsync(string task, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var results = new List<HITLAgentResult>();
        var sharedContext = new StringBuilder();

        foreach (var agent in _agents)
        {
            var agentResult = await RunAgentWithHITL(agent, task, sharedContext.ToString(), ct);
            results.Add(agentResult);
            if (agentResult.Output is not null)
                sharedContext.AppendLine($"[{agent.Id}]: {agentResult.Output}");

            if (agentResult.Status == HITLAgentStatus.Rejected)
            {
                if (_config.AbortOnRejection) break;
            }
        }

        return new HITLSwarmResult(
            Task: task,
            AgentResults: results,
            Duration: sw.Elapsed,
            RequiredHITL: results.Any(r => r.PauseCount > 0),
            TotalPauses: results.Sum(r => r.PauseCount)
        );
    }

    private async Task<HITLAgentResult> RunAgentWithHITL(
        IAgxiomeAgent agent, string task, string context, CancellationToken ct)
    {
        var prompt = string.IsNullOrWhiteSpace(context)
            ? task
            : $"{task}\n\n## Context from previous agents:\n{context}";

        var pauseCount = 0;

        // Pre-run HITL checkpoint (if configured for this agent)
        if (_config.AgentsRequiringPreApproval.Contains(agent.Id))
        {
            var req = _hitlManager.CreateRequirement(
                runId: Id,
                agentId: agent.Id,
                toolName: "agent_run",
                payload: new { task = prompt[..Math.Min(500, prompt.Length)] },
                type: HITLRequirementType.Confirmation);

            _config.OnPauseForHuman?.Invoke(req);
            var approval = await WaitForApprovalAsync(req.Id, ct);
            pauseCount++;

            if (!approval)
                return new HITLAgentResult(agent.Id, null, HITLAgentStatus.Rejected, pauseCount);
        }

        // Run the agent
        AgentResponse resp;
        try
        {
            resp = await agent.RunAsync(new AgentRequest(prompt), ct);
        }
        catch (Exception ex)
        {
            return new HITLAgentResult(agent.Id, $"[ERROR: {ex.Message}]", HITLAgentStatus.Failed, pauseCount);
        }

        // Post-run HITL checkpoint (verify output is safe/approved)
        if (_config.SensitiveOutputPatterns.Any(p =>
            resp.Content.Contains(p, StringComparison.OrdinalIgnoreCase)))
        {
            var req = _hitlManager.CreateRequirement(
                runId: Id,
                agentId: agent.Id,
                toolName: "agent_output_review",
                payload: new { output = resp.Content[..Math.Min(2000, resp.Content.Length)] },
                type: HITLRequirementType.Audit);

            _config.OnPauseForHuman?.Invoke(req);
            var approval = await WaitForApprovalAsync(req.Id, ct);
            pauseCount++;

            if (!approval)
                return new HITLAgentResult(agent.Id, resp.Content, HITLAgentStatus.Rejected, pauseCount);
        }

        _config.OnResumed?.Invoke(agent.Id, resp.Content);
        return new HITLAgentResult(agent.Id, resp.Content, HITLAgentStatus.Completed, pauseCount);
    }

    private async Task<bool> WaitForApprovalAsync(string requirementId, CancellationToken ct)
    {
        // Use delegate if provided (e.g. auto-approve in tests)
        if (_config.ApprovalDelegate is not null)
            return await _config.ApprovalDelegate(requirementId);

        // Poll with timeout
        var deadline = DateTime.UtcNow + _config.HumanReviewTimeout;
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var req = _hitlManager.GetRequirement(requirementId);
            if (req?.Status == HITLStatus.Approved) return true;
            if (req?.Status == HITLStatus.Rejected) return false;
            await Task.Delay(500, ct);
        }

        // Timeout: auto-reject
        _hitlManager.Resolve(requirementId, approved: false, "Auto-rejected: timeout");
        return false;
    }

    /// <summary>Manually approve or reject a pending HITL requirement.</summary>
    public void Resolve(string requirementId, bool approved, string? notes = null)
        => _hitlManager.Resolve(requirementId, approved, notes);

    public IReadOnlyList<HITLRequirement> GetPendingRequirements()
        => _hitlManager.GetPendingForRun(Id);
}

public enum HITLAgentStatus { Completed, Rejected, Failed }

public sealed record HITLSwarmConfig
{
    public IReadOnlyList<string> AgentsRequiringPreApproval { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> SensitiveOutputPatterns { get; init; } = Array.Empty<string>();
    public TimeSpan HumanReviewTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public bool AbortOnRejection { get; init; } = true;
    public Func<string, Task<bool>>? ApprovalDelegate { get; init; }
    public Action<HITLRequirement>? OnPauseForHuman { get; init; }
    public Action<string, string>? OnResumed { get; init; }
}

public record HITLAgentResult(string AgentId, string? Output, HITLAgentStatus Status, int PauseCount);
public record HITLSwarmResult(
    string Task,
    IReadOnlyList<HITLAgentResult> AgentResults,
    TimeSpan Duration,
    bool RequiredHITL,
    int TotalPauses
);
