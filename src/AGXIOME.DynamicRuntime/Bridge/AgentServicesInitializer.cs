using Microsoft.Extensions.Hosting;
using AGXIOME.Shared.Interfaces;

namespace AGXIOME.DynamicRuntime.Bridge;

/// <summary>
/// IHostedService che inizializza AgentServices al momento dell'avvio,
/// quando il DI container è già completamente costruito.
/// </summary>
internal sealed class AgentServicesInitializer(ILLMProvider llm) : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        AgentServices.Initialize(llm);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
