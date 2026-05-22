// T-34..T-47 — Data Analysis Toolkits
using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using AGXIOME.Shared.Errors;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.Sqlite;

namespace AGXIOME.Toolkits.DataAnalysis;

// T-34 — Generic SQL (delegates to provider-specific toolkits; base stub)
[Toolkit("sql","SQL",ToolkitCategory.DataAnalysis)]
public sealed class SqlToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"sql"; public override string Name=>"SQL";
    public override ToolkitCategory Category=>ToolkitCategory.DataAnalysis;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Fail("Use sqlite_query, postgresql_query, or mysql toolkit with a connection_string setting."));
}

// T-35 — DuckDB (requires DuckDB.NET.Data NuGet)
[Toolkit("duckdb","DuckDB",ToolkitCategory.DataAnalysis)]
public sealed class DuckDbToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"duckdb"; public override string Name=>"DuckDB";
    public override ToolkitCategory Category=>ToolkitCategory.DataAnalysis;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Fail("DuckDB requires package DuckDB.NET.Data. Add it to the project and implement via DuckDBConnection."));
}

// T-36 — PostgreSQL (requires Npgsql NuGet)
[Toolkit("postgresql_query","PostgreSQL",ToolkitCategory.DataAnalysis)]
public sealed class PostgreSqlToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"postgresql_query"; public override string Name=>"PostgreSQL";
    public override ToolkitCategory Category=>ToolkitCategory.DataAnalysis;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("connection_string"));
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Fail("PostgreSQL requires package Npgsql. Add it and use NpgsqlConnection with connection_string setting."));
}

// T-37 — MySQL (requires MySql.Data or Pomelo.EntityFrameworkCore.MySql NuGet)
[Toolkit("mysql","MySQL",ToolkitCategory.DataAnalysis)]
public sealed class MySqlToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"mysql"; public override string Name=>"MySQL";
    public override ToolkitCategory Category=>ToolkitCategory.DataAnalysis;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("connection_string"));
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Fail("MySQL requires package MySql.Data or MySqlConnector. Add it and use MySqlConnection with connection_string setting."));
}

// T-38 — SQLite (Microsoft.Data.Sqlite — BCL-friendly, zero config)
[Toolkit("sqlite_query","SQLite",ToolkitCategory.DataAnalysis)]
public sealed class SqliteToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"sqlite_query"; public override string Name=>"SQLite";
    public override ToolkitCategory Category=>ToolkitCategory.DataAnalysis;
    public override bool IsConfigured=>true; // in-memory db works without any setting
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var cs=GetSetting("connection_string")??$"Data Source={GetParam<string>(p,"database")??"file::memory:?cache=shared"};";
        var query=GetParam<string>(p,"query")??"";
        if(string.IsNullOrWhiteSpace(query))return ToolkitResult.Fail("query parameter required");
        await using var conn=new SqliteConnection(cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd=conn.CreateCommand();
        cmd.CommandText=query;
        // DDL/DML
        if(query.TrimStart().StartsWith("SELECT",StringComparison.OrdinalIgnoreCase)||
           query.TrimStart().StartsWith("WITH",StringComparison.OrdinalIgnoreCase)||
           query.TrimStart().StartsWith("PRAGMA",StringComparison.OrdinalIgnoreCase)){
            await using var reader=await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            var cols=Enumerable.Range(0,reader.FieldCount).Select(i=>reader.GetName(i)).ToArray();
            var rows=new List<Dictionary<string,object?>>();
            while(await reader.ReadAsync(ct).ConfigureAwait(false)){
                var row=new Dictionary<string,object?>();
                for(int i=0;i<reader.FieldCount;i++)row[cols[i]]=reader.IsDBNull(i)?null:reader.GetValue(i);
                rows.Add(row);
            }
            return ToolkitResult.Success(new{query,columns=cols,rows,rowCount=rows.Count});
        }else{
            var affected=await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return ToolkitResult.Success(new{query,rowsAffected=affected});
        }
    }
}

// T-39 — ClickHouse (requires ClickHouse.Client NuGet)
[Toolkit("clickhouse","ClickHouse",ToolkitCategory.DataAnalysis)]
public sealed class ClickHouseToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"clickhouse"; public override string Name=>"ClickHouse";
    public override ToolkitCategory Category=>ToolkitCategory.DataAnalysis;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Fail("ClickHouse requires package ClickHouse.Client. Add it and use ClickHouseConnection."));
}

