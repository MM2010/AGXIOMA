using AGXIOME.Shared.Errors;
using AGXIOME.Toolkits;
using AGXIOME.Toolkits.Communication;
using AGXIOME.Toolkits.Development;
using AGXIOME.Toolkits.Finance;
using AGXIOME.Toolkits.AiMl;
using AGXIOME.Toolkits.CloudInfra;
using AGXIOME.Toolkits.DataAnalysis;
using AGXIOME.Toolkits.FileStorage;
using AGXIOME.Toolkits.GamingCreative;

namespace AGXIOME.Tests;

public sealed class ToolkitBoundaryExceptionTests
{
    [Fact]
    public async Task ExecuteAsync_MissingRequiredSetting_ReturnsStructuredSafeError()
    {
        var toolkit = new MissingSettingToolkit(new ToolkitConfig("missing-setting", new Dictionary<string, string> { ["enabled"] = "true" }));

        var result = await toolkit.ExecuteAsync("run", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Configurazione obbligatoria del toolkit mancante.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-1301", result.Metadata!["errorCode"]);
    }

    [Fact]
    public async Task ExecuteAsync_MissingRequiredParameter_ReturnsStructuredSafeError()
    {
        var toolkit = new MissingParameterToolkit(new ToolkitConfig("missing-param", new Dictionary<string, string> { ["enabled"] = "true" }));

        var result = await toolkit.ExecuteAsync("run", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Parametri del toolkit non validi.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    [Fact]
    public async Task ExecuteAsync_RawException_IsSanitized()
    {
        var toolkit = new RawFailureToolkit(new ToolkitConfig("raw-failure", new Dictionary<string, string> { ["enabled"] = "true" }));

        var result = await toolkit.ExecuteAsync("run", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Esecuzione dello strumento fallita.", result.Error);
        Assert.DoesNotContain("secret-token", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("ERR-0301", result.Metadata!["errorCode"]);
    }

    [Fact]
    public async Task Slack_UnknownAction_ReturnsStructuredSafeError()
    {
        var toolkit = new SlackToolkit(new ToolkitConfig("slack", new Dictionary<string, string> { ["token"] = "test-token" }));

        var result = await toolkit.ExecuteAsync("unsupported_action", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Azione del toolkit non supportata.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
        Assert.Equal("Tool", result.Metadata!["category"]);
        Assert.False(result.Metadata.ContainsKey("toolkitId"));
    }

    [Fact]
    public async Task EmailSmtp_MissingTo_ReturnsStructuredSafeError()
    {
        var toolkit = new EmailSmtpToolkit(new ToolkitConfig("email_smtp", new Dictionary<string, string>
        {
            ["host"] = "smtp.example.test",
            ["username"] = "agent@example.test"
        }));

        var result = await toolkit.ExecuteAsync("send_email", new Dictionary<string, object?> { ["subject"] = "s" }, default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Parametri del toolkit non validi.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
        Assert.Equal("Tool", result.Metadata!["category"]);
        Assert.False(result.Metadata.ContainsKey("parameterName"));
    }

    [Fact]
    public async Task Webhook_MissingUrl_ReturnsStructuredSafeError()
    {
        var toolkit = new WebhookToolkit();

        var result = await toolkit.ExecuteAsync("post", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Parametri del toolkit non validi.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
        Assert.Equal("Tool", result.Metadata!["category"]);
        Assert.False(result.Metadata.ContainsKey("parameterName"));
    }

    [Fact]
    public async Task GitHub_UnknownAction_ReturnsStructuredSafeError()
    {
        var toolkit = new GitHubToolkit();

        var result = await toolkit.ExecuteAsync("list_repos", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Azione del toolkit non supportata.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
        Assert.Equal("Tool", result.Metadata!["category"]);
    }

    [Fact]
    public async Task Docker_MissingImage_ReturnsStructuredSafeError()
    {
        var toolkit = new DockerToolkit();

        var result = await toolkit.ExecuteAsync("pull", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Parametri del toolkit non validi.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
        Assert.Equal("Tool", result.Metadata!["category"]);
    }

    [Fact]
    public async Task Helm_MissingRelease_ReturnsStructuredSafeError()
    {
        var toolkit = new HelmToolkit();

        var result = await toolkit.ExecuteAsync("upgrade", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Parametri del toolkit non validi.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
        Assert.Equal("Tool", result.Metadata!["category"]);
    }

    [Fact]
    public async Task YahooFinance_UnknownAction_ReturnsStructuredSafeError()
    {
        var toolkit = new YahooFinanceToolkit();

        var result = await toolkit.ExecuteAsync("get_quote", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Azione del toolkit non supportata.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
        Assert.Equal("Tool", result.Metadata!["category"]);
    }

    [Fact]
    public async Task Polygon_AggsMissingTo_ReturnsStructuredSafeError()
    {
        var toolkit = new PolygonToolkit(new ToolkitConfig("polygon", new Dictionary<string, string> { ["api_key"] = "test" }));

        var result = await toolkit.ExecuteAsync("aggs", new Dictionary<string, object?> { ["symbol"] = "AAPL" }, default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Parametri del toolkit non validi.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
        Assert.Equal("Tool", result.Metadata!["category"]);
    }

    [Fact]
    public async Task Stripe_GetCustomerMissingId_ReturnsStructuredSafeError()
    {
        var toolkit = new StripeToolkit(new ToolkitConfig("stripe", new Dictionary<string, string> { ["api_key"] = "test" }));

        var result = await toolkit.ExecuteAsync("get_customer", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Parametri del toolkit non validi.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
        Assert.Equal("Tool", result.Metadata!["category"]);
    }

    [Fact]
    public async Task Steam_UnknownAction_ReturnsStructuredSafeError()
    {
        var toolkit = new SteamToolkit();

        var result = await toolkit.ExecuteAsync("get_player_summary", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Azione del toolkit non supportata.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
        Assert.Equal("Tool", result.Metadata!["category"]);
    }

    [Fact]
    public async Task Twitch_MissingClientSecret_ReturnsStructuredSafeError()
    {
        var toolkit = new TwitchToolkit(new ToolkitConfig("twitch", new Dictionary<string, string> { ["client_id"] = "test" }));

        var result = await toolkit.ExecuteAsync("get_streams", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Configurazione obbligatoria del toolkit mancante.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-1301", result.Metadata!["errorCode"]);
        Assert.Equal("Config", result.Metadata!["category"]);
    }

    [Fact]
    public async Task Spotify_GetTrackMissingId_ReturnsStructuredSafeError()
    {
        var toolkit = new SpotifyToolkit(new ToolkitConfig("spotify", new Dictionary<string, string> { ["access_token"] = "test" }));

        var result = await toolkit.ExecuteAsync("get_track", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Parametri del toolkit non validi.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
        Assert.Equal("Tool", result.Metadata!["category"]);
    }

    [Fact]
    public async Task Dalle_UnknownAction_ReturnsStructuredSafeError()
    {
        var toolkit = new DalleToolkit(new ToolkitConfig("dalle", new Dictionary<string, string> { ["api_key"] = "test" }));

        var result = await toolkit.ExecuteAsync("generate_image", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Azione del toolkit non supportata.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
        Assert.Equal("Tool", result.Metadata!["category"]);
    }

    [Fact]
    public async Task Whisper_MissingFile_ReturnsStructuredSafeError()
    {
        var toolkit = new WhisperToolkit(new ToolkitConfig("whisper", new Dictionary<string, string> { ["api_key"] = "test" }));

        var result = await toolkit.ExecuteAsync("transcribe", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Parametri del toolkit non validi.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
        Assert.Equal("Tool", result.Metadata!["category"]);
    }

    [Fact]
    public async Task ElevenLabs_UnknownAction_ReturnsStructuredSafeError()
    {
        var toolkit = new ElevenLabsToolkit(new ToolkitConfig("elevenlabs", new Dictionary<string, string> { ["api_key"] = "test" }));

        var result = await toolkit.ExecuteAsync("text_to_speech", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Azione del toolkit non supportata.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
        Assert.Equal("Tool", result.Metadata!["category"]);
    }

    [Fact]
    public async Task AwsLambda_MissingFunctionName_ReturnsStructuredSafeError()
    {
        var toolkit = new AwsLambdaToolkit();

        var result = await toolkit.ExecuteAsync("invoke", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Parametri del toolkit non validi.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
        Assert.Equal("Tool", result.Metadata!["category"]);
    }

    [Fact]
    public async Task Cloudflare_UnknownAction_ReturnsStructuredSafeError()
    {
        var toolkit = new CloudflareToolkit(new ToolkitConfig("cloudflare", new Dictionary<string, string> { ["api_token"] = "test" }));

        var result = await toolkit.ExecuteAsync("unsupported_action", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Azione del toolkit non supportata.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
        Assert.Equal("Tool", result.Metadata!["category"]);
    }

    [Fact]
    public async Task PandasLike_UnknownAction_ReturnsStructuredSafeError()
    {
        var toolkit = new PandasLikeToolkit();

        var result = await toolkit.ExecuteAsync("query", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Azione del toolkit non supportata.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
        Assert.Equal("Tool", result.Metadata!["category"]);
    }

    [Fact]
    public async Task JsonData_UnknownAction_ReturnsStructuredSafeError()
    {
        var toolkit = new JsonDataToolkit();

        var result = await toolkit.ExecuteAsync("read", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Azione del toolkit non supportata.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
        Assert.Equal("Tool", result.Metadata!["category"]);
    }

    [Fact]
    public async Task LocalFile_UnknownAction_ReturnsStructuredSafeError()
    {
        var toolkit = new LocalFileToolkit();

        var result = await toolkit.ExecuteAsync("compress", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Azione del toolkit non supportata.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
        Assert.Equal("Tool", result.Metadata!["category"]);
    }

    [Fact]
    public async Task Compression_UnknownAction_ReturnsStructuredSafeError()
    {
        var toolkit = new CompressionToolkit();

        var result = await toolkit.ExecuteAsync("compress", [], default);

        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("Azione del toolkit non supportata.", result.Error);
        Assert.NotNull(result.Metadata);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
        Assert.Equal("Tool", result.Metadata!["category"]);
    }

    private sealed class MissingSettingToolkit(ToolkitConfig? config) : ToolkitBase(config)
    {
        public override string Id => "missing-setting";
        public override string Name => "Missing Setting Toolkit";
        public override ToolkitCategory Category => ToolkitCategory.Development;
        public override bool IsConfigured => true;

        protected override Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> parameters, CancellationToken ct)
        {
            _ = GetRequiredSetting("api_key");
            return Task.FromResult(ToolkitResult.Success());
        }
    }

    private sealed class MissingParameterToolkit(ToolkitConfig? config) : ToolkitBase(config)
    {
        public override string Id => "missing-param";
        public override string Name => "Missing Parameter Toolkit";
        public override ToolkitCategory Category => ToolkitCategory.Development;
        public override bool IsConfigured => true;

        protected override Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> parameters, CancellationToken ct)
        {
            _ = GetRequiredParam<string>(parameters, "query");
            return Task.FromResult(ToolkitResult.Success());
        }
    }

    private sealed class RawFailureToolkit(ToolkitConfig? config) : ToolkitBase(config)
    {
        public override string Id => "raw-failure";
        public override string Name => "Raw Failure Toolkit";
        public override ToolkitCategory Category => ToolkitCategory.Development;
        public override bool IsConfigured => true;

        protected override Task<ToolkitResult> ExecuteCoreAsync(string action, Dictionary<string, object?> parameters, CancellationToken ct)
            => throw new InvalidOperationException("secret-token leaked failure");
    }
}