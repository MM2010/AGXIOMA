using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AgxiomaIncidentPipeline.Tools;

// ══════════════════════════════════════════════════════════════════════════════
// TOOL 1: query_logs
// Simulates an Elasticsearch / Kibana log query returning recent error entries.
// ══════════════════════════════════════════════════════════════════════════════

public sealed class QueryLogsTool : AgxiomeFunction
{
    public override string Name        => "query_logs";
    public override string Description =>
        "Query Elasticsearch for recent log entries matching a service or error pattern. " +
        "Returns the top matching log lines with timestamps, severity, and trace IDs.";

    [ToolParameter(Required = true,
        Description = "Service name or error pattern to search (e.g. 'auth-service', '401', 'NullPointerException').")]
    public string Query { get; set; } = string.Empty;

    [ToolParameter(Required = false,
        Description = "Time window in minutes to look back (default: 30).")]
    public int WindowMinutes { get; set; } = 30;

    private static string Param(ToolContext ctx, string key, string fallback)
    {
        if (ctx.Parameters?.TryGetValue(key, out var v) == true) return v?.ToString() ?? fallback;
        return fallback;
    }

    public override Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var query      = Param(ctx, "query", Query);
        var windowRaw  = Param(ctx, "window_minutes", WindowMinutes.ToString());
        var window     = int.TryParse(windowRaw, out var w) ? w : WindowMinutes;
        var now        = DateTime.UtcNow;

        // Mock log data — simulates auth-service 401 spike
        var logs = new[]
        {
            new { ts = now.AddMinutes(-2).ToString("o"),  level = "ERROR",   service = "auth-service",    msg = "JWT validation failed: token signature mismatch",         traceId = "4f3a1c9b", count = 842 },
            new { ts = now.AddMinutes(-3).ToString("o"),  level = "ERROR",   service = "auth-service",    msg = "Redis cache miss on session key: elevated latency 380ms",  traceId = "4f3a1c9b", count = 1204 },
            new { ts = now.AddMinutes(-5).ToString("o"),  level = "WARN",    service = "api-gateway",     msg = "Upstream auth-service responded 401 — request rejected",   traceId = "7e2b8d1a", count = 673 },
            new { ts = now.AddMinutes(-8).ToString("o"),  level = "ERROR",   service = "auth-service",    msg = "Secret rotation detected — JWK endpoint returned new key",  traceId = "9c4f7e2d", count = 1 },
            new { ts = now.AddMinutes(-8).ToString("o"),  level = "INFO",    service = "cert-manager",    msg = "TLS certificate renewed for auth.eu-west.internal",         traceId = "9c4f7e2d", count = 1 },
            new { ts = now.AddMinutes(-9).ToString("o"),  level = "WARN",    service = "auth-service",    msg = "JWKS cache TTL=0 detected — keys not propagated to replicas",traceId = "9c4f7e2d", count = 3 },
            new { ts = now.AddMinutes(-28).ToString("o"), level = "INFO",    service = "deploy-service",  msg = "auth-service v2.14.1 → v2.14.2 deployed to eu-west-1",     traceId = "a1b2c3d4", count = 1 },
        };

        var filtered = logs.Where(l =>
            l.service.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            l.msg.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            l.level.Contains(query, StringComparison.OrdinalIgnoreCase)
        ).ToArray();

        return Task.FromResult(new ToolResult(true, new
        {
            query,
            windowMinutes = window,
            totalMatches  = filtered.Length,
            entries       = filtered.Length > 0 ? filtered : logs   // fallback: return all if no match
        }));
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// TOOL 2: get_metrics
// Simulates a Grafana metrics query returning time-series data.
// ══════════════════════════════════════════════════════════════════════════════

public sealed class GetMetricsTool : AgxiomeFunction
{
    public override string Name        => "get_metrics";
    public override string Description =>
        "Fetch time-series metrics from Grafana/Prometheus for a given service. " +
        "Returns error rates, latency percentiles, request volume, and anomaly flags.";

    [ToolParameter(Required = true,
        Description = "Service name (e.g. 'auth-service', 'api-gateway', 'redis').")]
    public string Service { get; set; } = string.Empty;

    private static string Param(ToolContext ctx, string key, string fallback)
    {
        if (ctx.Parameters?.TryGetValue(key, out var v) == true) return v?.ToString() ?? fallback;
        return fallback;
    }

