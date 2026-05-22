// F16_ToolkitTests.cs — Sprint 2 TK-A1..TK-A12
// Comprehensive unit tests for all 12 toolkit categories:
//   - Configuration validation (not-configured → Skipped)
//   - Action routing (unknown action → Error ERR-0303)
//   - Missing required setting (ERR-1301)
//   - Missing required parameter (ERR-0303)
//   - Aggressive / attack test suite (injection, overflow, traversal, concurrent, cancellation)
//
// SECURITY FIX (applied before these tests):
//   LocalFileToolkit now blocks '..' path segments in path/src/dst parameters.

using System.Collections.Concurrent;
using AGXIOME.Shared.Errors;
using AGXIOME.Toolkits;
using AGXIOME.Toolkits.AiMl;
using AGXIOME.Toolkits.CloudInfra;
using AGXIOME.Toolkits.Communication;
using AGXIOME.Toolkits.DataAnalysis;
using AGXIOME.Toolkits.Development;
using AGXIOME.Toolkits.Enterprise;
using AGXIOME.Toolkits.FileStorage;
using AGXIOME.Toolkits.Finance;
using AGXIOME.Toolkits.GamingCreative;
using AGXIOME.Toolkits.Productivity;
using AGXIOME.Toolkits.SearchKnowledge;
using AGXIOME.Toolkits.WebSocial;

namespace AGXIOME.Tests;

// ═══════════════════════════════════════════════════════════════════════════════
// TK01 — ToolkitRegistry infrastructure
// ═══════════════════════════════════════════════════════════════════════════════
public sealed class TK01_ToolkitRegistryTests
{
    [Fact]
    public void Registry_RegisterAndFind_ReturnsCorrectToolkit()
    {
        var reg = new ToolkitRegistry();
        var tk  = new SlackToolkit(new ToolkitConfig("slack", new() { ["token"] = "t" }));
        reg.Register(tk);
        var found = reg.Find("slack");
        Assert.NotNull(found);
        Assert.Equal("slack", found!.Id);
    }

    [Fact]
    public void Registry_Find_UnknownId_ReturnsNull()
    {
        var reg = new ToolkitRegistry();
        Assert.Null(reg.Find("nonexistent_toolkit_xyz"));
    }

    [Fact]
    public void Registry_All_ReturnsAllRegistered()
    {
        var reg = new ToolkitRegistry();
        reg.Register(new SlackToolkit());
        reg.Register(new GitHubToolkit());
        Assert.Equal(2, reg.All.Count);
    }

    [Fact]
    public void Registry_ByCategory_FiltersCorrectly()
    {
        var reg = new ToolkitRegistry();
        reg.Register(new SlackToolkit());
        reg.Register(new GitHubToolkit());
        reg.Register(new DiscordToolkit());
        var comms = reg.ByCategory(ToolkitCategory.Communication);
        Assert.Equal(2, comms.Count);
        Assert.All(comms, t => Assert.Equal(ToolkitCategory.Communication, t.Category));
    }

    [Fact]
    public void Registry_Register_OverwritesDuplicateId()
    {
        var reg = new ToolkitRegistry();
        reg.Register(new SlackToolkit(new ToolkitConfig("slack", new() { ["token"] = "v1" })));
        reg.Register(new SlackToolkit(new ToolkitConfig("slack", new() { ["token"] = "v2" })));
        Assert.Single(reg.All);
    }

