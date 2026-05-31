using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Hooks;

/// <summary>Core implementation for the AGXIOME agentic orchestration framework.</summary>
public sealed class DelegateHook : IAgentHook
{
    private readonly Func<HookContext, CancellationToken, Task> _handler;
    public string Name { get; }
    public HookPoint Point { get; }
    public int Order { get; }
    public bool RunInBackground { get; }

    public DelegateHook(string name, HookPoint point, Func<HookContext, CancellationToken, Task> handler, int order = 0, bool background = false)
    {
        Name = name; Point = point; _handler = handler; Order = order; RunInBackground = background;
    }

    public Task ExecuteAsync(HookContext context, CancellationToken ct = default) => _handler(context, ct);
}
