using AGXIOME.Core.Logging;
using AGXIOME.Shared.Errors;
using AGXIOME.Shared.Logging;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AGXIOME.Tests;

public sealed class AgxiomeLoggerTests
{
    [Fact]
    public void Information_StructuredTemplate_IsRenderedWithoutRawPlaceholders()
    {
        var output = CaptureConsole(() =>
        {
            var logger = CreateLogger();
            logger.Information("Run {RunId} -> {Status}", "run-42", "Completed");
        });

        Assert.Contains("run-42", output);
        Assert.Contains("Completed", output);
        Assert.DoesNotContain("{RunId}", output, StringComparison.Ordinal);
        Assert.DoesNotContain("{Status}", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Exception_RawException_DoesNotEmitMessageOrStackTrace()
    {
        var exception = CaptureException();

        var output = CaptureConsole(() =>
        {
            var logger = CreateLogger();
            logger.Exception(exception, "Agent {Id} failed: {Msg}", "agent-7", "safe-message");
        });

        Assert.Contains("agent-7", output);
        Assert.Contains("safe-message", output);
        Assert.Contains("InvalidOperationException", output);
        Assert.DoesNotContain("secret-token", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at AGXIOME.Tests", output, StringComparison.Ordinal);
        Assert.DoesNotContain("{Id}", output, StringComparison.Ordinal);
        Assert.DoesNotContain("{Msg}", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Exception_AgxiomeException_EmitsCodeAndUserMessageOnly()
    {
        var output = CaptureConsole(() =>
        {
            var logger = CreateLogger();
            var exception = new AgxiomeException(
                AgxiomeErrorCode.AE_RunFailed,
                technicalMessage: "secret-token diagnostic",
                userMessage: "Messaggio sicuro",
                isRetryable: true);

            logger.Exception(exception, "Run {RunId} failed", "run-99");
        });

        Assert.Contains(ErrorCatalog.GetCode(AgxiomeErrorCode.AE_RunFailed), output);
        Assert.Contains("Messaggio sicuro", output);
        Assert.Contains("run-99", output);
        Assert.DoesNotContain("secret-token diagnostic", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("technical=", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("{RunId}", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SeqProvider_Exception_PayloadIsSanitized()
    {
        using var probe = await HttpProbe.StartAsync("api/events/raw");
        var logger = CreateLogger("Seq", probe.BaseUrl);

        var exception = CaptureException();
        logger.Exception(exception, "Agent {Id} failed: {Msg}", "agent-9", "safe-message");

        var payload = await probe.WaitForBodyAsync();
        using var json = JsonDocument.Parse(payload);
        var evt = json.RootElement.GetProperty("Events")[0];
        var template = evt.GetProperty("MessageTemplate").GetString() ?? string.Empty;
        var module = evt.GetProperty("Properties").GetProperty("Module").GetString() ?? string.Empty;

        Assert.Equal("AGXIOME.Tests.Logging", module);
        Assert.Contains("agent-9", template);
        Assert.Contains("safe-message", template);
        Assert.Contains("InvalidOperationException", template);
        Assert.DoesNotContain("secret-token", template, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at AGXIOME.Tests", template, StringComparison.Ordinal);
        Assert.DoesNotContain("{Id}", template, StringComparison.Ordinal);
        Assert.DoesNotContain("{Msg}", template, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GraylogProvider_AgxiomeException_PayloadIsSanitized()
    {
        using var probe = await HttpProbe.StartAsync("gelf");
        var logger = CreateLogger("Graylog", probe.EndpointUrl);
        var exception = new AgxiomeException(
            AgxiomeErrorCode.AE_RunFailed,
            technicalMessage: "secret-token diagnostic",
            userMessage: "Messaggio sicuro",
            isRetryable: true);

        logger.Exception(exception, "Run {RunId} failed", "run-77");

        var payload = await probe.WaitForBodyAsync();
        using var json = JsonDocument.Parse(payload);
        var shortMessage = json.RootElement.GetProperty("short_message").GetString() ?? string.Empty;
        var module = json.RootElement.GetProperty("_module").GetString() ?? string.Empty;

        Assert.Equal("AGXIOME.Tests.Logging", module);
        Assert.Contains(ErrorCatalog.GetCode(AgxiomeErrorCode.AE_RunFailed), shortMessage);
        Assert.Contains("Messaggio sicuro", shortMessage);
        Assert.Contains("run-77", shortMessage);
        Assert.DoesNotContain("secret-token diagnostic", shortMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at AGXIOME.Tests", shortMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("{RunId}", shortMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void NLogProvider_Exception_OutputIsSanitized()
    {
        var output = CaptureTrace(() =>
        {
            var logger = CreateLogger("NLog");
            var exception = CaptureException();

            logger.Exception(exception, "Agent {Id} failed: {Msg}", "agent-11", "safe-message");
        });

        Assert.Contains("[NLog] [Exception] [AGXIOME.Tests.Logging]", output);
        Assert.Contains("agent-11", output);
        Assert.Contains("safe-message", output);
        Assert.Contains("InvalidOperationException", output);
        Assert.DoesNotContain("secret-token", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at AGXIOME.Tests", output, StringComparison.Ordinal);
        Assert.DoesNotContain("{Id}", output, StringComparison.Ordinal);
        Assert.DoesNotContain("{Msg}", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SerilogProvider_AgxiomeException_OutputIsSanitized()
    {
        var output = CaptureTrace(() =>
        {
            var logger = CreateLogger("Serilog");
            var exception = new AgxiomeException(
                AgxiomeErrorCode.AE_RunFailed,
                technicalMessage: "secret-token diagnostic",
                userMessage: "Messaggio sicuro",
                isRetryable: true);

            logger.Exception(exception, "Run {RunId} failed", "run-123");
        });

        Assert.Contains("[Serilog] [Exception] [AGXIOME.Tests.Logging]", output);
        Assert.Contains(ErrorCatalog.GetCode(AgxiomeErrorCode.AE_RunFailed), output);
        Assert.Contains("Messaggio sicuro", output);
        Assert.Contains("run-123", output);
        Assert.DoesNotContain("secret-token diagnostic", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("technical=", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at AGXIOME.Tests", output, StringComparison.Ordinal);
        Assert.DoesNotContain("{RunId}", output, StringComparison.Ordinal);
    }

    private static IAgxiomeLogger CreateLogger(string provider = "Console", string? endpoint = null)
    {
        var factory = new AgxiomeLoggerFactory();
        factory.Configure(new LoggingConfig
        {
            Provider = provider,
            Endpoint = endpoint,
            DefaultMinimumLevel = LogLevel.Debug
        });

        return factory.CreateLogger("AGXIOME.Tests.Logging");
    }

    private static string CaptureConsole(Action action)
    {
        var originalOut = Console.Out;
        var writer = new StringWriter();

        try
        {
            Console.SetOut(writer);
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static string CaptureTrace(Action action)
    {
        var writer = new StringWriter();
        var listener = new TextWriterTraceListener(writer);
        var originalListeners = Trace.Listeners.Cast<TraceListener>().ToArray();

        try
        {
            Trace.Listeners.Clear();
            Trace.Listeners.Add(listener);
            Trace.AutoFlush = true;
            action();
            listener.Flush();
            return writer.ToString();
        }
        finally
        {
            Trace.Listeners.Clear();
            foreach (var original in originalListeners)
            {
                Trace.Listeners.Add(original);
            }

            listener.Dispose();
            writer.Dispose();
        }
    }

    private static InvalidOperationException CaptureException()
    {
        try
        {
            throw new InvalidOperationException("secret-token leaked");
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }

    private sealed class HttpProbe : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly TaskCompletionSource<string> _bodyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string BaseUrl { get; }
        public string EndpointUrl { get; }

        private HttpProbe(HttpListener listener, string baseUrl, string endpointUrl)
        {
            _listener = listener;
            BaseUrl = baseUrl;
            EndpointUrl = endpointUrl;
        }

        public static Task<HttpProbe> StartAsync(string relativePath)
        {
            var port = GetFreePort();
            var baseUrl = $"http://127.0.0.1:{port}/";
            var endpointUrl = new Uri(new Uri(baseUrl), relativePath.TrimStart('/')).ToString();
            var listener = new HttpListener();
            listener.Prefixes.Add(baseUrl);
            listener.Start();

            var probe = new HttpProbe(listener, baseUrl, endpointUrl);
            probe.ListenOnce();
            return Task.FromResult(probe);
        }

        public async Task<string> WaitForBodyAsync(int timeoutMs = 5000)
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            await using var _ = cts.Token.Register(() => _bodyTcs.TrySetCanceled(cts.Token));
            return await _bodyTcs.Task.WaitAsync(cts.Token);
        }

        public void Dispose()
        {
            if (_listener.IsListening)
            {
                _listener.Stop();
            }

            _listener.Close();
        }

        private async void ListenOnce()
        {
            try
            {
                var context = await _listener.GetContextAsync();
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8);
                var body = await reader.ReadToEndAsync();
                context.Response.StatusCode = 200;
                context.Response.Close();
                _bodyTcs.TrySetResult(body);
            }
            catch (Exception ex)
            {
                _bodyTcs.TrySetException(ex);
            }
        }

        private static int GetFreePort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
    }
}