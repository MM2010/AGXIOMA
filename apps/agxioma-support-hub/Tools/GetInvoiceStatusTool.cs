using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AgxiomaSupportHub.Tools;

/// <summary>
/// Returns the status of a customer invoice.
/// In production this would query a billing database or payment provider API.
/// </summary>
public sealed class GetInvoiceStatusTool : AgxiomeFunction
{
    public override string Name        => "get_invoice_status";
    public override string Description =>
        "Look up the status of a customer invoice by invoice ID or customer ID. " +
        "Returns amount, due date, payment status, and any outstanding balance.";

    [ToolParameter(Required = true, Description = "The invoice ID (e.g. INV-2026-0042) or customer ID (e.g. CUST-1234).")]
    public string InvoiceOrCustomerId { get; set; } = string.Empty;

    private static string Param(ToolContext ctx, string key, string fallback)
    {
        if (ctx.Parameters?.TryGetValue(key, out var v) == true) return v?.ToString() ?? fallback;
        return fallback;
    }

    public override Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var id = Param(context, "invoice_or_customer_id", InvoiceOrCustomerId).Trim().ToUpperInvariant();

        // ── Simulated billing database ─────────────────────────────────────────
        var invoices = new Dictionary<string, object>
        {
            ["INV-2026-0042"] = new
            {
                invoiceId   = "INV-2026-0042",
                customerId  = "CUST-1001",
                amount      = 149.99m,
                currency    = "USD",
                status      = "overdue",
                dueDate     = "2026-04-30",
                paidDate    = (string?)null,
                outstanding = 149.99m,
                items       = new[] { "Pro Plan — April 2026" }
            },
            ["INV-2026-0039"] = new
            {
                invoiceId   = "INV-2026-0039",
                customerId  = "CUST-1001",
                amount      = 149.99m,
                currency    = "USD",
                status      = "paid",
                dueDate     = "2026-03-31",
                paidDate    = "2026-03-28",
                outstanding = 0m,
                items       = new[] { "Pro Plan — March 2026" }
            },
            ["INV-2026-0055"] = new
            {
                invoiceId   = "INV-2026-0055",
                customerId  = "CUST-2002",
                amount      = 499.00m,
                currency    = "USD",
                status      = "pending",
                dueDate     = "2026-05-31",
                paidDate    = (string?)null,
                outstanding = 499.00m,
                items       = new[] { "Enterprise Plan — May 2026", "Seat expansion x5" }
            },
        };

        // Look up by invoice ID
        if (invoices.TryGetValue(id, out var invoice))
            return Task.FromResult(new ToolResult(true, invoice));

        // Look up all invoices for a customer
        var customerInvoices = invoices.Values
            .Where(v => v.GetType().GetProperty("customerId")?.GetValue(v)?.ToString() == id)
            .ToList();

        if (customerInvoices.Count > 0)
            return Task.FromResult(new ToolResult(true, new { customerId = id, invoices = customerInvoices }));

        return Task.FromResult(new ToolResult(false, null, $"No invoices found for ID '{id}'."));
    }
}