// T-40 — Apache Spark (requires Microsoft.Spark NuGet + Java/Spark runtime)
[Toolkit("spark","Apache Spark",ToolkitCategory.DataAnalysis)]
public sealed class SparkToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"spark"; public override string Name=>"Apache Spark";
    public override ToolkitCategory Category=>ToolkitCategory.DataAnalysis;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Fail("Apache Spark requires package Microsoft.Spark and a running Spark cluster. master_url setting required."));
}

// T-41 — Pandas-like in-memory table (pure BCL DataTable)
[Toolkit("pandas_like","Pandas-like",ToolkitCategory.DataAnalysis)]
public sealed class PandasLikeToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    // in-process DataTable — no external deps
    private static readonly Dictionary<string,DataTable> _tables=new();
    public override string Id=>"pandas_like"; public override string Name=>"Pandas-like";
    public override ToolkitCategory Category=>ToolkitCategory.DataAnalysis;
    public override bool IsConfigured=>true;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var name=GetParam<string>(p,"table")??"default";
        return a switch{
            "load_json"=>LoadJson(name,GetParam<string>(p,"json")??"[]"),
            "head"=>Head(name,GetParam<int?>(p,"n")??5),
            "describe"=>Describe(name),
            "filter"=>Filter(name,GetParam<string>(p,"column")??"",GetParam<string>(p,"value")??""),
            "drop"=>Drop(name),
            "list_tables"=>Task.FromResult(ToolkitResult.Success(new{tables=_tables.Keys.ToArray()})),
            _=>Task.FromException<ToolkitResult>(AgxiomeToolkitException.UnknownAction(Id, a))
        };
    }
    private Task<ToolkitResult> LoadJson(string name,string json){
        var docs=JsonDocument.Parse(json);
        var dt=new DataTable(name);
        _tables[name]=dt;
        if(docs.RootElement.ValueKind!=JsonValueKind.Array)return Task.FromResult(ToolkitResult.Fail("json must be array of objects"));
        foreach(var row in docs.RootElement.EnumerateArray()){
            if(row.ValueKind!=JsonValueKind.Object)continue;
            var dr=dt.NewRow();
            foreach(var prop in row.EnumerateObject()){
                if(!dt.Columns.Contains(prop.Name))dt.Columns.Add(prop.Name,typeof(string));
                dr[prop.Name]=prop.Value.GetRawText().Trim('"');
            }
            dt.Rows.Add(dr);
        }
        return Task.FromResult(ToolkitResult.Success(new{table=name,shape=$"{dt.Rows.Count}x{dt.Columns.Count}",columns=dt.Columns.Cast<DataColumn>().Select(c=>c.ColumnName).ToArray()}));
    }
    private Task<ToolkitResult> Head(string name,int n){
        if(!_tables.TryGetValue(name,out var dt))return Task.FromResult(ToolkitResult.Fail($"Table '{name}' not loaded"));
        var cols=dt.Columns.Cast<DataColumn>().Select(c=>c.ColumnName).ToArray();
        var rows=dt.AsEnumerable().Take(n).Select(r=>cols.ToDictionary(c=>c,c=>r[c]?.ToString())).ToArray();
        return Task.FromResult(ToolkitResult.Success(new{table=name,columns=cols,rows,total=dt.Rows.Count}));
    }
    private Task<ToolkitResult> Describe(string name){
        if(!_tables.TryGetValue(name,out var dt))return Task.FromResult(ToolkitResult.Fail($"Table '{name}' not loaded"));
        return Task.FromResult(ToolkitResult.Success(new{table=name,rows=dt.Rows.Count,columns=dt.Columns.Cast<DataColumn>().Select(c=>new{name=c.ColumnName,type=c.DataType.Name}).ToArray()}));
    }
    private Task<ToolkitResult> Filter(string name,string col,string val){
        if(!_tables.TryGetValue(name,out var dt))return Task.FromResult(ToolkitResult.Fail($"Table '{name}' not loaded"));
        var cols=dt.Columns.Cast<DataColumn>().Select(c=>c.ColumnName).ToArray();
        var rows=dt.AsEnumerable().Where(r=>r[col]?.ToString()==val).Select(r=>cols.ToDictionary(c=>c,c=>r[c]?.ToString())).ToArray();
        return Task.FromResult(ToolkitResult.Success(new{table=name,column=col,value=val,rows,matchCount=rows.Length}));
    }
    private Task<ToolkitResult> Drop(string name){
        _tables.Remove(name);
        return Task.FromResult(ToolkitResult.Success(new{table=name,dropped=true}));
    }
}

