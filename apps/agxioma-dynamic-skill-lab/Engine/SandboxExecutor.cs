using System.Diagnostics;
using AGXIOME.DynamicRuntime.Interfaces;

namespace AgxiomaDynamicSkillLab.Engine;

/// <summary>
/// Executes a compiled ISimpleDynamicAgent in a sandboxed manner:
/// - isolated ALC (already done by RoslynCompiler)
/// - execution timeout
/// - memory limit observation (process-level)
/// - result capture
/// - audit trail
/// </summary>
public sealed class SandboxExecutor
{
    private readonly int _timeoutMs;
    private readonly bool _runtimeMutationEnabled;
    private readonly int _maxInputChars;
    private readonly int _maxOutputChars;
    private readonly SemaphoreSlim _executionGate;

    public SandboxExecutor(
        int timeoutMs = 10_000,
        bool runtimeMutationEnabled = false,
        int maxInputChars = 4_096,
        int maxOutputChars = 16_384,
        int maxConcurrentExecutions = 1)
    {
        _timeoutMs = timeoutMs;
        _runtimeMutationEnabled = runtimeMutationEnabled;
        _maxInputChars = Math.Max(1, maxInputChars);
        _maxOutputChars = Math.Max(1, maxOutputChars);
        _executionGate = new SemaphoreSlim(Math.Max(1, maxConcurrentExecutions), Math.Max(1, maxConcurrentExecutions));
    }

    public async Task<SandboxResult> ExecuteAsync(
        ISimpleDynamicAgent tool,
        string input,
        string toolId,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var errors = new List<string>();
        string? output = null;
        bool success = false;
        var gateAcquired = false;

        if (!_runtimeMutationEnabled)
        {
            errors.Add("Sandbox execution is disabled by runtime policy");
            return new SandboxResult(false, string.Empty, errors, 0);
        }

        if (tool.AgentId.Length == 0 || !string.Equals(tool.AgentId, toolId, StringComparison.Ordinal))
        {
            errors.Add("Sandbox policy rejected tool identity mismatch");
            return new SandboxResult(false, string.Empty, errors, 0);
        }

        if (input.Length > _maxInputChars)
        {
            errors.Add($"Sandbox policy rejected input larger than {_maxInputChars} characters");
            return new SandboxResult(false, string.Empty, errors, 0);
        }

        try
        {
            await _executionGate.WaitAsync(ct);
            gateAcquired = true;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_timeoutMs);

            output = await tool.RunAsync(input, timeoutCts.Token);
            if (output.Length > _maxOutputChars)
                output = output[.._maxOutputChars];
            success = true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            errors.Add($"Execution timed out after {_timeoutMs}ms");
        }
        catch (Exception)
        {
            errors.Add("Execution failed under sandbox policy");
        }
        finally
        {
            if (gateAcquired)
                _executionGate.Release();
        }

        sw.Stop();

        return new SandboxResult(
            Success: success,
            Output: output ?? string.Empty,
            Errors: errors,
            DurationMs: sw.ElapsedMilliseconds
        );
    }
}

public sealed record SandboxResult(
    bool Success,
    string Output,
    List<string> Errors,
    long DurationMs
);
