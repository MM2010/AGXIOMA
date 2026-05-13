namespace AGXIOME.DynamicRuntime.Skills;

/// <summary>Tipo di skill importabile da Skills.sh.</summary>
public enum SkillType
{
    /// <summary>HTTP API endpoint — nessuna compilazione richiesta.</summary>
    HttpApi,
    /// <summary>Funzione C# embedded — compilata via Roslyn.</summary>
    CSharpFunction,
    /// <summary>Prompt LLM puro — nessuna compilazione richiesta.</summary>
    Prompt
}

/// <summary>Definizione di una skill importata da Skills.sh.</summary>
public sealed record SkillDefinition(
    string Id,
    string Name,
    string Description,
    SkillType Type,
    string? Endpoint = null,
    string? SourceCode = null,
    IReadOnlyDictionary<string, string>? Parameters = null
);

/// <summary>Status di una skill nel registry utente.</summary>
public enum SkillStatus { Trusted, Pending, Blocked }

/// <summary>Entry persistita per skill importata da un utente.</summary>
public sealed record UserSkillEntry(
    string SkillId,
    string UserId,
    SkillDefinition Definition,
    SkillStatus Status,
    DateTime ImportedAt
);