// T-42 — Excel read/write via ClosedXML
[Toolkit("excel","Excel",ToolkitCategory.DataAnalysis)]
public sealed class ExcelToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"excel"; public override string Name=>"Excel";
    public override ToolkitCategory Category=>ToolkitCategory.DataAnalysis;
    public override bool IsConfigured=>true;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var file=GetParam<string>(p,"file")??"";
        if(!IsSafePath(file))return Task.FromResult(ToolkitResult.Fail("Invalid file path: path traversal not allowed"));
        if(a!="write"&&!File.Exists(file))return Task.FromResult(ToolkitResult.Fail($"File not found: {file}"));
        return a switch{
            "read"        =>ReadExcel(file,GetParam<string>(p,"sheet"),GetParam<int?>(p,"max_rows")??1000),
            "list_sheets" =>ListSheets(file),
            "write"       =>WriteExcel(file,GetParam<string>(p,"sheet")??"Sheet1",GetParam<string>(p,"json")??"" ),
            "get_cell"    =>GetCell(file,GetParam<string>(p,"sheet"),GetRequiredParam<string>(p,"cell")),
            "update_cell" =>UpdateCell(file,GetParam<string>(p,"sheet"),GetRequiredParam<string>(p,"cell"),GetParam<string>(p,"value")??"" ),
            _=>Task.FromException<ToolkitResult>(AgxiomeToolkitException.UnknownAction(Id, a))
        };
    }
    private static bool IsSafePath(string path)=>
        !string.IsNullOrWhiteSpace(path)&&
        !path.Contains('\0')&&
        !path.Split(new[]{'/','\\'},StringSplitOptions.RemoveEmptyEntries).Contains("..");
    private Task<ToolkitResult> ReadExcel(string file,string? sheetName,int maxRows){
        using var wb=new ClosedXML.Excel.XLWorkbook(file);
        var ws=sheetName!=null?wb.Worksheet(sheetName):wb.Worksheets.First();
        var range=ws.RangeUsed();
        if(range==null)return Task.FromResult(ToolkitResult.Success(new{file,sheet=ws.Name,columns=Array.Empty<string>(),rows=Array.Empty<object>()}));
        var firstRow=range.FirstRowUsed();
        var headers=firstRow.Cells().Select(c=>c.GetString()).ToArray();
        var rows=range.RowsUsed().Skip(1).Take(maxRows).Select(r=>
            headers.Select((h,i)=>new{h,v=r.Cell(i+1).GetString()}).ToDictionary(x=>x.h,x=>x.v)).ToArray();
        return Task.FromResult(ToolkitResult.Success(new{file,sheet=ws.Name,columns=headers,rows,rowCount=rows.Length}));
    }
    private Task<ToolkitResult> ListSheets(string file){
        using var wb=new ClosedXML.Excel.XLWorkbook(file);
        return Task.FromResult(ToolkitResult.Success(new{file,sheets=wb.Worksheets.Select(w=>w.Name).ToArray()}));
    }
    private Task<ToolkitResult> WriteExcel(string file,string sheetName,string json){
        try{
            var docs=System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(json)?"[]": json);
            using var wb=File.Exists(file)?new ClosedXML.Excel.XLWorkbook(file):new ClosedXML.Excel.XLWorkbook();
            var ws=wb.Worksheets.FirstOrDefault(x=>x.Name==sheetName)??wb.Worksheets.Add(sheetName);
            int row=1;
            foreach(var item in docs.RootElement.EnumerateArray()){
                int col=1;
                foreach(var prop in item.EnumerateObject()){if(row==1&&ws.Cell(1,col).IsEmpty())ws.Cell(1,col).Value=prop.Name;ws.Cell(row+1,col).Value=prop.Value.GetRawText().Trim('"');col++;}
                row++;
            }
            wb.SaveAs(file);
            return Task.FromResult(ToolkitResult.Success(new{file,sheet=sheetName,rowsWritten=row-1}));
        }catch(Exception ex){return Task.FromResult(ToolkitResult.Fail($"Write failed: {ex.Message}"));}
    }
    private Task<ToolkitResult> GetCell(string file,string? sheetName,string cell){
        using var wb=new ClosedXML.Excel.XLWorkbook(file);
        var ws=sheetName!=null?wb.Worksheet(sheetName):wb.Worksheets.First();
        var c=ws.Cell(cell);
        return Task.FromResult(ToolkitResult.Success(new{file,sheet=ws.Name,cell,value=c.GetString(),type=c.DataType.ToString()}));
    }
    private Task<ToolkitResult> UpdateCell(string file,string? sheetName,string cell,string value){
        using var wb=new ClosedXML.Excel.XLWorkbook(file);
        var ws=sheetName!=null?wb.Worksheet(sheetName):wb.Worksheets.First();
        ws.Cell(cell).Value=value;
        wb.Save();
        return Task.FromResult(ToolkitResult.Success(new{file,sheet=ws.Name,cell,value,updated=true}));
    }
}

