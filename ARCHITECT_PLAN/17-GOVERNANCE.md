# 17 — Governance: Guardrails, HITL, Hooks & Retry

## Scopo

Sistema di sicurezza a strati: guardrail input/output, human-in-the-loop, hooks per lifecycle eventi, retry policy robusta.

## Architettura Governance

```
Input ──► Guardrail (pre) ──► Agent ──► Guardrail (post) ──► Output
              │                       │
              ▼                       ▼
         Block/Flag              Rewrite/Block
              
Agent lifecycle:
  Pre-hook ──► Run ──► Post-hook
                  │
            Tool hooks (per-tool pre/post)
                  │
            Stream hook (on chunk)
                  │
            Background hook (async post)
```

## Guardrails

```csharp
public interface IGuardrail
{
    string Name { get; }
    GuardrailType Type { get; }  // Input | Output
    GuardrailSeverity Severity { get; }  // Block | Flag | Log

    Task<GuardrailResult> ValidateAsync(
        string content, GuardrailContext context, CancellationToken ct);
}

public record GuardrailContext(
    string AgentId,
    string? SessionId,
    string? UserId,
    Dictionary<string, object>? Metadata = null
);

public record GuardrailResult(
    bool IsValid,
    string? Reason = null,
    GuardrailSeverity? OverrideSeverity = null,
    string? SanitizedContent = null
);

public enum GuardrailType { Input, Output }
public enum GuardrailSeverity { Log, Flag, Block }
```

### Built-in Guardrails

```csharp
// ── PII Detection ──
public class PIIDetectionGuardrail : IGuardrail
{
    public string Name => "pii_detection";
    public GuardrailType Type => GuardrailType.Input;
    public GuardrailSeverity Severity => GuardrailSeverity.Block;

    // Pattern: SSN, credit card, email, phone, IP address
    private static readonly Regex[] _patterns = {
        new(@"\b\d{3}-\d{2}-\d{4}\b"),           // SSN
        new(@"\b(?:\d[ -]*?){13,16}\b"),          // Credit card
        new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b"), // Email
        new(@"\b\d{3}[-.]?\d{3}[-.]?\d{4}\b"),   // Phone
        new(@"\b(?:\d{1,3}\.){3}\d{1,3}\b"),     // Basic IP
    };

    public async Task<GuardrailResult> ValidateAsync(
        string content, GuardrailContext ctx, CancellationToken ct)
    {
        foreach (var pattern in _patterns)
        {
            var match = pattern.Match(content);
            if (match.Success)
                return new GuardrailResult(false,
                    $"PII detected: {pattern.GetGroupNames()[0]}");
        }
        return new GuardrailResult(true);
    }
}

// ── Prompt Injection ──
public class PromptInjectionGuardrail : IGuardrail
{
    public string Name => "prompt_injection";
    public GuardrailType Type => GuardrailType.Input;
    public GuardrailSeverity Severity => GuardrailSeverity.Block;

    // Pattern: "ignore previous instructions", "system prompt:", etc.
    private static readonly Regex[] _patterns = { /* ... */ };

    public async Task<GuardrailResult> ValidateAsync(...) { /* ... */ }
}

// ── OpenAI Moderation ──
public class OpenAIModerationGuardrail : IGuardrail
{
    public string Name => "openai_moderation";
    public GuardrailType Type => GuardrailType.Input;
    public GuardrailSeverity Severity => GuardrailSeverity.Block;

    // Chiama API moderation di OpenAI
    // Categorie: hate, harassment, sexual, violence, self-harm
}

// ── Output Guardrail (content validation) ──
public class OutputContentGuardrail : IGuardrail
{
    public string Name => "output_content";
    public GuardrailType Type => GuardrailType.Output;
    public GuardrailSeverity Severity => GuardrailSeverity.Flag;

    // Verifica che l'output rispetti policy di contenuto
    // Rewrite automatico se viola policy leggere
}
```

## Human-in-the-Loop (HITL)

### Confirmation Required

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class RequiresConfirmationAttribute : Attribute
{
    public string? Message { get; set; }
    public string? UserPrompt { get; set; }
    public bool RequiresUserInput { get; set; }
}

// ── Tool che richiede conferma ──
[RequiresConfirmation(Message = "Send email to user?")]
public sealed class SendEmailTool : AgxiomeToolBase
{
    // Quando chiamato, il run si mette in pausa
    // Finché human non approva o rifiuta
}
```

### Approval System

```csharp
public interface IApprovalManager
{
    Task<ApprovalRequest> RequestApprovalAsync(
        string agentId, string toolName,
        object payload, ApprovalConfig config,
        CancellationToken ct);

    Task<ApprovalResolution> ResolveApprovalAsync(
        string approvalId, bool approved,
        string? notes = null, CancellationToken ct = default);

    Task<IReadOnlyList<ApprovalRequest>> ListPendingAsync(
        string? agentId = null, CancellationToken ct = default);
}

public record ApprovalRequest(
    string Id,
    string AgentId,
    string ToolName,
    object Payload,
    ApprovalStatus Status,
    ApprovalConfig Config,
    DateTime CreatedAt
);

public enum ApprovalStatus { Pending, Approved, Rejected, Expired }

