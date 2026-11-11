using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.SystemOperations.Core.Diagnostics;

namespace VirtoCommerce.SystemOperations.Data.Diagnostics;

public class IncidentTimelineService(
    IErrorJournal errorJournal,
    IBackgroundJobDiagnosticsService jobDiagnostics,
    TimeProvider timeProvider)
    : IIncidentTimelineService
{
    private const int MaxErrorGroups = 200;

    public virtual IList<TimelineEvent> GetTimeline(DateTime since, DateTime? until = null, int take = 100)
    {
        var end = until ?? timeProvider.GetUtcNow().UtcDateTime;
        var events = new List<TimelineEvent>();

        // The job servers' start times are the starts of the instances that process jobs; this instance is added in
        // case it does not process jobs.
        var instance = EnvironmentProfileService.GetInstance(end);
        var instanceKey = $"{instance.MachineName}:{instance.ProcessId}";
        events.Add(new TimelineEvent
        {
            Time = instance.StartedAt,
            Kind = TimelineEventKinds.InstanceStart,
            Title = $"Instance {instance.MachineName} (process {instance.ProcessId}) started",
            Reference = instanceKey,
        });

        // The job server of this process starts with it and is not another event.
        var jobs = jobDiagnostics.GetCounts();
        events.AddRange(jobs.Servers
            .Where(x => x.StartedAt != null)
            .Where(x => !string.Equals(BackgroundJobDiagnosticsService.GetInstanceKey(x.Name), instanceKey, StringComparison.OrdinalIgnoreCase))
            .Select(x => new TimelineEvent
            {
                Time = x.StartedAt.Value,
                Kind = TimelineEventKinds.InstanceStart,
                Title = $"Job server on {x.Host} started with {x.Workers} workers",
                Reference = x.Name,
            }));

        events.AddRange(errorJournal.Search(new ErrorSearchCriteria { Since = since, Take = MaxErrorGroups })
            .Select(x => new TimelineEvent
            {
                Time = x.FirstSeen,
                Kind = TimelineEventKinds.ErrorFirstSeen,
                Title = $"{x.ExceptionType ?? x.Level}: {x.MessageTemplate} (x{x.Count} in the window, last {x.LastSeen:HH:mm:ss})",
                Reference = x.Fingerprint,
            }));

        events.AddRange(jobDiagnostics.GetFailedJobs(since, take)
            .Where(x => x.FailedAt != null)
            .Select(x => new TimelineEvent
            {
                Time = x.FailedAt.Value,
                Kind = TimelineEventKinds.JobFailed,
                Title = $"Job {x.Job} failed: {x.ExceptionType} {x.ExceptionMessage}",
                Reference = x.JobId,
            }));

        // A start of the same instance reported twice, by the process and by its job server, is one event.
        return events
            .Where(x => x.Time >= since && x.Time <= end)
            .GroupBy(x => (x.Kind, x.Reference, Minute: x.Time.Ticks / TimeSpan.TicksPerMinute))
            .Select(x => x.First())
            .OrderBy(x => x.Time)
            .TakeLast(Math.Max(1, take))
            .ToList();
    }
}
