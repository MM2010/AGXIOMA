using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AgxiomaSupportHub.Tools;

/// <summary>
/// Creates a support ticket in the ticketing system.
/// Returns a ticket ID and estimated response time based on priority.
/// </summary>
public sealed class CreateTicketTool : AgxiomeFunction
{
    public override string Name        => "create_ticket";
    public override string Description =>
        "Create a support ticket for an issue that needs engineer follow-up. " +
        "Use this when the problem cannot be resolved immediately. " +
        "Returns a ticket ID and estimated response time.";

    [ToolParameter(Required = true, Description = "Short title describing the issue (max 120 chars).")]
    public string Title { get; set; } = string.Empty;

    [ToolParameter(Required = true, Description = "Detailed description of the issue, steps to reproduce, and impact.")]
    public string IssueDescription { get; set; } = string.Empty;

    [ToolParameter(Required = false,
        Description = "Priority: 'critical' (SLA 1h), 'high' (4h), 'medium' (24h), 'low' (72h). Default: medium.")]
    public string Priority { get; set; } = "medium";

    [ToolParameter(Required = false, Description = "Customer or user ID affected by this issue.")]
    public string? AffectedCustomerId { get; set; }

    private static string Param(ToolContext ctx, string key, string fallback)
    {
        if (ctx.Parameters?.TryGetValue(key, out var v) == true) return v?.ToString() ?? fallback;
        return fallback;
    }

    public override Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var title       = Param(context, "title", Title).Trim();
        var description = Param(context, "issue_description", IssueDescription).Trim();
        var priority    = Param(context, "priority", Priority).Trim().ToLowerInvariant();
        object? custV = null;
        context.Parameters?.TryGetValue("affected_customer_id", out custV);
        var customerId  = custV?.ToString();

        if (string.IsNullOrWhiteSpace(title))
            return Task.FromResult(new ToolResult(false, null, "title is required."));
        if (title.Length > 120)
            return Task.FromResult(new ToolResult(false, null, "title must be 120 characters or less."));
        if (string.IsNullOrWhiteSpace(description))
            return Task.FromResult(new ToolResult(false, null, "issue_description is required."));

        var validPriorities = new[] { "critical", "high", "medium", "low" };
        if (!validPriorities.Contains(priority))
            priority = "medium";

        var sla = priority switch
        {
            "critical" => "1 hour",
            "high"     => "4 hours",
            "medium"   => "24 hours",
            "low"      => "72 hours",
            _          => "24 hours"
        };

        var ticketId = $"TKT-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}";

        return Task.FromResult(new ToolResult(true, new
        {
            ticketId           = ticketId,
            title              = title,
            priority           = priority,
            status             = "open",
            assignedTeam       = priority is "critical" or "high" ? "on-call-engineering" : "support-tier-2",
            affectedCustomerId = customerId,
            createdAt          = DateTime.UtcNow.ToString("o"),
            estimatedResponse  = sla,
            message            = $"Ticket {ticketId} created with {priority} priority. " +
                                 $"Expected first response within {sla}."
        }));
    }
}
