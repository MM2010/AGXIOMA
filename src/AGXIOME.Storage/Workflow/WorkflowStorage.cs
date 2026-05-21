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
        var json = WorkflowSerializer.Serialize(definition);
        await store.SaveAsync(new WorkflowEntity(definition.Id, json), ct);
    }

    /// <summary>Load a workflow definition by ID.</summary>
    public static async Task<WorkflowDefinition?> LoadAsync(
        string workflowId,
        IQueryStore<object> store,
        CancellationToken ct = default)
    {
        var raw = await store.GetByIdAsync(workflowId, ct);
        if (raw is WorkflowEntity entity)
            return WorkflowSerializer.Deserialize(entity.Json);
        return null;
    }

    /// <summary>Internal entity for storage round-trip.</summary>
    private sealed record WorkflowEntity(string Id, string Json);
}
