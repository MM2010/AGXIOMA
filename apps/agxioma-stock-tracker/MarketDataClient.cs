using System.Text.Json;

namespace AgxiomaStockTracker;

/// <summary>
/// Fetches live market quotes from Yahoo Finance (unofficial JSON endpoint — no API key required).
/// Falls back to a stub when the ticker is unknown.
/// </summary>
public sealed class MarketDataClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public MarketDataClient(HttpClient http)
    {
        _http = http;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (compatible; AgxiomaStockTracker/1.0)");
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<QuoteResult?> GetQuoteAsync(string ticker, CancellationToken ct = default)
    {
        try
        {
            // Yahoo Finance v8 quote endpoint (unofficial, free, no key)
            var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(ticker)}?interval=1d&range=1d";
            var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            var meta = doc.RootElement
                .GetProperty("chart")
                .GetProperty("result")[0]
                .GetProperty("meta");

            var price     = meta.TryGetProperty("regularMarketPrice",     out var p)   ? p.GetDecimal()   : 0m;
            var prevClose = meta.TryGetProperty("chartPreviousClose",      out var pc)  ? pc.GetDecimal()  : price;
            var volume    = meta.TryGetProperty("regularMarketVolume",     out var v)   ? v.GetInt64()     : 0L;
            var mcap      = meta.TryGetProperty("marketCap",               out var mc)  ? FormatMarketCap(mc.GetDecimal()) : null;

            var change    = price - prevClose;
            var changePct = prevClose != 0m ? change / prevClose * 100m : 0m;

            return new QuoteResult(ticker, price, change, changePct, volume, mcap, "Yahoo Finance");
        }
        catch
        {
            return null;
        }
    }

    private static string FormatMarketCap(decimal value)
    {
        return value switch
        {
            > 1_000_000_000_000m => $"${value / 1_000_000_000_000m:F2}T",
            > 1_000_000_000m     => $"${value / 1_000_000_000m:F2}B",
            > 1_000_000m         => $"${value / 1_000_000m:F2}M",
            _                    => $"${value:F0}"
        };
    }
}