// T-43 — CSV via CsvHelper
[Toolkit("csv","CSV",ToolkitCategory.DataAnalysis)]
public sealed class CsvToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"csv"; public override string Name=>"CSV";
    public override ToolkitCategory Category=>ToolkitCategory.DataAnalysis;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var file=GetParam<string>(p,"file")??"";
        return a switch{
            "read"=>await ReadCsv(file,GetParam<int?>(p,"max_rows")??10000,GetParam<string>(p,"delimiter")??",",ct).ConfigureAwait(false),
            "write"=>await WriteCsv(file,GetParam<object?>(p,"rows"),GetParam<string>(p,"delimiter")??",",ct).ConfigureAwait(false),
            "info"=>await InfoCsv(file,GetParam<string>(p,"delimiter")??",",ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private async Task<ToolkitResult> ReadCsv(string file,int maxRows,string delim,CancellationToken ct){
        if(!File.Exists(file))return ToolkitResult.Fail($"File not found: {file}");
        var cfg=new CsvConfiguration(CultureInfo.InvariantCulture){Delimiter=delim,HasHeaderRecord=true};
        using var reader=new StreamReader(file);
        using var csv=new CsvReader(reader,cfg);
        var records=new List<Dictionary<string,string>>();
        string[]? headers=null;
        await foreach(var row in csv.GetRecordsAsync<dynamic>(ct)){
            if(headers==null)headers=csv.HeaderRecord??[];
            var dict=new Dictionary<string,string>();
            foreach(var h in headers)dict[h]=csv.GetField(h)??"";
            records.Add(dict);
            if(records.Count>=maxRows)break;
        }
        return ToolkitResult.Success(new{file,columns=headers??[],rows=records,rowCount=records.Count});
    }
    private async Task<ToolkitResult> WriteCsv(string file,object? rowsObj,string delim,CancellationToken ct){
        if(rowsObj is not IEnumerable<Dictionary<string,string>> rows)
            return ToolkitResult.Fail("rows must be an array of {key:value} objects");
        var cfg=new CsvConfiguration(CultureInfo.InvariantCulture){Delimiter=delim};
        await using var writer=new StreamWriter(file);
        await using var csv=new CsvWriter(writer,cfg);
        await csv.WriteRecordsAsync(rows,ct).ConfigureAwait(false);
        return ToolkitResult.Success(new{file,rowsWritten=rows.Count()});
    }
    private async Task<ToolkitResult> InfoCsv(string file,string delim,CancellationToken ct){
        if(!File.Exists(file))return ToolkitResult.Fail($"File not found: {file}");
        var cfg=new CsvConfiguration(CultureInfo.InvariantCulture){Delimiter=delim,HasHeaderRecord=true};
        using var reader=new StreamReader(file);
        using var csv=new CsvReader(reader,cfg);
        await csv.ReadAsync().ConfigureAwait(false);
        csv.ReadHeader();
        var headers=csv.HeaderRecord??[];
        int rowCount=0;
        while(await csv.ReadAsync().ConfigureAwait(false))rowCount++;
        var fi=new FileInfo(file);
        return ToolkitResult.Success(new{file,columns=headers,columnCount=headers.Length,rowCount,sizeBytes=fi.Length});
    }
}

// T-44 — Parquet (requires Parquet.Net NuGet)
[Toolkit("parquet","Parquet",ToolkitCategory.DataAnalysis)]
public sealed class ParquetToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"parquet"; public override string Name=>"Parquet";
    public override ToolkitCategory Category=>ToolkitCategory.DataAnalysis;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Fail("Parquet requires package Parquet.Net. Add it and use ParquetReader."));
}

