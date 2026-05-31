namespace AGXIOME.Shared.Models;

/// <summary>
/// Marks a tool class as requiring human confirmation before execution.
/// When the agent encounters a tool decorated with this attribute it MUST
/// raise an <c>HITLRequirement</c> and pause until the human resolves it.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class RequiresConfirmationAttribute : Attribute
{
    /// <summary>Human-readable explanation shown in the approval UI.</summary>
    public string? Message { get; set; }

    /// <summary>
    /// When true the runtime collects free-form user input before resuming
    /// (maps to <c>HITLRequirementType.UserInput</c>).
    /// </summary>
    public bool RequiresUserInput { get; set; }

    /// <summary>
    /// When true the tool is NOT called by the agent — execution is fully
    /// delegated to an external operator (maps to
    /// <c>HITLRequirementType.ExternalExecution</c>).
    /// </summary>
    public bool ExternalExecution { get; set; }
}
