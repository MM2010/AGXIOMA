namespace AGXIOME.Shared.Models;

/// <summary>Registration descriptor for a tool, used to resolve and instantiate tool implementations.</summary>
public record ToolRegistration(
    string TypeName,
    Dictionary<string, object>? Config = null
);
