# Guardrails

Guardrails validate and protect agent input and output. They run automatically in the agent pipeline before LLM inference (input) and after (output).

## Guardrail Interface

```csharp
public interface IGuardrail
{
    string Name { get; }
    GuardrailType Type { get; }         // Input, Output, Both
    GuardrailSeverity Severity { get; } // Block, Warn, Transform

    Task<GuardrailResult> ValidateAsync(string content, GuardrailContext ctx, CancellationToken ct);
}
```

## Severity Levels

| Severity | Behavior | Use Case |
|----------|----------|----------|
| **Block** | Reject the content, stop the agent | PII in output, prompt injection |
| **Warn** | Allow but log a warning | Mild toxicity, borderline content |
| **Transform** | Modify content (e.g., redact PII) | Phone numbers, emails, credit cards |

## Built-in Guardrails

```csharp
var agent = AgxiomeBuilder.Agent("agent")
    // Block if PII detected (credit cards, SSN, phone numbers)
    .WithGuardrail(new PiiDetectionGuardrail())

    // Block known prompt injection patterns
    .WithGuardrail(new PromptInjectionGuardrail())

    // OpenAI moderation API (requires OpenAI key)
    .WithGuardrail(new OpenAIModerationGuardrail())
    .Build();
```

## Custom Guardrail

```csharp
public class ProfanityGuardrail : IGuardrail
{
    public string Name => "profanity";
    public GuardrailType Type => GuardrailType.Input;
    public GuardrailSeverity Severity => GuardrailSeverity.Block;

    private static readonly string[] _words = { "badword1", "badword2" };

    public Task<GuardrailResult> ValidateAsync(string content, GuardrailContext ctx, CancellationToken ct)
    {
        var found = _words.Where(w => content.Contains(w, StringComparison.OrdinalIgnoreCase)).ToList();
        if (found.Any())
            return Task.FromResult(GuardrailResult.Block($"Profanity detected: {string.Join(", ", found)}"));
        return Task.FromResult(GuardrailResult.Pass());
    }
}
```

## Input vs Output Guardrails

```csharp
// Input guardrail — runs BEFORE LLM
public class MaxPromptLengthGuard : IGuardrail
{
    public GuardrailType Type => GuardrailType.Input;
    public GuardrailSeverity Severity => GuardrailSeverity.Block;

    public Task<GuardrailResult> ValidateAsync(string content, GuardrailContext ctx, CancellationToken ct)
    {
        if (content.Length > 100_000)
            return Task.FromResult(GuardrailResult.Block("Prompt exceeds 100K character limit"));
        return Task.FromResult(GuardrailResult.Pass());
    }
}

// Output guardrail — runs AFTER LLM
public class NoCodeInResponseGuard : IGuardrail
{
    public GuardrailType Type => GuardrailType.Output;
    public GuardrailSeverity Severity => GuardrailSeverity.Warn;

    public Task<GuardrailResult> ValidateAsync(string content, GuardrailContext ctx, CancellationToken ct)
    {
        if (content.Contains("<script>"))
            return Task.FromResult(GuardrailResult.Warn("Response contains HTML script tags"));
        return Task.FromResult(GuardrailResult.Pass());
    }
}
```

## Guardrail Context

```csharp
public record GuardrailContext(
    string AgentId,
    string? SessionId,
    string? UserId,
    Dictionary<string, object> Metadata
);
```

## Performance

Guardrails run **synchronously** in the agent pipeline. Keep them fast (<50ms). For heavy validation (e.g., ML-based toxicity), use background evaluation hooks.

## Next

- [Hooks](hooks.md) — Lifecycle hooks (pre/post/tool)
- [HITL](hitl.md) — Human-in-the-Loop approval
- [RBAC](rbac.md) — Role-Based Access Control