    public override Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var svc = Param(ctx, "service", Service).ToLowerInvariant();
        var now = DateTime.UtcNow;

        var metrics = svc switch
        {
            var s when s.Contains("auth") => new
            {
                service         = "auth-service",
                window          = "last 30m",
                errorRatePct    = 94.2,
                p50LatencyMs    = 380,
                p99LatencyMs    = 2100,
                requestsPerMin  = 4200,
                anomalyDetected = true,
                anomalyStart    = now.AddMinutes(-8).ToString("o"),
                sparkline       = new[] { 0.1, 0.2, 0.1, 0.1, 94.2, 95.1, 93.8 }  // error% per 5m bucket
            },
            var s when s.Contains("redis") => new
            {
                service         = "redis",
                window          = "last 30m",
                errorRatePct    = 0.0,
                p50LatencyMs    = 38,
                p99LatencyMs    = 420,
                requestsPerMin  = 18000,
                anomalyDetected = true,
                anomalyStart    = now.AddMinutes(-9).ToString("o"),
                sparkline       = new[] { 1.2, 1.1, 1.3, 42.0, 38.0, 40.1, 38.5 }  // latency ms p50
            },
            _ => new
            {
                service         = svc,
                window          = "last 30m",
                errorRatePct    = 0.3,
                p50LatencyMs    = 45,
                p99LatencyMs    = 120,
                requestsPerMin  = 2100,
                anomalyDetected = false,
                anomalyStart    = (string?)null,
                sparkline       = new[] { 0.2, 0.3, 0.3, 0.4, 0.3, 0.2, 0.3 }
            }
        };

        return Task.FromResult(new ToolResult(true, metrics));
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// TOOL 3: get_k8s_pods
// Simulates kubectl describe for a namespace/deployment.
// ══════════════════════════════════════════════════════════════════════════════

public sealed class GetK8sPodsTool : AgxiomeFunction
{
    public override string Name        => "get_k8s_pods";
    public override string Description =>
        "List Kubernetes pod status for a namespace or deployment. " +
        "Returns pod health, restart counts, resource usage, and recent events.";

    [ToolParameter(Required = true,
        Description = "Namespace or deployment name (e.g. 'auth', 'api-gateway', 'eu-west').")]
    public string Namespace { get; set; } = string.Empty;

    private static string Param(ToolContext ctx, string key, string fallback)
    {
        if (ctx.Parameters?.TryGetValue(key, out var v) == true) return v?.ToString() ?? fallback;
        return fallback;
    }

    public override Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var ns  = Param(ctx, "namespace", Namespace);
        var now = DateTime.UtcNow;

        var pods = new[]
        {
            new { name = "auth-service-7d9f4b-xk2q9", status = "Running",   ready = "1/1", restarts = 0,  cpuPct = 12,  memMb = 180, node = "node-eu-west-1a" },
            new { name = "auth-service-7d9f4b-p8m3n", status = "Running",   ready = "1/1", restarts = 0,  cpuPct = 14,  memMb = 178, node = "node-eu-west-1b" },
            new { name = "auth-service-7d9f4b-r7t1w", status = "Running",   ready = "1/1", restarts = 3,  cpuPct = 95,  memMb = 512, node = "node-eu-west-1c" },
            new { name = "auth-service-7d9f4b-n5v8k", status = "CrashLoop", ready = "0/1", restarts = 12, cpuPct = 0,   memMb = 0,   node = "node-eu-west-1a" },
        };

        var events = new[]
        {
            new { ts = now.AddMinutes(-8).ToString("o"), type = "Normal",  reason = "Pulled",         msg = "Successfully pulled image agxiome/auth-service:v2.14.2" },
            new { ts = now.AddMinutes(-8).ToString("o"), type = "Normal",  reason = "Created",        msg = "Created container auth-service" },
            new { ts = now.AddMinutes(-7).ToString("o"), type = "Warning", reason = "BackOff",        msg = "Back-off restarting failed container auth-service-7d9f4b-n5v8k" },
            new { ts = now.AddMinutes(-6).ToString("o"), type = "Warning", reason = "OOMKilled",      msg = "Container auth-service-7d9f4b-r7t1w OOMKilled (limit 512Mi)" },
        };

