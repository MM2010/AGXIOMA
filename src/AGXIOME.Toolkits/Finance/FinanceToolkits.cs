// T-80..T-89 — Finance Toolkits
using System.Net.Http.Json;
using System.Text.Json;

namespace AGXIOME.Toolkits.Finance;

// ── shared HTTP client ─────────────────────────────────────────────────────
file static class Http
{
    internal static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };
}

// T-80 — Yahoo Finance (unofficial public JSON API — no auth needed)
[Toolkit("yahoo_finance","Yahoo Finance",ToolkitCategory.Finance)]
public sealed class YahooFinanceToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"yahoo_finance";public override string Name=>"Yahoo Finance";
    public override ToolkitCategory Category=>ToolkitCategory.Finance;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var symbol=(GetParam<string>(p,"symbol")??GetParam<string>(p,"ticker")??"").ToUpperInvariant();
        return a switch{
            "quote"   =>await YfGet($"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?interval=1d&range=1d",ct).ConfigureAwait(false),
            "history" =>await YfGet($"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?interval={GetParam<string>(p,"interval")??"1d"}&range={GetParam<string>(p,"range")??"1mo"}",ct).ConfigureAwait(false),
            "search"  =>await YfGet($"https://query2.finance.yahoo.com/v1/finance/search?q={Uri.EscapeDataString(GetParam<string>(p,"query")??symbol)}&quotesCount={GetParam<int?>(p,"count")??10}",ct).ConfigureAwait(false),
            "summary" =>await YfGet($"https://query1.finance.yahoo.com/v10/finance/quoteSummary/{Uri.EscapeDataString(symbol)}?modules=summaryProfile,financialData,defaultKeyStatistics",ct).ConfigureAwait(false),
            _=>ToolkitResult.Fail($"Unknown action '{a}'. Valid: quote|history|search|summary")
        };
    }
    private static async Task<ToolkitResult> YfGet(string url,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);
        req.Headers.Add("User-Agent","Mozilla/5.0");
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-81 — Alpha Vantage (api_key required — free tier available at alphavantage.co)
[Toolkit("alpha_vantage","Alpha Vantage",ToolkitCategory.Finance)]
public sealed class AlphaVantageToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"alpha_vantage";public override string Name=>"Alpha Vantage";
    public override ToolkitCategory Category=>ToolkitCategory.Finance;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetSetting("api_key")??throw new InvalidOperationException("Alpha Vantage api_key not set");
        var sym=GetParam<string>(p,"symbol")??"";
        return a switch{
            "quote"       =>await AvGet($"GLOBAL_QUOTE&symbol={sym}",key,ct).ConfigureAwait(false),
            "daily"       =>await AvGet($"TIME_SERIES_DAILY&symbol={sym}&outputsize={GetParam<string>(p,"outputsize")??"compact"}",key,ct).ConfigureAwait(false),
            "weekly"      =>await AvGet($"TIME_SERIES_WEEKLY&symbol={sym}",key,ct).ConfigureAwait(false),
            "intraday"    =>await AvGet($"TIME_SERIES_INTRADAY&symbol={sym}&interval={GetParam<string>(p,"interval")??"5min"}",key,ct).ConfigureAwait(false),
            "search"      =>await AvGet($"SYMBOL_SEARCH&keywords={Uri.EscapeDataString(GetParam<string>(p,"query")??sym)}",key,ct).ConfigureAwait(false),
            "forex"       =>await AvGet($"FX_DAILY&from_symbol={GetParam<string>(p,"from")??"USD"}&to_symbol={GetParam<string>(p,"to")??"EUR"}",key,ct).ConfigureAwait(false),
            "crypto"      =>await AvGet($"DIGITAL_CURRENCY_DAILY&symbol={sym}&market={GetParam<string>(p,"market")??"USD"}",key,ct).ConfigureAwait(false),
            "news"        =>await AvGet($"NEWS_SENTIMENT&tickers={sym}&limit={GetParam<int?>(p,"limit")??10}",key,ct).ConfigureAwait(false),
            _=>ToolkitResult.Fail($"Unknown action '{a}'. Valid: quote|daily|weekly|intraday|search|forex|crypto|news")
        };
    }
    private static async Task<ToolkitResult> AvGet(string function,string key,CancellationToken ct){
        var json=await Http.Client.GetFromJsonAsync<JsonElement>($"https://www.alphavantage.co/query?function={function}&apikey={key}",ct).ConfigureAwait(false);
        if(json.TryGetProperty("Note",out var note))return ToolkitResult.Fail($"Alpha Vantage rate limit: {note.GetString()}");
        if(json.TryGetProperty("Error Message",out var err))return ToolkitResult.Fail(err.GetString()!);
        return ToolkitResult.Success(json);
    }
}

