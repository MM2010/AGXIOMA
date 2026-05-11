namespace AGXIOME.Shared.Logging;

/// <summary>Log severity levels.</summary>
public enum LogLevel
{
    Debug = 0,
    Information = 1,
    Warning = 2,
    Error = 3,
    Exception = 4
}

/// <summary>Per-module log level configuration.</summary>
public record DebugContextConfig
{
    public string Module { get; init; } = "*";          // "*" = all, "AGXIOME.Core.Agent", etc.
    public LogLevel MinimumLevel { get; init; } = LogLevel.Information;
    public bool EnableCallerInfo { get; init; } = true;
}

/// <summary>Global logging configuration.</summary>
public record LoggingConfig
{
    public LogLevel DefaultMinimumLevel { get; init; } = LogLevel.Information;
    public List<DebugContextConfig> ModuleOverrides { get; init; } = new();
    public string Provider { get; init; } = "Console";   // Console, Seq, NLog, Graylog, Serilog
    public string? Endpoint { get; init; }               // Seq/Graylog endpoint
    public string? ApiKey { get; init; }
}