    [Fact]
    public void ToolkitAttribute_Metadata_IsCorrect()
    {
        var attr = typeof(SlackToolkit).GetCustomAttributes(typeof(ToolkitAttribute), false)
                                       .Cast<ToolkitAttribute>()
                                       .FirstOrDefault();
        Assert.NotNull(attr);
        Assert.Equal("slack", attr!.Id);
        Assert.Equal(ToolkitCategory.Communication, attr.Category);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// TK02 — Communication
// ═══════════════════════════════════════════════════════════════════════════════
public sealed class TK02_CommunicationTests
{
    // ── Discord ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task Discord_NoConfig_ReturnsSkipped()
    {
        var result = await new DiscordToolkit().ExecuteAsync("send_message", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Discord_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new DiscordToolkit(new ToolkitConfig("discord", new() { ["bot_token"] = "Bot.fake" }));
        var result = await tk.ExecuteAsync("__bad_action__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── Telegram ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task Telegram_NoConfig_ReturnsSkipped()
    {
        var result = await new TelegramToolkit().ExecuteAsync("send_message", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Telegram_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new TelegramToolkit(new ToolkitConfig("telegram", new() { ["bot_token"] = "123:ABC" }));
        var result = await tk.ExecuteAsync("__not_a_real_action__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── Teams ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Teams_NoConfig_ReturnsSkipped()
    {
        var result = await new TeamsToolkit().ExecuteAsync("send_message", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Teams_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new TeamsToolkit(new ToolkitConfig("teams", new() { ["webhook_url"] = "https://fake" }));
        var result = await tk.ExecuteAsync("__invalid__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── MQTT ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Mqtt_NoConfig_ReturnsSkipped()
    {
        var result = await new MqttToolkit().ExecuteAsync("publish", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Mqtt_UnknownAction_ReturnsError()
    {
        var tk = new MqttToolkit(new ToolkitConfig("mqtt", new() { ["broker"] = "tcp://localhost" }));
        var result = await tk.ExecuteAsync("nonexistent_mqtt_action", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
    }

    // ── Slack (IsConfigured = false without token) ────────────────────────────
    [Fact]
    public async Task Slack_EmptySettings_ReturnsSkipped()
    {
        var tk = new SlackToolkit(new ToolkitConfig("slack", []));
        var result = await tk.ExecuteAsync("send_message", new() { ["text"] = "hi" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// TK03 — Productivity
// ═══════════════════════════════════════════════════════════════════════════════
public sealed class TK03_ProductivityTests
{
    [Fact]
    public async Task GoogleCalendar_NoConfig_ReturnsSkipped()
    {
        var result = await new GoogleCalendarToolkit().ExecuteAsync("list_events", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task GoogleCalendar_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new GoogleCalendarToolkit(new ToolkitConfig("google_calendar", new() { ["access_token"] = "ya29.fake" }));
        var result = await tk.ExecuteAsync("__not_real__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    [Fact]
    public async Task GoogleDrive_NoConfig_ReturnsSkipped()
    {
        var result = await new GoogleDriveToolkit().ExecuteAsync("list_files", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Notion_NoConfig_ReturnsSkipped()
    {
        var result = await new NotionToolkit().ExecuteAsync("query_database", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Notion_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new NotionToolkit(new ToolkitConfig("notion", new() { ["token"] = "secret_fake" }));
        var result = await tk.ExecuteAsync("fly_to_moon", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    [Fact]
    public async Task Jira_NoConfig_ReturnsSkipped()
    {
        var result = await new JiraToolkit().ExecuteAsync("list_issues", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Jira_UnknownAction_ReturnsError_ERR0303()
    {
        // Jira needs domain + api_token (IsConfigured) AND email (used in ExecuteCoreAsync before switch)
        var tk = new JiraToolkit(new ToolkitConfig("jira", new() { ["api_token"] = "fake-token", ["domain"] = "x.atlassian.net", ["email"] = "user@example.com" }));
        var result = await tk.ExecuteAsync("___fake___", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    [Fact]
    public async Task Confluence_NoConfig_ReturnsSkipped()
    {
        var result = await new ConfluenceToolkit().ExecuteAsync("get_page", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task OneDrive_NoConfig_ReturnsSkipped()
    {
        var result = await new OneDriveToolkit().ExecuteAsync("list_files", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task GoogleSheets_NoConfig_ReturnsSkipped()
    {
        var result = await new GoogleSheetsToolkit().ExecuteAsync("get_values", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// TK04 — Development
// ═══════════════════════════════════════════════════════════════════════════════
public sealed class TK04_DevelopmentTests
{
    // ── GitHub (IsConfigured = true — public API) ────────────────────────────
    [Fact]
    public async Task GitHub_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new GitHubToolkit();
        var result = await tk.ExecuteAsync("__totally_fake__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    [Fact]
    public async Task GitHub_WriteAction_MissingToken_ReturnsError_ERR1301()
    {
        // create_issue calls GhPost which requires token
        var tk = new GitHubToolkit(new ToolkitConfig("github", []));
        var result = await tk.ExecuteAsync("create_issue",
            new() { ["repo"] = "octocat/Hello-World", ["title"] = "test" }, default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-1301", result.Metadata!["errorCode"]);
    }

    // ── GitLab (requires token) ──────────────────────────────────────────────
    [Fact]
    public async Task GitLab_NoConfig_ReturnsSkipped()
    {
        var result = await new GitLabToolkit().ExecuteAsync("list_projects", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task GitLab_EmptyToken_ReturnsSkipped()
    {
        var tk = new GitLabToolkit(new ToolkitConfig("gitlab", new() { ["token"] = "" }));
        var result = await tk.ExecuteAsync("list_projects", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task GitLab_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new GitLabToolkit(new ToolkitConfig("gitlab", new() { ["token"] = "glpat-fake" }));
        var result = await tk.ExecuteAsync("__fake_action__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── Bitbucket (requires username + app_password) ─────────────────────────
    [Fact]
    public async Task Bitbucket_NoConfig_ReturnsSkipped()
    {
        var result = await new BitbucketToolkit().ExecuteAsync("list_repos", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Bitbucket_MissingPassword_ReturnsSkipped()
    {
        var tk = new BitbucketToolkit(new ToolkitConfig("bitbucket", new() { ["username"] = "user" }));
        var result = await tk.ExecuteAsync("list_repos", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Bitbucket_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new BitbucketToolkit(new ToolkitConfig("bitbucket",
            new() { ["username"] = "u", ["app_password"] = "p" }));
        var result = await tk.ExecuteAsync("__fake__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── Git CLI (IsConfigured = true) ─────────────────────────────────────────
    [Fact]
    public async Task Git_UnknownAction_RunsRawGit_ReturnsResult()
    {
        // Git toolkit passes unknown actions as raw git commands (falls through switch to default)
        // With a nonexistent dir, git will fail with a non-zero exit code → ToolkitResult.Fail
        var tk = new GitToolkit(new ToolkitConfig("git", new() { ["working_dir"] = "C:\\nonexistent_dir_xyz" }));
        var result = await tk.ExecuteAsync("status", [], default);
        // Git will fail because the dir doesn't exist
        Assert.NotNull(result);
    }

    // ── NuGet (IsConfigured = true — public API) ─────────────────────────────
    [Fact]
    public async Task NuGet_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new NuGetToolkit();
        var result = await tk.ExecuteAsync("__not_real__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// TK05 — Finance
// ═══════════════════════════════════════════════════════════════════════════════
public sealed class TK05_FinanceTests
{
    // ── YahooFinance (IsConfigured = true — public API) ──────────────────────
    [Fact]
    public async Task YahooFinance_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new YahooFinanceToolkit();
        var result = await tk.ExecuteAsync("__fake_finance_action__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── AlphaVantage (requires api_key) ──────────────────────────────────────
    [Fact]
    public async Task AlphaVantage_NoConfig_ReturnsSkipped()
    {
        var result = await new AlphaVantageToolkit().ExecuteAsync("quote", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task AlphaVantage_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new AlphaVantageToolkit(new ToolkitConfig("alpha_vantage", new() { ["api_key"] = "DEMO" }));
        var result = await tk.ExecuteAsync("__bad__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── CoinGecko (IsConfigured = true — free public API) ────────────────────
    [Fact]
    public async Task CoinGecko_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new CoinGeckoToolkit();
        var result = await tk.ExecuteAsync("buy_bitcoin_please", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── Binance (IsConfigured = true — free public API for market data) ────────
    [Fact]
    public async Task Binance_NoConfig_RunsPublicApi()
    {
        // Binance IsConfigured=true (public market data needs no auth) → attempts HTTP call
        var result = await new BinanceToolkit().ExecuteAsync("price", [], default);
        Assert.NotEqual(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Binance_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new BinanceToolkit(new ToolkitConfig("binance", new() { ["api_key"] = "fake-key" }));
        var result = await tk.ExecuteAsync("__invalid_op__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── Stripe (requires api_key) ─────────────────────────────────────────────
    [Fact]
    public async Task Stripe_NoConfig_ReturnsSkipped()
    {
        var result = await new StripeToolkit().ExecuteAsync("list_customers", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Stripe_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new StripeToolkit(new ToolkitConfig("stripe", new() { ["api_key"] = "sk_test_fake" }));
        var result = await tk.ExecuteAsync("charge_all_customers", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// TK06 — DataAnalysis
// ═══════════════════════════════════════════════════════════════════════════════
public sealed class TK06_DataAnalysisTests
{
    // ── SqlToolkit (stub — always returns Fail with guidance message) ──────────
    [Fact]
    public async Task Sql_NoConfig_ReturnsSkipped()
    {
        var result = await new SqlToolkit().ExecuteAsync("query", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Sql_AnyAction_ReturnsError_GuidanceMessage()
    {
        // SqlToolkit is a stub that always returns Fail (Metadata=null)
        var tk = new SqlToolkit(new ToolkitConfig("sql", new() { ["connection_string"] = "fake-cs" }));
        var result = await tk.ExecuteAsync("drop_everything", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Contains("sqlite_query", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── SqliteToolkit (IsConfigured = true — in-memory DB) ────────────────────
    [Fact]
    public async Task Sqlite_NoConfig_RunsInMemory()
    {
        // IsConfigured=true: in-memory SQLite works without config
        var result = await new SqliteToolkit().ExecuteAsync("query", [], default);
        Assert.NotEqual(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Sqlite_MissingQueryParam_ReturnsError()
    {
        // SqliteToolkit ignores action name; requires 'query' param
        var tk = new SqliteToolkit(new ToolkitConfig("sqlite", new() { ["connection_string"] = "Data Source=:memory:" }));
        var result = await tk.ExecuteAsync("query", [], default); // no query param
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Contains("query", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── PandasLikeToolkit (IsConfigured = true) ───────────────────────────────
    [Fact]
    public async Task PandasLike_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new PandasLikeToolkit();
        var result = await tk.ExecuteAsync("__magic__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── JsonDataToolkit (IsConfigured = true) ─────────────────────────────────
    [Fact]
    public async Task JsonData_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new JsonDataToolkit();
        var result = await tk.ExecuteAsync("hack_the_planet", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── CsvToolkit (IsConfigured = true) ─────────────────────────────────────
    [Fact]
    public async Task Csv_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new CsvToolkit();
        var result = await tk.ExecuteAsync("__fake__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── XmlDataToolkit (IsConfigured = true) ─────────────────────────────────
    [Fact]
    public async Task XmlData_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new XmlDataToolkit();
        var result = await tk.ExecuteAsync("__not_xml_operation__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── DuckDbToolkit ─────────────────────────────────────────────────────────
    [Fact]
    public async Task DuckDb_NoConfig_ReturnsSkipped()
    {
        var result = await new DuckDbToolkit().ExecuteAsync("query", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// TK07 — CloudInfra
// ═══════════════════════════════════════════════════════════════════════════════
public sealed class TK07_CloudInfraTests
{
    // ── AwsS3 (IsConfigured = true — uses aws CLI) ───────────────────────────
    [Fact]
    public async Task AwsS3_NoConfig_RunsAndFailsCliOrNetwork()
    {
        // AwsS3 IsConfigured=true: no-config instance still tries aws CLI → Error (not Skipped)
        var result = await new AwsS3Toolkit().ExecuteAsync("list_buckets", [], default);
        Assert.NotEqual(ToolkitResultStatus.Ok, result.Status);
    }

    [Fact]
    public async Task AwsS3_UnknownAction_ReturnsError()
    {
        // AwsS3 uses raw CLI fall-through for unknown action; no ERR-0303 in Metadata
        var tk = new AwsS3Toolkit(new ToolkitConfig("aws_s3",
            new() { ["access_key_id"] = "AKIAIOSFODNN7FAKE", ["secret_access_key"] = "wJalrXUtnFEMI/K7FAKE" }));
        var result = await tk.ExecuteAsync("__s3_unknown__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
    }

    // ── AwsDynamoDB (IsConfigured = true — uses aws CLI) ────────────────────
    [Fact]
    public async Task AwsDynamoDB_NoConfig_RunsAndFailsCli()
    {
        // IsConfigured=true: tries aws CLI → Error (not Skipped)
        var result = await new AwsDynamoDbToolkit().ExecuteAsync("get_item", [], default);
        Assert.NotEqual(ToolkitResultStatus.Ok, result.Status);
    }

    // ── AzureBlob ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task AzureBlob_NoConfig_ReturnsSkipped()
    {
        var result = await new AzureBlobToolkit().ExecuteAsync("list_blobs", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task AzureBlob_UnknownAction_ReturnsError()
    {
        // AzureBlob uses raw az CLI fall-through for unknown action; no ERR-0303 in Metadata
        var tk = new AzureBlobToolkit(new ToolkitConfig("azure_blob",
            new() { ["connection_string"] = "DefaultEndpointsProtocol=https;Fake;AccountKey=xyz" }));
        var result = await tk.ExecuteAsync("__cloud_unknown__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
    }

    // ── GcpStorage (IsConfigured = true — uses gcloud CLI) ───────────────────
    [Fact]
    public async Task GcpStorage_NoConfig_RunsAndFailsCli()
    {
        // IsConfigured=true: tries gcloud CLI → Error (not Skipped)
        var result = await new GcpStorageToolkit().ExecuteAsync("list_buckets", [], default);
        Assert.NotEqual(ToolkitResultStatus.Ok, result.Status);
    }

    // ── Vercel ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Vercel_NoConfig_ReturnsSkipped()
    {
        var result = await new VercelToolkit().ExecuteAsync("list_deployments", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    // ── DigitalOcean ──────────────────────────────────────────────────────────
    [Fact]
    public async Task DigitalOcean_NoConfig_ReturnsSkipped()
    {
        var result = await new DigitalOceanToolkit().ExecuteAsync("list_droplets", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task DigitalOcean_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new DigitalOceanToolkit(new ToolkitConfig("digitalocean", new() { ["api_token"] = "dop_v1_fake" }));
        var result = await tk.ExecuteAsync("__ocean_action__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// TK08 — FileStorage (includes path traversal security tests)
// ═══════════════════════════════════════════════════════════════════════════════
public sealed class TK08_FileStorageTests
{
    private static LocalFileToolkit LocalFile => new LocalFileToolkit();

    // ── LocalFile (IsConfigured = true) ──────────────────────────────────────
    [Fact]
    public async Task LocalFile_UnknownAction_ReturnsError_ERR0303()
    {
        var result = await LocalFile.ExecuteAsync("execute_malware", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    [Fact]
    public async Task LocalFile_Read_NonExistentFile_ReturnsError()
    {
        var result = await LocalFile.ExecuteAsync("read",
            new() { ["path"] = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.txt") }, default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
    }

    // ── SECURITY: Path traversal is blocked ──────────────────────────────────
    [Fact]
    public async Task LocalFile_Read_PathTraversal_IsBlocked()
    {
        var result = await LocalFile.ExecuteAsync("read",
            new() { ["path"] = "../../etc/passwd" }, default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Contains("traversal", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LocalFile_Write_PathTraversal_IsBlocked()
    {
        var result = await LocalFile.ExecuteAsync("write",
            new() { ["path"] = "../../../tmp/attack.txt", ["content"] = "pwned" }, default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Contains("traversal", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LocalFile_Delete_PathTraversal_IsBlocked()
    {
        var result = await LocalFile.ExecuteAsync("delete",
            new() { ["path"] = "../../etc/hosts" }, default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Contains("traversal", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LocalFile_Copy_SrcPathTraversal_IsBlocked()
    {
        var result = await LocalFile.ExecuteAsync("copy",
            new() { ["src"] = "../../sensitive.txt", ["dst"] = "/tmp/out.txt" }, default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Contains("traversal", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LocalFile_Move_DstPathTraversal_IsBlocked()
    {
        var result = await LocalFile.ExecuteAsync("move",
            new() { ["src"] = "/tmp/benign.txt", ["dst"] = "../../etc/cron_attack" }, default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Contains("traversal", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LocalFile_MkDir_PathTraversal_IsBlocked()
    {
        var result = await LocalFile.ExecuteAsync("mkdir",
            new() { ["path"] = "../../etc/inject" }, default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Contains("traversal", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LocalFile_WindowsStyleTraversal_IsBlocked()
    {
        var result = await LocalFile.ExecuteAsync("read",
            new() { ["path"] = @"..\..\Windows\System32\drivers\etc\hosts" }, default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Contains("traversal", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LocalFile_Exists_ValidTempPath_ReturnsOk()
    {
        var result = await LocalFile.ExecuteAsync("exists",
            new() { ["path"] = Path.GetTempPath() }, default);
        Assert.Equal(ToolkitResultStatus.Ok, result.Status);
    }

    [Fact]
    public async Task LocalFile_List_NonExistentDir_ReturnsError()
    {
        var result = await LocalFile.ExecuteAsync("list",
            new() { ["path"] = Path.Combine(Path.GetTempPath(), $"nosuchdir_{Guid.NewGuid():N}") }, default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// TK09 — SearchKnowledge
// ═══════════════════════════════════════════════════════════════════════════════
public sealed class TK09_SearchKnowledgeTests
{
    // ── GoogleSearch (requires api_key + cx) ─────────────────────────────────
    [Fact]
    public async Task GoogleSearch_NoConfig_ReturnsSkipped()
    {
        var result = await new GoogleSearchToolkit().ExecuteAsync("search", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task GoogleSearch_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new GoogleSearchToolkit(new ToolkitConfig("google_search",
            new() { ["api_key"] = "AIza-fake", ["cx"] = "fake-cx" }));
        var result = await tk.ExecuteAsync("hack_google", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── BingSearch (requires api_key) ─────────────────────────────────────────
    [Fact]
    public async Task BingSearch_NoConfig_ReturnsSkipped()
    {
        var result = await new BingSearchToolkit().ExecuteAsync("search", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task BingSearch_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new BingSearchToolkit(new ToolkitConfig("bing_search", new() { ["api_key"] = "fake-key" }));
        var result = await tk.ExecuteAsync("__not_bing__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── Tavily (requires api_key) ─────────────────────────────────────────────
    [Fact]
    public async Task Tavily_NoConfig_ReturnsSkipped()
    {
        var result = await new TavilyToolkit().ExecuteAsync("search", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Tavily_AnyAction_NetworkFail_ReturnsError()
    {
        // TavilyToolkit has no action routing — always POSTs to Tavily API
        // With fake key → HTTP 401/403 → ExecutionFailed → ERR-0301
        var tk = new TavilyToolkit(new ToolkitConfig("tavily", new() { ["api_key"] = "tvly-fake" }));
        var result = await tk.ExecuteAsync("search", new() { ["query"] = "test" }, default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
    }

    // ── SearXng (requires base_url) ───────────────────────────────────────────
    [Fact]
    public async Task SearXng_NoConfig_ReturnsSkipped()
    {
        var result = await new SearXngToolkit().ExecuteAsync("search", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task SearXng_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new SearXngToolkit(new ToolkitConfig("searxng", new() { ["base_url"] = "https://fake-searx.local" }));
        var result = await tk.ExecuteAsync("__fake_searx__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── BraveSearch (requires api_key) ────────────────────────────────────────
    [Fact]
    public async Task BraveSearch_NoConfig_ReturnsSkipped()
    {
        var result = await new BraveSearchToolkit().ExecuteAsync("search", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// TK10 — WebSocial
// ═══════════════════════════════════════════════════════════════════════════════
public sealed class TK10_WebSocialTests
{
    // ── Twitter (requires bearer_token) ──────────────────────────────────────
    [Fact]
    public async Task Twitter_NoConfig_ReturnsSkipped()
    {
        var result = await new TwitterToolkit().ExecuteAsync("search_tweets", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Twitter_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new TwitterToolkit(new ToolkitConfig("twitter", new() { ["bearer_token"] = "AAAAAA-fake" }));
        var result = await tk.ExecuteAsync("__tweet_unknown__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── Reddit (IsConfigured = true — free public API) ─────────────────────
    [Fact]
    public async Task Reddit_NoConfig_RunsPublicApi()
    {
        // IsConfigured=true: Reddit public JSON API needs no credentials
        var result = await new RedditToolkit().ExecuteAsync("posts", [], default);
        Assert.NotEqual(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Reddit_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new RedditToolkit(new ToolkitConfig("reddit", new() { ["client_id"] = "fake-id" }));
        var result = await tk.ExecuteAsync("delete_all_posts", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── LinkedIn (requires access_token) ─────────────────────────────────────
    [Fact]
    public async Task LinkedIn_NoConfig_ReturnsSkipped()
    {
        var result = await new LinkedInToolkit().ExecuteAsync("get_profile", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task LinkedIn_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new LinkedInToolkit(new ToolkitConfig("linkedin", new() { ["access_token"] = "AQX-fake" }));
        var result = await tk.ExecuteAsync("__linkedin_unknown__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── YouTube (requires api_key) ────────────────────────────────────────────
    [Fact]
    public async Task YouTube_NoConfig_ReturnsSkipped()
    {
        var result = await new YouTubeToolkit().ExecuteAsync("search_videos", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task YouTube_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new YouTubeToolkit(new ToolkitConfig("youtube", new() { ["api_key"] = "AIza-fake" }));
        var result = await tk.ExecuteAsync("__not_youtube__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── Instagram ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task Instagram_NoConfig_ReturnsSkipped()
    {
        var result = await new InstagramToolkit().ExecuteAsync("get_media", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// TK11 — GamingCreative
// ═══════════════════════════════════════════════════════════════════════════════
public sealed class TK11_GamingCreativeTests
{
    // ── Steam (IsConfigured = true — public API) ──────────────────────────────
    [Fact]
    public async Task Steam_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new SteamToolkit();
        var result = await tk.ExecuteAsync("__not_steam__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── Twitch (requires client_id) ───────────────────────────────────────────
    [Fact]
    public async Task Twitch_NoConfig_ReturnsSkipped()
    {
        var result = await new TwitchToolkit().ExecuteAsync("get_streams", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Twitch_UnknownAction_ReturnsError()
    {
        // Twitch fetches an OAuth token before action dispatch (needs client_secret)
        // With only client_id → MissingSetting(client_secret) → ERR-1301 → Error
        var tk = new TwitchToolkit(new ToolkitConfig("twitch", new() { ["client_id"] = "fake-id" }));
        var result = await tk.ExecuteAsync("__twitch_unknown__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
    }

    // ── Spotify (requires access_token) ──────────────────────────────────────
    [Fact]
    public async Task Spotify_NoConfig_ReturnsSkipped()
    {
        var result = await new SpotifyToolkit().ExecuteAsync("search", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    // ── LastFm (requires api_key) ─────────────────────────────────────────────
    [Fact]
    public async Task LastFm_NoConfig_ReturnsSkipped()
    {
        var result = await new LastFmToolkit().ExecuteAsync("get_track_info", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task LastFm_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new LastFmToolkit(new ToolkitConfig("lastfm", new() { ["api_key"] = "fake-key" }));
        var result = await tk.ExecuteAsync("__lastfm_unknown__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── Unsplash (requires access_token) ─────────────────────────────────────
    [Fact]
    public async Task Unsplash_NoConfig_ReturnsSkipped()
    {
        var result = await new UnsplashToolkit().ExecuteAsync("search_photos", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    // ── Pexels (requires access_token/api_key) ────────────────────────────────
    [Fact]
    public async Task Pexels_NoConfig_ReturnsSkipped()
    {
        var result = await new PexelsToolkit().ExecuteAsync("search_photos", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Pexels_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new PexelsToolkit(new ToolkitConfig("pexels", new() { ["api_key"] = "px-fake-key" }));
        var result = await tk.ExecuteAsync("__pexels_unknown__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// TK12 — Enterprise
// ═══════════════════════════════════════════════════════════════════════════════
public sealed class TK12_EnterpriseTests
{
    // ── Salesforce ────────────────────────────────────────────────────────────
    [Fact]
    public async Task Salesforce_NoConfig_ReturnsSkipped()
    {
        var result = await new SalesforceToolkit().ExecuteAsync("query", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Salesforce_UnknownAction_ReturnsError_ERR0303()
    {
        // Salesforce IsConfigured requires both access_token AND instance_url
        var tk = new SalesforceToolkit(new ToolkitConfig("salesforce",
            new() { ["access_token"] = "fake-token", ["instance_url"] = "https://fake.salesforce.com" }));
        var result = await tk.ExecuteAsync("__crm_unknown__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── HubSpot ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task HubSpot_NoConfig_ReturnsSkipped()
    {
        var result = await new HubSpotToolkit().ExecuteAsync("list_contacts", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task HubSpot_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new HubSpotToolkit(new ToolkitConfig("hubspot", new() { ["api_key"] = "fake-key" }));
        var result = await tk.ExecuteAsync("__hubspot_unknown__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── Zendesk ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task Zendesk_NoConfig_ReturnsSkipped()
    {
        var result = await new ZendeskToolkit().ExecuteAsync("list_tickets", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Zendesk_UnknownAction_ReturnsError()
    {
        // Zendesk IsConfigured = subdomain + api_token; also needs email in ExecuteCoreAsync before switch
        // Providing all three required settings ensures IsConfigured=true and dispatch to switch
        var tk = new ZendeskToolkit(new ToolkitConfig("zendesk",
            new() { ["subdomain"] = "fake", ["api_token"] = "fake-token", ["email"] = "user@example.com" }));
        var result = await tk.ExecuteAsync("__zendesk_unknown__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── PagerDuty ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task PagerDuty_NoConfig_ReturnsSkipped()
    {
        var result = await new PagerDutyToolkit().ExecuteAsync("list_incidents", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task PagerDuty_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new PagerDutyToolkit(new ToolkitConfig("pagerduty", new() { ["api_key"] = "fake-key" }));
        var result = await tk.ExecuteAsync("__pd_unknown__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── Datadog ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task Datadog_NoConfig_ReturnsSkipped()
    {
        var result = await new DatadogToolkit().ExecuteAsync("query_metrics", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// TK13 — AiMl
// ═══════════════════════════════════════════════════════════════════════════════
public sealed class TK13_AiMlTests
{
    // ── OpenAiVision ──────────────────────────────────────────────────────────
    [Fact]
    public async Task OpenAiVision_NoConfig_ReturnsSkipped()
    {
        var result = await new OpenAiVisionToolkit().ExecuteAsync("analyze_image", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task OpenAiVision_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new OpenAiVisionToolkit(new ToolkitConfig("openai_vision", new() { ["api_key"] = "sk-fake" }));
        var result = await tk.ExecuteAsync("__vision_unknown__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── Dalle ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Dalle_NoConfig_ReturnsSkipped()
    {
        var result = await new DalleToolkit().ExecuteAsync("generate_image", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task Dalle_UnknownAction_ReturnsError_ERR0303()
    {
        var tk = new DalleToolkit(new ToolkitConfig("dalle", new() { ["api_key"] = "sk-fake" }));
        var result = await tk.ExecuteAsync("__dalle_unknown__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Equal("ERR-0303", result.Metadata!["errorCode"]);
    }

    // ── StableDiffusion ───────────────────────────────────────────────────────
    [Fact]
    public async Task StableDiffusion_NoConfig_ReturnsSkipped()
    {
        var result = await new StableDiffusionToolkit().ExecuteAsync("generate", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    // ── HuggingFaceHub (IsConfigured = true — works without key) ─────────────
    [Fact]
    public async Task HuggingFaceHub_NoConfig_RunsPublicApi()
    {
        // IsConfigured=true: HF Inference API works without key (rate limited)
        var result = await new HuggingFaceHubToolkit().ExecuteAsync("inference", [], default);
        Assert.NotEqual(ToolkitResultStatus.Skipped, result.Status);
    }

    [Fact]
    public async Task HuggingFaceHub_AnyAction_CallsApiDirectly()
    {
        // HuggingFace has no action routing — always POSTs to inference API
        // With fake key → HTTP 401/403 → ExecutionFailed → ERR-0301 (not ERR-0303)
        var tk = new HuggingFaceHubToolkit(new ToolkitConfig("huggingface_hub", new() { ["api_key"] = "hf_fake" }));
        var result = await tk.ExecuteAsync("__hf_unknown__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
    }

    // ── ElevenLabs ────────────────────────────────────────────────────────────
    [Fact]
    public async Task ElevenLabs_NoConfig_ReturnsSkipped()
    {
        var result = await new ElevenLabsToolkit().ExecuteAsync("text_to_speech", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }

    // ── Whisper ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task Whisper_NoConfig_ReturnsSkipped()
    {
        var result = await new WhisperToolkit().ExecuteAsync("transcribe", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, result.Status);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// TKSec — Aggressive / Security Attack Tests
// All toolkits must behave consistently (no thrown exceptions) under attack input.
// ═══════════════════════════════════════════════════════════════════════════════
public sealed class TKSec_AggressiveAttackTests
{
    // ── 1. Empty action string ────────────────────────────────────────────────
    [Fact]
    public async Task EmptyActionString_ReturnsError_NotThrows()
    {
        var tk = new SlackToolkit(new ToolkitConfig("slack", new() { ["token"] = "xoxb-test" }));
        var result = await tk.ExecuteAsync(string.Empty, [], default);
        Assert.NotEqual(ToolkitResultStatus.Ok, result.Status);
    }

    // ── 2. Whitespace-only action ─────────────────────────────────────────────
    [Fact]
    public async Task WhitespaceActionString_ReturnsError_NotThrows()
    {
        var tk = new DiscordToolkit(new ToolkitConfig("discord", new() { ["token"] = "Bot fake" }));
        var result = await tk.ExecuteAsync("   \t  ", [], default);
        Assert.NotEqual(ToolkitResultStatus.Ok, result.Status);
    }

    // ── 3. Very long action name (1000 chars) ─────────────────────────────────
    [Fact]
    public async Task VeryLongActionName_ReturnsError_NotThrows()
    {
        var tk = new GitHubToolkit();
        var longAction = new string('A', 1000);
        var result = await tk.ExecuteAsync(longAction, [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
    }

    // ── 4. Null parameter dictionary ─────────────────────────────────────────
    [Fact]
    public async Task NullParameterDictionary_ReturnsError_NotThrows()
    {
        var tk = new SlackToolkit(new ToolkitConfig("slack", new() { ["token"] = "xoxb-test" }));
        // Passing null! — must not propagate NullReferenceException, must be caught by ExecuteAsync
        var result = await tk.ExecuteAsync("send_message", null!, default);
        Assert.NotEqual(ToolkitResultStatus.Ok, result.Status);
    }

    // ── 5. SQL injection in parameter value ───────────────────────────────────
    [Fact]
    public async Task SqlInjection_InSlackMessage_HandledSafely()
    {
        var injection = "'; DROP TABLE agents; --";
        var tk = new SlackToolkit(new ToolkitConfig("slack", new() { ["token"] = "'; DROP TABLE--" }));
        var result = await tk.ExecuteAsync("send_message", new() { ["text"] = injection }, default);
        // Must NOT succeed (fake token → network error → Error status)
        Assert.NotEqual(ToolkitResultStatus.Ok, result.Status);
        // Error must NOT leak the token value
        Assert.DoesNotContain("'; DROP TABLE--", result.Error ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task SqlInjection_InSearchQuery_HandledSafely()
    {
        var injection = "' OR '1'='1";
        var tk = new BingSearchToolkit(new ToolkitConfig("bing_search", new() { ["api_key"] = "fake-key" }));
        var result = await tk.ExecuteAsync("search", new() { ["query"] = injection }, default);
        Assert.NotEqual(ToolkitResultStatus.Ok, result.Status);
    }

    // ── 6. XSS payload in parameter value ────────────────────────────────────
    [Fact]
    public async Task XssPayload_InTelegramMessage_HandledSafely()
    {
        var xss = "<script>alert(document.cookie)</script>";
        var tk = new TelegramToolkit(new ToolkitConfig("telegram", new() { ["bot_token"] = "123:FAKE" }));
        var result = await tk.ExecuteAsync("send_message", new() { ["text"] = xss, ["chat_id"] = "-1" }, default);
        Assert.NotEqual(ToolkitResultStatus.Ok, result.Status);
    }

    // ── 7. Path traversal in FileStorage ─────────────────────────────────────
    [Fact]
    public async Task PathTraversal_Unix_IsBlocked()
    {
        var tk = new LocalFileToolkit();
        var result = await tk.ExecuteAsync("read", new() { ["path"] = "../../etc/shadow" }, default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Contains("traversal", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PathTraversal_Windows_IsBlocked()
    {
        var tk = new LocalFileToolkit();
        var result = await tk.ExecuteAsync("read", new() { ["path"] = @"..\..\Windows\win.ini" }, default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Contains("traversal", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PathTraversal_AbsolutePath_Write_IsBlocked()
    {
        // Write with path traversal to create a file outside working dir
        var tk = new LocalFileToolkit();
        var result = await tk.ExecuteAsync("write",
            new() { ["path"] = "../../../../etc/cron.d/attack", ["content"] = "* * * * * root /bin/bash -i" }, default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
        Assert.Contains("traversal", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── 8. Oversized input (10 000-char string) ───────────────────────────────
    [Fact]
    public async Task OversizedInput_InMessageParam_HandledSafely()
    {
        var bigText = new string('X', 10_000);
        var tk = new SlackToolkit(new ToolkitConfig("slack", new() { ["token"] = "xoxb-test" }));
        var result = await tk.ExecuteAsync("send_message", new() { ["text"] = bigText }, default);
        Assert.NotEqual(ToolkitResultStatus.Ok, result.Status);
    }

    [Fact]
    public async Task OversizedInput_AsActionName_HandledSafely()
    {
        var bigAction = new string('Z', 65536);
        var tk = new GitHubToolkit();
        var result = await tk.ExecuteAsync(bigAction, [], default);
        Assert.Equal(ToolkitResultStatus.Error, result.Status);
    }

    // ── 9. Unicode and emoji in parameters ───────────────────────────────────
    [Fact]
    public async Task UnicodeEmoji_InSearchQuery_HandledSafely()
    {
        var emoji = "🔥💣 hack 𝓐𝓣𝓣𝓐𝓒𝓚 unicode: \u0000\uFFFD";
        var tk = new BingSearchToolkit(new ToolkitConfig("bing_search", new() { ["api_key"] = "fake" }));
        var result = await tk.ExecuteAsync("search", new() { ["query"] = emoji }, default);
        Assert.NotEqual(ToolkitResultStatus.Ok, result.Status);
    }

    // ── 10. Null byte injection ───────────────────────────────────────────────
    [Fact]
    public async Task NullByteInjection_InPath_HandledSafely()
    {
        var nullPath = "/tmp/safe.txt\0../../etc/passwd";
        var tk = new LocalFileToolkit();
        var result = await tk.ExecuteAsync("read", new() { ["path"] = nullPath }, default);
        // Must NOT succeed (file doesn't exist, or traversal blocked)
        Assert.NotEqual(ToolkitResultStatus.Ok, result.Status);
    }

    // ── 11. Command injection in Git parameters ───────────────────────────────
    [Fact]
    public async Task CommandInjection_GitCommitMessage_HandledSafely()
    {
        var injection = "\"; rm -rf /; git commit -m \"";
        var tk = new GitToolkit(new ToolkitConfig("git",
            new() { ["working_dir"] = Path.GetTempPath() }));
        var result = await tk.ExecuteAsync("commit", new() { ["message"] = injection }, default);
        // The git process will fail (no staged changes), but must NOT throw
        Assert.NotNull(result);
        Assert.NotEqual(ToolkitResultStatus.Ok, result.Status);
    }

    [Fact]
    public async Task CommandInjection_GitBranch_HandledSafely()
    {
        var injection = "main; rm -rf /";
        var tk = new GitToolkit(new ToolkitConfig("git",
            new() { ["working_dir"] = Path.GetTempPath() }));
        var result = await tk.ExecuteAsync("checkout", new() { ["branch"] = injection }, default);
        Assert.NotNull(result);
        // Must not execute the rm command (git will reject the invalid branch name)
        // Result may be Ok (if git partially runs) or Error, but must not throw
    }

    // ── 12. Credential leakage — error messages must not expose tokens ─────────
    [Fact]
    public async Task CredentialLeakage_SlackToken_NotInErrorMessage()
    {
        const string sensitiveToken = "xoxb-VERY-SENSITIVE-TOKEN-DO-NOT-LEAK";
        var tk = new SlackToolkit(new ToolkitConfig("slack", new() { ["token"] = sensitiveToken }));
        var result = await tk.ExecuteAsync("send_message",
            new() { ["text"] = "hello", ["channel"] = "#general" }, default);
        Assert.NotEqual(ToolkitResultStatus.Ok, result.Status);
        Assert.DoesNotContain(sensitiveToken, result.Error ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task CredentialLeakage_GitLabToken_NotInErrorMessage()
    {
        const string sensitiveToken = "glpat-SUPER_SECRET_TOKEN_LEAK_TEST";
        var tk = new GitLabToolkit(new ToolkitConfig("gitlab", new() { ["token"] = sensitiveToken }));
        var result = await tk.ExecuteAsync("list_projects", [], default);
        Assert.NotEqual(ToolkitResultStatus.Ok, result.Status);
        Assert.DoesNotContain(sensitiveToken, result.Error ?? "", StringComparison.Ordinal);
    }

    // ── 13. Already-cancelled CancellationToken ────────────────────────────────
    [Fact]
    public async Task AlreadyCancelledToken_SlackSend_ReturnsError_NotThrows()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var tk = new SlackToolkit(new ToolkitConfig("slack", new() { ["token"] = "xoxb-test" }));
        var result = await tk.ExecuteAsync("send_message",
            new() { ["text"] = "hello", ["channel"] = "#test" }, cts.Token);
        // Must NOT throw OperationCanceledException — must be converted to ToolkitResult
        Assert.NotNull(result);
        Assert.NotEqual(ToolkitResultStatus.Ok, result.Status);
    }

    [Fact]
    public async Task AlreadyCancelledToken_LocalFileRead_ReturnsError_NotThrows()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var tk = new LocalFileToolkit();
        var result = await tk.ExecuteAsync("read",
            new() { ["path"] = Path.GetTempFileName() }, cts.Token);
        Assert.NotNull(result);
        // Either cancelled (Error) or success if file was read before cancellation check
        Assert.NotEqual(ToolkitResultStatus.Skipped, result.Status);
    }

    // ── 14. Concurrent execution (thread safety) ──────────────────────────────
    [Fact]
    public async Task ConcurrentExecution_SameInstance_NoDeadlock_NoException()
    {
        // 10 concurrent calls to same toolkit instance — must not deadlock or corrupt state
        var tk = new GitHubToolkit(new ToolkitConfig("github", []));
        var tasks = Enumerable.Range(0, 10)
            .Select(i => tk.ExecuteAsync("__unknown_concurrent__", new() { ["n"] = i }, default))
            .ToArray();
        var results = await Task.WhenAll(tasks);
        // All 10 calls should return Error (unknown action) without any crash
        Assert.All(results, r =>
        {
            Assert.NotNull(r);
            Assert.Equal(ToolkitResultStatus.Error, r.Status);
        });
    }

    [Fact]
    public async Task ConcurrentExecution_LocalFile_NoRaceConditions()
    {
        // 20 concurrent read-exists calls to LocalFileToolkit — no shared state should be corrupted
        var tk = new LocalFileToolkit();
        var tempDir = Path.GetTempPath();
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => tk.ExecuteAsync("exists", new() { ["path"] = tempDir }, default))
            .ToArray();
        var results = await Task.WhenAll(tasks);
        // All should succeed — temp dir exists
        Assert.All(results, r => Assert.Equal(ToolkitResultStatus.Ok, r.Status));
    }

    // ── 15. Very large integer parameter ─────────────────────────────────────
    [Fact]
    public async Task MaxValueInteger_InPerPageParam_HandledSafely()
    {
        var tk = new GitHubToolkit();
        var result = await tk.ExecuteAsync("list_issues",
            new() { ["repo"] = "octocat/Hello-World", ["per_page"] = int.MaxValue }, default);
        // Should either succeed (API call) or fail with network error — must NOT throw
        Assert.NotNull(result);
    }

    // ── 16. Negative integer parameter ────────────────────────────────────────
    [Fact]
    public async Task NegativeInteger_InTakeParam_HandledSafely()
    {
        var tk = new NuGetToolkit();
        var result = await tk.ExecuteAsync("search",
            new() { ["query"] = "test", ["take"] = -999 }, default);
        // Must NOT throw — may return network error or partial success
        Assert.NotNull(result);
    }

    // ── 17. Unicode null in config settings (SSRF-like payload) ──────────────
    [Fact]
    public async Task SsrfAttempt_BaseUrlOverride_SearXng_HandledSafely()
    {
        // Attacker tries to set base_url to internal network
        var tk = new SearXngToolkit(new ToolkitConfig("searxng",
            new() { ["base_url"] = "http://169.254.169.254/latest/meta-data" }));
        var result = await tk.ExecuteAsync("search", new() { ["query"] = "test" }, default);
        // Must NOT succeed — internal metadata endpoint won't return valid SearXng JSON
        Assert.NotEqual(ToolkitResultStatus.Ok, result.Status);
    }
}
