namespace AGXIOME.Shared.Models;

/// <summary>Specifies whether a guardrail applies to agent input or output.</summary>
public enum GuardrailType { Input, Output }

/// <summary>Action taken when a guardrail is triggered: log-only, flag for review, or hard block.</summary>
public enum GuardrailSeverity { Log, Flag, Block }

/// <summary>The point at which a guardrail check is triggered during agent execution.</summary>
public enum CheckTrigger { InputNotAllowed, OutputNotAllowed }