public record ApprovalConfig(
    TimeSpan? Timeout = null,
    bool RequiresAuditTrail = true,
    string? NotifyEndpoint = null,
    List<string>? RequiredApprovers = null
);

public enum ApprovalType
{
    Standard,    // Pausa finché approvato
    Audit        // Procede, logga per audit post-hoc
}
```

### External Execution

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class ExternalExecutionAttribute : Attribute
{
    // Il tool non viene eseguito dall'agente
    // Ma da un sistema esterno tramite callback
    // L'agente si mette in pausa, aspetta risultato esterno
}
```

## Hooks System

```csharp
// ── Hook types ──
public enum HookPoint
{
    PreRun,          // Prima dell'esecuzione agente
    PostRun,         // Dopo completamento
    PreTool,         // Prima di ogni tool call
    PostTool,        // Dopo ogni tool call
    StreamChunk,     // Ad ogni chunk streaming
    BackgroundPost   // Post-run asincrono (non bloccante)
}

public interface IAgentHook
{
    string Name { get; }
    HookPoint Point { get; }
    int Order { get; }  // Ordine esecuzione
    Task ExecuteAsync(HookContext context, CancellationToken ct);
}

public record HookContext(
    string AgentId,
    string RunId,
    HookPoint Point,
    object? Input = null,
    object? Output = null,
    Exception? Error = null,
    bool IsBackground = false
);

// ── Hook examples ──
public class InputValidationPreHook : IAgentHook
{
    public string Name => "input_validation";
    public HookPoint Point => HookPoint.PreRun;
    public int Order => 10;

    public async Task ExecuteAsync(HookContext ctx, CancellationToken ct)
    {
        var request = ctx.Input as AgentRequest;
        if (string.IsNullOrWhiteSpace(request?.Prompt))
            throw new ArgumentException("Prompt cannot be empty");
    }
}

public class AuditLogPostHook : IAgentHook
{
    public string Name => "audit_log";
    public HookPoint Point => HookPoint.PostRun;
    public int Order => 100;

    public async Task ExecuteAsync(HookContext ctx, CancellationToken ct)
    {
        await auditLogger.LogAsync(new AuditEvent(
            "agent.run.completed",
            ctx.AgentId,
            ctx.RunId,
            null,
            "EXECUTE",
            new { input = ctx.Input, output = ctx.Output },
            null,
            ctx.Error == null,
            ctx.Error?.Message
        ), ct);
    }
}

// ── Background hooks (async, non-bloccanti) ──
public class BackgroundEvalHook : IAgentHook
{
    public string Name => "background_eval";
    public HookPoint Point => HookPoint.BackgroundPost;
    public int Order => 1000;

    public async Task ExecuteAsync(HookContext ctx, CancellationToken ct)
    {
        // Esegue agent-as-judge evaluation in background
        // Non blocca la response al client
    }
}

// ── Tool hooks (per-tool) ──
public interface IToolHook
{
    Task BeforeExecuteAsync(ToolContext ctx, CancellationToken ct);
    Task AfterExecuteAsync(ToolContext ctx, ToolResult result, CancellationToken ct);
}
```

## Retry & Error Handling

```csharp
public class RetryPolicy
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);
    public bool ExponentialBackoff { get; set; } = true;
    public double JitterFactor { get; set; } = 0.2; // Randomizzazione
    public List<Type> RetryableExceptions { get; set; } = new()
    {
        typeof(TimeoutException),
        typeof(HttpRequestException),
        typeof(IOException)
    };
    public Func<Exception, bool>? RetryCondition { get; set; }

    public TimeSpan GetDelay(int attempt)
    {
        var delay = ExponentialBackoff
            ? BaseDelay * Math.Pow(2, attempt)
            : BaseDelay;

        // Jitter: random ±20%
        var jitter = delay.TotalMilliseconds * JitterFactor;
        delay += TimeSpan.FromMilliseconds(
            Random.Shared.NextDouble() * jitter * 2 - jitter);

        return delay;
    }
}

// ── Special exceptions ──
public class RetryAgentRunException : Exception
{
    public string Feedback { get; }
    public RetryAgentRunException(string feedback) : base(feedback)
    {
        Feedback = feedback;
    }
    // Quando lanciata in un tool, l'agente riprova
    // con il feedback come contesto aggiuntivo
}

public class StopAgentRunException : Exception
{
    public string? FinalResponse { get; }
    public StopAgentRunException(string? finalResponse = null)
    {
        FinalResponse = finalResponse;
    }
    // Termina immediatamente il run
}

// ── Config ──
public class GovernanceConfig
{
    public List<IGuardrail> Guardrails { get; set; } = new();
    public List<IAgentHook> Hooks { get; set; } = new();
    public RetryPolicy RetryPolicy { get; set; } = new();
    public bool EnableHITL { get; set; } = true;
    public bool EnableAuditHooks { get; set; } = true;
    public bool EnableBackgroundEvals { get; set; } = false;
}
```

## NFR

| Metrica | Target |
|---------|--------|
| Guardrail validation p95 | < 50 ms |
| Hook execution (pre/post) p95 | < 100 ms |
| Background hook latency impact | 0 (non bloccante) |
| Retry overhead per tentativo | < 500 ms |
| Approval creation p95 | < 20 ms |
| Approval resolution p95 | < 10 ms |
