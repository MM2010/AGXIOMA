using AGXIOME.Toolkits;
using AGXIOME.Toolkits.Finance;

namespace AGXIOME.Tests.F12;

public sealed class YahooFinanceToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("yahoo_finance", new YahooFinanceToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Yahoo Finance", new YahooFinanceToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Finance, new YahooFinanceToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new YahooFinanceToolkit().ExecuteAsync("get_quote", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("yahoo_finance", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new YahooFinanceToolkit(cfg).ExecuteAsync("get_quote", new Dictionary<string, object?> { ["symbol"] = "AAPL" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class AlphaVantageToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("alpha_vantage", new AlphaVantageToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Alpha Vantage", new AlphaVantageToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Finance, new AlphaVantageToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new AlphaVantageToolkit().ExecuteAsync("get_time_series", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("alpha_vantage", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new AlphaVantageToolkit(cfg).ExecuteAsync("get_time_series", new Dictionary<string, object?> { ["symbol"] = "MSFT", ["interval"] = "daily" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class PolygonToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("polygon", new PolygonToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Polygon.io", new PolygonToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Finance, new PolygonToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new PolygonToolkit().ExecuteAsync("get_ticker", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("polygon", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new PolygonToolkit(cfg).ExecuteAsync("get_ticker", new Dictionary<string, object?> { ["ticker"] = "GOOG" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class CoinGeckoToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("coingecko", new CoinGeckoToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("CoinGecko", new CoinGeckoToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Finance, new CoinGeckoToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new CoinGeckoToolkit().ExecuteAsync("get_price", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("coingecko", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new CoinGeckoToolkit(cfg).ExecuteAsync("get_price", new Dictionary<string, object?> { ["coin_id"] = "bitcoin", ["vs_currency"] = "usd" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class BinanceToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("binance", new BinanceToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Binance", new BinanceToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Finance, new BinanceToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new BinanceToolkit().ExecuteAsync("get_ticker", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("binance", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new BinanceToolkit(cfg).ExecuteAsync("get_ticker", new Dictionary<string, object?> { ["symbol"] = "BTCUSDT" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class StripeToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("stripe", new StripeToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Stripe", new StripeToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Finance, new StripeToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new StripeToolkit().ExecuteAsync("create_payment_intent", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("stripe", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new StripeToolkit(cfg).ExecuteAsync("create_payment_intent", new Dictionary<string, object?> { ["amount"] = 1000, ["currency"] = "usd" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class PlaidToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("plaid", new PlaidToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Plaid", new PlaidToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Finance, new PlaidToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new PlaidToolkit().ExecuteAsync("get_accounts", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("plaid", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new PlaidToolkit(cfg).ExecuteAsync("get_accounts", new Dictionary<string, object?> { ["access_token"] = "tok" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class QuickBooksToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("quickbooks", new QuickBooksToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("QuickBooks", new QuickBooksToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Finance, new QuickBooksToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new QuickBooksToolkit().ExecuteAsync("list_invoices", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("quickbooks", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new QuickBooksToolkit(cfg).ExecuteAsync("list_invoices", new Dictionary<string, object?> { ["company_id"] = "1" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class XeroToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("xero", new XeroToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Xero", new XeroToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Finance, new XeroToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new XeroToolkit().ExecuteAsync("list_invoices", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("xero", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new XeroToolkit(cfg).ExecuteAsync("list_invoices", new Dictionary<string, object?> { ["tenant_id"] = "1" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class ForexToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("forex", new ForexToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Forex", new ForexToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.Finance, new ForexToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new ForexToolkit().ExecuteAsync("get_rate", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("forex", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new ForexToolkit(cfg).ExecuteAsync("get_rate", new Dictionary<string, object?> { ["from"] = "USD", ["to"] = "EUR" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}
