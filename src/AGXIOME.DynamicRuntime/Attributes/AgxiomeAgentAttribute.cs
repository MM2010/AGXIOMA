namespace AGXIOME.DynamicRuntime.Attributes;

/// <summary>
/// Marca una classe come agente dinamico AGXIOMA e ne specifica l'identità.
/// Permette metadata extraction via Roslyn SyntaxTree senza istanziare la classe.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AgxiomeAgentAttribute : Attribute
{
    /// <summary>Identificatore univoco dell'agente (snake_case o kebab-case).</summary>
    public string Id { get; }

    /// <summary>Descrizione leggibile esposta nel catalogo agenti.</summary>
    public string Description { get; }

    public AgxiomeAgentAttribute(string id, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Id = id;
        Description = description;
    }
}