        return Task.FromResult(new ToolResult(true, new
        {
            @namespace = ns,
            pods,
            events,
            summary = new { total = pods.Length, running = 3, crashLoop = 1, restarting = 1 }
        }));
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// TOOL 4: get_recent_deploys
// Simulates querying deployment history.
// ══════════════════════════════════════════════════════════════════════════════

public sealed class GetRecentDeploysTool : AgxiomeFunction
{
    public override string Name        => "get_recent_deploys";
    public override string Description =>
        "Retrieve recent deployments for a service from the CI/CD pipeline. " +
        "Returns version, deployer, timestamp, change summary, and rollback status.";

    [ToolParameter(Required = false,
        Description = "Service name to filter (default: all services). Example: 'auth-service'.")]
    public string Service { get; set; } = "all";

    private static string Param(ToolContext ctx, string key, string fallback)
    {
        if (ctx.Parameters?.TryGetValue(key, out var v) == true) return v?.ToString() ?? fallback;
        return fallback;
    }

    public override Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var svc = Param(ctx, "service", Service);
        var now = DateTime.UtcNow;

        var deploys = new[]
        {
            new
            {
                service     = "auth-service",
                fromVersion = "v2.14.1",
                toVersion   = "v2.14.2",
                deployedBy  = "ci-pipeline / PR #1847",
                deployedAt  = now.AddMinutes(-28).ToString("o"),
                changeSummary = new[]
                {
                    "feat: rotate JWKS signing key monthly (closes #1801)",
                    "fix: increase Redis session TTL from 3600 to 7200",
                    "chore: bump openssl to 3.3.2"
                },
                canRollback = true,
                rollbackCmd = "kubectl rollout undo deployment/auth-service -n auth"
            },
            new
            {
                service     = "api-gateway",
                fromVersion = "v1.9.4",
                toVersion   = "v1.9.5",
                deployedBy  = "ci-pipeline / PR #1839",
                deployedAt  = now.AddHours(-4).ToString("o"),
                changeSummary = new[]
                {
                    "perf: enable HTTP/2 upstream connections",
                    "fix: rate-limiter bucket size correction"
                },
                canRollback = true,
                rollbackCmd = "kubectl rollout undo deployment/api-gateway -n gateway"
            }
        };

        var filtered = svc.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? deploys
            : deploys.Where(d => d.service.Contains(svc, StringComparison.OrdinalIgnoreCase)).ToArray();

        return Task.FromResult(new ToolResult(true, new
        {
            service = svc,
            count   = filtered.Length,
            deploys = filtered
        }));
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// TOOL 5: get_github_commits
// Simulates recent git commits for a repo/service.
// ══════════════════════════════════════════════════════════════════════════════

public sealed class GetGitHubCommitsTool : AgxiomeFunction
{
    public override string Name        => "get_github_commits";
    public override string Description =>
        "Fetch recent commits for a GitHub repository. " +
        "Returns commit SHA, author, message, changed files, and PR links.";

    [ToolParameter(Required = true,
        Description = "Repository name or path (e.g. 'agxiome/auth-service').")]
    public string Repo { get; set; } = string.Empty;

    private static string Param(ToolContext ctx, string key, string fallback)
    {
        if (ctx.Parameters?.TryGetValue(key, out var v) == true) return v?.ToString() ?? fallback;
        return fallback;
    }

    public override Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var repo = Param(ctx, "repo", Repo);
        var now  = DateTime.UtcNow;

        var commits = new[]
        {
            new
            {
                sha      = "3f9a1c2d",
                author   = "alice@agxiome.ai",
                message  = "feat: rotate JWKS signing key monthly",
                pr       = "PR #1847",
                mergedAt = now.AddMinutes(-35).ToString("o"),
                files    = new[] { "src/auth/jwks_rotator.go", "src/auth/config.go", "deploy/k8s/auth-configmap.yaml" },
                note     = "Introduced JWKS rotation without hot-reload of replica caches"
            },
            new
            {
                sha      = "8b2e4f1a",
                author   = "bob@agxiome.ai",
                message  = "fix: increase Redis session TTL",
                pr       = "PR #1847",
                mergedAt = now.AddMinutes(-35).ToString("o"),
                files    = new[] { "src/auth/session_store.go" },
                note     = "TTL extended but cache warm-up on rotation not handled"
            },
            new
            {
                sha      = "1d5c8e3b",
                author   = "carol@agxiome.ai",
                message  = "chore: bump openssl 3.3.2",
                pr       = "PR #1845",
                mergedAt = now.AddHours(-2).ToString("o"),
                files    = new[] { "go.mod", "go.sum" },
                note     = ""
            }
        };

