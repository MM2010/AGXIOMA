using System.Text.Json;
using AGXIOME.Core.Patterns;
using AGXIOME.Storage.Workflow;
using AGXIOME.Core.Workflows;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.Stores;
using AGXIOME.Toolkits;
using AGXIOME.Toolkits.Communication;
using AGXIOME.Toolkits.DataAnalysis;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

public sealed class BackofficeRuntimeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ForestSwarm _router;
    private readonly ForestSwarm _contentSwarm;
    private readonly SqliteCommandStore _projectionStore;
    private readonly ICommandStore _canonicalStore;
    private readonly RedisSessionStore? _hotStore;
    private readonly WorkflowHistoryStore _historyStore;
    private readonly WorkflowHitlStore _hitlStore;
    private readonly SqliteToolkit _sql;
    private readonly EmailSmtpToolkit? _email;
    private readonly TelegramToolkit? _telegram;
    private readonly WhatsAppToolkit? _whatsapp;
    private readonly ILogger<BackofficeRuntimeService> _log;
    private readonly string _dbPath;
    private readonly string _runtimeMode;
    private readonly bool _isEnterpriseMode;
    private readonly string? _postgresConnectionString;

    public BackofficeRuntimeService(
        IEnumerable<ForestSwarm> swarms,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<BackofficeRuntimeService> log)
    {
        _router = swarms.First(s => string.Equals(s.Name, "backoffice-router", StringComparison.OrdinalIgnoreCase));
        _contentSwarm = swarms.First(s => string.Equals(s.Name, "backoffice-content", StringComparison.OrdinalIgnoreCase));
        _log = log;

        var appDataPath = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(appDataPath);

        _dbPath = Path.Combine(appDataPath, "backoffice-demo.db");
        _projectionStore = new SqliteCommandStore(new SqliteStoreOptions
        {
            ConnectionString = $"Data Source={_dbPath}",
            AutoCreateSchema = true
        });

        _runtimeMode = (configuration["Backoffice:Mode"] ?? "local").Trim().ToLowerInvariant();
        _isEnterpriseMode = string.Equals(_runtimeMode, "enterprise", StringComparison.OrdinalIgnoreCase);
        _postgresConnectionString = configuration["Backoffice:Enterprise:PostgresConnectionString"];

        if (_isEnterpriseMode)
        {
            if (string.IsNullOrWhiteSpace(_postgresConnectionString))
                throw new InvalidOperationException("Backoffice enterprise mode requires Backoffice:Enterprise:PostgresConnectionString.");

            var redisConnectionString = configuration["Backoffice:Enterprise:RedisConnectionString"];
            if (string.IsNullOrWhiteSpace(redisConnectionString))
                throw new InvalidOperationException("Backoffice enterprise mode requires Backoffice:Enterprise:RedisConnectionString.");

            _canonicalStore = new PostgresCommandStore(_postgresConnectionString);
            _hotStore = new RedisSessionStore(new RedisSessionStoreOptions
            {
                ConnectionString = redisConnectionString,
                KeyPrefix = "agxiome:backoffice:runs",
                Expiry = TimeSpan.FromHours(24)
            });
        }
        else
        {
            _canonicalStore = _projectionStore;
        }

        _historyStore = new WorkflowHistoryStore(_canonicalStore);
        _hitlStore = new WorkflowHitlStore(_canonicalStore);
        _sql = new SqliteToolkit(new ToolkitConfig("sqlite_query", new()));

        _email = BuildEmailToolkit(configuration);
        _telegram = BuildTelegramToolkit(configuration);
        _whatsapp = BuildWhatsAppToolkit(configuration);
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await ExecSqlAsync(
            """
            CREATE TABLE IF NOT EXISTS customers (
                id TEXT PRIMARY KEY,
                email TEXT NOT NULL,
                phone TEXT,
                tier TEXT,
                locale TEXT
            );

            CREATE TABLE IF NOT EXISTS orders (
                id TEXT PRIMARY KEY,
                customer_id TEXT NOT NULL,
                status TEXT NOT NULL,
                total_amount REAL NOT NULL,
                shipping_address TEXT,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS invoices (
                id TEXT PRIMARY KEY,
                order_id TEXT NOT NULL,
                customer_id TEXT NOT NULL,
                amount REAL NOT NULL,
                status TEXT NOT NULL,
                issued_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS policies (
                id TEXT PRIMARY KEY,
                area TEXT NOT NULL,
                title TEXT NOT NULL,
                content TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS backoffice_metrics (
                id TEXT PRIMARY KEY,
                run_id TEXT NOT NULL,
                area TEXT NOT NULL,
                response_seconds REAL NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS "BackofficeRunStates" (
                "Id" TEXT PRIMARY KEY,
                "CustomerId" TEXT NOT NULL,
                "CustomerEmail" TEXT NOT NULL,
                "CustomerPhone" TEXT NULL,
                "TelegramChatId" TEXT NULL,
                "Subject" TEXT NOT NULL,
                "Message" TEXT NOT NULL,
                "PreferredLanguage" TEXT NOT NULL,
                "PreferredChannelsJson" TEXT NOT NULL,
                "Area" TEXT NOT NULL,
                "Priority" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "TicketId" TEXT NULL,
                "CheckpointId" TEXT NULL,
                "ConfirmationToken" TEXT NULL,
                "ConfirmationId" TEXT NULL,
                "CustomerReply" TEXT NOT NULL,
                "ResolutionPlan" TEXT NOT NULL,
                "ReviewNotes" TEXT NOT NULL,
                "OrdersDataJson" TEXT NOT NULL,
                "InvoiceDataJson" TEXT NOT NULL,
                "PolicyMatchesJson" TEXT NOT NULL,
                "ChannelsNotifiedJson" TEXT NOT NULL,
                "TimelineJson" TEXT NOT NULL,
                "AvgResponseSeconds" REAL NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS "EmailConfirmations" (
                "Id" TEXT PRIMARY KEY,
                "RunId" TEXT NOT NULL,
                "CustomerEmail" TEXT NOT NULL,
                "Purpose" TEXT NOT NULL,
                "Token" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "ConfirmedAt" TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS "BackofficeTickets" (
                "Id" TEXT PRIMARY KEY,
                "RunId" TEXT NOT NULL,
                "CustomerId" TEXT NOT NULL,
                "Area" TEXT NOT NULL,
                "Subject" TEXT NOT NULL,
                "Description" TEXT NOT NULL,
                "Priority" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS "FollowUps" (
                "Id" TEXT PRIMARY KEY,
                "TicketId" TEXT NOT NULL,
                "CustomerId" TEXT NOT NULL,
                "DueAt" TEXT NOT NULL,
                "Channel" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "Notes" TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS "BackofficeSurveys" (
                "Id" TEXT PRIMARY KEY,
                "TicketId" TEXT NOT NULL,
                "CustomerEmail" TEXT NOT NULL,
                "SurveyUrl" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS "BackofficeSurveyResponses" (
                "Id" TEXT PRIMARY KEY,
                "TicketId" TEXT NOT NULL,
                "Score" INTEGER NOT NULL,
                "Comment" TEXT NULL,
                "SubmittedAt" TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS "BackofficeNotifications" (
                "Id" TEXT PRIMARY KEY,
                "RunId" TEXT NOT NULL,
                "Channel" TEXT NOT NULL,
                "Recipient" TEXT NOT NULL,
                "Subject" TEXT NULL,
                "Message" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL
            );
            """,
            ct);

        await ExecSqlAsync(
            """
            INSERT OR IGNORE INTO customers (id, email, phone, tier, locale)
            VALUES ('CUST-1001', 'mario.rossi@example.com', '+393331234567', 'vip', 'it-IT');

            INSERT OR IGNORE INTO orders (id, customer_id, status, total_amount, shipping_address, created_at)
            VALUES ('ORD-5001', 'CUST-1001', 'shipped', 249.00, 'Via Roma 10, Milano', '2026-05-12T09:00:00Z');

            INSERT OR IGNORE INTO invoices (id, order_id, customer_id, amount, status, issued_at)
            VALUES ('INV-9001', 'ORD-5001', 'CUST-1001', 249.00, 'issued', '2026-05-12T09:05:00Z');

            INSERT OR IGNORE INTO policies (id, area, title, content)
            VALUES ('POL-ORD-01', 'orders', 'Delivery change policy', 'Address changes require verification before shipment handoff.');

            INSERT OR IGNORE INTO policies (id, area, title, content)
            VALUES ('POL-BIL-01', 'billing', 'Double charge policy', 'Double charges require invoice lookup, payment review, and refund validation.');

            INSERT OR IGNORE INTO policies (id, area, title, content)
            VALUES ('POL-REF-01', 'refunds', 'Refund approval policy', 'Refunds and disputes require explicit customer confirmation before execution.');

            INSERT OR IGNORE INTO policies (id, area, title, content)
            VALUES ('POL-ACC-01', 'account', 'Sensitive account policy', 'Identity or profile changes require confirmation and audit logging.');

            INSERT OR IGNORE INTO policies (id, area, title, content)
            VALUES ('POL-TEC-01', 'technical', 'Incident handling policy', 'Technical incidents must capture impact, workaround, and escalation notes.');
            """,
            ct);

        if (_isEnterpriseMode)
            await EnsurePostgresSchemaAsync(ct);
    }

    public async Task<RunRecord> CreateRunAsync(BackofficeRequestInput input, CancellationToken ct = default)
    {
        var runId = Guid.CreateVersion7().ToString();
        var startedAt = DateTimeOffset.UtcNow;
        var timeline = new List<RunTimelineEntry>
        {
            NewTimeline("Richiesta ricevuta", input.Subject)
        };

        var state = new Dictionary<string, object>
        {
            ["request"] = input,
            ["runId"] = runId,
            ["startedAt"] = startedAt,
            ["timeline"] = timeline,
            ["channelsNotified"] = new List<string>()
        };

        var workflow = BuildInitialWorkflow();
        try
        {
            await workflow.ExecuteAsync(new WorkflowContext($"backoffice-{runId}", state, ct), ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Backoffice run {RunId} failed during initial workflow execution for subject {Subject}", runId, input.Subject);
            throw;
        }

        var run = await GetRunAsync(runId, ct);
        if (run is null)
            throw new InvalidOperationException($"Backoffice run '{runId}' was not persisted.");

        return run;
    }

    public async Task<RunRecord?> GetRunAsync(string runId, CancellationToken ct = default)
    {
        var entity = await GetRunStateAsync(runId, ct);
        return entity is null ? null : MapToRunRecord(entity);
    }

    public async Task<RunRecord?> ConfirmRunAsync(string token, CancellationToken ct = default)
    {
        var confirmation = await GetConfirmationByTokenAsync(token, ct);
        if (confirmation is null)
            return null;

        var run = await GetRunStateAsync(confirmation.RunId, ct);
        if (run is null)
            return null;

        if (!string.Equals(run.Status, "paused-awaiting-confirmation", StringComparison.OrdinalIgnoreCase))
            return MapToRunRecord(run);

        var updatedConfirmation = confirmation with
        {
            Status = "confirmed",
            ConfirmedAt = DateTimeOffset.UtcNow
        };

        await UpdateEntityAsync(updatedConfirmation, ct);

        var timeline = DeserializeTimeline(run.TimelineJson).ToList();
        timeline.Add(NewTimeline("Conferma ricevuta", "L'utente ha confermato via mail l'operazione sensibile."));

        var state = new Dictionary<string, object>
        {
            ["request"] = ToRequest(run),
            ["runId"] = run.Id,
            ["startedAt"] = run.CreatedAt,
            ["timeline"] = timeline,
            ["channelsNotified"] = DeserializeStringList(run.ChannelsNotifiedJson).ToList(),
            ["area"] = run.Area,
            ["priority"] = run.Priority,
            ["ordersDataJson"] = run.OrdersDataJson,
            ["invoiceDataJson"] = run.InvoiceDataJson,
            ["policyMatchesJson"] = run.PolicyMatchesJson,
            ["resolutionPlan"] = run.ResolutionPlan,
            ["reviewNotes"] = run.ReviewNotes,
            ["customerReply"] = run.CustomerReply,
            ["resumedAfterConfirmation"] = true
        };

        var workflow = BuildPostConfirmationWorkflow();
        await workflow.ExecuteAsync(new WorkflowContext($"backoffice-{run.Id}", state, ct), ct);

        return await GetRunAsync(run.Id, ct);
    }

    public async Task<RunRecord?> CloseRunAsync(string runId, CancellationToken ct = default)
    {
        var run = await GetRunStateAsync(runId, ct);
        if (run is null)
            return null;

        var timeline = DeserializeTimeline(run.TimelineJson).ToList();
        timeline.Add(NewTimeline("Caso chiuso", "Il backoffice ha chiuso il caso e ha inviato il questionario CSAT."));

        var updated = run with
        {
            Status = "closed",
            TimelineJson = Serialize(timeline),
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await UpdateEntityAsync(updated, ct);
        return MapToRunRecord(updated);
    }

    public async Task<SurveyRecord> SaveSurveyAsync(string ticketId, int score, string? comment, CancellationToken ct = default)
    {
        var entity = new BackofficeSurveyResponse(
            Id: Guid.CreateVersion7().ToString(),
            TicketId: ticketId,
            Score: score,
            Comment: comment,
            SubmittedAt: DateTimeOffset.UtcNow);

        await SaveEntityAsync(entity, ct);
        return new SurveyRecord(ticketId, score, comment, entity.SubmittedAt);
    }

    public async Task<IReadOnlyList<BackofficeRunListItem>> ListRunsAsync(int limit = 25, CancellationToken ct = default)
    {
        var rows = await QueryRowsAsync(
            $"SELECT Id, Area, Priority, Status, TicketId, CheckpointId, AvgResponseSeconds, CreatedAt, UpdatedAt FROM \"BackofficeRunStates\" ORDER BY UpdatedAt DESC LIMIT {Math.Max(1, limit)};",
            ct);

        return rows.Select(MapRunListItem).ToList();
    }

    public async Task<IReadOnlyList<BackofficeTicketListItem>> ListTicketsAsync(int limit = 25, CancellationToken ct = default)
    {
        var rows = await QueryRowsAsync(
            $"SELECT Id, RunId, CustomerId, Area, Subject, Priority, Status, CreatedAt FROM \"BackofficeTickets\" ORDER BY CreatedAt DESC LIMIT {Math.Max(1, limit)};",
            ct);

        return rows.Select(MapTicketListItem).ToList();
    }

    public async Task<IReadOnlyList<BackofficeSurveyListItem>> ListSurveysAsync(int limit = 25, CancellationToken ct = default)
    {
        var rows = await QueryRowsAsync(
            $"SELECT s.TicketId, s.CustomerEmail, s.SurveyUrl, s.Status, s.CreatedAt, r.Score, r.Comment, r.SubmittedAt FROM \"BackofficeSurveys\" s LEFT JOIN \"BackofficeSurveyResponses\" r ON r.TicketId = s.TicketId ORDER BY s.CreatedAt DESC LIMIT {Math.Max(1, limit)};",
            ct);

        return rows.Select(MapSurveyListItem).ToList();
    }

    public async Task<BackofficeDashboard> GetDashboardAsync(CancellationToken ct = default)
    {
        var summaryRows = await QueryRowsAsync(
            "SELECT " +
            "(SELECT COUNT(*) FROM \"BackofficeRunStates\") AS runs_total, " +
            "(SELECT COUNT(*) FROM \"BackofficeRunStates\" WHERE Status = 'paused-awaiting-confirmation') AS runs_paused, " +
            "(SELECT COUNT(*) FROM \"BackofficeRunStates\" WHERE Status = 'closed') AS runs_closed, " +
            "(SELECT COUNT(*) FROM \"BackofficeTickets\") AS tickets_total, " +
            "(SELECT COUNT(*) FROM \"BackofficeSurveyResponses\") AS surveys_completed, " +
            "(SELECT COUNT(*) FROM \"BackofficeNotifications\" WHERE Status = 'sent') AS notifications_sent, " +
            "(SELECT COUNT(*) FROM \"BackofficeNotifications\" WHERE Status = 'outbox') AS notifications_outbox, " +
            "(SELECT COUNT(*) FROM \"BackofficeNotifications\" WHERE Status = 'failed') AS notifications_failed;",
            ct);

        var summary = summaryRows.FirstOrDefault() ?? new Dictionary<string, JsonElement>();

        return new BackofficeDashboard(
            RuntimeMode: _runtimeMode,
            CanonicalStore: _isEnterpriseMode ? "postgres" : "sqlite",
            HotStore: _isEnterpriseMode ? "redis" : "none",
            Channels: GetChannelDiagnostics(),
            RunsTotal: (int)GetDouble(summary, "runs_total"),
            RunsPaused: (int)GetDouble(summary, "runs_paused"),
            RunsClosed: (int)GetDouble(summary, "runs_closed"),
            TicketsTotal: (int)GetDouble(summary, "tickets_total"),
            SurveysCompleted: (int)GetDouble(summary, "surveys_completed"),
            NotificationsSent: (int)GetDouble(summary, "notifications_sent"),
            NotificationsOutbox: (int)GetDouble(summary, "notifications_outbox"),
            NotificationsFailed: (int)GetDouble(summary, "notifications_failed"),
            RecentRuns: await ListRunsAsync(8, ct),
            RecentTickets: await ListTicketsAsync(8, ct),
            RecentSurveys: await ListSurveysAsync(8, ct));
    }

    private AgxiomeWorkflow BuildInitialWorkflow()
    {
        var workflow = new AgxiomeWorkflow("backoffice-enterprise-flow");

        workflow.AddStep("route_request", async ctx =>
        {
            var request = GetRequest(ctx);
            var timeline = GetTimeline(ctx);
            var (tree, _, matched) = _router.FindBestMatch($"{request.Subject} {request.Message}");

            ctx.SharedState!["area"] = tree.TreeName;
            ctx.SharedState["matchedKeywordsJson"] = Serialize(matched);
            timeline.Add(NewTimeline("Routing completato", $"Area {tree.TreeName}, keyword: {string.Join(", ", matched.DefaultIfEmpty("fallback"))}"));
            await Task.CompletedTask;
        });

        workflow.AddStep("load_customer_and_operational_data", async ctx =>
        {
            var request = GetRequest(ctx);
            var customerRows = await QueryRowsAsync(
                $"SELECT * FROM customers WHERE id = '{EscapeSql(request.CustomerId)}' LIMIT 1;", ctx.CancellationToken);
            var orderRows = await QueryRowsAsync(
                $"SELECT * FROM orders WHERE customer_id = '{EscapeSql(request.CustomerId)}' ORDER BY created_at DESC;", ctx.CancellationToken);
            var invoiceRows = await QueryRowsAsync(
                $"SELECT * FROM invoices WHERE customer_id = '{EscapeSql(request.CustomerId)}' ORDER BY issued_at DESC;", ctx.CancellationToken);

            var isVip = customerRows.FirstOrDefault() is { } customer &&
                        string.Equals(GetString(customer, "tier"), "vip", StringComparison.OrdinalIgnoreCase);

            ctx.SharedState!["ordersDataJson"] = Serialize(orderRows);
            ctx.SharedState["invoiceDataJson"] = Serialize(invoiceRows);
            ctx.SharedState["priority"] = BuildPriority(request, isVip);
            ctx.SharedState["isVip"] = isVip;
        });

        workflow.AddStep("retrieve_policy_context", async ctx =>
        {
            var area = (string)ctx.SharedState!["area"];
            var policies = await QueryRowsAsync(
                $"SELECT title, content FROM policies WHERE area = '{EscapeSql(area)}';", ctx.CancellationToken);
            ctx.SharedState["policyMatchesJson"] = Serialize(policies);
        });

        workflow.AddStep("build_resolution_plan", async ctx =>
        {
            var request = GetRequest(ctx);
            var area = (string)ctx.SharedState!["area"];
            var needsConfirmation = NeedsConfirmation(area, request.Message, request.Subject);
            ctx.SharedState["needsConfirmation"] = needsConfirmation;

            var content = await GenerateBackofficeContentAsync(area, request, ctx.SharedState, needsConfirmation, ctx.CancellationToken);
            ctx.SharedState["resolutionPlan"] = content.ResolutionPlan;
            ctx.SharedState["reviewNotes"] = content.ReviewNotes;
            ctx.SharedState["customerReply"] = content.CustomerReply;
        });

        workflow.AddStep("review_and_prepare_reply", async ctx =>
        {
            var request = GetRequest(ctx);
            var area = (string)ctx.SharedState!["area"];
            var priority = (string)ctx.SharedState["priority"];
            var needsConfirmation = (bool)ctx.SharedState["needsConfirmation"];

            if (!ctx.SharedState.ContainsKey("reviewNotes"))
                ctx.SharedState["reviewNotes"] = BuildReviewNotes(area, priority, needsConfirmation);

            if (!ctx.SharedState.ContainsKey("customerReply"))
                ctx.SharedState["customerReply"] = BuildCustomerReply(area, request, needsConfirmation);

            await Task.CompletedTask;
        });

        workflow.AddStep("request_email_confirmation_if_needed", async ctx =>
        {
            if (!(bool)ctx.SharedState!["needsConfirmation"])
                return;

            var request = GetRequest(ctx);
            var runId = (string)ctx.SharedState["runId"];
            var confirmation = new EmailConfirmation(
                Id: Guid.CreateVersion7().ToString(),
                RunId: runId,
                CustomerEmail: request.CustomerEmail,
                Purpose: "sensitive-backoffice-action",
                Token: Guid.CreateVersion7().ToString("N"),
                Status: "pending",
                CreatedAt: DateTimeOffset.UtcNow,
                ConfirmedAt: null);

            await SaveEntityAsync(confirmation, ctx.CancellationToken);

            var checkpoint = await _hitlStore.PauseAsync(
                runId,
                "backoffice-enterprise-flow",
                "request_email_confirmation_if_needed",
                HITLRequirementType.Confirmation,
                new { confirmation.Id, confirmation.Token, request.CustomerEmail, request.Subject },
                expiry: TimeSpan.FromHours(24),
                ctx.CancellationToken);

            var channels = GetChannels(ctx);
            if (!channels.Contains("email", StringComparer.OrdinalIgnoreCase))
                channels.Add("email");

            GetTimeline(ctx).Add(NewTimeline("Conferma richiesta", "Operazione sensibile in pausa, conferma via mail inviata o registrata in outbox."));

            ctx.SharedState["paused"] = true;
            ctx.SharedState["confirmationId"] = confirmation.Id;
            ctx.SharedState["confirmationToken"] = confirmation.Token;
            ctx.SharedState["checkpointId"] = checkpoint.CheckpointId;

            await TrySendEmailAsync(
                request.CustomerEmail,
                "Conferma richiesta backoffice",
                $"Conferma la richiesta visitando /confirm?token={confirmation.Token}",
                runId,
                ctx.CancellationToken);
        });

        workflow.AddStep("open_ticket", async ctx =>
        {
            await OpenTicketAsync(ctx, "open");
        });

        workflow.AddStep("notify_customer_and_ops", async ctx =>
        {
            await NotifyAsync(ctx);
        });

        workflow.AddStep("schedule_followup", async ctx =>
        {
            var request = GetRequest(ctx);
            var ticketId = (string)ctx.SharedState!["ticketId"];
            var followUp = new FollowUp(
                Id: Guid.CreateVersion7().ToString(),
                TicketId: ticketId,
                CustomerId: request.CustomerId,
                DueAt: DateTimeOffset.UtcNow.AddHours(24),
                Channel: request.PreferredChannels.FirstOrDefault() ?? "email",
                Status: "scheduled",
                Notes: "Follow up if customer has not confirmed resolution.");

            await SaveEntityAsync(followUp, ctx.CancellationToken);
            GetTimeline(ctx).Add(NewTimeline("Follow-up schedulato", $"Promemoria creato per il ticket {ticketId}."));
        });

        workflow.AddStep("send_satisfaction_survey", async ctx =>
        {
            var request = GetRequest(ctx);
            var ticketId = (string)ctx.SharedState!["ticketId"];
            var survey = new BackofficeSurvey(
                Id: Guid.CreateVersion7().ToString(),
                TicketId: ticketId,
                CustomerEmail: request.CustomerEmail,
                SurveyUrl: $"/survey/{ticketId}",
                Status: "sent",
                CreatedAt: DateTimeOffset.UtcNow);

            await SaveEntityAsync(survey, ctx.CancellationToken);
            await TrySendEmailAsync(request.CustomerEmail, $"Survey ticket {ticketId}", $"Questionario disponibile su {survey.SurveyUrl}", (string)ctx.SharedState["runId"], ctx.CancellationToken);
            GetTimeline(ctx).Add(NewTimeline("Survey inviata", $"Questionario CSAT creato per il ticket {ticketId}."));
        });

        workflow.AddStep("persist_run_and_metrics", async ctx =>
        {
            var isPaused = ctx.SharedState!.TryGetValue("paused", out var paused) && paused is true;
            await PersistRunStateAsync(ctx, defaultStatus: isPaused ? "paused-awaiting-confirmation" : "completed");
        });

        workflow.AddTransition("route_request", "load_customer_and_operational_data");
        workflow.AddTransition("load_customer_and_operational_data", "retrieve_policy_context");
        workflow.AddTransition("retrieve_policy_context", "build_resolution_plan");
        workflow.AddTransition("build_resolution_plan", "review_and_prepare_reply");
        workflow.AddTransition("review_and_prepare_reply", "request_email_confirmation_if_needed");
        workflow.AddTransition("request_email_confirmation_if_needed", "persist_run_and_metrics", ctx => ctx.SharedState!.TryGetValue("paused", out var paused) && paused is true);
        workflow.AddTransition("request_email_confirmation_if_needed", "open_ticket", ctx => !ctx.SharedState!.TryGetValue("paused", out var paused) || paused is not true);
        workflow.AddTransition("open_ticket", "notify_customer_and_ops");
        workflow.AddTransition("notify_customer_and_ops", "schedule_followup");
        workflow.AddTransition("schedule_followup", "send_satisfaction_survey");
        workflow.AddTransition("send_satisfaction_survey", "persist_run_and_metrics");

        return workflow;
    }

    private AgxiomeWorkflow BuildPostConfirmationWorkflow()
    {
        var workflow = new AgxiomeWorkflow("backoffice-confirmation-resume-flow");

        workflow.AddStep("open_ticket", async ctx => await OpenTicketAsync(ctx, "confirmed"));
        workflow.AddStep("notify_customer_and_ops", async ctx => await NotifyAsync(ctx));
        workflow.AddStep("schedule_followup", async ctx =>
        {
            var request = GetRequest(ctx);
            var ticketId = (string)ctx.SharedState!["ticketId"];
            var followUp = new FollowUp(
                Id: Guid.CreateVersion7().ToString(),
                TicketId: ticketId,
                CustomerId: request.CustomerId,
                DueAt: DateTimeOffset.UtcNow.AddHours(24),
                Channel: request.PreferredChannels.FirstOrDefault() ?? "email",
                Status: "scheduled",
                Notes: "Follow up after confirmation resume.");

            await SaveEntityAsync(followUp, ctx.CancellationToken);
            GetTimeline(ctx).Add(NewTimeline("Run ripreso", $"Il ticket {ticketId} e' stato ripreso dopo conferma."));
        });
        workflow.AddStep("send_satisfaction_survey", async ctx =>
        {
            var request = GetRequest(ctx);
            var ticketId = (string)ctx.SharedState!["ticketId"];
            var survey = new BackofficeSurvey(
                Id: Guid.CreateVersion7().ToString(),
                TicketId: ticketId,
                CustomerEmail: request.CustomerEmail,
                SurveyUrl: $"/survey/{ticketId}",
                Status: "sent",
                CreatedAt: DateTimeOffset.UtcNow);

            await SaveEntityAsync(survey, ctx.CancellationToken);
            GetTimeline(ctx).Add(NewTimeline("Survey inviata", $"Questionario CSAT creato per il ticket {ticketId}."));
        });
        workflow.AddStep("persist_run_and_metrics", async ctx => await PersistRunStateAsync(ctx, "confirmed"));

        workflow.AddTransition("open_ticket", "notify_customer_and_ops");
        workflow.AddTransition("notify_customer_and_ops", "schedule_followup");
        workflow.AddTransition("schedule_followup", "send_satisfaction_survey");
        workflow.AddTransition("send_satisfaction_survey", "persist_run_and_metrics");

        return workflow;
    }

    private async Task OpenTicketAsync(WorkflowContext ctx, string resumedStatus)
    {
        var request = GetRequest(ctx);
        var ticket = new BackofficeTicket(
            Id: $"BO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8]}",
            RunId: (string)ctx.SharedState!["runId"],
            CustomerId: request.CustomerId,
            Area: (string)ctx.SharedState["area"],
            Subject: request.Subject,
            Description: request.Message,
            Priority: (string)ctx.SharedState["priority"],
            Status: resumedStatus,
            CreatedAt: DateTimeOffset.UtcNow);

        await SaveEntityAsync(ticket, ctx.CancellationToken);
        ctx.SharedState["ticketId"] = ticket.Id;
        GetTimeline(ctx).Add(NewTimeline("Ticket aperto", $"Ticket {ticket.Id} creato nell'area {ticket.Area}."));
    }

    private async Task NotifyAsync(WorkflowContext ctx)
    {
        var request = GetRequest(ctx);
        var runId = (string)ctx.SharedState!["runId"];
        var ticketId = (string)ctx.SharedState["ticketId"];
        var channels = GetChannels(ctx);
        var reply = (string)ctx.SharedState["customerReply"];

        await TrySendEmailAsync(request.CustomerEmail, $"Aggiornamento ticket {ticketId}", reply, runId, ctx.CancellationToken);
        if (!channels.Contains("email", StringComparer.OrdinalIgnoreCase))
            channels.Add("email");

        if (request.PreferredChannels.Contains("whatsapp") && !string.IsNullOrWhiteSpace(request.CustomerPhone))
        {
            await TrySendWhatsAppAsync(request.CustomerPhone, $"Ticket {ticketId}: {reply}", runId, ctx.CancellationToken);
            if (!channels.Contains("whatsapp", StringComparer.OrdinalIgnoreCase))
                channels.Add("whatsapp");
        }

        if (!string.IsNullOrWhiteSpace(request.TelegramChatId))
        {
            await TrySendTelegramAsync(request.TelegramChatId, $"Nuovo ticket {ticketId}\nArea: {ctx.SharedState["area"]}\nPriority: {ctx.SharedState["priority"]}", runId, ctx.CancellationToken);
            if (!channels.Contains("telegram", StringComparer.OrdinalIgnoreCase))
                channels.Add("telegram");
        }

        GetTimeline(ctx).Add(NewTimeline("Notifiche inviate", $"Canali aggiornati: {string.Join(", ", channels)}."));
    }

    private async Task PersistRunStateAsync(WorkflowContext ctx, string defaultStatus)
    {
        var request = GetRequest(ctx);
        var runId = (string)ctx.SharedState!["runId"];
        var startedAt = (DateTimeOffset)ctx.SharedState["startedAt"];
        var now = DateTimeOffset.UtcNow;
        var responseSeconds = Math.Max((now - startedAt).TotalSeconds, 1);
        var area = (string)ctx.SharedState["area"];

        await ExecSqlAsync(
            $"INSERT INTO backoffice_metrics (id, run_id, area, response_seconds, created_at) VALUES ('{Guid.CreateVersion7()}', '{EscapeSql(runId)}', '{EscapeSql(area)}', {responseSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}, '{now:O}');",
            ctx.CancellationToken);

        var avgRows = await QueryRowsAsync(
            $"SELECT AVG(response_seconds) AS avg_response_seconds FROM backoffice_metrics WHERE area = '{EscapeSql(area)}';",
            ctx.CancellationToken);

        var avgResponseSeconds = avgRows.FirstOrDefault() is { } avgRow ? GetDouble(avgRow, "avg_response_seconds") : responseSeconds;
        var existing = await GetRunStateAsync(runId, ctx.CancellationToken);
        var status = defaultStatus;

        var entity = new BackofficeRunState(
            Id: runId,
            CustomerId: request.CustomerId,
            CustomerEmail: request.CustomerEmail,
            CustomerPhone: request.CustomerPhone,
            TelegramChatId: request.TelegramChatId,
            Subject: request.Subject,
            Message: request.Message,
            PreferredLanguage: request.PreferredLanguage,
            PreferredChannelsJson: Serialize(request.PreferredChannels),
            Area: area,
            Priority: (string)ctx.SharedState["priority"],
            Status: status,
            TicketId: ctx.SharedState.TryGetValue("ticketId", out var ticketId) ? ticketId?.ToString() : existing?.TicketId,
            CheckpointId: ctx.SharedState.TryGetValue("checkpointId", out var checkpointId) ? checkpointId?.ToString() : existing?.CheckpointId,
            ConfirmationToken: ctx.SharedState.TryGetValue("confirmationToken", out var token) ? token?.ToString() : existing?.ConfirmationToken,
            ConfirmationId: ctx.SharedState.TryGetValue("confirmationId", out var confirmationId) ? confirmationId?.ToString() : existing?.ConfirmationId,
            CustomerReply: (string)ctx.SharedState["customerReply"],
            ResolutionPlan: (string)ctx.SharedState["resolutionPlan"],
            ReviewNotes: (string)ctx.SharedState["reviewNotes"],
            OrdersDataJson: (string)ctx.SharedState["ordersDataJson"],
            InvoiceDataJson: (string)ctx.SharedState["invoiceDataJson"],
            PolicyMatchesJson: (string)ctx.SharedState["policyMatchesJson"],
            ChannelsNotifiedJson: Serialize(GetChannels(ctx)),
            TimelineJson: Serialize(GetTimeline(ctx)),
            AvgResponseSeconds: avgResponseSeconds,
            CreatedAt: existing?.CreatedAt ?? startedAt,
            UpdatedAt: now);

        if (existing is null)
            await SaveEntityAsync(entity, ctx.CancellationToken);
        else
            await UpdateEntityAsync(entity, ctx.CancellationToken);

        await _historyStore.AppendAsync(new WorkflowHistoryEntry
        {
            RunId = runId,
            WorkflowId = defaultStatus == "confirmed" ? "backoffice-confirmation-resume-flow" : "backoffice-enterprise-flow",
            Status = defaultStatus == "paused-awaiting-confirmation" ? WorkflowStatus.Running : WorkflowStatus.Completed,
            StepResultsJson = Serialize(new[]
            {
                new StepResult("route_request", StepStatus.Completed, area, TimeSpan.Zero),
                new StepResult("build_resolution_plan", StepStatus.Completed, entity.ResolutionPlan, TimeSpan.Zero),
                new StepResult("persist_run_and_metrics", StepStatus.Completed, entity.Status, TimeSpan.Zero)
            }),
            StartedAt = startedAt,
            FinishedAt = now,
            SessionId = $"backoffice:{runId}",
            UserId = request.CustomerId
        }, ctx.CancellationToken);
    }

    private async Task<BackofficeRunState?> GetRunStateAsync(string runId, CancellationToken ct)
    {
        var rows = await QueryRowsAsync(
            $"SELECT * FROM \"BackofficeRunStates\" WHERE Id = '{EscapeSql(runId)}' LIMIT 1;",
            ct);

        var row = rows.FirstOrDefault();
        return row is null ? null : MapRunState(row);
    }

    private async Task<EmailConfirmation?> GetConfirmationByTokenAsync(string token, CancellationToken ct)
    {
        var rows = await QueryRowsAsync(
            $"SELECT * FROM \"EmailConfirmations\" WHERE Token = '{EscapeSql(token)}' LIMIT 1;",
            ct);

        var row = rows.FirstOrDefault();
        return row is null ? null : new EmailConfirmation(
            Id: GetRequiredString(row, "Id"),
            RunId: GetRequiredString(row, "RunId"),
            CustomerEmail: GetRequiredString(row, "CustomerEmail"),
            Purpose: GetRequiredString(row, "Purpose"),
            Token: GetRequiredString(row, "Token"),
            Status: GetRequiredString(row, "Status"),
            CreatedAt: GetDateTimeOffset(row, "CreatedAt"),
            ConfirmedAt: GetNullableDateTimeOffset(row, "ConfirmedAt"));
    }

    private async Task ExecSqlAsync(string query, CancellationToken ct)
    {
        var result = await _sql.ExecuteAsync("query", new Dictionary<string, object?>
        {
            ["database"] = _dbPath,
            ["query"] = query
        }, ct);

        if (result.Status != ToolkitResultStatus.Ok)
            throw new InvalidOperationException(result.Error ?? "SQLite execution failed.");
    }

    private async Task<IReadOnlyList<Dictionary<string, JsonElement>>> QueryRowsAsync(string query, CancellationToken ct)
    {
        var result = await _sql.ExecuteAsync("query", new Dictionary<string, object?>
        {
            ["database"] = _dbPath,
            ["query"] = query
        }, ct);

        if (result.Status != ToolkitResultStatus.Ok)
            throw new InvalidOperationException(result.Error ?? "SQLite query failed.");

        var payload = JsonSerializer.Deserialize<SqliteQueryPayload>(Serialize(result.Data), JsonOptions);
        return payload?.Rows ?? [];
    }

    private static BackofficeRunState MapRunState(Dictionary<string, JsonElement> row)
        => new(
            Id: GetRequiredString(row, "Id"),
            CustomerId: GetRequiredString(row, "CustomerId"),
            CustomerEmail: GetRequiredString(row, "CustomerEmail"),
            CustomerPhone: GetString(row, "CustomerPhone"),
            TelegramChatId: GetString(row, "TelegramChatId"),
            Subject: GetRequiredString(row, "Subject"),
            Message: GetRequiredString(row, "Message"),
            PreferredLanguage: GetRequiredString(row, "PreferredLanguage"),
            PreferredChannelsJson: GetRequiredString(row, "PreferredChannelsJson"),
            Area: GetRequiredString(row, "Area"),
            Priority: GetRequiredString(row, "Priority"),
            Status: GetRequiredString(row, "Status"),
            TicketId: GetString(row, "TicketId"),
            CheckpointId: GetString(row, "CheckpointId"),
            ConfirmationToken: GetString(row, "ConfirmationToken"),
            ConfirmationId: GetString(row, "ConfirmationId"),
            CustomerReply: GetRequiredString(row, "CustomerReply"),
            ResolutionPlan: GetRequiredString(row, "ResolutionPlan"),
            ReviewNotes: GetRequiredString(row, "ReviewNotes"),
            OrdersDataJson: GetRequiredString(row, "OrdersDataJson"),
            InvoiceDataJson: GetRequiredString(row, "InvoiceDataJson"),
            PolicyMatchesJson: GetRequiredString(row, "PolicyMatchesJson"),
            ChannelsNotifiedJson: GetRequiredString(row, "ChannelsNotifiedJson"),
            TimelineJson: GetRequiredString(row, "TimelineJson"),
            AvgResponseSeconds: GetDouble(row, "AvgResponseSeconds"),
            CreatedAt: GetDateTimeOffset(row, "CreatedAt"),
            UpdatedAt: GetDateTimeOffset(row, "UpdatedAt"));

    private static RunRecord MapToRunRecord(BackofficeRunState state)
        => new(
            RunId: state.Id,
            Status: state.Status,
            Area: state.Area,
            Priority: state.Priority,
            TicketId: state.TicketId,
            CheckpointId: state.CheckpointId,
            ConfirmationToken: state.ConfirmationToken,
            CustomerReply: state.CustomerReply,
            ResolutionPlan: state.ResolutionPlan,
            ReviewNotes: state.ReviewNotes,
            CreatedAt: state.CreatedAt,
            AvgResponseSeconds: state.AvgResponseSeconds,
            ChannelsNotified: DeserializeStringList(state.ChannelsNotifiedJson),
            Timeline: DeserializeTimeline(state.TimelineJson));

    private static BackofficeRequestInput ToRequest(BackofficeRunState state)
        => new(
            CustomerId: state.CustomerId,
            CustomerEmail: state.CustomerEmail,
            CustomerPhone: state.CustomerPhone,
            TelegramChatId: state.TelegramChatId,
            Subject: state.Subject,
            Message: state.Message,
            PreferredLanguage: state.PreferredLanguage,
            PreferredChannels: DeserializeStringList(state.PreferredChannelsJson));

    private static BackofficeRunListItem MapRunListItem(Dictionary<string, JsonElement> row)
        => new(
            RunId: GetRequiredString(row, "Id"),
            Area: GetRequiredString(row, "Area"),
            Priority: GetRequiredString(row, "Priority"),
            Status: GetRequiredString(row, "Status"),
            TicketId: GetString(row, "TicketId"),
            CheckpointId: GetString(row, "CheckpointId"),
            AvgResponseSeconds: GetDouble(row, "AvgResponseSeconds"),
            CreatedAt: GetDateTimeOffset(row, "CreatedAt"),
            UpdatedAt: GetDateTimeOffset(row, "UpdatedAt"));

    private static BackofficeTicketListItem MapTicketListItem(Dictionary<string, JsonElement> row)
        => new(
            TicketId: GetRequiredString(row, "Id"),
            RunId: GetRequiredString(row, "RunId"),
            CustomerId: GetRequiredString(row, "CustomerId"),
            Area: GetRequiredString(row, "Area"),
            Subject: GetRequiredString(row, "Subject"),
            Priority: GetRequiredString(row, "Priority"),
            Status: GetRequiredString(row, "Status"),
            CreatedAt: GetDateTimeOffset(row, "CreatedAt"));

    private static BackofficeSurveyListItem MapSurveyListItem(Dictionary<string, JsonElement> row)
        => new(
            TicketId: GetRequiredString(row, "TicketId"),
            CustomerEmail: GetRequiredString(row, "CustomerEmail"),
            SurveyUrl: GetRequiredString(row, "SurveyUrl"),
            Status: GetRequiredString(row, "Status"),
            CreatedAt: GetDateTimeOffset(row, "CreatedAt"),
            Score: row.TryGetValue("Score", out _) ? (int?)GetDouble(row, "Score") : null,
            Comment: GetString(row, "Comment"),
            SubmittedAt: GetNullableDateTimeOffset(row, "SubmittedAt"));

    private static IReadOnlyList<string> DeserializeStringList(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];

    private static IReadOnlyList<RunTimelineEntry> DeserializeTimeline(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<RunTimelineEntry>>(json, JsonOptions) ?? [];

    private static string Serialize(object? value)
        => JsonSerializer.Serialize(value, JsonOptions);

    private static string BuildPriority(BackofficeRequestInput request, bool isVip)
    {
        if (isVip)
            return "high";

        var text = $"{request.Subject} {request.Message}".ToLowerInvariant();
        return text.Contains("urgent", StringComparison.Ordinal) ||
               text.Contains("critico", StringComparison.Ordinal) ||
               text.Contains("vip", StringComparison.Ordinal) ||
               text.Contains("sla", StringComparison.Ordinal)
            ? "high"
            : "normal";
    }

    private static bool NeedsConfirmation(string area, string message, string subject)
    {
        var text = $"{subject} {message}".ToLowerInvariant();
        return area is "refunds" or "account" ||
               text.Contains("rimborso", StringComparison.Ordinal) ||
               text.Contains("refund", StringComparison.Ordinal) ||
               text.Contains("doppio addebito", StringComparison.Ordinal) ||
               text.Contains("change address", StringComparison.Ordinal) ||
               text.Contains("indirizzo", StringComparison.Ordinal);
    }

    private static string BuildResolutionPlan(string area, BackofficeRequestInput request, Dictionary<string, object> state)
    {
        var orders = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>((string)state["ordersDataJson"], JsonOptions) ?? [];
        var invoices = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>((string)state["invoiceDataJson"], JsonOptions) ?? [];
        var policies = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>((string)state["policyMatchesJson"], JsonOptions) ?? [];
        var needsConfirmation = (bool)state["needsConfirmation"];

        return string.Join('\n',
        [
            $"1. Classificare il caso nell'area {area} per il cliente {request.CustomerId}.",
            $"2. Verificare i dati operativi: {orders.Count} ordini e {invoices.Count} fatture correlate.",
            $"3. Applicare il playbook locale con {policies.Count} policy rilevanti.",
            needsConfirmation
                ? "4. Richiedere conferma esplicita prima di aprire il ticket operativo definitivo."
                : "4. Aprire il ticket operativo e notificare i canali richiesti.",
            "5. Tracciare metriche, follow-up e survey CSAT fino alla chiusura del caso."
        ]);
    }

    private async Task<BackofficeGeneratedContent> GenerateBackofficeContentAsync(
        string area,
        BackofficeRequestInput request,
        Dictionary<string, object> state,
        bool needsConfirmation,
        CancellationToken ct)
    {
        var priority = state.TryGetValue("priority", out var priorityValue) ? priorityValue?.ToString() ?? "normal" : "normal";
        var orders = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>((string)state["ordersDataJson"], JsonOptions) ?? [];
        var invoices = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>((string)state["invoiceDataJson"], JsonOptions) ?? [];
        var policies = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>((string)state["policyMatchesJson"], JsonOptions) ?? [];
        var language = string.IsNullOrWhiteSpace(request.PreferredLanguage) ? "it-IT" : request.PreferredLanguage;
        var prompt = BuildBackofficeContentPrompt(area, priority, request, orders, invoices, policies, needsConfirmation, language);

        try
        {
            var result = await _contentSwarm.RunAsync(prompt);
            var generated = DeserializeGeneratedContent(result.Output);
            if (generated is not null && IsGeneratedContentAcceptable(generated, needsConfirmation))
                return generated;

            _log.LogWarning("Backoffice content swarm returned unusable output for customer {CustomerId} in area {Area}; using fallback.", request.CustomerId, area);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Backoffice content generation failed for customer {CustomerId} in area {Area}; using fallback.", request.CustomerId, area);
        }

        return new BackofficeGeneratedContent(
            ResolutionPlan: BuildResolutionPlan(area, request, state),
            ReviewNotes: BuildReviewNotes(area, priority, needsConfirmation),
            CustomerReply: BuildCustomerReply(area, request, needsConfirmation));
    }

    private static string BuildBackofficeContentPrompt(
        string area,
        string priority,
        BackofficeRequestInput request,
        IReadOnlyList<Dictionary<string, JsonElement>> orders,
        IReadOnlyList<Dictionary<string, JsonElement>> invoices,
        IReadOnlyList<Dictionary<string, JsonElement>> policies,
        bool needsConfirmation,
        string language)
    {
        var orderSummary = orders.Count == 0
            ? "no recent orders"
            : string.Join("; ", orders.Take(2).Select(order =>
                $"{GetString(order, "id") ?? GetString(order, "Id") ?? "order"}: status={GetString(order, "status") ?? GetString(order, "Status") ?? "unknown"}, total={GetString(order, "total_amount") ?? GetString(order, "TotalAmount") ?? "n/a"}"));

        var invoiceSummary = invoices.Count == 0
            ? "no recent invoices"
            : string.Join("; ", invoices.Take(2).Select(invoice =>
                $"{GetString(invoice, "id") ?? GetString(invoice, "Id") ?? "invoice"}: status={GetString(invoice, "status") ?? GetString(invoice, "Status") ?? "unknown"}, amount={GetString(invoice, "amount") ?? GetString(invoice, "Amount") ?? "n/a"}"));

        var policySummary = policies.Count == 0
            ? "no matching policies"
            : string.Join(" | ", policies.Take(3).Select(policy =>
                $"{GetString(policy, "title") ?? GetString(policy, "Title") ?? "policy"}: {GetString(policy, "content") ?? GetString(policy, "Content") ?? string.Empty}"));

        var targetLanguage = language.StartsWith("it", StringComparison.OrdinalIgnoreCase)
            ? "Italian"
            : language.StartsWith("en", StringComparison.OrdinalIgnoreCase)
                ? "English"
                : language;

        return $"""
Generate backoffice case content as strict JSON.

Language: {targetLanguage} (locale {language})
Case area: {area}
Priority: {priority}
Customer ID: {request.CustomerId}
Customer email: {request.CustomerEmail}
Subject: {request.Subject}
Message: {request.Message}
Preferred channels: {string.Join(", ", request.PreferredChannels.DefaultIfEmpty("email"))}
Needs explicit confirmation: {(needsConfirmation ? "yes" : "no")}
Orders summary: {orderSummary}
Invoices summary: {invoiceSummary}
Policy context: {policySummary}

Constraints:
- Return only JSON with keys resolutionPlan, reviewNotes, customerReply.
- resolutionPlan must be a concise numbered plan with 4-6 steps, each on its own line.
- reviewNotes must be short internal operator notes grounded only in the supplied facts.
- customerReply must be customer-facing and mention confirmation only if needed.
- If language is Italian, write natural Italian only.
- Do not use placeholders like [name], [Il tuo nome], or signatures.
- Do not claim refunds, payments, approvals, or investigations are already completed unless explicitly stated above.
- If explicit confirmation is required, say that confirmation is still required before the sensitive action can proceed.
- If explicit confirmation is not required, do not mention confirmation.
- Keep grounded in the supplied data and do not invent unavailable IDs.
""";
    }

    private static BackofficeGeneratedContent? DeserializeGeneratedContent(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        var trimmed = output.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = trimmed.IndexOf('\n');
            if (firstNewLine >= 0)
                trimmed = trimmed[(firstNewLine + 1)..];

            if (trimmed.EndsWith("```", StringComparison.Ordinal))
                trimmed = trimmed[..^3];

            trimmed = trimmed.Trim();
        }

        using var document = JsonDocument.Parse(trimmed);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            return null;

        var resolutionPlan = ReadGeneratedText(document.RootElement, "resolutionPlan");
        var reviewNotes = ReadGeneratedText(document.RootElement, "reviewNotes");
        var customerReply = ReadGeneratedText(document.RootElement, "customerReply");

        if (string.IsNullOrWhiteSpace(resolutionPlan) ||
            string.IsNullOrWhiteSpace(reviewNotes) ||
            string.IsNullOrWhiteSpace(customerReply))
        {
            return null;
        }

        return new BackofficeGeneratedContent(resolutionPlan, reviewNotes, customerReply);
    }

    private static string? ReadGeneratedText(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Array => string.Join('\n', value.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))),
            JsonValueKind.Object => value.ToString(),
            _ => value.ToString()
        };
    }

    private static bool IsGeneratedContentAcceptable(BackofficeGeneratedContent generated, bool needsConfirmation)
    {
        if (generated.CustomerReply.Contains('[', StringComparison.Ordinal) ||
            generated.CustomerReply.Contains(']', StringComparison.Ordinal) ||
            generated.CustomerReply.Contains("Il tuo nome", StringComparison.OrdinalIgnoreCase) ||
            generated.CustomerReply.Contains("your name", StringComparison.OrdinalIgnoreCase) ||
            generated.CustomerReply.Contains("Ã", StringComparison.Ordinal) ||
            generated.ReviewNotes.Contains("Ã", StringComparison.Ordinal) ||
            generated.ResolutionPlan.Contains("Ã", StringComparison.Ordinal))
        {
            return false;
        }

        if (!generated.ResolutionPlan.Contains("1.", StringComparison.Ordinal))
            return false;

        if (needsConfirmation &&
            (generated.CustomerReply.Contains("non ho bisogno di conferma", StringComparison.OrdinalIgnoreCase) ||
             generated.CustomerReply.Contains("non serve conferma", StringComparison.OrdinalIgnoreCase) ||
             generated.CustomerReply.Contains("no confirmation", StringComparison.OrdinalIgnoreCase) ||
             generated.CustomerReply.Contains("refund has been processed", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    private static string BuildReviewNotes(string area, string priority, bool needsConfirmation)
    {
        var risk = priority == "high" ? "High priority / VIP handling required." : "Standard priority.";
        var approval = needsConfirmation ? "Approval required before sensitive action." : "No approval gate required.";
        return $"{risk} Area: {area}. {approval}";
    }

    private static string BuildCustomerReply(string area, BackofficeRequestInput request, bool needsConfirmation)
    {
        var suffix = needsConfirmation
            ? "Prima di procedere con l'azione sensibile ti chiederemo una conferma esplicita."
            : "Il team backoffice sta gia' procedendo con l'operativita'.";

        return area switch
        {
            "billing" => $"Abbiamo preso in carico la tua richiesta su fattura e addebiti. {suffix}",
            "refunds" => $"Abbiamo preso in carico la richiesta di rimborso o contestazione. {suffix}",
            "orders" => $"Abbiamo preso in carico la richiesta relativa all'ordine e alla spedizione. {suffix}",
            "technical" => $"Abbiamo preso in carico il problema tecnico e stiamo verificando impatto e workaround. {suffix}",
            _ => $"Abbiamo preso in carico la tua richiesta sul profilo e sui dati sensibili. {suffix}"
        };
    }

    private async Task TrySendEmailAsync(string to, string subject, string body, string runId, CancellationToken ct)
    {
        if (_email is null)
        {
            await RecordNotificationAsync(runId, "email", to, subject, body, "outbox", ct);
            return;
        }

        var result = await _email.ExecuteAsync("send_email", new Dictionary<string, object?>
        {
            ["to"] = to,
            ["subject"] = subject,
            ["body"] = body
        }, ct);

        await RecordNotificationAsync(runId, "email", to, subject, body, result.Status == ToolkitResultStatus.Ok ? "sent" : "failed", ct);
    }

    private async Task TrySendTelegramAsync(string chatId, string text, string runId, CancellationToken ct)
    {
        if (_telegram is null)
        {
            await RecordNotificationAsync(runId, "telegram", chatId, null, text, "outbox", ct);
            return;
        }

        var result = await _telegram.ExecuteAsync("send_message", new Dictionary<string, object?>
        {
            ["chat_id"] = chatId,
            ["text"] = text
        }, ct);

        await RecordNotificationAsync(runId, "telegram", chatId, null, text, result.Status == ToolkitResultStatus.Ok ? "sent" : "failed", ct);
    }

    private async Task TrySendWhatsAppAsync(string to, string text, string runId, CancellationToken ct)
    {
        if (_whatsapp is null)
        {
            await RecordNotificationAsync(runId, "whatsapp", to, null, text, "outbox", ct);
            return;
        }

        var result = await _whatsapp.ExecuteAsync("send_text", new Dictionary<string, object?>
        {
            ["to"] = to,
            ["text"] = text
        }, ct);

        await RecordNotificationAsync(runId, "whatsapp", to, null, text, result.Status == ToolkitResultStatus.Ok ? "sent" : "failed", ct);
    }

    private async Task RecordNotificationAsync(string runId, string channel, string recipient, string? subject, string message, string status, CancellationToken ct)
    {
        var notification = new BackofficeNotification(
            Id: Guid.CreateVersion7().ToString(),
            RunId: runId,
            Channel: channel,
            Recipient: recipient,
            Subject: subject,
            Message: message,
            Status: status,
            CreatedAt: DateTimeOffset.UtcNow);

        await SaveEntityAsync(notification, ct);
    }

    private async Task EnsurePostgresSchemaAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_postgresConnectionString);
        await conn.OpenAsync(ct);

        await conn.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS backofficerunstates (
                id text primary key,
                customerid text not null,
                customeremail text not null,
                customerphone text null,
                telegramchatid text null,
                subject text not null,
                message text not null,
                preferredlanguage text not null,
                preferredchannelsjson text not null,
                area text not null,
                priority text not null,
                status text not null,
                ticketid text null,
                checkpointid text null,
                confirmationtoken text null,
                confirmationid text null,
                customerreply text not null,
                resolutionplan text not null,
                reviewnotes text not null,
                ordersdatajson text not null,
                invoicedatajson text not null,
                policymatchesjson text not null,
                channelsnotifiedjson text not null,
                timelinejson text not null,
                avgresponseseconds double precision not null,
                createdat timestamptz not null,
                updatedat timestamptz not null
            );

            CREATE TABLE IF NOT EXISTS emailconfirmations (
                id text primary key,
                runid text not null,
                customeremail text not null,
                purpose text not null,
                token text not null,
                status text not null,
                createdat timestamptz not null,
                confirmedat timestamptz null
            );

            CREATE TABLE IF NOT EXISTS backofficetickets (
                id text primary key,
                runid text not null,
                customerid text not null,
                area text not null,
                subject text not null,
                description text not null,
                priority text not null,
                status text not null,
                createdat timestamptz not null
            );

            CREATE TABLE IF NOT EXISTS followups (
                id text primary key,
                ticketid text not null,
                customerid text not null,
                dueat timestamptz not null,
                channel text not null,
                status text not null,
                notes text not null
            );

            CREATE TABLE IF NOT EXISTS backofficesurveys (
                id text primary key,
                ticketid text not null,
                customeremail text not null,
                surveyurl text not null,
                status text not null,
                createdat timestamptz not null
            );

            CREATE TABLE IF NOT EXISTS backofficesurveyresponses (
                id text primary key,
                ticketid text not null,
                score integer not null,
                comment text null,
                submittedat timestamptz not null
            );

            CREATE TABLE IF NOT EXISTS backofficenotifications (
                id text primary key,
                runid text not null,
                channel text not null,
                recipient text not null,
                subject text null,
                message text not null,
                status text not null,
                createdat timestamptz not null
            );

            CREATE TABLE IF NOT EXISTS workflowhistoryentrys (
                runid text primary key,
                workflowid text not null,
                status integer not null,
                stepresultsjson text not null,
                metadatajson text null,
                startedat timestamptz not null,
                finishedat timestamptz not null,
                sessionid text null,
                userid text null,
                schemaversion text not null
            );

            CREATE TABLE IF NOT EXISTS workflowpauserecords (
                checkpointid text primary key,
                runid text not null,
                workflowid text not null,
                stepname text not null,
                requirementtype integer not null,
                status integer not null,
                payloadjson text not null,
                resolutionjson text null,
                resolverid text null,
                createdat timestamptz not null,
                expiresat timestamptz null,
                resolvedat timestamptz null,
                schemaversion text not null
            );
            """);
    }

    private async Task SaveEntityAsync<T>(T entity, CancellationToken ct) where T : class
    {
        await _canonicalStore.SaveAsync(entity, ct);

        if (!ReferenceEquals(_canonicalStore, _projectionStore))
            await _projectionStore.SaveAsync(entity, ct);

        if (entity is BackofficeRunState runState && _hotStore is not null)
            await _hotStore.SaveAsync(runState, ct);
    }

    private async Task UpdateEntityAsync<T>(T entity, CancellationToken ct) where T : class
    {
        await _canonicalStore.UpdateAsync(entity, ct);

        if (!ReferenceEquals(_canonicalStore, _projectionStore))
            await _projectionStore.UpdateAsync(entity, ct);

        if (entity is BackofficeRunState runState && _hotStore is not null)
            await _hotStore.UpdateAsync(runState, ct);
    }

    private IReadOnlyList<BackofficeChannelStatus> GetChannelDiagnostics()
        =>
        [
            new("email", _email is not null ? "configured" : "outbox", _email is not null),
            new("telegram", _telegram is not null ? "configured" : "outbox", _telegram is not null),
            new("whatsapp", _whatsapp is not null ? "configured" : "outbox", _whatsapp is not null)
        ];

    private static BackofficeRequestInput GetRequest(WorkflowContext ctx)
        => (BackofficeRequestInput)ctx.SharedState!["request"];

    private static List<RunTimelineEntry> GetTimeline(WorkflowContext ctx)
        => (List<RunTimelineEntry>)ctx.SharedState!["timeline"];

    private static List<string> GetChannels(WorkflowContext ctx)
        => (List<string>)ctx.SharedState!["channelsNotified"];

    private static RunTimelineEntry NewTimeline(string title, string description)
        => new(Guid.CreateVersion7().ToString(), title, description, DateTimeOffset.UtcNow);

    private static string EscapeSql(string value)
        => value.Replace("'", "''", StringComparison.Ordinal);

    private static string? GetString(Dictionary<string, JsonElement> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static string GetRequiredString(Dictionary<string, JsonElement> row, string key)
        => GetString(row, key) ?? string.Empty;

    private static double GetDouble(Dictionary<string, JsonElement> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value.ValueKind == JsonValueKind.Null)
            return 0;

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String when double.TryParse(value.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0
        };
    }

    private static DateTimeOffset GetDateTimeOffset(Dictionary<string, JsonElement> row, string key)
        => DateTimeOffset.TryParse(GetString(row, key), out var parsed) ? parsed : DateTimeOffset.UtcNow;

    private static DateTimeOffset? GetNullableDateTimeOffset(Dictionary<string, JsonElement> row, string key)
        => DateTimeOffset.TryParse(GetString(row, key), out var parsed) ? parsed : null;

    private static EmailSmtpToolkit? BuildEmailToolkit(IConfiguration configuration)
    {
        var host = configuration["Backoffice:Email:Host"];
        var username = configuration["Backoffice:Email:Username"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username))
            return null;

        return new EmailSmtpToolkit(new ToolkitConfig("email_smtp", new Dictionary<string, string>
        {
            ["host"] = host,
            ["port"] = configuration["Backoffice:Email:Port"] ?? "587",
            ["username"] = username,
            ["password"] = configuration["Backoffice:Email:Password"] ?? string.Empty,
            ["from"] = configuration["Backoffice:Email:From"] ?? username
        }));
    }

    private static TelegramToolkit? BuildTelegramToolkit(IConfiguration configuration)
    {
        var token = configuration["Backoffice:Telegram:BotToken"];
        return string.IsNullOrWhiteSpace(token)
            ? null
            : new TelegramToolkit(new ToolkitConfig("telegram", new Dictionary<string, string>
            {
                ["bot_token"] = token
            }));
    }

    private static WhatsAppToolkit? BuildWhatsAppToolkit(IConfiguration configuration)
    {
        var accessToken = configuration["Backoffice:WhatsApp:AccessToken"];
        var phoneNumberId = configuration["Backoffice:WhatsApp:PhoneNumberId"];
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(phoneNumberId))
            return null;

        return new WhatsAppToolkit(new ToolkitConfig("whatsapp", new Dictionary<string, string>
        {
            ["access_token"] = accessToken,
            ["phone_number_id"] = phoneNumberId
        }));
    }

    private sealed record SqliteQueryPayload(
        string Query,
        IReadOnlyList<string> Columns,
        IReadOnlyList<Dictionary<string, JsonElement>> Rows,
        int RowCount);
}

internal sealed record BackofficeRunState(
    string Id,
    string CustomerId,
    string CustomerEmail,
    string? CustomerPhone,
    string? TelegramChatId,
    string Subject,
    string Message,
    string PreferredLanguage,
    string PreferredChannelsJson,
    string Area,
    string Priority,
    string Status,
    string? TicketId,
    string? CheckpointId,
    string? ConfirmationToken,
    string? ConfirmationId,
    string CustomerReply,
    string ResolutionPlan,
    string ReviewNotes,
    string OrdersDataJson,
    string InvoiceDataJson,
    string PolicyMatchesJson,
    string ChannelsNotifiedJson,
    string TimelineJson,
    double AvgResponseSeconds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed record BackofficeGeneratedContent(
    string ResolutionPlan,
    string ReviewNotes,
    string CustomerReply);

internal sealed record BackofficeTicket(
    string Id,
    string RunId,
    string CustomerId,
    string Area,
    string Subject,
    string Description,
    string Priority,
    string Status,
    DateTimeOffset CreatedAt);

internal sealed record EmailConfirmation(
    string Id,
    string RunId,
    string CustomerEmail,
    string Purpose,
    string Token,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt);

internal sealed record FollowUp(
    string Id,
    string TicketId,
    string CustomerId,
    DateTimeOffset DueAt,
    string Channel,
    string Status,
    string Notes);

internal sealed record BackofficeSurvey(
    string Id,
    string TicketId,
    string CustomerEmail,
    string SurveyUrl,
    string Status,
    DateTimeOffset CreatedAt);

internal sealed record BackofficeSurveyResponse(
    string Id,
    string TicketId,
    int Score,
    string? Comment,
    DateTimeOffset SubmittedAt);

internal sealed record BackofficeNotification(
    string Id,
    string RunId,
    string Channel,
    string Recipient,
    string? Subject,
    string Message,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record BackofficeRunListItem(
    string RunId,
    string Area,
    string Priority,
    string Status,
    string? TicketId,
    string? CheckpointId,
    double AvgResponseSeconds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record BackofficeTicketListItem(
    string TicketId,
    string RunId,
    string CustomerId,
    string Area,
    string Subject,
    string Priority,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record BackofficeSurveyListItem(
    string TicketId,
    string CustomerEmail,
    string SurveyUrl,
    string Status,
    DateTimeOffset CreatedAt,
    int? Score,
    string? Comment,
    DateTimeOffset? SubmittedAt);

public sealed record BackofficeChannelStatus(
    string Channel,
    string Mode,
    bool Configured);

public sealed record BackofficeDashboard(
    string RuntimeMode,
    string CanonicalStore,
    string HotStore,
    IReadOnlyList<BackofficeChannelStatus> Channels,
    int RunsTotal,
    int RunsPaused,
    int RunsClosed,
    int TicketsTotal,
    int SurveysCompleted,
    int NotificationsSent,
    int NotificationsOutbox,
    int NotificationsFailed,
    IReadOnlyList<BackofficeRunListItem> RecentRuns,
    IReadOnlyList<BackofficeTicketListItem> RecentTickets,
    IReadOnlyList<BackofficeSurveyListItem> RecentSurveys);