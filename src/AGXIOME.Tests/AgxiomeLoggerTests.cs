using AGXIOME.Core.Logging;
using AGXIOME.Shared.Errors;
using AGXIOME.Shared.Logging;

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

    private static IAgxiomeLogger CreateLogger()
    {
        var factory = new AgxiomeLoggerFactory();
        factory.Configure(new LoggingConfig
        {
            Provider = "Console",
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
}