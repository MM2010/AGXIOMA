# P17 — Scheduler (Cron)

## File: `Scheduler.cs`, `SchedulePoller.cs`

Namespace: `AGXIOME.Core.Scheduling`

## Classi

### `ScheduleManager : IWorkflowScheduler`

```csharp
public sealed class ScheduleManager : IWorkflowScheduler
{
    private readonly ICommandStore _store;
    private readonly IQueryStore<Schedule> _query;

    public async Task<Schedule> CreateScheduleAsync(string name, string workflowId,
        string cronExpression, SchedulePayload? payload = null, CancellationToken ct = default)
    {
        var schedule = new Schedule(
            Id: Guid.CreateVersion7().ToString(),
            Name: name,
            WorkflowId: workflowId,
            CronExpression: cronExpression,
            Payload: payload,
            Status: ScheduleStatus.Active
        );
        await _store.SaveAsync(schedule, ct);
        return schedule;
    }

    public async Task TriggerAsync(string scheduleId, CancellationToken ct)
    {
        // Esegue schedule immediatamente (manual trigger)
    }
}
```

### `SchedulePoller`

```csharp
// Background service che:
// 1. Ogni 15 secondi (config) interroga DB per schedule due
// 2. Calcola next_run_at basato su cron expression
// 3. Se due, esegue via ScheduleExecutor
// 4. Scrive run history

public sealed class SchedulePoller : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessDueSchedulesAsync(stoppingToken);
            await Task.Delay(_config.PollInterval, stoppingToken);
        }
    }
}
```

### Cron parser

```csharp
// 5-field cron: minute hour day-of-month month day-of-week
// Libreria: Cronos (NuGet)
// CronExpression.Parse("*/5 * * * *").GetNextOccurrence(DateTime.UtcNow)
```

### Schedule models

```csharp
public record Schedule(string Id, string Name, string WorkflowId, string CronExpression,
    SchedulePayload? Payload, ScheduleStatus Status, DateTime? CreatedAt = null);
public record SchedulePayload(Dictionary<string, object>? Input = null, string? SessionId = null);
public enum ScheduleStatus { Active, Paused, Completed, Failed }
public record ScheduleRun(string ScheduleId, string RunId, ScheduleRunStatus Status, DateTime StartedAt);
public enum ScheduleRunStatus { Running, Completed, Failed, Cancelled }
```

## Test

| Test | Descrizione |
|------|------------|
| Create schedule | DB record creato |
| Cron parsing prossima occorrenza | Data futura corretta |
| Poller esegue schedule due | Workflow eseguito |
| Manual trigger | Esecuzione immediata |
| Schedule pause/resume | Status cambiato |
