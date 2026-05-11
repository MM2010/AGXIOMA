using System.Diagnostics.Metrics;

namespace AGXIOME.Telemetry;

/// <summary>AGXIOME metrics: counters, histograms, up-down counters for agents, tools, LLM, storage, X-Link.</summary>
public static class AgxiomeMetrics
{
    public static readonly Meter Meter = new("AGXIOME.Core", "1.0.0");

    // Counters
    public static Counter<int> AgentRuns { get; } = Meter.CreateCounter<int>("agxiome.agent.runs");
    public static Counter<int> AgentErrors { get; } = Meter.CreateCounter<int>("agxiome.agent.errors");
    public static Counter<int> ToolCalls { get; } = Meter.CreateCounter<int>("agxiome.agent.tool_calls");
    public static Counter<int> GuardrailHits { get; } = Meter.CreateCounter<int>("agxiome.guardrail.hits");
    public static Counter<int> HITLPauses { get; } = Meter.CreateCounter<int>("agxiome.hitl.pauses");
    public static Counter<int> ScheduleRuns { get; } = Meter.CreateCounter<int>("agxiome.schedule.runs");
    public static Counter<long> XLinkBytesSent { get; } = Meter.CreateCounter<long>("agxiome.xlink.bytes_sent");

    // Histograms
    public static Histogram<double> AgentRunDuration { get; } = Meter.CreateHistogram<double>("agxiome.agent.run.duration", "ms", "Agent execution duration");
    public static Histogram<double> LLMLatency { get; } = Meter.CreateHistogram<double>("agxiome.llm.latency", "ms", "LLM inference latency");
    public static Histogram<double> LLMTokensPerRun { get; } = Meter.CreateHistogram<double>("agxiome.llm.tokens", "tokens", "Tokens per inference");
    public static Histogram<double> RAGSearchLatency { get; } = Meter.CreateHistogram<double>("agxiome.rag.search.latency", "ms", "RAG semantic search latency");
    public static Histogram<double> StorageWriteLatency { get; } = Meter.CreateHistogram<double>("agxiome.storage.write.latency", "ms");
    public static Histogram<double> StorageReadLatency { get; } = Meter.CreateHistogram<double>("agxiome.storage.read.latency", "ms");
    public static Histogram<double> XLinkLatency { get; } = Meter.CreateHistogram<double>("agxiome.xlink.latency", "ms");

    // UpDownCounters
    public static UpDownCounter<int> ActiveAgents { get; } = Meter.CreateUpDownCounter<int>("agxiome.agent.active");
    public static UpDownCounter<int> ActiveWorkflows { get; } = Meter.CreateUpDownCounter<int>("agxiome.workflow.active");
}
