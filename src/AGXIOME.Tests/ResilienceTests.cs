using AGXIOME.Shared.Resilience;
using Xunit;

namespace AGXIOME.Tests;

public class ResilienceTests
{
    [Fact]
    public void RetryPolicy_ExponentialBackoff_IncreasesDelay()
    {
        var p = new RetryPolicy { MaxRetries = 3, BaseDelay = TimeSpan.FromMilliseconds(100), JitterFactor = 0 };
        Assert.True(p.GetDelay(0) < p.GetDelay(1) && p.GetDelay(1) < p.GetDelay(2));
    }

    [Fact]
    public void RetryPolicy_Jitter_ProducesVariation()
    {
        var p = new RetryPolicy { BaseDelay = TimeSpan.FromMilliseconds(100), ExponentialBackoff = false, JitterFactor = 0.3 };
        Assert.True(Enumerable.Range(0, 100).Select(_ => p.GetDelay(0).TotalMilliseconds).Distinct().Count() > 1);
    }

    [Fact]
    public void RetryPolicy_MaxDelay_Clamps()
    {
        var p = new RetryPolicy { BaseDelay = TimeSpan.FromSeconds(10), MaxDelay = TimeSpan.FromSeconds(2), JitterFactor = 0 };
        Assert.True(p.GetDelay(5) <= TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void CircuitBreaker_OpenAfterThreshold()
    {
        var cb = new CircuitBreaker(failureThreshold: 3);
        cb.RecordFailure(); cb.RecordFailure(); cb.RecordFailure();
        Assert.True(cb.IsOpen);
    }

    [Fact]
    public void CircuitBreaker_ResetsOnSuccess()
    {
        var cb = new CircuitBreaker(failureThreshold: 3);
        cb.RecordFailure(); cb.RecordFailure(); cb.RecordSuccess();
        cb.RecordFailure(); cb.RecordFailure();
        Assert.False(cb.IsOpen);
    }

    [Theory]
    [InlineData(429, InfrastructureErrorType.RateLimited)]
    [InlineData(503, InfrastructureErrorType.ServerError)]
    [InlineData(500, InfrastructureErrorType.Transient)]
    [InlineData(400, InfrastructureErrorType.Unknown)]
    public void ErrorClassifier_Codes(int code, InfrastructureErrorType e) => Assert.Equal(e, ErrorClassifier.Classify(code));

    [Fact]
    public async Task ResilientExecutor_RetriesThenSucceeds()
    {
        int n = 0;
        var ex = new ResilientExecutor(new RetryPolicy { MaxRetries = 3, BaseDelay = TimeSpan.FromMilliseconds(1), JitterFactor = 0 }, componentName: "t");
        var r = await ex.ExecuteAsync<string>("op", async ct => { n++; return n >= 3 ? "ok" : throw new TimeoutException(); });
        Assert.True(r.Success); Assert.Equal("ok", r.Data); Assert.Equal(3, r.Attempts);
    }

    [Fact]
    public async Task ResilientExecutor_Exhausted_ReturnsFail()
    {
        int n = 0;
        var ex = new ResilientExecutor(new RetryPolicy { MaxRetries = 1, BaseDelay = TimeSpan.FromMilliseconds(1) }, componentName: "t");
        var r = await ex.ExecuteAsync<string>("op", async ct => { n++; throw new TimeoutException(); });
        Assert.False(r.Success); Assert.Equal(2, r.Attempts);
    }

    [Fact]
    public async Task ResilientExecutor_Fallback_Used()
    {
        var ex = new ResilientExecutor(new RetryPolicy { MaxRetries = 1, BaseDelay = TimeSpan.FromMilliseconds(1) }, componentName: "t");
        var r = await ex.ExecuteAsync<string>("op",
            async ct => throw new HttpRequestException("d", null, System.Net.HttpStatusCode.ServiceUnavailable),
            async ct => "fb");
        Assert.True(r.Success); Assert.Equal("fb", r.Data); Assert.True(r.UsedFallback);
    }

    [Fact]
    public async Task ResilientExecutor_Circuit_SkipsOp()
    {
        var cb = new CircuitBreaker(failureThreshold: 1, resetTimeout: TimeSpan.FromMinutes(10));
        var ex = new ResilientExecutor(new RetryPolicy { MaxRetries = 0 }, cb, "t");
        await ex.ExecuteAsync<string>("fail", async ct => throw new TimeoutException());
        bool called = false;
        var r = await ex.ExecuteAsync<string>("skip", async ct => { called = true; return "x"; });
        Assert.False(r.Success); Assert.False(called);
    }

    [Fact]
    public async Task ResilientExecutor_Circuit_Fallback()
    {
        var cb = new CircuitBreaker(failureThreshold: 1, resetTimeout: TimeSpan.FromMinutes(10));
        var ex = new ResilientExecutor(new RetryPolicy { MaxRetries = 0 }, cb, "t");
        await ex.ExecuteAsync<string>("fail", async ct => throw new TimeoutException());
        var r = await ex.ExecuteAsync<string>("fb", async ct => throw new TimeoutException(), async ct => "f");
        Assert.True(r.Success); Assert.Equal("f", r.Data); Assert.True(r.UsedFallback);
    }

    [Fact]
    public async Task ResilientExecutor_AllErrorTypes()
    {
        var ex = new ResilientExecutor(new RetryPolicy { MaxRetries = 0 }, componentName: "t");
        var cases = new (Exception x, InfrastructureErrorType e)[]
        {
            (new TimeoutException(), InfrastructureErrorType.Transient),
            (new HttpRequestException(null, null, System.Net.HttpStatusCode.TooManyRequests), InfrastructureErrorType.RateLimited),
            (new InvalidDataException(), InfrastructureErrorType.DataCorruption),
            (new InvalidOperationException(), InfrastructureErrorType.Unknown),
        };
        foreach (var (x, e) in cases)
        {
            var r = await ex.ExecuteAsync<string>("c", async ct => throw x);
            Assert.Equal(e, r.ErrorType);
        }
    }
}
