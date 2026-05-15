using AGXIOME.Core.Generator;

namespace AgxiomaAgenticBuilder;

public record GenRequest(string? Name, string Prompt, string? Architecture, string? OutputPath);