// T-45 — JSON data manipulation (System.Text.Json, BCL)
[Toolkit("json_data","JSON",ToolkitCategory.DataAnalysis)]
public sealed class JsonDataToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"json_data"; public override string Name=>"JSON";
    public override ToolkitCategory Category=>ToolkitCategory.DataAnalysis;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        return a switch{
            "read_file"=>await ReadFile(GetParam<string>(p,"path")??"",ct).ConfigureAwait(false),
            "write_file"=>await WriteFile(GetParam<string>(p,"path")??"",GetParam<string>(p,"json")??"{}",GetParam<bool?>(p,"pretty")??true,ct).ConfigureAwait(false),
            "query"=>Query(GetParam<string>(p,"json")??"{}",GetParam<string>(p,"path")??"$"),
            "merge"=>Merge(GetParam<string>(p,"base")??"{}",GetParam<string>(p,"patch")??"{}"),
            "flatten"=>Flatten(GetParam<string>(p,"json")??"{}"),
            "schema"=>Schema(GetParam<string>(p,"json")??"{}"),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> ReadFile(string path,CancellationToken ct){
        if(!File.Exists(path))return ToolkitResult.Fail($"File not found: {path}");
        var text=await File.ReadAllTextAsync(path,ct).ConfigureAwait(false);
        var doc=JsonDocument.Parse(text);
        return ToolkitResult.Success(new{path,json=doc.RootElement,charCount=text.Length});
    }
    private static async Task<ToolkitResult> WriteFile(string path,string json,bool pretty,CancellationToken ct){
        var doc=JsonDocument.Parse(json);
        var opts=new JsonSerializerOptions{WriteIndented=pretty};
        var formatted=JsonSerializer.Serialize(doc.RootElement,opts);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path,formatted,ct).ConfigureAwait(false);
        return ToolkitResult.Success(new{path,bytesWritten=formatted.Length});
    }
    private static ToolkitResult Query(string json,string jsonPath){
        // simple dot-path: $.store.name or $.items[0].title
        try{
            var doc=JsonDocument.Parse(json);
            var el=doc.RootElement;
            var parts=jsonPath.TrimStart('$','.').Split('.',StringSplitOptions.RemoveEmptyEntries);
            foreach(var part in parts){
                var m=System.Text.RegularExpressions.Regex.Match(part,@"^(\w+)\[(\d+)\]$");
                if(m.Success){
                    el=el.GetProperty(m.Groups[1].Value)[int.Parse(m.Groups[2].Value)];
                }else el=el.GetProperty(part);
            }
            return ToolkitResult.Success(new{path=jsonPath,value=el.GetRawText()});
        }catch(Exception ex){return ToolkitResult.Fail($"Query error: {ex.Message}");}
    }
    private static ToolkitResult Merge(string baseJson,string patchJson){
        var baseDoc=JsonDocument.Parse(baseJson).RootElement;
        var patchDoc=JsonDocument.Parse(patchJson).RootElement;
        var merged=new Dictionary<string,JsonElement>();
        if(baseDoc.ValueKind==JsonValueKind.Object)foreach(var p in baseDoc.EnumerateObject())merged[p.Name]=p.Value;
        if(patchDoc.ValueKind==JsonValueKind.Object)foreach(var p in patchDoc.EnumerateObject())merged[p.Name]=p.Value;
        return ToolkitResult.Success(new{merged=JsonSerializer.Serialize(merged),keyCount=merged.Count});
    }
    private static ToolkitResult Flatten(string json){
        var result=new Dictionary<string,string>();
        void Walk(JsonElement el,string prefix){
            switch(el.ValueKind){
                case JsonValueKind.Object:foreach(var p in el.EnumerateObject())Walk(p.Value,string.IsNullOrEmpty(prefix)?p.Name:$"{prefix}.{p.Name}");break;
                case JsonValueKind.Array:{int i=0;foreach(var item in el.EnumerateArray())Walk(item,$"{prefix}[{i++}]");break;}
                default:result[prefix]=el.GetRawText().Trim('"');break;
            }
        }
        Walk(JsonDocument.Parse(json).RootElement,"");
        return ToolkitResult.Success(new{keys=result,count=result.Count});
    }
    private static ToolkitResult Schema(string json){
        var result=new Dictionary<string,string>();
        void Walk(JsonElement el,string prefix){
            switch(el.ValueKind){
                case JsonValueKind.Object:foreach(var p in el.EnumerateObject())Walk(p.Value,string.IsNullOrEmpty(prefix)?p.Name:$"{prefix}.{p.Name}");break;
                case JsonValueKind.Array:result[prefix]="array";if(el.GetArrayLength()>0)Walk(el[0],$"{prefix}[]");break;
                default:result[prefix]=el.ValueKind.ToString().ToLowerInvariant();break;
            }
        }
        Walk(JsonDocument.Parse(json).RootElement,"");
        return ToolkitResult.Success(new{schema=result});
    }
}