// T-82 — Polygon.io (api_key required — free tier available)
[Toolkit("polygon","Polygon.io",ToolkitCategory.Finance)]
public sealed class PolygonToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"polygon";public override string Name=>"Polygon.io";
    public override ToolkitCategory Category=>ToolkitCategory.Finance;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetSetting("api_key")??throw new InvalidOperationException("Polygon api_key not set");
        var sym=GetParam<string>(p,"symbol")??"";
        return a switch{
            "ticker_details" =>await PolyGet($"v3/reference/tickers/{sym}",key,ct).ConfigureAwait(false),
            "prev_close"     =>await PolyGet($"v2/aggs/ticker/{sym}/prev",key,ct).ConfigureAwait(false),
            "aggs"           =>await PolyGet($"v2/aggs/ticker/{sym}/range/{GetParam<string>(p,"multiplier")??"1"}/{GetParam<string>(p,"timespan")??"day"}/{GetParam<string>(p,"from")??"2024-01-01"}/{GetParam<string>(p,"to")?? throw new ArgumentException("to required")}",key,ct).ConfigureAwait(false),
            "last_trade"     =>await PolyGet($"v2/last/trade/{sym}",key,ct).ConfigureAwait(false),
            "ticker_news"    =>await PolyGet($"v2/reference/news?ticker={sym}&limit={GetParam<int?>(p,"limit")??10}",key,ct).ConfigureAwait(false),
            "market_status"  =>await PolyGet("v1/marketstatus/now",key,ct).ConfigureAwait(false),
            "exchanges"      =>await PolyGet("v3/reference/exchanges",key,ct).ConfigureAwait(false),
            _=>ToolkitResult.Fail($"Unknown action '{a}'. Valid: ticker_details|prev_close|aggs|last_trade|ticker_news|market_status|exchanges")
        };
    }
    private static async Task<ToolkitResult> PolyGet(string path,string key,CancellationToken ct){
        var json=await Http.Client.GetFromJsonAsync<JsonElement>($"https://api.polygon.io/{path}?apiKey={key}",ct).ConfigureAwait(false);
        return ToolkitResult.Success(json);
    }
}

