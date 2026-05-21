namespace AGXIOME.Shared.Models;

/// <summary>Execution context passed to a guardrail check.</summary>
public record GuardrailContext(string AgentId, string? SessionId, string? UserId);

/// <summary>Result returned by a guardrail after evaluating input or output.</summary>
public record GuardrailResult(bool IsValid, string? Reason = null, GuardrailSeverity? OverrideSeverity = null);
