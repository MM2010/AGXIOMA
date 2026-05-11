# P13 — Guardrails

## File: `Guardrails/Guardrails.cs`

Namespace: `AGXIOME.Core.Guardrails`

## Classi

### `IGuardrail` / `GuardrailBase`

```csharp
public interface IGuardrail
{
    string Name { get; }
    GuardrailType Type { get; }
    GuardrailSeverity Severity { get; }
    Task<GuardrailResult> ValidateAsync(string content, GuardrailContext ctx, CancellationToken ct);
}

public abstract class GuardrailBase : IGuardrail
{
    public abstract string Name { get; }
    public abstract GuardrailType Type { get; }
    public virtual GuardrailSeverity Severity => GuardrailSeverity.Block;
    public abstract Task<GuardrailResult> ValidateAsync(string content, GuardrailContext ctx, CancellationToken ct);
}

public record GuardrailContext(string AgentId, string? SessionId, string? UserId);
public record GuardrailResult(bool IsValid, string? Reason = null, GuardrailSeverity? OverrideSeverity = null);
public enum GuardrailType { Input, Output }
public enum GuardrailSeverity { Log, Flag, Block }
```

### Built-in guardrails

```csharp
public sealed class PIIDetectionGuardrail : GuardrailBase
{
    public override string Name => "pii_detection";
    public override GuardrailType Type => GuardrailType.Input;

    private static readonly Regex[] _patterns = {
        new(@"\b\d{3}-\d{2}-\d{4}\b"),                          // SSN
        new(@"\b(?:\d[ -]*?){13,16}\b"),                        // Credit card
        new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b"), // Email
        new(@"\b(?:\d{1,3}\.){3}\d{1,3}\b"),                   // IP basic
    };

    public override Task<GuardrailResult> ValidateAsync(string content, GuardrailContext ctx, CancellationToken ct)
    {
        foreach (var p in _patterns)
            if (p.IsMatch(content))
                return Task.FromResult(new GuardrailResult(false, $"PII detected: {p}"));
        return Task.FromResult(new GuardrailResult(true));
    }
}

public sealed class PromptInjectionGuardrail : GuardrailBase { /* Pattern injection detection */ }
public sealed class OpenAIModerationGuardrail : GuardrailBase { /* Chiamata API moderation */ }
public sealed class ContentPolicyGuardrail : GuardrailBase { /* Output content validation */ }
```

## Custom guardrails (Agno compat)

```csharp
// Estendere GuardrailBase implementando check + async_check
// Usare eccezioni: InputCheckError / OutputCheckError con CheckTrigger

public class InputCheckError : Exception
{
    public CheckTrigger Trigger { get; }
    public InputCheckError(string msg, CheckTrigger trigger) : base(msg) => Trigger = trigger;
}

public enum CheckTrigger { InputNotAllowed, OutputNotAllowed }
```

## Test

| Test | Descrizione |
|------|------------|
| PII blocks SSN | InputCheckError thrown |
| Prompt injection detected | Block attivato |
| Clean input passes | IsValid = true |
| Severity Log (non block) | Warning loggato, run continua |
| Custom guardrail | Validazione custom funziona |
