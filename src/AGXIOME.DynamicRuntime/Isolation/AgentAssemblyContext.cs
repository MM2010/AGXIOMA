using System.Runtime.Loader;

namespace AGXIOME.DynamicRuntime.Isolation;

/// <summary>
/// Contesto di caricamento assembly isolato e collettabile per agenti dinamici.
/// IsCollectible=true permette lo scaricamento via GC dopo Unload().
/// Usare WeakReference[AgentAssemblyContext] per verificare che il GC abbia
/// effettivamente liberato la memoria dopo Unload().
/// </summary>
public sealed class AgentAssemblyContext : AssemblyLoadContext
{
    public string AgentId { get; }

    public AgentAssemblyContext(string agentId)
        : base(name: $"AgentCtx_{agentId}", isCollectible: true)
    {
        AgentId = agentId;
    }

    protected override System.Reflection.Assembly? Load(System.Reflection.AssemblyName assemblyName)
    {
        // Non caricare nessun assembly aggiuntivo — solo quelli passati esplicitamente via LoadFromStream
        return null;
    }
}