// T-83 — CoinGecko (free public API — no auth needed for most endpoints)
[Toolkit("coingecko","CoinGecko",ToolkitCategory.Finance)]
public sealed class CoinGeckoToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"coingecko";public override string Name=>"CoinGecko";
    public override ToolkitCategory Category=>ToolkitCategory.Finance;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var base_="https://api.coingecko.com/api/v3";
        return a switch{
            "price"     =>await CgGet($"{base_}/simple/price?ids={GetParam<string>(p,"coin_id")??"bitcoin"}&vs_currencies={GetParam<string>(p,"vs_currencies")??"usd"}&include_24hr_change=true",ct).ConfigureAwait(false),
            "list"      =>await CgGet($"{base_}/coins/list",ct).ConfigureAwait(false),
            "markets"   =>await CgGet($"{base_}/coins/markets?vs_currency={GetParam<string>(p,"vs_currency")??"usd"}&per_page={GetParam<int?>(p,"per_page")??20}&page={GetParam<int?>(p,"page")??1}&order={GetParam<string>(p,"order")??"market_cap_desc"}",ct).ConfigureAwait(false),
            "coin_info" =>await CgGet($"{base_}/coins/{GetParam<string>(p,"coin_id")??"bitcoin"}?localization=false",ct).ConfigureAwait(false),
            "trending"  =>await CgGet($"{base_}/search/trending",ct).ConfigureAwait(false),
            "history"   =>await CgGet($"{base_}/coins/{GetParam<string>(p,"coin_id")??"bitcoin"}/market_chart?vs_currency={GetParam<string>(p,"vs_currency")??"usd"}&days={GetParam<string>(p,"days")??"30"}",ct).ConfigureAwait(false),
            "ohlc"      =>await CgGet($"{base_}/coins/{GetParam<string>(p,"coin_id")??"bitcoin"}/ohlc?vs_currency=usd&days={GetParam<string>(p,"days")??"7"}",ct).ConfigureAwait(false),
            "exchanges" =>await CgGet($"{base_}/exchanges?per_page={GetParam<int?>(p,"per_page")??10}",ct).ConfigureAwait(false),
            _=>ToolkitResult.Fail($"Unknown action '{a}'. Valid: price|list|markets|coin_info|trending|history|ohlc|exchanges")
        };
    }
    private static async Task<ToolkitResult> CgGet(string url,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);
        req.Headers.Add("Accept","application/json");
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-84 — Binance (public API for market data, api_key only needed for account ops)
[Toolkit("binance","Binance",ToolkitCategory.Finance)]
public sealed class BinanceToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"binance";public override string Name=>"Binance";
    public override ToolkitCategory Category=>ToolkitCategory.Finance;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var sym=(GetParam<string>(p,"symbol")??"BTCUSDT").ToUpperInvariant();
        return a switch{
            "price"     =>await BnGet($"ticker/price?symbol={sym}",ct).ConfigureAwait(false),
            "24hr"      =>await BnGet($"ticker/24hr?symbol={sym}",ct).ConfigureAwait(false),
            "orderbook" =>await BnGet($"depth?symbol={sym}&limit={GetParam<int?>(p,"limit")??20}",ct).ConfigureAwait(false),
            "klines"    =>await BnGet($"klines?symbol={sym}&interval={GetParam<string>(p,"interval")??"1h"}&limit={GetParam<int?>(p,"limit")??100}",ct).ConfigureAwait(false),
            "trades"    =>await BnGet($"trades?symbol={sym}&limit={GetParam<int?>(p,"limit")??50}",ct).ConfigureAwait(false),
            "all_prices"=>await BnGet("ticker/price",ct).ConfigureAwait(false),
            "exchange_info"=>await BnGet("exchangeInfo",ct).ConfigureAwait(false),
            "avg_price" =>await BnGet($"avgPrice?symbol={sym}",ct).ConfigureAwait(false),
            _=>ToolkitResult.Fail($"Unknown action '{a}'. Valid: price|24hr|orderbook|klines|trades|all_prices|exchange_info|avg_price")
        };
    }
    private static async Task<ToolkitResult> BnGet(string path,CancellationToken ct){
        var json=await Http.Client.GetFromJsonAsync<JsonElement>($"https://api.binance.com/api/v3/{path}",ct).ConfigureAwait(false);
        return ToolkitResult.Success(json);
    }
}

