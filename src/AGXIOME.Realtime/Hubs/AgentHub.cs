using AGXIOME.Shared.Models;
using AGXIOME.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace AGXIOME.Realtime.Hubs;
// Alias for WorkflowStatus to avoid collision
using WfStatus = AGXIOME.Shared.Models.WorkflowStatus;

/// <summary>Agent execution hub: stream chunks, status changes, HITL pauses.</summary>
public interface IAgentClient
{
    Task OnChunk(AgentChunk chunk);
    Task OnComplete(AgentResponse response);
    Task OnError(string runId, string error);
    Task OnStatusChange(string agentId, AgentStatus status);
    Task OnPaused(string runId, IReadOnlyList<HITLRequirement> requirements);
}

public class AgentHub : Hub<IAgentClient>
{
    public async Task StreamAgentRun(string agentId, AgentRequest request)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"agent:{agentId}");
        // Stream execution — called from AgentExecutor
    }

    public async Task CancelAgentRun(string runId)
    {
        // Propagate cancellation token
        await Clients.Caller.OnError(runId, "Cancelled by user");
    }

    public async Task JoinAgentSession(string sessionId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"session:{sessionId}");

    public async Task LeaveAgentSession(string sessionId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session:{sessionId}");
}

public interface INotificationClient
{
    Task OnNotification(NotifyEvent evt);
    Task OnWorkflowUpdate(string workflowId, WfStatus status);
}

public class NotificationHub : Hub<INotificationClient>
{
    public async Task SubscribeToAgent(string agentId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"agent:{agentId}");
}

public interface IPresenceClient
{
    Task OnAgentOnline(string agentId);
    Task OnAgentOffline(string agentId);
}

public class PresenceHub : Hub<IPresenceClient>
{
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "presence");
        await Clients.Others.OnAgentOnline(Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Clients.Others.OnAgentOffline(Context.ConnectionId);
    }
}
