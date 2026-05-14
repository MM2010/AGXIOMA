using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AgxiomaSupportHub.Tools;

/// <summary>
/// Initiates a refund for an invoice.
/// Returns a refund confirmation with a reference number.
/// </summary>
public sealed class ProcessRefundTool : AgxiomeFunction
{
    public override string Name        => "process_refund";
    public override string Description =>
        "Initiate a full or partial refund for a paid invoice. " +
        "Requires the invoice ID and the amount to refund. Returns a refund reference number.";

    [ToolParameter(Required = true, Description = "The invoice ID to refund (e.g. INV-2026-0039).")]
    public string InvoiceId { get; set; } = string.Empty;

    [ToolParameter(Required = true, Description = "The refund amount in the invoice's currency. Must be > 0 and ≤ invoice amount.", Type = "number")]
    public decimal Amount { get; set; }

    [ToolParameter(Required = false, Description = "Reason for the refund (optional, stored for records).")]
    public string Reason { get; set; } = "Customer request";

    private static string Param(ToolContext ctx, string key, string fallback)
    {
        if (ctx.Parameters?.TryGetValue(key, out var v) == true) return v?.ToString() ?? fallback;
        return fallback;
    }

    public override Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var invoiceId = Param(context, "invoice_id", InvoiceId).Trim().ToUpperInvariant();
        var amountRaw = Param(context, "amount", Amount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var amount    = decimal.TryParse(amountRaw, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var a) ? a : Amount;
        var reason    = Param(context, "reason", Reason);

        if (string.IsNullOrWhiteSpace(invoiceId))
            return Task.FromResult(new ToolResult(false, null, "invoice_id is required."));
        if (amount <= 0)
            return Task.FromResult(new ToolResult(false, null, "Refund amount must be greater than zero."));

        // ── Simulate refund logic ──────────────────────────────────────────────
        // Only paid invoices can be refunded
        var paidInvoices = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "INV-2026-0039" };

        if (!paidInvoices.Contains(invoiceId))
            return Task.FromResult(new ToolResult(false, null,
                $"Invoice '{invoiceId}' cannot be refunded — it is not in 'paid' status. " +
                "Only paid invoices are eligible for refunds."));

        if (amount > 149.99m)
            return Task.FromResult(new ToolResult(false, null,
                $"Refund amount ${amount:F2} exceeds the invoice total of $149.99."));

        var refundRef = $"REF-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";

        return Task.FromResult(new ToolResult(true, new
        {
            refundReference  = refundRef,
            invoiceId        = invoiceId,
            amountRefunded   = amount,
            currency         = "USD",
            reason           = reason,
            estimatedArrival = "3-5 business days",
            status           = "approved",
            message          = $"Refund of ${amount:F2} approved. Reference: {refundRef}. " +
                               "Funds will appear in 3-5 business days."
        }));
    }
}