// T-85 — Stripe (api_key required — sk_test_... or sk_live_...)
[Toolkit("stripe","Stripe",ToolkitCategory.Finance)]
public sealed class StripeToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"stripe";public override string Name=>"Stripe";
    public override ToolkitCategory Category=>ToolkitCategory.Finance;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetSetting("api_key")??throw new InvalidOperationException("Stripe api_key not set");
        return a switch{
            "list_customers"    =>await StripeGet("customers",key,ct).ConfigureAwait(false),
            "get_customer"      =>await StripeGet($"customers/{GetParam<string>(p,"id")}",key,ct).ConfigureAwait(false),
            "list_charges"      =>await StripeGet($"charges?limit={GetParam<int?>(p,"limit")??10}",key,ct).ConfigureAwait(false),
            "list_invoices"     =>await StripeGet($"invoices?limit={GetParam<int?>(p,"limit")??10}",key,ct).ConfigureAwait(false),
            "list_subscriptions"=>await StripeGet($"subscriptions?limit={GetParam<int?>(p,"limit")??10}",key,ct).ConfigureAwait(false),
            "get_balance"       =>await StripeGet("balance",key,ct).ConfigureAwait(false),
            "list_products"     =>await StripeGet("products",key,ct).ConfigureAwait(false),
            _=>ToolkitResult.Fail($"Unknown action '{a}'. Valid: list_customers|get_customer|list_charges|list_invoices|list_subscriptions|get_balance|list_products")
        };
    }
    private static async Task<ToolkitResult> StripeGet(string path,string key,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.stripe.com/v1/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-86 — Plaid (requires vendor SDK — informative stub)
[Toolkit("plaid","Plaid",ToolkitCategory.Finance)]
public sealed class PlaidToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"plaid";public override string Name=>"Plaid";
    public override ToolkitCategory Category=>ToolkitCategory.Finance;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("client_id"));
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Fail("Plaid requires package Going.Plaid (NuGet). Add it and implement bank account linking, transaction retrieval, and balance checks via PlaidClient. Settings: client_id, secret, environment (sandbox/production)."));
}

