using System.Diagnostics.Metrics;
using AGXIOME.Orchestration.Monitoring;
using AGXIOME.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AGXIOME.Tests.F24;

// ══════════════════════════════════════════════════════════════════════════════
// TS01  AgxiomeMetrics — instrument presence and meter identity
// ══════════════════════════════════════════════════════════════════════════════

[Trait("Phase", "F24")]
public class F24_TS01_AgxiomeMetrics
{
    [Fact]
    public void Meter_HasCorrectName()
    {
        Assert.Equal("AGXIOME.Core", AgxiomeMetrics.Meter.Name);
    }

    [Fact]
    public void Meter_HasCorrectVersion()
    {
        Assert.Equal("1.0.0", AgxiomeMetrics.Meter.Version);
    }

    // ── Counters ──

    [Fact]
    public void Counter_AgentRuns_NotNull()
        => Assert.NotNull(AgxiomeMetrics.AgentRuns);

    [Fact]
    public void Counter_AgentErrors_NotNull()
        => Assert.NotNull(AgxiomeMetrics.AgentErrors);

    [Fact]
    public void Counter_ToolCalls_NotNull()
        => Assert.NotNull(AgxiomeMetrics.ToolCalls);

    [Fact]
    public void Counter_GuardrailHits_NotNull()
        => Assert.NotNull(AgxiomeMetrics.GuardrailHits);

    [Fact]
    public void Counter_HITLPauses_NotNull()
        => Assert.NotNull(AgxiomeMetrics.HITLPauses);

    [Fact]
    public void Counter_ScheduleRuns_NotNull()
        => Assert.NotNull(AgxiomeMetrics.ScheduleRuns);

    [Fact]
    public void Counter_XLinkBytesSent_NotNull()
        => Assert.NotNull(AgxiomeMetrics.XLinkBytesSent);

    // ── Histograms ──

    [Fact]
    public void Histogram_AgentRunDuration_NotNull()
        => Assert.NotNull(AgxiomeMetrics.AgentRunDuration);

    [Fact]
    public void Histogram_LLMLatency_NotNull()
        => Assert.NotNull(AgxiomeMetrics.LLMLatency);

    [Fact]
    public void Histogram_LLMTokensPerRun_NotNull()
        => Assert.NotNull(AgxiomeMetrics.LLMTokensPerRun);

    [Fact]
    public void Histogram_RAGSearchLatency_NotNull()
        => Assert.NotNull(AgxiomeMetrics.RAGSearchLatency);

    [Fact]
    public void Histogram_StorageWriteLatency_NotNull()
        => Assert.NotNull(AgxiomeMetrics.StorageWriteLatency);

    [Fact]
    public void Histogram_StorageReadLatency_NotNull()
        => Assert.NotNull(AgxiomeMetrics.StorageReadLatency);

    [Fact]
    public void Histogram_XLinkLatency_NotNull()
        => Assert.NotNull(AgxiomeMetrics.XLinkLatency);

    // ── UpDownCounters ──

    [Fact]
    public void UpDown_ActiveAgents_NotNull()
        => Assert.NotNull(AgxiomeMetrics.ActiveAgents);

    [Fact]
    public void UpDown_ActiveWorkflows_NotNull()
        => Assert.NotNull(AgxiomeMetrics.ActiveWorkflows);

    // ── Operational: counters accept Add without throw ──

    [Fact]
    public void Counter_AgentRuns_Add_DoesNotThrow()
    {
        AgxiomeMetrics.AgentRuns.Add(1); // no exception expected
    }

    [Fact]
    public void Histogram_LLMLatency_Record_DoesNotThrow()
    {
        AgxiomeMetrics.LLMLatency.Record(123.4);
    }

    [Fact]
    public void UpDown_ActiveAgents_AddAndRemove_DoesNotThrow()
    {
        AgxiomeMetrics.ActiveAgents.Add(1);
        AgxiomeMetrics.ActiveAgents.Add(-1);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// TS02  OrchestratorMetrics — instrument presence
// ══════════════════════════════════════════════════════════════════════════════

[Trait("Phase", "F24")]
public class F24_TS02_OrchestratorMetrics
{
    [Fact]
    public void Meter_HasCorrectName()
        => Assert.Equal("AGXIOME.Orchestration", OrchestratorMetrics.Meter.Name);

    [Fact]
    public void Counter_PlansStarted_NotNull()
        => Assert.NotNull(OrchestratorMetrics.PlansStarted);

    [Fact]
    public void Counter_PlansCompleted_NotNull()
        => Assert.NotNull(OrchestratorMetrics.PlansCompleted);

    [Fact]
    public void Counter_PlansFailed_NotNull()
        => Assert.NotNull(OrchestratorMetrics.PlansFailed);

    [Fact]
    public void Counter_TasksSpawned_NotNull()
        => Assert.NotNull(OrchestratorMetrics.TasksSpawned);

    [Fact]
    public void Counter_PlansStarted_Add_DoesNotThrow()
    {
        OrchestratorMetrics.PlansStarted.Add(1);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// TS03  TelemetryConfig — record properties
// ══════════════════════════════════════════════════════════════════════════════

[Trait("Phase", "F24")]
public class F24_TS03_TelemetryConfig
{
    [Fact]
    public void DefaultConfig_HasExpectedDefaults()
    {
        var cfg = new TelemetryConfig();

        Assert.Null(cfg.OtlpEndpoint);
        Assert.Equal("agxiome",     cfg.ServiceName);
        Assert.Equal("development", cfg.Environment);
        Assert.Equal(0.1,           cfg.SamplingRate, 1);
    }

    [Fact]
    public void CustomConfig_Roundtrip()
    {
        var endpoint = new Uri("http://otel-collector:4317");
        var cfg = new TelemetryConfig
        {
            OtlpEndpoint  = endpoint,
            ServiceName   = "wordstack-arena",
            Environment   = "production",
            SamplingRate  = 1.0
        };

        Assert.Equal(endpoint,         cfg.OtlpEndpoint);
        Assert.Equal("wordstack-arena", cfg.ServiceName);
        Assert.Equal("production",      cfg.Environment);
        Assert.Equal(1.0,              cfg.SamplingRate);
    }

    [Fact]
    public void TelemetrySetup_AddAgxiomeTelemetry_IsExtensionOnIServiceCollection()
    {
        // Verify the extension method exists and returns the same IServiceCollection (no throw).
        var services = new ServiceCollection();
        var returned = services.AddAgxiomeTelemetry(new TelemetryConfig());

        Assert.Same(services, returned);
    }
}
