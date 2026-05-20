using Microsoft.AspNetCore.SignalR;
using AgxiomaLegalEmail.Models;

namespace AgxiomaLegalEmail.Hubs;

public sealed class PipelineHub : Hub<IPipelineHubClient>
{
    public async Task SubscribeToEmail(string emailId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, emailId);
    }

    public async Task UnsubscribeFromEmail(string emailId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, emailId);
    }
}