// T-87 — QuickBooks (requires Intuit OAuth2 — informative stub)
[Toolkit("quickbooks","QuickBooks",ToolkitCategory.Finance)]
public sealed class QuickBooksToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"quickbooks";public override string Name=>"QuickBooks";
    public override ToolkitCategory Category=>ToolkitCategory.Finance;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("access_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetSetting("access_token")??throw new InvalidOperationException("QuickBooks access_token not set");
        var realmId=GetSetting("realm_id")??throw new InvalidOperationException("QuickBooks realm_id not set");
        var base_=$"https://quickbooks.api.intuit.com/v3/company/{realmId}";
        return a switch{
            "get_company_info"=>await QbGet($"{base_}/companyinfo/{realmId}",token,ct).ConfigureAwait(false),
            "list_invoices"   =>await QbQuery("SELECT * FROM Invoice MAXRESULTS 50",realmId,token,ct).ConfigureAwait(false),
            "list_customers"  =>await QbQuery("SELECT * FROM Customer MAXRESULTS 50",realmId,token,ct).ConfigureAwait(false),
            "list_accounts"   =>await QbQuery("SELECT * FROM Account MAXRESULTS 50",realmId,token,ct).ConfigureAwait(false),
            _=>ToolkitResult.Fail($"Unknown action '{a}'. Valid: get_company_info|list_invoices|list_customers|list_accounts")
        };
    }
    private static async Task<ToolkitResult> QbGet(string url,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        req.Headers.Add("Accept","application/json");
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private static async Task<ToolkitResult> QbQuery(string query,string realmId,string token,CancellationToken ct){
        var url=$"https://quickbooks.api.intuit.com/v3/company/{realmId}/query?query={Uri.EscapeDataString(query)}";
        var req=new HttpRequestMessage(HttpMethod.Get,url);
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        req.Headers.Add("Accept","application/json");
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-88 — Xero (requires OAuth2 access_token)
[Toolkit("xero","Xero",ToolkitCategory.Finance)]
public sealed class XeroToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"xero";public override string Name=>"Xero";
    public override ToolkitCategory Category=>ToolkitCategory.Finance;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("access_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetSetting("access_token")??throw new InvalidOperationException("Xero access_token not set");
        var tenantId=GetSetting("tenant_id")??"";
        return a switch{
            "list_invoices" =>await XeroGet("Invoices",token,tenantId,ct).ConfigureAwait(false),
            "list_contacts" =>await XeroGet("Contacts",token,tenantId,ct).ConfigureAwait(false),
            "list_accounts" =>await XeroGet("Accounts",token,tenantId,ct).ConfigureAwait(false),
            "get_invoice"   =>await XeroGet($"Invoices/{GetParam<string>(p,"id")}",token,tenantId,ct).ConfigureAwait(false),
            _=>ToolkitResult.Fail($"Unknown action '{a}'. Valid: list_invoices|list_contacts|list_accounts|get_invoice")
        };
    }
    private static async Task<ToolkitResult> XeroGet(string path,string token,string tenantId,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.xero.com/api.xro/2.0/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        req.Headers.Add("Accept","application/json");
        if(!string.IsNullOrWhiteSpace(tenantId))req.Headers.Add("Xero-tenant-id",tenantId);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-89 — Forex (Open Exchange Rates / exchangerate-api.com — free tier available)
[Toolkit("forex","Forex",ToolkitCategory.Finance)]
public sealed class ForexToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"forex";public override string Name=>"Forex";
    public override ToolkitCategory Category=>ToolkitCategory.Finance;
    public override bool IsConfigured=>true; // uses free open.er-api.com without key; key unlocks more features
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var base_=(GetParam<string>(p,"base")??"USD").ToUpperInvariant();
        var target=(GetParam<string>(p,"target")??(GetParam<string>(p,"to")??"EUR")).ToUpperInvariant();
        var apiKey=GetSetting("api_key");
        // free tier: https://open.er-api.com/v6/latest/{base}
        // paid tier: https://v6.exchangerate-api.com/v6/{key}/...
        var baseUrl=string.IsNullOrWhiteSpace(apiKey)
            ?$"https://open.er-api.com/v6/latest/{base_}"
            :$"https://v6.exchangerate-api.com/v6/{apiKey}/latest/{base_}";
        return a switch{
            "rate"    =>await GetRate(baseUrl,base_,target,ct).ConfigureAwait(false),
            "convert" =>await ConvertAmount(baseUrl,base_,target,GetParam<double?>(p,"amount")??1.0,ct).ConfigureAwait(false),
            "all"     =>await ForexGet(baseUrl,ct).ConfigureAwait(false),
            "list"    =>await ForexGet(baseUrl,ct).ConfigureAwait(false),
            _=>ToolkitResult.Fail($"Unknown action '{a}'. Valid: rate|convert|all|list")
        };
    }
    private static async Task<ToolkitResult> GetRate(string url,string from,string to,CancellationToken ct){
        var json=await ForexGetJson(url,ct).ConfigureAwait(false);
        if(!json.TryGetProperty("rates",out var rates))return ToolkitResult.Fail("rates not found in response");
        if(!rates.TryGetProperty(to,out var rateEl))return ToolkitResult.Fail($"Currency {to} not found");
        return ToolkitResult.Success(new{from,to,rate=rateEl.GetDouble(),lastUpdated=json.TryGetProperty("time_last_update_utc",out var t)?t.GetString():""});
    }
    private static async Task<ToolkitResult> ConvertAmount(string url,string from,string to,double amount,CancellationToken ct){
        var json=await ForexGetJson(url,ct).ConfigureAwait(false);
        if(!json.TryGetProperty("rates",out var rates)||!rates.TryGetProperty(to,out var rateEl))return ToolkitResult.Fail($"Currency {to} not found");
        var rate=rateEl.GetDouble();
        return ToolkitResult.Success(new{from,to,amount,rate,result=Math.Round(amount*rate,6)});
    }
    private static async Task<ToolkitResult> ForexGet(string url,CancellationToken ct)=>ToolkitResult.Success(await ForexGetJson(url,ct).ConfigureAwait(false));
    private static async Task<JsonElement> ForexGetJson(string url,CancellationToken ct){
        var json=await Http.Client.GetFromJsonAsync<JsonElement>(url,ct).ConfigureAwait(false);
        return json;
    }
}
