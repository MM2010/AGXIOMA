using System.Collections.Concurrent;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Scheduling;

public sealed class ScheduleManager : IWorkflowScheduler
{
    private readonly ConcurrentDictionary<string, Schedule> _schedules = new();
    private readonly ConcurrentDictionary<string, List<ScheduleRun>> _runHistory = new();

    public Task<Schedule> CreateScheduleAsync(string name, string workflowId, string cronExpression, SchedulePayload? payload = null, CancellationToken ct = default)
    {
        var schedule = new Schedule(Guid.NewGuid().ToString(), name, workflowId, cronExpression, payload);
        _schedules[schedule.Id] = schedule;
        return Task.FromResult(schedule);
    }

    public Task UpdateScheduleAsync(string scheduleId, string? cronExpression = null, bool? enabled = null, CancellationToken ct = default)
    {
        if (_schedules.TryGetValue(scheduleId, out var s))
        {
            var updated = s with
            {
                CronExpression = cronExpression ?? s.CronExpression,
                Status = enabled.HasValue ? (enabled.Value ? ScheduleStatus.Active : ScheduleStatus.Paused) : s.Status
            };
            _schedules.TryUpdate(scheduleId, updated, s);
        }
        return Task.CompletedTask;
    }

    public Task DeleteScheduleAsync(string scheduleId, CancellationToken ct = default)
    {
        _schedules.TryRemove(scheduleId, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Schedule>> ListSchedulesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Schedule>>(_schedules.Values.ToList());

    public Task TriggerAsync(string scheduleId, CancellationToken ct = default)
    {
        // Execute immediately
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ScheduleRun>> GetRunHistoryAsync(string scheduleId, int limit = 20, CancellationToken ct = default)
    {
        if (_runHistory.TryGetValue(scheduleId, out var runs))
            return Task.FromResult<IReadOnlyList<ScheduleRun>>(runs.TakeLast(limit).ToList());
        return Task.FromResult<IReadOnlyList<ScheduleRun>>(Array.Empty<ScheduleRun>());
    }
}
