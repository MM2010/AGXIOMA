using AgxiomaLegalEmail.Models;

namespace AgxiomaLegalEmail.Hubs;

public interface IPipelineHubClient
{
    Task PhaseCompleted(string emailId, string phase, string description, int durationMs);
    Task PipelineStateChanged(string emailId, string state, List<PipelineTimelineEntry> timeline);
    Task HitlPending(string emailId, string lawyerId);
    Task IngestionProgress(int current, int total, string stage);
}
