using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace AGXIOME.Telemetry;

/// <summary>OpenTelemetry setup: traces, metrics, exporters.</summary>
public static class AgxiomeTelemetrySetup
{
    public static IServiceCollection AddAgxiomeTelemetry(this IServiceCollection services, TelemetryConfig config)
    {
        services.AddOpenTelemetry()
            .WithTracing(t => t
                .AddSource("AGXIOME.Core", "AGXIOME.LLMWIKY", "AGXIOME.XLink")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = config.OtlpEndpoint ?? new Uri("http://localhost:4317");
                    o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                }))
            .WithMetrics(m => m
                .AddMeter("AGXIOME.Core")
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation());

        return services;
    }
}

public record TelemetryConfig
{
    public Uri? OtlpEndpoint { get; init; }
    public string ServiceName { get; init; } = "agxiome";
    public string Environment { get; init; } = "development";
    public double SamplingRate { get; init; } = 0.1;
}
