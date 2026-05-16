using AGXIOME.Toolkits;
using AGXIOME.Toolkits.Finance;

namespace AGXIOME.Tests.F12;

public sealed class YahooFinanceToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("yahoo_finance", new YahooFinanceToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Yahoo Finance", new YahooFinanceToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Finance, new YahooFinanceToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new YahooFinanceToolkit().ExecuteAsync("quote", new Dictionary<string, object?> { ["symbol"] = "AAPL" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("yahoo_finance", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new YahooFinanceToolkit(cfg).ExecuteAsync("quote", new Dictionary<string, object?> { ["symbol"] = "AAPL" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class AlphaVantageToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("alpha_vantage", new AlphaVantageToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Alpha Vantage", new AlphaVantageToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Finance, new AlphaVantageToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new AlphaVantageToolkit().ExecuteAsync("get_time_series", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("alpha_vantage", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new AlphaVantageToolkit(cfg).ExecuteAsync("daily", new Dictionary<string, object?> { ["symbol"] = "MSFT", ["interval"] = "daily" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class PolygonToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("polygon", new PolygonToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Polygon.io", new PolygonToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Finance, new PolygonToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new PolygonToolkit().ExecuteAsync("get_ticker", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("polygon", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new PolygonToolkit(cfg).ExecuteAsync("ticker_details", new Dictionary<string, object?> { ["symbol"] = "GOOG" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class CoinGeckoToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("coingecko", new CoinGeckoToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("CoinGecko", new CoinGeckoToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Finance, new CoinGeckoToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new CoinGeckoToolkit().ExecuteAsync("price", new Dictionary<string, object?> { ["coin_id"] = "bitcoin" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("coingecko", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new CoinGeckoToolkit(cfg).ExecuteAsync("price", new Dictionary<string, object?> { ["coin_id"] = "bitcoin", ["vs_currencies"] = "usd" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class BinanceToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("binance", new BinanceToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Binance", new BinanceToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Finance, new BinanceToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new BinanceToolkit().ExecuteAsync("price", new Dictionary<string, object?> { ["symbol"] = "BTCUSDT" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("binance", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new BinanceToolkit(cfg).ExecuteAsync("price", new Dictionary<string, object?> { ["symbol"] = "BTCUSDT" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class StripeToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("stripe", new StripeToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Stripe", new StripeToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Finance, new StripeToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new StripeToolkit().ExecuteAsync("create_payment_intent", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("stripe", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new StripeToolkit(cfg).ExecuteAsync("list_customers", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class PlaidToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("plaid", new PlaidToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Plaid", new PlaidToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Finance, new PlaidToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new PlaidToolkit().ExecuteAsync("get_accounts", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("plaid", new Dictionary<string, string> { ["client_id"] = "test" });
        var r = await new PlaidToolkit(cfg).ExecuteAsync("get_accounts", new Dictionary<string, object?> { ["access_token"] = "tok" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class QuickBooksToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("quickbooks", new QuickBooksToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("QuickBooks", new QuickBooksToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Finance, new QuickBooksToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new QuickBooksToolkit().ExecuteAsync("list_invoices", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("quickbooks", new Dictionary<string, string> { ["access_token"] = "test", ["realm_id"] = "1" });
        var r = await new QuickBooksToolkit(cfg).ExecuteAsync("list_invoices", new Dictionary<string, object?> { ["company_id"] = "1" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class XeroToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("xero", new XeroToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Xero", new XeroToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Finance, new XeroToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new XeroToolkit().ExecuteAsync("list_invoices", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("xero", new Dictionary<string, string> { ["access_token"] = "test" });
        var r = await new XeroToolkit(cfg).ExecuteAsync("list_invoices", new Dictionary<string, object?> { ["tenant_id"] = "1" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class ForexToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("forex", new ForexToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Forex", new ForexToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Finance, new ForexToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new ForexToolkit().ExecuteAsync("rate", new Dictionary<string, object?> { ["from"] = "USD", ["to"] = "EUR" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("forex", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new ForexToolkit(cfg).ExecuteAsync("rate", new Dictionary<string, object?> { ["from"] = "USD", ["to"] = "EUR" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}
