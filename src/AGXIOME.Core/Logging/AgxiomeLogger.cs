using AGXIOME.Shared.Logging;

namespace AGXIOME.Core.Logging;

/// <summary>
/// Logger factory with per-module level overrides and provider routing.
/// Supports Console, Seq, NLog, Graylog, Serilog.
/// </summary>
public sealed class AgxiomeLoggerFactory : IAgxiomeLoggerFactory
{
    private LoggingConfig _config = new();
    private readonly Dictionary<string, LogLevel> _moduleOverrides = new();
    private readonly object _lock = new();

    public void Configure(LoggingConfig config)
    {
        _config = config;
        lock (_lock)
        {
            _moduleOverrides.Clear();
            foreach (var ov in config.ModuleOverrides)
                _moduleOverrides[ov.Module] = ov.MinimumLevel;
        }
    }

    public IAgxiomeLogger CreateLogger(string moduleName)
    {
        return new AgxiomeLogger(moduleName, _config, _moduleOverrides);
    }

    /// <summary>Create with default config (Console provider, Information level).</summary>
    public static IAgxiomeLogger Default(string moduleName)
    {
        return new AgxiomeLogger(moduleName, new LoggingConfig(), new());
    }
}

/// <summary>Logger instance per module. Delegates to configured provider.</summary>
internal sealed class AgxiomeLogger : IAgxiomeLogger
{
    public string ModuleName { get; }
    private readonly LoggingConfig _config;
    private readonly Dictionary<string, LogLevel> _overrides;
    private readonly ILogProvider _provider;

    public AgxiomeLogger(string moduleName, LoggingConfig config, Dictionary<string, LogLevel> overrides)
    {
        ModuleName = moduleName;
        _config = config;
        _overrides = overrides;
        _provider = CreateProvider(config);
    }

    public bool IsEnabled(LogLevel level)
    {
        var minLevel = _config.DefaultMinimumLevel;

        // Check module override with wildcard support
        foreach (var (pattern, ovLevel) in _overrides)
        {
            if (pattern == "*" || pattern == ModuleName || (pattern.EndsWith("*") && ModuleName.StartsWith(pattern[..^1])))
            {
                minLevel = ovLevel;
                break;
            }
        }

        return level >= minLevel;
    }

    public void Debug(string message, params object?[] args) => Log(LogLevel.Debug, message, args);
    public void Information(string message, params object?[] args) => Log(LogLevel.Information, message, args);
    public void Warning(string message, params object?[] args) => Log(LogLevel.Warning, message, args);
    public void Error(string message, params object?[] args) => Log(LogLevel.Error, message, args);
    public void Exception(Exception ex, string message, params object?[] args)
    {
        if (IsEnabled(LogLevel.Exception))
            _provider.Log(LogLevel.Exception, ModuleName, $"{message} | {ex.GetType().Name}: {ex.Message} | {ex.StackTrace}", args);
    }

    private void Log(LogLevel level, string message, object?[] args)
    {
        if (!IsEnabled(level)) return;
        try
        {
            var formatted = args.Length > 0 ? string.Format(message, args) : message;
            _provider.Log(level, ModuleName, formatted, args);
        }
        catch (FormatException)
        {
            // If message uses structured logging placeholders ({Name}), fall back to raw message
            _provider.Log(level, ModuleName, message, args);
        }
    }

    private static ILogProvider CreateProvider(LoggingConfig config) => config.Provider.ToLowerInvariant() switch
    {
        "seq" => new SeqLogProvider(config.Endpoint, config.ApiKey),
        "nlog" => new NLogProvider(),
        "graylog" => new GraylogProvider(config.Endpoint),
        "serilog" => new SerilogProvider(),
        _ => new ConsoleLogProvider()
    };
}

/// <summary>Internal provider interface.</summary>
internal interface ILogProvider
{
    void Log(LogLevel level, string module, string message, object?[] args);
}

/// <summary>Console log provider (default). Color-coded output.</summary>
internal sealed class ConsoleLogProvider : ILogProvider
{
    private static readonly object ConsoleLock = new();

    public void Log(LogLevel level, string module, string message, object?[] args)
    {
        lock (ConsoleLock)
        {
            var color = level switch
            {
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Information => ConsoleColor.White,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Exception => ConsoleColor.DarkRed,
                _ => ConsoleColor.White
            };

            var original = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{level,-12}] [{module}] {message}");
            Console.ForegroundColor = original;
        }
    }
}

/// <summary>Seq log provider (structured JSON over HTTP).</summary>
internal sealed class SeqLogProvider : ILogProvider
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;

    public SeqLogProvider(string? endpoint, string? apiKey)
    {
        _http = new HttpClient { BaseAddress = endpoint != null ? new Uri(endpoint) : null };
        _apiKey = apiKey;
        if (_apiKey != null)
            _http.DefaultRequestHeaders.Add("X-Seq-ApiKey", _apiKey);
    }

    public void Log(LogLevel level, string module, string message, object?[] args)
    {
        try
        {
            var payload = new
            {
                Events = new[]
                {
                    new
                    {
                        Timestamp = DateTime.UtcNow,
                        Level = level.ToString(),
                        MessageTemplate = message,
                        Properties = new { Module = module }
                    }
                }
            };
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            _http.PostAsync("api/events/raw", new StringContent(json, System.Text.Encoding.UTF8, "application/json"))
                .ConfigureAwait(false);
        }
        catch { /* fire-and-forget, never throw in logging */ }
    }
}

/// <summary>Graylog/GELF log provider (UDP or HTTP).</summary>
internal sealed class GraylogProvider : ILogProvider
{
    private readonly string? _endpoint;
    private readonly HttpClient _http;

    public GraylogProvider(string? endpoint)
    {
        _endpoint = endpoint;
        _http = new HttpClient();
    }

    public void Log(LogLevel level, string module, string message, object?[] args)
    {
        try
        {
            var gelf = new System.Text.Json.Nodes.JsonObject
            {
                ["version"] = "1.1",
                ["host"] = Environment.MachineName,
                ["short_message"] = message,
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ["_module"] = module,
                ["level"] = (int)level
            };
            var json = gelf.ToJsonString();
            _http.PostAsync(_endpoint ?? "http://localhost:12201/gelf",
                new StringContent(json, System.Text.Encoding.UTF8, "application/json"))
                .ConfigureAwait(false);
        }
        catch { }
    }
}

/// <summary>NLog provider stub — redirects to NLog.</summary>
internal sealed class NLogProvider : ILogProvider
{
    public void Log(LogLevel level, string module, string message, object?[] args)
    {
        // In production: NLog.LogManager.GetLogger(module).Log(ConvertLevel(level), message);
        System.Diagnostics.Trace.WriteLine($"[NLog] [{level}] [{module}] {message}");
    }
}

/// <summary>Serilog provider stub — redirects to Serilog.</summary>
internal sealed class SerilogProvider : ILogProvider
{
    public void Log(LogLevel level, string module, string message, object?[] args)
    {
        // In production: Log.ForContext("Module", module).Write(ConvertLevel(level), message, args);
        System.Diagnostics.Trace.WriteLine($"[Serilog] [{level}] [{module}] {message}");
    }
}
