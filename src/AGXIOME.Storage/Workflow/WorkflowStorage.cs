using AGXIOME.Core.Storage;
using AGXIOME.Shared.Interfaces;

namespace AGXIOME.Storage.Workflow;

/// <summary>
/// Persistence helpers for <see cref="WorkflowDefinition"/>.
/// Saves/loads serialized workflows via <see cref="ICommandStore"/>.
///
/// This file contains ONLY the storage logic.
/// The serialization format + records live in AGXIOME.Core.Storage.WorkflowSerializer.
/// </summary>
public static class WorkflowStorage
{
    /// <summary>Persist a workflow definition.</summary>
    public static async Task SaveAsync(
        WorkflowDefinition definition,
        ICommandStore store,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(store);

        var now   = DateTimeOffset.UtcNow;
        var entry = new WorkflowDefinitionEntry
        {
            Id             = definition.Id,
            Name           = definition.Name,
            DefinitionJson = WorkflowSerializer.Serialize(definition),
            SchemaVersion  = definition.SchemaVersion,
            CreatedAt      = definition.CreatedAt,
            UpdatedAt      = now,
        };
        await store.SaveAsync(entry, ct);
    }

    /// <summary>Load a workflow definition by ID.</summary>
    public static async Task<WorkflowDefinition?> LoadAsync(
        string workflowId,
        IQueryStore<WorkflowDefinitionEntry> store,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentNullException.ThrowIfNull(store);

        var entry = await store.GetByIdAsync(workflowId, ct);
        return WorkflowSerializer.FromEntry(entry);
    }
}
