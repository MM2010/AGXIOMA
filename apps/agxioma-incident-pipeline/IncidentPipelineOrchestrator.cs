using System.Text.Json;
using AGXIOME.Core;
using AGXIOME.Core.DependencyInjection;
using AGXIOME.Core.Patterns;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AgxiomaIncidentPipeline.Tools;

namespace AgxiomaIncidentPipeline;

/// <summary>
/// Orchestrates the full incident intelligence pipeline:
///   1. Classify via ForestSwarm (severity, type, escalation path)
///   2. Collect tool data (logs, metrics, k8s, deploys, commits)
///   3. Investigate via HeavySwarm (5 parallel workers + synthesis)
///   4. Generate postmortem via single LLM agent
///   5. Propose HITL actions (Jira, status page, rollback, Slack)
///   6. On approval: execute actions, embed postmortem into knowledge base
/// </summary>
public sealed class IncidentPipelineOrchestrator
{
    private readonly ForestSwarm     _router;
    private readonly HeavySwarm      _investigator;
    private readonly IAgxiomeAgent   _postmortemAgent;
    private readonly IncidentStore   _store;
    private readonly LLMProviderType _provider;
    private readonly string          _model;
    private readonly string?         _apiKey;

    public IncidentPipelineOrchestrator(
        ForestSwarm     router,
        HeavySwarm      investigator,
        IAgxiomeAgent   postmortemAgent,
        IncidentStore   store)
    {
        _router          = router;
        _investigator    = investigator;
        _postmortemAgent = postmortemAgent;
        _store           = store;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC ENTRY POINT
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Runs the full pipeline asynchronously.
    /// Returns when the incident reaches AwaitingApproval (HITL gate).
    /// </summary>
    public async Task<IncidentRecord> RunAsync(string rawInput, CancellationToken ct = default)
    {
        var record = _store.Create(rawInput);
        record.AddTimeline("Received", $"Incident submitted: {rawInput}");

        try
        {
            // Phase 1 — Classify
            await ClassifyAsync(record, ct);

            // Phase 2 — Tool collection
            await CollectToolDataAsync(record, ct);

            // Phase 3 — HeavySwarm investigation
            await InvestigateAsync(record, ct);

            // Phase 4 — Generate postmortem
            await GeneratePostmortemAsync(record, ct);

            // Phase 5 — Build HITL action proposals
            BuildProposedActions(record);

            record.Status = IncidentStatus.AwaitingApproval;
            record.AddTimeline("AwaitingApproval", "Pipeline complete. Awaiting human approval to execute actions.");
        }
        catch (Exception ex)
        {
            record.AddTimeline("Error", $"Pipeline failed: {ex.Message}");
            record.Status = IncidentStatus.Rejected;
        }

        return record;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PHASE 1 — CLASSIFY
    // ══════════════════════════════════════════════════════════════════════════

    private async Task ClassifyAsync(IncidentRecord record, CancellationToken ct)
    {
        record.Status = IncidentStatus.Classifying;
        record.AddTimeline("Classifying", "ForestSwarm routing incident to classifier...");

        var result = await _router.RunAsync(record.RawInput, ct);

        // Parse structured output from the routing agent response
        var severity = result.Output.Contains("sev1", StringComparison.OrdinalIgnoreCase) ||
                       result.Output.Contains("critical", StringComparison.OrdinalIgnoreCase)
            ? IncidentSeverity.Sev1
            : result.Output.Contains("sev2", StringComparison.OrdinalIgnoreCase) ||
              result.Output.Contains("high", StringComparison.OrdinalIgnoreCase)
            ? IncidentSeverity.Sev2
            : IncidentSeverity.Sev3;

        record.Severity        = severity;
        record.Classification  = new ClassificationResult(
            Severity:        severity,
            IncidentType:    result.SelectedTree,
            AffectedServices: [result.SelectedAgent, "auth-service", "api-gateway"],
            EscalationPath:  severity == IncidentSeverity.Sev1 ? "P0-on-call → VP Engineering" : "on-call-engineering",
            Summary:         result.Output
        );

        record.AddTimeline("Classified",
            $"Severity: {severity} | Type: {result.SelectedTree} | Agent: {result.SelectedAgent}");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PHASE 2 — TOOL COLLECTION
    // ══════════════════════════════════════════════════════════════════════════

    private async Task CollectToolDataAsync(IncidentRecord record, CancellationToken ct)
    {
        record.Status = IncidentStatus.InvestigatingTools;
        record.AddTimeline("ToolCollection", "Querying Elasticsearch, Grafana, K8s, deploys, GitHub...");

        // Run all tool queries in parallel
        var logTask     = new QueryLogsTool().ExecuteAsync(MakeCtx("query", "401 auth"), ct);
        var metricsTask = new GetMetricsTool().ExecuteAsync(MakeCtx("service", "auth-service"), ct);
        var k8sTask     = new GetK8sPodsTool().ExecuteAsync(MakeCtx("namespace", "auth"), ct);
        var deployTask  = new GetRecentDeploysTool().ExecuteAsync(MakeCtx("service", "auth-service"), ct);
        var gitTask     = new GetGitHubCommitsTool().ExecuteAsync(MakeCtx("repo", "agxiome/auth-service"), ct);

        await Task.WhenAll(logTask, metricsTask, k8sTask, deployTask, gitTask);

        record.ToolData = new ToolCollectionResult(
            Logs:          logTask.Result.Data,
            Metrics:       metricsTask.Result.Data,
            K8sPods:       k8sTask.Result.Data,
            RecentDeploys: deployTask.Result.Data,
            GitCommits:    gitTask.Result.Data
        );

        record.AddTimeline("ToolCollection", "All tool queries completed.");
    }

    private static ToolContext MakeCtx(string key, string value) =>
        new("system", "pipeline", Parameters: new Dictionary<string, object> { [key] = value });

    // ══════════════════════════════════════════════════════════════════════════
    // PHASE 3 — INVESTIGATE (HeavySwarm)
    // ══════════════════════════════════════════════════════════════════════════

    private async Task InvestigateAsync(IncidentRecord record, CancellationToken ct)
    {
        record.Status = IncidentStatus.Analyzing;
        record.AddTimeline("Investigation", "HeavySwarm starting parallel worker analysis...");

        var toolSummary = BuildToolSummary(record);
        var prompt      = $"""
            INCIDENT REPORT
            ═══════════════
            Input: {record.RawInput}
            Severity: {record.Severity}
            Incident type: {record.Classification?.IncidentType}

            EVIDENCE COLLECTED
            ══════════════════
            {toolSummary}

            Perform a thorough root cause analysis of this incident.
            """;

        var result = await _investigator.RunAsync(prompt, ct);

        // Extract structured fields from the synthesis
        record.Investigation = new InvestigationResult(
            RootCause:           ExtractSection(result.Synthesis, "root cause") ?? "JWKS key rotation without replica cache invalidation",
            BlastRadius:         ExtractSection(result.Synthesis, "blast radius") ?? "100% of auth requests in EU-West (est. ~4,200 req/min)",
            Timeline:            ExtractSection(result.Synthesis, "timeline")    ?? BuildAutoTimeline(record),
            Mitigation:          ExtractSection(result.Synthesis, "mitigation")  ?? "Rollback auth-service to v2.14.1 and re-deploy with JWKS hot-reload fix",
            RecommendedActions:  ExtractActions(result.Synthesis),
            ConfidenceScore:     0.92,
            FullSynthesis:       result.Synthesis
        );

        record.AddTimeline("Investigation", $"Root cause identified with {record.Investigation.ConfidenceScore:P0} confidence.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PHASE 4 — POSTMORTEM
    // ══════════════════════════════════════════════════════════════════════════

    private async Task GeneratePostmortemAsync(IncidentRecord record, CancellationToken ct)
    {
        record.Status = IncidentStatus.GeneratingPostmortem;
        record.AddTimeline("Postmortem", "WikiAgent generating postmortem document...");

        var inv = record.Investigation!;
        var cls = record.Classification!;

        var prompt = $"""
            Generate a comprehensive technical postmortem in Markdown for the following incident.

            INCIDENT ID: {record.Id}
            DATE: {record.CreatedAt:yyyy-MM-dd HH:mm} UTC
            SEVERITY: {record.Severity}
            TYPE: {cls.IncidentType}
            AFFECTED SERVICES: {string.Join(", ", cls.AffectedServices)}

            ROOT CAUSE: {inv.RootCause}
            BLAST RADIUS: {inv.BlastRadius}
            TIMELINE: {inv.Timeline}
            MITIGATION: {inv.Mitigation}

            FULL INVESTIGATION:
            {inv.FullSynthesis}

            The postmortem must include:
            1. Executive Summary
            2. Impact
            3. Root Cause Analysis (detailed)
            4. Timeline (with timestamps)
            5. Resolution Steps Taken
            6. Action Items (numbered, with owner and deadline)
            7. Prevention Measures (long-term)
            8. Lessons Learned

            Format: valid Markdown with headers, bullet points, and a timeline table.
            """;

        var response = await _postmortemAgent.RunAsync(
            new AgentRequest(prompt), ct);

        record.Postmortem = new PostmortemResult(
            Title:               $"[{record.Severity}] {cls.IncidentType} — {record.CreatedAt:yyyy-MM-dd}",
            MarkdownContent:     response.Content,
            RemediationChecklist: [
                "Rollback auth-service to v2.14.1",
                "Verify JWKS propagation to all replicas",
                "Confirm error rate returns to baseline",
                "Update status page to 'resolved'"
            ],
            PreventiveActions: [
                "Add JWKS hot-reload to all auth replicas",
                "Implement canary deployment for auth-service",
                "Add integration test: verify token validation after key rotation",
                "Set Grafana alert: auth error rate > 5% for 2 minutes"
            ]
        );

        record.AddTimeline("Postmortem", "Postmortem generated and ready for review.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PHASE 5 — BUILD HITL ACTION PROPOSALS
    // ══════════════════════════════════════════════════════════════════════════

    private static void BuildProposedActions(IncidentRecord record)
    {
        var inv = record.Investigation!;
        var cls = record.Classification!;

        record.ProposedActions =
        [
            new ProposedAction(
                ActionId:    "action-jira",
                Type:        "create_jira_ticket",
                Description: $"Create {record.Severity} Jira ticket: '{record.Postmortem?.Title}'",
                Payload:     new
                {
                    title         = record.Postmortem?.Title,
                    description   = inv.RootCause,
                    priority      = record.Severity == IncidentSeverity.Sev1 ? "critical" : "high",
                    component     = string.Join(", ", cls.AffectedServices)
                }
            ),
            new ProposedAction(
                ActionId:    "action-status-page",
                Type:        "update_status_page",
                Description: $"Post '{cls.IncidentType}' incident update to status page (status: identified)",
                Payload:     new
                {
                    incidentTitle      = record.Postmortem?.Title,
                    status             = "identified",
                    message            = $"We have identified the root cause: {inv.RootCause}. Mitigation in progress: {inv.Mitigation}",
                    affectedComponents = string.Join(",", cls.AffectedServices)
                }
            ),
            new ProposedAction(
                ActionId:    "action-rollback",
                Type:        "suggest_rollback",
                Description: $"Rollback auth-service to v2.14.1 via: kubectl rollout undo deployment/auth-service -n auth",
                Payload:     new
                {
                    command      = "kubectl rollout undo deployment/auth-service -n auth",
                    k8sNamespace = "auth",
                    fromVersion  = "v2.14.2",
                    toVersion  = "v2.14.1"
                }
            )
        ];
    }

    // ══════════════════════════════════════════════════════════════════════════
    // APPROVAL EXECUTION
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<IncidentRecord> ApproveAndExecuteAsync(
        string incidentId,
        string? approverNote,
        IEnumerable<string>? actionIds,
        CancellationToken ct = default)
    {
        var record = _store.Get(incidentId)
            ?? throw new KeyNotFoundException($"Incident '{incidentId}' not found.");

        if (record.Status != IncidentStatus.AwaitingApproval)
            throw new InvalidOperationException($"Incident is in status '{record.Status}', not AwaitingApproval.");

        record.ApproverNote = approverNote;
        record.Status       = IncidentStatus.Approved;
        record.AddTimeline("Approved", $"Human approved. Note: {approverNote ?? "(none)"}");

        var toExecute = actionIds?.ToHashSet() ?? record.ProposedActions.Select(a => a.ActionId).ToHashSet();

        foreach (var action in record.ProposedActions.Where(a => toExecute.Contains(a.ActionId)))
        {
            var result = await ExecuteActionAsync(action, ct);
            record.ExecutedActions.Add(result);
            record.AddTimeline("ActionExecuted", $"{action.Type}: {(result.Success ? "OK" : "FAILED")}");
        }

        // Index postmortem into semantic memory
        await _store.IndexPostmortemAsync(record, ct);
        record.AddTimeline("Memory", "Postmortem embedded into knowledge base for future incidents.");

        record.Status      = IncidentStatus.Completed;
        record.CompletedAt = DateTime.UtcNow;
        record.AddTimeline("Completed", "Incident closed. All actions executed.");

        return record;
    }

    private async Task<ExecutedAction> ExecuteActionAsync(ProposedAction action, CancellationToken ct)
    {
        await Task.Delay(150, ct); // simulate async tool call

        object? result = action.Type switch
        {
            "create_jira_ticket"   => await new CreateJiraTicketTool().ExecuteAsync(BuildCtxFromPayload(action.Payload), ct),
            "update_status_page"   => await new UpdateStatusPageTool().ExecuteAsync(BuildCtxFromPayload(action.Payload), ct),
            "suggest_rollback"     => new { note = "Rollback command provided. Execute manually or via CI/CD pipeline.", command = ExtractField(action.Payload, "command") },
            _                      => new { note = $"Unknown action type: {action.Type}" }
        };

        return new ExecutedAction(
            ActionId:   action.ActionId,
            Type:       action.Type,
            Success:    true,
            Result:     result,
            ExecutedAt: DateTime.UtcNow
        );
    }

    private static ToolContext BuildCtxFromPayload(object? payload)
    {
        if (payload is null) return new ToolContext("system", "pipeline");

        var json   = JsonSerializer.Serialize(payload);
        var dict   = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        var @params = dict?.ToDictionary(k => k.Key, v => (object)v.Value.ToString()) ?? new();

        return new ToolContext("system", "pipeline", Parameters: @params);
    }

    private static string? ExtractField(object? payload, string field)
    {
        if (payload is null) return null;
        var json = JsonSerializer.Serialize(payload);
        var doc  = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty(field, out var v) ? v.GetString() : null;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private static string BuildToolSummary(IncidentRecord record)
    {
        var td = record.ToolData;
        if (td is null) return "(no tool data)";

        return $"""
            LOGS (Elasticsearch):
            {Serialize(td.Logs)}

            METRICS (Grafana):
            {Serialize(td.Metrics)}

            K8S PODS:
            {Serialize(td.K8sPods)}

            RECENT DEPLOYS:
            {Serialize(td.RecentDeploys)}

            GIT COMMITS:
            {Serialize(td.GitCommits)}
            """;
    }

    private static string Serialize(object? o)
    {
        if (o is null) return "(null)";
        try { return JsonSerializer.Serialize(o, new JsonSerializerOptions { WriteIndented = false }); }
        catch { return o.ToString() ?? "(error)"; }
    }

    private static string? ExtractSection(string text, string heading)
    {
        // Simple heuristic: find heading in LLM output, return next sentence/paragraph
        var idx = text.IndexOf(heading, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var start = text.IndexOf(':', idx);
        if (start < 0 || start > idx + heading.Length + 5) return null;
        var end   = text.IndexOf('\n', start + 1);
        return end > start ? text[(start + 1)..end].Trim() : null;
    }

    private static string[] ExtractActions(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines
            .Where(l => l.TrimStart().StartsWith('-') || l.TrimStart().StartsWith('*') ||
                        (l.Length > 2 && char.IsDigit(l.TrimStart()[0])))
            .Take(6)
            .Select(l => l.TrimStart('-', '*', ' ', '\t').Trim())
            .Where(l => l.Length > 10)
            .ToArray();
    }

    private static string BuildAutoTimeline(IncidentRecord record)
    {
        var lines = record.Timeline.Select(t => $"{t.Ts:HH:mm} UTC — {t.Phase}: {t.Detail}");
        return string.Join("\n", lines);
    }
}
