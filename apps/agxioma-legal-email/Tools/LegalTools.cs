using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AgxiomaLegalEmail.Tools;

public sealed class SearchLegalCodeTool : AgxiomeFunction
{
    public override string Name => "search_legal_code";
    public override string Description =>
        "Search the Italian Civil Code or Penal Code for articles matching a legal question. " +
        "Returns relevant articles with citations.";

    [ToolParameter(Required = true, Description = "The legal question to search for (e.g., 'reato di furto con scasso').")]
    public string Query { get; set; } = string.Empty;

    [ToolParameter(Required = false, Description = "Code type: 'codice-civile', 'codice-penale', or 'all'.")]
    public string CodeType { get; set; } = "all";

    public override Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var query = context.Parameters?.TryGetValue("query", out var q) == true ? q?.ToString() : Query;
        var codeType = context.Parameters?.TryGetValue("code_type", out var c) == true ? c?.ToString() : CodeType;

        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult(new ToolResult(false, null, "Query is required."));

        return Task.FromResult(new ToolResult(true, new
        {
            query,
            codeType,
            message = "Query will be processed via WikiQueryEngine RAG pipeline.",
            instruction = "Use the RAG retrieval phase to find relevant articles."
        }));
    }
}

public sealed class DraftResponseTool : AgxiomeFunction
{
    public override string Name => "draft_legal_response";
    public override string Description =>
        "Generate a draft email response to a client's legal inquiry, citing relevant articles from the legal codes.";

    [ToolParameter(Required = true, Description = "The original client email/query.")]
    public string ClientQuery { get; set; } = string.Empty;

    [ToolParameter(Required = false, Description = "Relevant legal articles context (with citations).")]
    public string LegalContext { get; set; } = string.Empty;

    [ToolParameter(Required = false, Description = "The assigned legal area (civile, penale, etc.).")]
    public string LegalArea { get; set; } = string.Empty;

    public override Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        return Task.FromResult(new ToolResult(true, new
        {
            instruction = "Generate a professional email response with article citations.",
            format = "Dear [Client],\n\nIn response to your query regarding [topic],\nbased on [Article X Code Y]...\n\nBest regards,\n[Law Firm]"
        }));
    }
}

public sealed class LookupPrecedentTool : AgxiomeFunction
{
    public override string Name => "lookup_precedent";
    public override string Description =>
        "Search for similar legal cases in the archive based on keywords or legal area.";

    [ToolParameter(Required = true, Description = "The search query for finding similar cases.")]
    public string Query { get; set; } = string.Empty;

    public override Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        return Task.FromResult(new ToolResult(true, new
        {
            query = Query,
            message = "Precedent lookup feature - returns archived similar cases.",
            note = "Archive is seeded with demo data on first run."
        }));
    }
}

public sealed class EstimateCostTool : AgxiomeFunction
{
    public override string Name => "estimate_legal_cost";
    public override string Description =>
        "Estimate legal fees based on case complexity and type.";

    [ToolParameter(Required = true, Description = "Type of case: 'civil', 'criminal', 'administrative', 'labor'.")]
    public string CaseType { get; set; } = "civil";

    [ToolParameter(Required = false, Description = "Complexity: 'low', 'medium', 'high'.")]
    public string Complexity { get; set; } = "medium";

    public override Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var fees = new Dictionary<string, object>
        {
            ["civil"] = new { low = "€1.500-€3.000", medium = "€3.000-€8.000", high = "€8.000-€20.000" },
            ["criminal"] = new { low = "€2.000-€5.000", medium = "€5.000-€15.000", high = "€15.000-€40.000" },
            ["administrative"] = new { low = "€2.000-€4.000", medium = "€4.000-€10.000", high = "€10.000-€25.000" },
            ["labor"] = new { low = "€1.500-€3.000", medium = "€3.000-€7.000", high = "€7.000-€15.000" }
        };

        var estimate = fees.ContainsKey(CaseType) ? fees[CaseType] : fees["civil"];
        var range = ((dynamic)estimate).GetType().GetProperty(Complexity)?.GetValue(estimate) ?? "contattare lo studio";

        return Task.FromResult(new ToolResult(true, new
        {
            caseType = CaseType,
            complexity = Complexity,
            estimatedFees = range,
            note = "Stima indicativa. Il costo definitivo sarà determinato dopo valutazione del caso."
        }));
    }
}
