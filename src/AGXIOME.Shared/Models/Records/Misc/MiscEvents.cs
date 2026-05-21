namespace AGXIOME.Shared.Models;

/// <summary>A notification event emitted by agent infrastructure components.</summary>
public record NotifyEvent(
    string Type,
    string SourceId,
    string? Payload = null,
    DateTime Timestamp = default
);

/// <summary>Descriptor for an LLM or embedder provider listing its capabilities.</summary>
public record ProviderDescriptor(
    string Name,
    IReadOnlyList<string> SupportedModels,
    bool RequiresApiKey,
    bool SupportsLocalEndpoint,
    bool IsEmbeddingSupported
);
