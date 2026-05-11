namespace AGXIOME.Shared.Logging;

/// <summary>Unified logger interface for AGXIOME. All modules log through this.</summary>
public interface IAgxiomeLogger
{
    string ModuleName { get; }
    bool IsEnabled(LogLevel level);
    void Debug(string message, params object?[] args);
    void Information(string message, params object?[] args);
    void Warning(string message, params object?[] args);
    void Error(string message, params object?[] args);
    void Exception(Exception ex, string message, params object?[] args);
}

/// <summary>Factory interface for creating loggers per module.</summary>
public interface IAgxiomeLoggerFactory
{
    IAgxiomeLogger CreateLogger(string moduleName);
    void Configure(LoggingConfig config);
}
