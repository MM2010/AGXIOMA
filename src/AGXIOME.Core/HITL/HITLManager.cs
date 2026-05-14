using System.Collections.Concurrent;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.HITL;

public sealed class HITLManager
{
    private readonly ConcurrentDictionary<string, HITLRequirement> _pending = new();

    /// <summary>
    /// Optional orchestration bridge (P15): when set, Resolve() notifies the orchestrator
    /// so the paused task resumes without polling or timeout.
    /// Injected at runtime — null is valid (standalone HITL without orchestration).
    /// </summary>
    private readonly IHitlCompletionSource? _orchestrationBridge;

    public HITLManager(IHitlCompletionSource? orchestrationBridge = null)
    {
        _orchestrationBridge = orchestrationBridge;
    }

    public HITLRequirement CreateRequirement(string runId, string agentId, string toolName, object payload,
        HITLRequirementType type, string? memberName = null,
        Guid? orchestratorTaskId = null, Guid? orchestratorPlanId = null)
    {
        var req = new HITLRequirement(Guid.NewGuid().ToString(), runId, agentId, toolName, payload, type,
            HITLStatus.Pending, memberName,
            OrchestratorTaskId: orchestratorTaskId,
            OrchestratorPlanId: orchestratorPlanId);
        _pending[req.Id] = req;
        return req;
    }

    public HITLRequirement? GetRequirement(string id) => _pending.GetValueOrDefault(id);

    public void Resolve(string id, bool approved, string? notes = null)
    {
        if (_pending.TryGetValue(id, out var req) && req.Status == HITLStatus.Pending)
        {
            var newReq = req with { Status = approved ? HITLStatus.Approved : HITLStatus.Rejected };
            _pending.TryUpdate(id, newReq, req);

            // P15: notify orchestration layer if this requirement was triggered by an orchestrated task.
            // Fire-and-forget — orchestration failure must not block HITL resolution.
            if (_orchestrationBridge is not null
                && req.OrchestratorTaskId.HasValue
                && req.OrchestratorPlanId.HasValue)
            {
                _ = _orchestrationBridge.NotifyResolvedAsync(
                    req.OrchestratorTaskId.Value,
                    req.OrchestratorPlanId.Value,
                    id, approved);
            }
        }
    }

    public IReadOnlyList<HITLRequirement> GetPendingForRun(string runId)
        => _pending.Values.Where(r => r.RunId == runId && r.Status == HITLStatus.Pending).ToList();
}
