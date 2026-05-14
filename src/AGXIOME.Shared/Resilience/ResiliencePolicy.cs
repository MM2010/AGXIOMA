namespace AGXIOME.Shared.Resilience;

/// <summary>Types of infrastructure errors with recovery guidance.</summary>
public enum InfrastructureErrorType
{
    Transient,      // Retryable: timeout, 429, 503, network glitch
    RateLimited,    // 429 Too Many Requests → backoff
    ServerError,    // 500/502/503 → retry + fallback
    ConnectionLost, // DB/Redis/Network down → circuit breaker
    DataCorruption, // Checksum fail → retry with fresh data
    Unknown         // Non-recoverable → log + degrade
}

/// <summary>Result of an infrastructure operation with failure details.</summary>
public record InfrastructureResult<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public InfrastructureErrorType? ErrorType { get; init; }
    public string? ErrorMessage { get; init; }
    public int Attempts { get; init; }
    public TimeSpan Duration { get; init; }
    public bool UsedFallback { get; init; }

    public static InfrastructureResult<T> Ok(T data, int attempts = 1, TimeSpan? duration = null)
        => new() { Success = true, Data = data, Attempts = attempts, Duration = duration ?? TimeSpan.Zero };

    public static InfrastructureResult<T> Fail(InfrastructureErrorType type, string msg, int attempts = 1, TimeSpan? duration = null)
        => new() { Success = false, ErrorType = type, ErrorMessage = msg, Attempts = attempts, Duration = duration ?? TimeSpan.Zero };
}

/// <summary>Retry policy with exponential backoff and jitter.</summary>
public record RetryPolicy
{
    public int MaxRetries { get; init; } = 3;
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromMilliseconds(200);
    public bool ExponentialBackoff { get; init; } = true;
    public double JitterFactor { get; init; } = 0.2;
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan GetDelay(int attempt)
    {
        var delay = ExponentialBackoff
            ? BaseDelay * Math.Pow(2, attempt)
            : BaseDelay;

        delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds, MaxDelay.TotalMilliseconds));

        if (JitterFactor > 0)
        {
            var jitter = delay.TotalMilliseconds * JitterFactor;
            delay += TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * jitter * 2 - jitter);
        }

        return delay;
    }
}

/// <summary>Circuit breaker state machine.</summary>
public sealed class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _resetTimeout;
    private int _failureCount;
    private DateTime? _lastFailure;
    private CircuitBreakerState _state = CircuitBreakerState.Closed;

    public enum CircuitBreakerState { Closed, Open, HalfOpen }

    public CircuitBreaker(int failureThreshold = 5, TimeSpan? resetTimeout = null)
    {
        _failureThreshold = failureThreshold;
        _resetTimeout = resetTimeout ?? TimeSpan.FromSeconds(30);
    }

    public bool IsOpen
    {
        get
        {
            if (_state == CircuitBreakerState.Open && _lastFailure.HasValue)
            {
                if (DateTime.UtcNow - _lastFailure.Value > _resetTimeout)
                    _state = CircuitBreakerState.HalfOpen;
            }
            return _state == CircuitBreakerState.Open;
        }
    }

    public void RecordSuccess()
    {
        _failureCount = 0;
        _state = CircuitBreakerState.Closed;
    }

    public void RecordFailure()
    {
        _failureCount++;
        _lastFailure = DateTime.UtcNow;
        if (_failureCount >= _failureThreshold)
            _state = CircuitBreakerState.Open;
    }
}

/// <summary>Classifies HTTP status codes and exceptions into error types.</summary>
public static class ErrorClassifier
{
    public static InfrastructureErrorType Classify(int httpStatusCode)
        => httpStatusCode switch
        {
            429 => InfrastructureErrorType.RateLimited,
            502 or 503 or 504 => InfrastructureErrorType.ServerError,
            500 => InfrastructureErrorType.Transient,
            >= 400 => InfrastructureErrorType.Unknown,
            _ => InfrastructureErrorType.Transient
        };

    public static InfrastructureErrorType Classify(Exception ex)
        => ex switch
        {
            TimeoutException => InfrastructureErrorType.Transient,
            HttpRequestException h => Classify(h.StatusCode.HasValue ? (int)h.StatusCode.Value : 0),
            System.Net.Sockets.SocketException => InfrastructureErrorType.ConnectionLost,
            System.IO.IOException => InfrastructureErrorType.ConnectionLost,
            InvalidDataException => InfrastructureErrorType.DataCorruption,
            _ => InfrastructureErrorType.Unknown
        };
}
