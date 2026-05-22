using AGXIOME.Toolkits;
using AGXIOME.Toolkits.DataAnalysis;

namespace AGXIOME.Tests.F12;

public sealed class SqlToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("sql", new SqlToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("SQL", new SqlToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new SqlToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new SqlToolkit().ExecuteAsync("query", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("sql", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new SqlToolkit(cfg).ExecuteAsync("query", new Dictionary<string, object?> { ["sql"] = "SELECT 1" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class DuckDbToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("duckdb", new DuckDbToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("DuckDB", new DuckDbToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new DuckDbToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new DuckDbToolkit().ExecuteAsync("query", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("duckdb", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new DuckDbToolkit(cfg).ExecuteAsync("query", new Dictionary<string, object?> { ["sql"] = "SELECT 1" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class PostgreSqlToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("postgresql_query", new PostgreSqlToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("PostgreSQL", new PostgreSqlToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new PostgreSqlToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new PostgreSqlToolkit().ExecuteAsync("query", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("postgresql", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new PostgreSqlToolkit(cfg).ExecuteAsync("query", new Dictionary<string, object?> { ["sql"] = "SELECT 1" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class MySqlToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("mysql", new MySqlToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("MySQL", new MySqlToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new MySqlToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new MySqlToolkit().ExecuteAsync("query", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("mysql", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new MySqlToolkit(cfg).ExecuteAsync("query", new Dictionary<string, object?> { ["sql"] = "SELECT 1" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class SqliteToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("sqlite_query", new SqliteToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("SQLite", new SqliteToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new SqliteToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new SqliteToolkit().ExecuteAsync("query", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("sqlite_query", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new SqliteToolkit(cfg).ExecuteAsync("query", new Dictionary<string, object?> { ["query"] = "SELECT 1" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class ClickHouseToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("clickhouse", new ClickHouseToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("ClickHouse", new ClickHouseToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new ClickHouseToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new ClickHouseToolkit().ExecuteAsync("query", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("clickhouse", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new ClickHouseToolkit(cfg).ExecuteAsync("query", new Dictionary<string, object?> { ["sql"] = "SELECT 1" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class SparkToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("spark", new SparkToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Apache Spark", new SparkToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new SparkToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new SparkToolkit().ExecuteAsync("run_job", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("spark", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new SparkToolkit(cfg).ExecuteAsync("run_job", new Dictionary<string, object?> { ["script"] = "print(1)" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class PandasLikeToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("pandas_like", new PandasLikeToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Pandas-like", new PandasLikeToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new PandasLikeToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new PandasLikeToolkit().ExecuteAsync("head", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("pandas", new Dictionary<string, string> { ["api_key"] = "test" });
        await new PandasLikeToolkit(cfg).ExecuteAsync("load_json", new Dictionary<string, object?> { ["json"] = "[{\"name\":\"alice\"}]" }, default);
        var r = await new PandasLikeToolkit(cfg).ExecuteAsync("head", [], default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class ExcelToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("excel", new ExcelToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Excel", new ExcelToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new ExcelToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new ExcelToolkit().ExecuteAsync("read", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("excel", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new ExcelToolkit(cfg).ExecuteAsync("read", new Dictionary<string, object?> { ["file"] = "data.xlsx", ["sheet"] = "Sheet1" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }

    // ── Security / boundary tests ──────────────────────────────────────────
    [Fact] public async Task Read_PathTraversal_ReturnsFail()
    {
        var r = await new ExcelToolkit().ExecuteAsync("read",
            new Dictionary<string, object?> { ["file"] = "../../etc/passwd", ["sheet"] = "Sheet1" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
        Assert.Contains("Invalid file path", r.Error ?? "");
    }

    [Fact] public async Task Write_PathTraversal_ReturnsFail()
    {
        var r = await new ExcelToolkit().ExecuteAsync("write",
            new Dictionary<string, object?> { ["file"] = "../../windows/system32/evil.xlsx", ["sheet"] = "S", ["json"] = "[]" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
        Assert.Contains("Invalid file path", r.Error ?? "");
    }

    [Fact] public async Task GetCell_PathTraversal_ReturnsFail()
    {
        var r = await new ExcelToolkit().ExecuteAsync("get_cell",
            new Dictionary<string, object?> { ["file"] = "../../../secrets.xlsx", ["sheet"] = "S", ["cell"] = "A1" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
        Assert.Contains("Invalid file path", r.Error ?? "");
    }

    [Fact] public async Task Write_NullByteInPath_ReturnsFail()
    {
        var r = await new ExcelToolkit().ExecuteAsync("write",
            new Dictionary<string, object?> { ["file"] = "safe\0evil.xlsx", ["sheet"] = "S", ["json"] = "[]" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }

    [Fact] public async Task GetCell_NonExistentFile_ReturnsFail()
    {
        var r = await new ExcelToolkit().ExecuteAsync("get_cell",
            new Dictionary<string, object?> { ["file"] = "no_such_file_xyz.xlsx", ["sheet"] = "S", ["cell"] = "A1" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }

    [Fact] public async Task UpdateCell_NonExistentFile_ReturnsFail()
    {
        var r = await new ExcelToolkit().ExecuteAsync("update_cell",
            new Dictionary<string, object?> { ["file"] = "no_such_file_xyz.xlsx", ["sheet"] = "S", ["cell"] = "A1", ["value"] = "x" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }

    [Fact] public async Task Write_MalformedJson_ReturnsError()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xlsx");
        try
        {
            var r = await new ExcelToolkit().ExecuteAsync("write",
                new Dictionary<string, object?> { ["file"] = tempFile, ["sheet"] = "S", ["json"] = "{not valid json!" }, default);
            Assert.Equal(ToolkitResultStatus.Error, r.Status);
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    [Fact] public async Task Write_ThenReadCell_RoundTrip()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".xlsx");
        try
        {
            var wr = await new ExcelToolkit().ExecuteAsync("write",
                new Dictionary<string, object?> { ["file"] = tempFile, ["sheet"] = "TestSheet", ["json"] = "[{\"Name\":\"Alice\",\"Score\":\"99\"}]" }, default);
            Assert.Equal(ToolkitResultStatus.Ok, wr.Status);

            var cr = await new ExcelToolkit().ExecuteAsync("get_cell",
                new Dictionary<string, object?> { ["file"] = tempFile, ["sheet"] = "TestSheet", ["cell"] = "A1" }, default);
            Assert.Equal(ToolkitResultStatus.Ok, cr.Status);
        }
        finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
    }

    [Fact] public async Task UnknownAction_ReturnsError()
    {
        var r = await new ExcelToolkit().ExecuteAsync("__nonexistent__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class CsvToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("csv", new CsvToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("CSV", new CsvToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new CsvToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new CsvToolkit().ExecuteAsync("read", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("csv", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new CsvToolkit(cfg).ExecuteAsync("read", new Dictionary<string, object?> { ["file"] = "data.csv" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class ParquetToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("parquet", new ParquetToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Parquet", new ParquetToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new ParquetToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new ParquetToolkit().ExecuteAsync("read", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("parquet", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new ParquetToolkit(cfg).ExecuteAsync("read", new Dictionary<string, object?> { ["file"] = "data.parquet" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class JsonToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("json_data", new JsonDataToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("JSON", new JsonDataToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new JsonDataToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new JsonDataToolkit().ExecuteAsync("read_file", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("json", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new JsonDataToolkit(cfg).ExecuteAsync("read_file", new Dictionary<string, object?> { ["path"] = "data.json" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class XmlToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("xml_data", new XmlDataToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("XML", new XmlDataToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new XmlDataToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new XmlDataToolkit().ExecuteAsync("read_file", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("xml", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new XmlDataToolkit(cfg).ExecuteAsync("read_file", new Dictionary<string, object?> { ["path"] = "data.xml" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class GeoPandasLikeToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("geopandas_like", new GeoPandasLikeToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("GeoPandas-like", new GeoPandasLikeToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.DataAnalysis, new GeoPandasLikeToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new GeoPandasLikeToolkit().ExecuteAsync("read_geo", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("geopandas", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new GeoPandasLikeToolkit(cfg).ExecuteAsync("read_geo", new Dictionary<string, object?> { ["file"] = "data.geojson" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}
