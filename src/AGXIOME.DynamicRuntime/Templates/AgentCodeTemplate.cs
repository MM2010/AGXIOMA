namespace AGXIOME.DynamicRuntime.Templates;

/// <summary>
/// Template C# per un agente dinamico minimale.
/// I placeholder vengono sostituiti da AgentCodeGenerator prima della compilazione.
/// Placeholder:
///   {{AGENT_ID}}          → id univoco dell'agente (es. "sentiment-analyzer")
///   {{AGENT_CLASSNAME}}   → nome C# della classe (PascalCase, es. "SentimentAnalyzer")
///   {{DESCRIPTION}}       → descrizione leggibile
///   {{LOGIC_BODY}}        → corpo del metodo RunAsync (codice generato dall'LLM)
/// </summary>
public static class AgentCodeTemplate
{
    public const string Source = """
        using System.Threading;
        using System.Threading.Tasks;
        using AGXIOME.DynamicRuntime.Attributes;
        using AGXIOME.DynamicRuntime.Interfaces;

        [AgxiomeAgent("{{AGENT_ID}}", "{{DESCRIPTION}}")]
        public sealed class {{AGENT_CLASSNAME}} : ISimpleDynamicAgent
        {
            public string AgentId => "{{AGENT_ID}}";
            public string Description => "{{DESCRIPTION}}";

            public async Task<string> RunAsync(string input, CancellationToken ct = default)
            {
                {{LOGIC_BODY}}
            }
        }
        """;

    /// <summary>Sostituisce tutti i placeholder nel template.</summary>
    public static string Fill(string agentId, string description, string logicBody)
    {
        var className = ToPascalCase(agentId);
        return Source
            .Replace("{{AGENT_ID}}", agentId)
            .Replace("{{AGENT_CLASSNAME}}", className)
            .Replace("{{DESCRIPTION}}", description)
            .Replace("{{LOGIC_BODY}}", logicBody);
    }

    private static string ToPascalCase(string id)
    {
        // "sentiment-analyzer" → "SentimentAnalyzer"
        var parts = id.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(p =>
            p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p[1..]));
    }
}
