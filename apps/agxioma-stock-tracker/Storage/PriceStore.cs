using Microsoft.Data.Sqlite;

namespace AgxiomaStockTracker.Storage;

public record PriceRow(string Ticker, DateTime Date, decimal Price, decimal Change, decimal ChangePercent);

/// <summary>
/// Lightweight SQLite store for price history.
/// Table: price_history (ticker, date, price, change, change_pct)
/// </summary>
public sealed class PriceStore
{
    private readonly string _connStr;

    public PriceStore(string dbPath)
    {
        _connStr = $"Data Source={dbPath}";
    }

    public async Task InitAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connStr);
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS price_history (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                ticker      TEXT NOT NULL,
                recorded_at TEXT NOT NULL,
                price       REAL NOT NULL,
                change      REAL NOT NULL,
                change_pct  REAL NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_price_ticker_date ON price_history(ticker, recorded_at);
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SavePriceAsync(
        string ticker,
        decimal price,
        decimal change,
        decimal changePct,
        CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connStr);
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO price_history (ticker, recorded_at, price, change, change_pct)
            VALUES ($ticker, $date, $price, $change, $pct)
            """;
        cmd.Parameters.AddWithValue("$ticker", ticker.ToUpperInvariant());
        cmd.Parameters.AddWithValue("$date",   DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$price",  (double)price);
        cmd.Parameters.AddWithValue("$change", (double)change);
        cmd.Parameters.AddWithValue("$pct",    (double)changePct);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<PriceRow>> GetHistoryAsync(
        string ticker,
        int days = 30,
        CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connStr);
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT ticker, recorded_at, price, change, change_pct
            FROM price_history
            WHERE ticker = $ticker
              AND recorded_at >= $since
            ORDER BY recorded_at ASC
            LIMIT 500
            """;
        cmd.Parameters.AddWithValue("$ticker", ticker.ToUpperInvariant());
        cmd.Parameters.AddWithValue("$since",  DateTime.UtcNow.AddDays(-days).ToString("o"));

        var rows = new List<PriceRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new PriceRow(
                Ticker:        reader.GetString(0),
                Date:          DateTime.Parse(reader.GetString(1)),
                Price:         (decimal)reader.GetDouble(2),
                Change:        (decimal)reader.GetDouble(3),
                ChangePercent: (decimal)reader.GetDouble(4)
            ));
        }
        return rows;
    }

    public async Task<List<string>> GetAllTickersAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connStr);
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT ticker FROM price_history ORDER BY ticker";

        var tickers = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) tickers.Add(reader.GetString(0));
        return tickers;
    }
}
