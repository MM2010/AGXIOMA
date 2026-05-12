using AGXIOME.Toolkits;
using AGXIOME.Toolkits.DataAnalysis;

namespace AGXIOME.Tests.F12;

public sealed class SqlToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("sql", new SqlToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("SQL", new SqlToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new SqlToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new SqlToolkit().ExecuteAsync("query", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("sql", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new SqlToolkit(cfg).ExecuteAsync("query", new Dictionary<string, object?> { ["sql"] = "SELECT 1" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class DuckDbToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("duckdb", new DuckDbToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("DuckDB", new DuckDbToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new DuckDbToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new DuckDbToolkit().ExecuteAsync("query", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("duckdb", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new DuckDbToolkit(cfg).ExecuteAsync("query", new Dictionary<string, object?> { ["sql"] = "SELECT 1" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class PostgreSqlToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("postgresql_query", new PostgreSqlToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("PostgreSQL", new PostgreSqlToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new PostgreSqlToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new PostgreSqlToolkit().ExecuteAsync("query", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("postgresql", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new PostgreSqlToolkit(cfg).ExecuteAsync("query", new Dictionary<string, object?> { ["sql"] = "SELECT 1" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class MySqlToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("mysql", new MySqlToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("MySQL", new MySqlToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new MySqlToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new MySqlToolkit().ExecuteAsync("query", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("mysql", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new MySqlToolkit(cfg).ExecuteAsync("query", new Dictionary<string, object?> { ["sql"] = "SELECT 1" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class SqliteToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("sqlite_query", new SqliteToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("SQLite", new SqliteToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new SqliteToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new SqliteToolkit().ExecuteAsync("query", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("sqlite", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new SqliteToolkit(cfg).ExecuteAsync("query", new Dictionary<string, object?> { ["sql"] = "SELECT 1" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class ClickHouseToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("clickhouse", new ClickHouseToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("ClickHouse", new ClickHouseToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new ClickHouseToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new ClickHouseToolkit().ExecuteAsync("query", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("clickhouse", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new ClickHouseToolkit(cfg).ExecuteAsync("query", new Dictionary<string, object?> { ["sql"] = "SELECT 1" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class SparkToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("spark", new SparkToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Apache Spark", new SparkToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new SparkToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new SparkToolkit().ExecuteAsync("run_job", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("spark", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new SparkToolkit(cfg).ExecuteAsync("run_job", new Dictionary<string, object?> { ["script"] = "print(1)" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class PandasLikeToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("pandas_like", new PandasLikeToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Pandas-like", new PandasLikeToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new PandasLikeToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new PandasLikeToolkit().ExecuteAsync("query", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("pandas", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new PandasLikeToolkit(cfg).ExecuteAsync("query", new Dictionary<string, object?> { ["expression"] = "df.head()" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class ExcelToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("excel", new ExcelToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Excel", new ExcelToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new ExcelToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new ExcelToolkit().ExecuteAsync("read_sheet", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("excel", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new ExcelToolkit(cfg).ExecuteAsync("read_sheet", new Dictionary<string, object?> { ["file"] = "data.xlsx", ["sheet"] = "Sheet1" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class CsvToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("csv", new CsvToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("CSV", new CsvToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new CsvToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new CsvToolkit().ExecuteAsync("read", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("csv", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new CsvToolkit(cfg).ExecuteAsync("read", new Dictionary<string, object?> { ["file"] = "data.csv" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class ParquetToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("parquet", new ParquetToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Parquet", new ParquetToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new ParquetToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new ParquetToolkit().ExecuteAsync("read", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("parquet", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new ParquetToolkit(cfg).ExecuteAsync("read", new Dictionary<string, object?> { ["file"] = "data.parquet" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class JsonToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("json_data", new JsonDataToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("JSON", new JsonDataToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new JsonDataToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new JsonDataToolkit().ExecuteAsync("read", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("json", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new JsonDataToolkit(cfg).ExecuteAsync("read", new Dictionary<string, object?> { ["file"] = "data.json" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class XmlToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("xml_data", new XmlDataToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("XML", new XmlDataToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new XmlDataToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new XmlDataToolkit().ExecuteAsync("read", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("xml", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new XmlDataToolkit(cfg).ExecuteAsync("read", new Dictionary<string, object?> { ["file"] = "data.xml" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class GeoPandasLikeToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("geopandas_like", new GeoPandasLikeToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("GeoPandas-like", new GeoPandasLikeToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new GeoPandasLikeToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new GeoPandasLikeToolkit().ExecuteAsync("read_geo", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("geopandas", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new GeoPandasLikeToolkit(cfg).ExecuteAsync("read_geo", new Dictionary<string, object?> { ["file"] = "data.geojson" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}
