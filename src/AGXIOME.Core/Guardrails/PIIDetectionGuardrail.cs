using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Text.RegularExpressions;

namespace AGXIOME.Core.Guardrails;

/// <summary>Core implementation for the AGXIOME agentic orchestration framework.</summary>
public sealed class PIIDetectionGuardrail : IGuardrail
{
    public string Name => "pii_detection";
    public GuardrailType Type => GuardrailType.Input;
    public GuardrailSeverity Severity => GuardrailSeverity.Block;

    private static readonly Regex[] Patterns =
    {
        new(@"\b\d{3}-\d{2}-\d{4}\b"),                       // SSN
        new(@"\b(?:\d[ -]*?){13,16}\b"),                     // Credit card
        new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b"), // Email
        new(@"\b\d{3}[-.]?\d{3}[-.]?\d{4}\b"),              // Phone
    };

    public Task<GuardrailResult> ValidateAsync(string content, GuardrailContext ctx, CancellationToken ct = default)
    {
        foreach (var p in Patterns)
            if (p.IsMatch(content))
                return Task.FromResult(new GuardrailResult(false, $"PII detected matching pattern: {p}"));
        return Task.FromResult(new GuardrailResult(true));
    }
}

public sealed class PromptInjectionGuardrail : IGuardrail
{
    public string Name => "prompt_injection";
    public GuardrailType Type => GuardrailType.Input;
    public GuardrailSeverity Severity => GuardrailSeverity.Block;

    private static readonly Regex[] Patterns =
    {
        new(@"ignore\s+(all\s+)?(previous|above)\s+(instructions|prompts)", RegexOptions.IgnoreCase),
        new(@"you\s+are\s+(now|not\s+an?\s+AI)", RegexOptions.IgnoreCase),
    };

    public Task<GuardrailResult> ValidateAsync(string content, GuardrailContext ctx, CancellationToken ct = default)
    {
        foreach (var p in Patterns)
            if (p.IsMatch(content))
                return Task.FromResult(new GuardrailResult(false, $"Prompt injection detected: {p}"));
        return Task.FromResult(new GuardrailResult(true));
    }
}
