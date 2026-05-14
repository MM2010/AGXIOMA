using AGXIOME.Shared.Logging;
using AGXIOME.Shared.Resilience;
using System.Diagnostics;

namespace AGXIOME.Shared.Resilience;

/// <summary>
/// Executes operations with retry, backoff, circuit breaker, and fallback.
/// Logs every attempt with full context via Trace (zero dependency).
/// </summary>
public sealed class ResilientExecutor
{
    private readonly RetryPolicy _retryPolicy;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly string _componentName;

    public ResilientExecutor(RetryPolicy? retryPolicy = null, CircuitBreaker? circuitBreaker = null, string componentName = "Unknown")
    {
        _retryPolicy = retryPolicy ?? new RetryPolicy();
        _circuitBreaker = circuitBreaker ?? new CircuitBreaker();
        _componentName = componentName;
    }

    private void Log(LogLevel level, string message, params object?[] args)
    {
        var prefix = level switch
        {
            LogLevel.Debug => "DBG", LogLevel.Information => "INF",
            LogLevel.Warning => "WRN", LogLevel.Error => "ERR", LogLevel.Exception => "EXC",
            _ => "INF"
        };
        try
        {
            var formatted = args.Length > 0 ? string.Format(message, args) : message;
            Trace.WriteLine($"[{prefix}] [{_componentName}] [{DateTime.UtcNow:HH:mm:ss.fff}] {formatted}");
        }
        catch (FormatException)
        {
            // Structured logging placeholders like {Op} → skip formatting
            Trace.WriteLine($"[{prefix}] [{_componentName}] [{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
        }
    }

    /// <summary>Execute with retry. Returns InfrastructureResult with full diagnostics.</summary>
    public async Task<InfrastructureResult<T>> ExecuteAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> operation,
        Func<CancellationToken, Task<T>>? fallback = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (_circuitBreaker.IsOpen)
        {
            Trace.TraceWarning($"[{_componentName}] Circuit OPEN for '{operationName}', attempting fallback");
            if (fallback != null)
            {
                try
                {
                    var fb = await fallback(ct);
                    Trace.TraceInformation($"[{_componentName}] Fallback succeeded for '{operationName}'");
                    return InfrastructureResult<T>.Ok(fb, duration: sw.Elapsed) with { UsedFallback = true };
                }
                catch (Exception fbEx)
                {
                    Trace.TraceError($"[{_componentName}] Fallback also failed: {fbEx.Message}");
                    return InfrastructureResult<T>.Fail(InfrastructureErrorType.ConnectionLost, $"Circuit open + fallback: {fbEx.Message}", duration: sw.Elapsed);
                }
            }
            return InfrastructureResult<T>.Fail(InfrastructureErrorType.ConnectionLost, $"Circuit open for '{operationName}'", duration: sw.Elapsed);
        }

        int attempt = 0;

        while (attempt <= _retryPolicy.MaxRetries && !ct.IsCancellationRequested)
        {
            attempt++;
            try
            {
                var result = await operation(ct);
                _circuitBreaker.RecordSuccess();
                Log(LogLevel.Debug, "'{Op}' succeeded attempt {A}", operationName, attempt);
                return InfrastructureResult<T>.Ok(result, attempt, sw.Elapsed);
            }
            catch (Exception ex)
            {
                var errorType = ErrorClassifier.Classify(ex);
                Log(LogLevel.Warning, "'{Op}' failed attempt {A}: {ET} - {Msg}", operationName, attempt, errorType, ex.Message);
                _circuitBreaker.RecordFailure();

                if (attempt <= _retryPolicy.MaxRetries)
                {
                    var delay = _retryPolicy.GetDelay(attempt - 1);
                    Log(LogLevel.Debug, "Retry in {Delay}ms", delay.TotalMilliseconds);
                    try { await Task.Delay(delay, ct); } catch { break; }
                }
                else
                {
                    Log(LogLevel.Error, "'{Op}' failed after {A} attempts", operationName, attempt);
                    if (fallback != null)
                    {
                        try
                        {
                            var fb = await fallback(ct);
                            return InfrastructureResult<T>.Ok(fb, attempt, sw.Elapsed) with { UsedFallback = true };
                        }
                        catch (Exception fbEx)
                        {
                            Log(LogLevel.Exception, "Fallback also failed: {Msg}", fbEx.Message);
                        }
                    }
                    return InfrastructureResult<T>.Fail(errorType, $"{ex.Message} ({attempt} attempts)", attempt, sw.Elapsed);
                }
            }
        }
        return InfrastructureResult<T>.Fail(InfrastructureErrorType.Unknown, "Cancelled/exhausted", attempt, sw.Elapsed);
    }
}
