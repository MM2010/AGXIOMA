using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AgxiomaSupportHub.Tools;

/// <summary>
/// Returns current pricing and plan details.
/// Useful for the sales agent to give accurate information to prospects.
/// </summary>
public sealed class GetPricingInfoTool : AgxiomeFunction
{
    public override string Name        => "get_pricing_info";
    public override string Description =>
        "Retrieve current pricing, feature comparison, and available discounts for a plan. " +
        "Pass a plan name or 'all' to get the full pricing table.";

    [ToolParameter(Required = false,
        Description = "Plan name: 'starter', 'pro', 'enterprise', or 'all' (default: all).")]
    public string Plan { get; set; } = "all";

    private static readonly IReadOnlyDictionary<string, object> _plans =
        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["starter"] = new
            {
                name        = "Starter",
                monthlyUsd  = 0m,
                annualUsd   = 0m,
                seats       = 1,
                apiCalls    = "10,000 / month",
                storage     = "5 GB",
                support     = "Community",
                highlights  = new[] { "1 user", "Basic API access", "Community forum" }
            },
            ["pro"] = new
            {
                name        = "Pro",
                monthlyUsd  = 149.99m,
                annualUsd   = 1_319.88m,   // 10% off
                seats       = 10,
                apiCalls    = "500,000 / month",
                storage     = "100 GB",
                support     = "Email (24h SLA)",
                highlights  = new[] {
                    "Up to 10 users", "Priority API access", "Webhooks", "Analytics dashboard",
                    "Email support", "SSO (SAML 2.0)"
                }
            },
            ["enterprise"] = new
            {
                name        = "Enterprise",
                monthlyUsd  = (decimal?)null,      // custom pricing
                annualUsd   = (decimal?)null,
                seats       = "Unlimited",
                apiCalls    = "Unlimited",
                storage     = "Unlimited",
                support     = "Dedicated CSM + 1h SLA",
                highlights  = new[] {
                    "Unlimited seats", "Custom API limits", "On-premise option",
                    "Dedicated success manager", "Custom contracts", "Audit logs",
                    "Custom data retention", "SLA guarantee 99.99%"
                },
                contactSales = "sales@agxiome.ai"
            }
        };

    public override Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var plan = (context.Parameters?.TryGetValue("plan", out var v) == true && v != null
            ? v.ToString() : Plan) ?? Plan;
        plan = plan.Trim().ToLowerInvariant();

        if (plan == "all")
            return Task.FromResult(new ToolResult(true, new
            {
                currency       = "USD",
                annualDiscount = "10% off all paid plans",
                plans          = _plans.Values,
                trialAvailable = "14-day free trial on Pro plan — no credit card required",
                contact        = "sales@agxiome.ai"
            }));

        if (_plans.TryGetValue(plan, out var info))
            return Task.FromResult(new ToolResult(true, info));

        return Task.FromResult(new ToolResult(false, null,
            $"Unknown plan '{plan}'. Available: {string.Join(", ", _plans.Keys)}, all."));
    }
}
