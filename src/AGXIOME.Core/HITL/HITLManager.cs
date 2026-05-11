using System.Collections.Concurrent;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.HITL;

public sealed class HITLManager
{
    private readonly ConcurrentDictionary<string, HITLRequirement> _pending = new();

    public HITLRequirement CreateRequirement(string runId, string agentId, string toolName, object payload, HITLRequirementType type, string? memberName = null)
    {
        var req = new HITLRequirement(Guid.NewGuid().ToString(), runId, agentId, toolName, payload, type, HITLStatus.Pending, memberName);
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
        }
    }

    public IReadOnlyList<HITLRequirement> GetPendingForRun(string runId)
        => _pending.Values.Where(r => r.RunId == runId && r.Status == HITLStatus.Pending).ToList();
}
