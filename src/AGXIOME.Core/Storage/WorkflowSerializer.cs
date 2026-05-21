using System.Text.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Storage;

// ─────────────────────────────────────────────────────────────────────────────
// SS-05 — Workflow Serialization
//
// Provides deterministic serialize/deserialize of a workflow DAG (directed
// acyclic graph of steps and transitions) to/from a JSON blob.
//
// Design:
//   • WorkflowDefinition — pure data record; carries steps (nodes) and
//     transitions (edges) without any behavior/handler closures.
//     Handler closures are never serialized (they are not wire-safe).
//   • WorkflowStepDescriptor — serializable node descriptor; StepType enum
//     ensures enum-first policy, no magic strings.
//   • WorkflowTransitionDescriptor — serializable edge with optional
//     condition label (condition *logic* is resolved at runtime by name).
//   • WorkflowSerializer — stateless utility:
//       Serialize(def) → string (JSON)
//       Deserialize(json) → WorkflowDefinition
//     Persistence (SaveAsync/LoadAsync) moved to AGXIOME.Storage.Workflow.WorkflowStorage.
//
// Security: never execute deserialized handler code. The condition string is
// a lookup key only — real logic lives in a server-side registry.
// ─────────────────────────────────────────────────────────────────────────────

// ── DAG records ──────────────────────────────────────────────────────────────

/// <summary>Serializable descriptor for a single workflow step (node).</summary>
public record WorkflowStepDescriptor
{
    /// <summary>Unique name within the workflow (used as DAG node key).</summary>
    public string Name        { get; init; } = string.Empty;

    /// <summary>Step type — enum-first, no magic strings.</summary>
    public StepType Type      { get; init; }

    /// <summary>
    /// For Agent steps: the agent config ID to resolve at runtime.
    /// For Function steps: the registered function name.
    /// Null for structural steps (Condition, Parallel, Router, Delay).
    /// </summary>
    public string? HandlerRef { get; init; }

    /// <summary>Step-level timeout override (null = use workflow default).</summary>
    public TimeSpan? Timeout  { get; init; }

    /// <summary>Maximum retry count for this step (null = no retry).</summary>
    public int? MaxRetries    { get; init; }

    public Dictionary<string, string>? ExtraProperties { get; init; }
}

/// <summary>Serializable descriptor for a workflow transition (edge).</summary>
public record WorkflowTransitionDescriptor
{
    /// <summary>Source step name.</summary>
    public string From          { get; init; } = string.Empty;

    /// <summary>Target step name.</summary>
    public string To            { get; init; } = string.Empty;

    /// <summary>
    /// Optional condition label — looked up in a server-side condition registry
    /// at runtime. Null means unconditional transition.
    /// </summary>
    public string? ConditionRef { get; init; }
}

/// <summary>Complete serializable workflow DAG definition (nodes + edges).</summary>
public record WorkflowDefinition
{
    public string   Id            { get; init; } = Guid.CreateVersion7().ToString();
    public string   Name          { get; init; } = string.Empty;
    public string?  Description   { get; init; }

    /// <summary>Name of the entry-point step (first to execute).</summary>
    public string   EntryStep     { get; init; } = string.Empty;

    public IReadOnlyList<WorkflowStepDescriptor>      Steps       { get; init; } = Array.Empty<WorkflowStepDescriptor>();
    public IReadOnlyList<WorkflowTransitionDescriptor> Transitions { get; init; } = Array.Empty<WorkflowTransitionDescriptor>();

    public string  SchemaVersion { get; init; } = "1";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string>? ExtraProperties { get; init; }
}

// ── Storage DTO ──────────────────────────────────────────────────────────────

/// <summary>Flat DTO persisted in any backing store. Wraps a serialized WorkflowDefinition.</summary>
public record WorkflowDefinitionEntry
{
    public string Id            { get; init; } = string.Empty;
    public string Name          { get; init; } = string.Empty;
    public string DefinitionJson { get; init; } = string.Empty;
    public string SchemaVersion { get; init; } = "1";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

// ── Serializer ────────────────────────────────────────────────────────────────

/// <summary>
/// Stateless utility for serializing and deserializing <see cref="WorkflowDefinition"/> DAGs.
/// </summary>
public static class WorkflowSerializer
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented          = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters             = { new JsonStringEnumConverter() },
    };

    /// <summary>Serialize a workflow definition to a stable JSON string.</summary>
    public static string Serialize(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return JsonSerializer.Serialize(definition, JsonOpts);
    }

    /// <summary>Deserialize a workflow definition from a JSON string.</summary>
    public static WorkflowDefinition Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<WorkflowDefinition>(json, JsonOpts)
               ?? throw new InvalidOperationException("Deserialized workflow definition was null.");
    }

    /// <summary>
    /// Load and deserialize a definition from a store entry.
    /// Returns null if no entry exists for the given ID.
    /// </summary>
    public static WorkflowDefinition? FromEntry(WorkflowDefinitionEntry? entry)
    {
        if (entry is null) return null;
        return Deserialize(entry.DefinitionJson);
    }
}