        return Task.FromResult(new ToolResult(true, new { repo, commits }));
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// TOOL 6: create_jira_ticket
// Simulates opening a Jira incident ticket.
// ══════════════════════════════════════════════════════════════════════════════

public sealed class CreateJiraTicketTool : AgxiomeFunction
{
    public override string Name        => "create_jira_ticket";
    public override string Description =>
        "Create a Jira incident ticket and assign it to the on-call team. " +
        "Returns the ticket ID, URL, and SLA deadline.";

    [ToolParameter(Required = true,  Description = "Ticket title / summary.")]
    public string Title { get; set; } = string.Empty;

    [ToolParameter(Required = true,  Description = "Detailed description including impact, timeline, and findings.")]
    public string IssueDescription { get; set; } = string.Empty;

    [ToolParameter(Required = false, Description = "Priority: critical, high, medium, low. Default: high.")]
    public string Priority { get; set; } = "high";

    [ToolParameter(Required = false, Description = "Component / service affected.")]
    public string Component { get; set; } = "unknown";

    private static string Param(ToolContext ctx, string key, string fallback)
    {
        if (ctx.Parameters?.TryGetValue(key, out var v) == true) return v?.ToString() ?? fallback;
        return fallback;
    }

    public override Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var title       = Param(ctx, "title", Title);
        var description = Param(ctx, "issue_description", IssueDescription);
        var priority    = Param(ctx, "priority", Priority).ToLowerInvariant();
        var component   = Param(ctx, "component", Component);

        var ticketId = $"INC-{Random.Shared.Next(10000, 99999)}";
        var sla      = priority switch { "critical" => "1h", "high" => "4h", _ => "24h" };

        return Task.FromResult(new ToolResult(true, new
        {
            ticketId,
            url           = $"https://jira.agxiome.internal/browse/{ticketId}",
            title,
            priority,
            component,
            assignedTo    = "on-call-engineering",
            status        = "open",
            slaDeadline   = DateTime.UtcNow.Add(priority == "critical" ? TimeSpan.FromHours(1) : TimeSpan.FromHours(4)).ToString("o"),
            slaDuration   = sla,
            createdAt     = DateTime.UtcNow.ToString("o"),
            message       = $"Jira ticket {ticketId} created ({priority} priority, SLA {sla}). On-call team notified."
        }));
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// TOOL 7: update_status_page
// Simulates posting an incident update to the public status page.
// ══════════════════════════════════════════════════════════════════════════════

public sealed class UpdateStatusPageTool : AgxiomeFunction
{
    public override string Name        => "update_status_page";
    public override string Description =>
        "Post an incident update to the public status page and optionally send a Slack notification. " +
        "Use this after confirming root cause and mitigation steps.";

    [ToolParameter(Required = true,  Description = "Incident title displayed on the status page.")]
    public string IncidentTitle { get; set; } = string.Empty;

    [ToolParameter(Required = true,  Description = "Status: investigating, identified, monitoring, resolved.")]
    public string Status { get; set; } = "investigating";

    [ToolParameter(Required = true,  Description = "Public-facing message describing the impact and mitigation.")]
    public string Message { get; set; } = string.Empty;

    [ToolParameter(Required = false, Description = "Affected components (comma-separated). Example: 'API,Auth,Dashboard'.")]
    public string AffectedComponents { get; set; } = "API";

    private static string Param(ToolContext ctx, string key, string fallback)
    {
        if (ctx.Parameters?.TryGetValue(key, out var v) == true) return v?.ToString() ?? fallback;
        return fallback;
    }

    public override Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var title      = Param(ctx, "incident_title", IncidentTitle);
        var status     = Param(ctx, "status", Status).ToLowerInvariant();
        var message    = Param(ctx, "message", Message);
        var components = Param(ctx, "affected_components", AffectedComponents)
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var updateId = $"STAT-{Random.Shared.Next(100000, 999999)}";

        return Task.FromResult(new ToolResult(true, new
        {
            updateId,
            statusPageUrl  = $"https://status.agxiome.ai/incidents/{updateId}",
            title,
            status,
            message,
            affectedComponents = components,
            postedAt       = DateTime.UtcNow.ToString("o"),
            slackNotified  = true,
            slackChannel   = "#incidents",
            message2       = $"Status page updated ('{status}'). Slack notification sent to #incidents."
        }));
    }
}