// T-46 — XML data manipulation (System.Xml, BCL)
[Toolkit("xml_data","XML",ToolkitCategory.DataAnalysis)]
public sealed class XmlDataToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"xml_data"; public override string Name=>"XML";
    public override ToolkitCategory Category=>ToolkitCategory.DataAnalysis;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        return a switch{
            "read_file"=>await ReadXml(GetParam<string>(p,"path")??"",ct).ConfigureAwait(false),
            "xpath"=>await XPath(GetParam<string>(p,"path")??"",GetParam<string>(p,"xpath")??"/*",ct).ConfigureAwait(false),
            "to_json"=>await ToJson(GetParam<string>(p,"path")??"",ct).ConfigureAwait(false),
            "validate"=>await Validate(GetParam<string>(p,"path")??"",ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> ReadXml(string path,CancellationToken ct){
        if(!File.Exists(path))return ToolkitResult.Fail($"File not found: {path}");
        var text=await File.ReadAllTextAsync(path,ct).ConfigureAwait(false);
        var doc=XDocument.Parse(text);
        return ToolkitResult.Success(new{path,root=doc.Root?.Name.LocalName,charCount=text.Length,xml=text});
    }
    private static async Task<ToolkitResult> XPath(string path,string xpath,CancellationToken ct){
        if(!File.Exists(path))return ToolkitResult.Fail($"File not found: {path}");
        var text=await File.ReadAllTextAsync(path,ct).ConfigureAwait(false);
        var doc=new XmlDocument();
        doc.LoadXml(text);
        var nodes=doc.SelectNodes(xpath);
        var results=new List<string>();
        if(nodes!=null)foreach(XmlNode n in nodes)results.Add(n.OuterXml);
        return ToolkitResult.Success(new{xpath,matchCount=results.Count,nodes=results.Take(100).ToArray()});
    }
    private static async Task<ToolkitResult> ToJson(string path,CancellationToken ct){
        if(!File.Exists(path))return ToolkitResult.Fail($"File not found: {path}");
        var text=await File.ReadAllTextAsync(path,ct).ConfigureAwait(false);
        var doc=XDocument.Parse(text);
        string ConvertElement(XElement el){
            var children=el.Elements().ToArray();
            if(!children.Any())return $"\"{el.Name.LocalName}\":\"{el.Value.Replace("\"","\\\"")}\"";
            var parts=children.Select(c=>{
                var group=children.Where(x=>x.Name==c.Name).ToArray();
                return group.Length>1?$"\"{c.Name.LocalName}\":[{string.Join(",",group.Select(g=>$"{{{ConvertElement(g)}}}"))}]":$"{ConvertElement(c)}";
            }).Distinct();
            return string.Join(",",parts);
        }
        var json=doc.Root!=null?$"{{{ConvertElement(doc.Root)}}}":"{}";
        return ToolkitResult.Success(new{path,json});
    }
    private static async Task<ToolkitResult> Validate(string path,CancellationToken ct){
        if(!File.Exists(path))return ToolkitResult.Fail($"File not found: {path}");
        try{
            var text=await File.ReadAllTextAsync(path,ct).ConfigureAwait(false);
            XDocument.Parse(text);
            return ToolkitResult.Success(new{path,valid=true,errors=Array.Empty<string>()});
        }catch(XmlException ex){
            return ToolkitResult.Success(new{path,valid=false,errors=new[]{ex.Message}});
        }
    }
}

// T-47 — GeoPandas-like (requires NetTopologySuite or similar)
[Toolkit("geopandas_like","GeoPandas-like",ToolkitCategory.DataAnalysis)]
public sealed class GeoPandasLikeToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"geopandas_like"; public override string Name=>"GeoPandas-like";
    public override ToolkitCategory Category=>ToolkitCategory.DataAnalysis;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Fail("Geo operations require package NetTopologySuite. Add it and implement geo queries."));
}
