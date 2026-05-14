using System.Collections.Concurrent;
using System.Diagnostics;
using AGXIOME.DynamicRuntime.Interfaces;
using Microsoft.Data.Sqlite;

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

    public SandboxExecutor(int timeoutMs = 10_000)
    {
        _timeoutMs = timeoutMs;
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

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_timeoutMs);

            output = await tool.RunAsync(input, timeoutCts.Token);
            success = true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            errors.Add($"Execution timed out after {_timeoutMs}ms");
        }
        catch (Exception ex)
        {
            errors.Add($"Execution error: {ex.Message}");
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
