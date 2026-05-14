using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AgxiomaSupportHub.Tools;

/// <summary>
/// Returns the real-time health status of platform services.
/// In production this would call an internal status API or monitoring system.
/// </summary>
public sealed class GetSystemStatusTool : AgxiomeFunction
{
    public override string Name        => "get_system_status";
    public override string Description =>
        "Check the operational status of a platform service or component. " +
        "Pass a service name (e.g. 'api', 'auth', 'webhooks', 'dashboard', 'all') to get current health.";

    [ToolParameter(Required = true,
        Description = "Service to check: 'api', 'auth', 'webhooks', 'dashboard', 'database', 'cdn', or 'all'.")]
    public string Service { get; set; } = "all";

    private static readonly Dictionary<string, ServiceStatus> _statuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["api"]       = new("api",       "operational",  99.97, null),
        ["auth"]      = new("auth",      "degraded",     95.40, "Elevated latency on token refresh endpoints. Engineers investigating. Started 14:22 UTC."),
        ["webhooks"]  = new("webhooks",  "operational",  99.91, null),
        ["dashboard"] = new("dashboard", "operational",  99.99, null),
        ["database"]  = new("database",  "operational",  100.0, null),
        ["cdn"]       = new("cdn",       "operational",  99.85, null),
    };

    public override Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var svc = (context.Parameters?.TryGetValue("service", out var v) == true && v != null
            ? v.ToString() : Service) ?? Service;
        svc = svc.Trim().ToLowerInvariant();

        if (svc == "all")
        {
            var overall = _statuses.Values.All(s => s.Status == "operational")
                ? "all_operational"
                : "partial_outage";
            return Task.FromResult(new ToolResult(true, new
            {
                overall,
                checkedAt = DateTime.UtcNow.ToString("o"),
                services  = _statuses.Values
            }));
        }

        if (_statuses.TryGetValue(svc, out var status))
            return Task.FromResult(new ToolResult(true, new
            {
                checkedAt = DateTime.UtcNow.ToString("o"),
                service   = status
            }));

        return Task.FromResult(new ToolResult(false, null,
            $"Unknown service '{svc}'. Available: {string.Join(", ", _statuses.Keys)}, all."));
    }

    private record ServiceStatus(
        string Name,
        string Status,
        double UptimePct,
        string? Incident);
}
