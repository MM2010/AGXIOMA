using AGXIOME.Toolkits;
using AGXIOME.Toolkits.Enterprise;

namespace AGXIOME.Tests.F12;

public sealed class SalesforceToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("salesforce", new SalesforceToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Salesforce", new SalesforceToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Enterprise, new SalesforceToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new SalesforceToolkit().ExecuteAsync("query", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("salesforce", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new SalesforceToolkit(cfg).ExecuteAsync("query", new Dictionary<string, object?> { ["soql"] = "SELECT Id FROM Lead LIMIT 1" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class HubSpotToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("hubspot", new HubSpotToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("HubSpot", new HubSpotToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Enterprise, new HubSpotToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new HubSpotToolkit().ExecuteAsync("list_contacts", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("hubspot", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new HubSpotToolkit(cfg).ExecuteAsync("list_contacts", new Dictionary<string, object?> { ["limit"] = 10 }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class ZendeskToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("zendesk", new ZendeskToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Zendesk", new ZendeskToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Enterprise, new ZendeskToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new ZendeskToolkit().ExecuteAsync("list_tickets", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("zendesk", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new ZendeskToolkit(cfg).ExecuteAsync("list_tickets", new Dictionary<string, object?> { ["status"] = "open" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class ServiceNowToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("servicenow", new ServiceNowToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("ServiceNow", new ServiceNowToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Enterprise, new ServiceNowToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new ServiceNowToolkit().ExecuteAsync("list_incidents", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("servicenow", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new ServiceNowToolkit(cfg).ExecuteAsync("list_incidents", new Dictionary<string, object?> { ["state"] = "1" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class DatadogToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("datadog", new DatadogToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Datadog", new DatadogToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Enterprise, new DatadogToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new DatadogToolkit().ExecuteAsync("query_metrics", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("datadog", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new DatadogToolkit(cfg).ExecuteAsync("query_metrics", new Dictionary<string, object?> { ["query"] = "avg:system.cpu.user{*}" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class PagerDutyToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("pagerduty", new PagerDutyToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("PagerDuty", new PagerDutyToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Enterprise, new PagerDutyToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new PagerDutyToolkit().ExecuteAsync("list_incidents", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("pagerduty", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new PagerDutyToolkit(cfg).ExecuteAsync("list_incidents", new Dictionary<string, object?> { ["status"] = "triggered" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class SentryToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("sentry", new SentryToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Sentry", new SentryToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Enterprise, new SentryToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new SentryToolkit().ExecuteAsync("list_issues", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("sentry", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new SentryToolkit(cfg).ExecuteAsync("list_issues", new Dictionary<string, object?> { ["project"] = "p" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class GrafanaToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("grafana", new GrafanaToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Grafana", new GrafanaToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Enterprise, new GrafanaToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new GrafanaToolkit().ExecuteAsync("list_dashboards", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("grafana", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new GrafanaToolkit(cfg).ExecuteAsync("list_dashboards", new Dictionary<string, object?> { ["org_id"] = "1" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class NewRelicToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("newrelic", new NewRelicToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("New Relic", new NewRelicToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Enterprise, new NewRelicToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new NewRelicToolkit().ExecuteAsync("query_nrql", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("newrelic", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new NewRelicToolkit(cfg).ExecuteAsync("query_nrql", new Dictionary<string, object?> { ["nrql"] = "SELECT count(*) FROM Transaction" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class ElasticsearchToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("elasticsearch", new ElasticsearchToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Elasticsearch", new ElasticsearchToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Enterprise, new ElasticsearchToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new ElasticsearchToolkit().ExecuteAsync("search", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("elasticsearch", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new ElasticsearchToolkit(cfg).ExecuteAsync("search", new Dictionary<string, object?> { ["index"] = "logs", ["query"] = "{\"match_all\":{}}" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class OktaToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("okta", new OktaToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Okta", new OktaToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Enterprise, new OktaToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new OktaToolkit().ExecuteAsync("list_users", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("okta", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new OktaToolkit(cfg).ExecuteAsync("list_users", new Dictionary<string, object?> { ["filter"] = "status eq \"ACTIVE\"" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class Auth0ToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("auth0", new Auth0Toolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Auth0", new Auth0Toolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Enterprise, new Auth0Toolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new Auth0Toolkit().ExecuteAsync("list_users", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("auth0", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new Auth0Toolkit(cfg).ExecuteAsync("list_users", new Dictionary<string, object?> { ["page"] = 0, ["per_page"] = 10 }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}
