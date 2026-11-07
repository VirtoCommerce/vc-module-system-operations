using System;
using System.Collections.Generic;
using System.Linq;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VirtoCommerce.SystemOperations.Core.Diagnostics;

namespace VirtoCommerce.SystemOperations.Data.Diagnostics;

// Reads the Hangfire storage the platform runs with. Every platform instance that processes jobs is a Hangfire server
// with a heartbeat, so the servers are also the known instances of the cluster and their start times its restarts.
public class BackgroundJobDiagnosticsService(
    IServiceProvider serviceProvider,
    ISensitiveDataRedactor redactor,
    TimeProvider timeProvider,
    ILogger<BackgroundJobDiagnosticsService> logger)
    : IBackgroundJobDiagnosticsService
{
    private const int MaxFailedJobsRead = 500;

    public virtual BackgroundJobCounts GetCounts()
    {
        var monitoring = GetMonitoringApi();
        if (monitoring == null)
        {
            return new BackgroundJobCounts { Available = false };
        }

        try
        {
            var statistics = monitoring.GetStatistics();
            var servers = monitoring.Servers().Select(x => new JobServerInfo
            {
                Name = x.Name,
                Host = GetHost(x.Name),
                Workers = x.WorkersCount,
                Queues = [.. x.Queues ?? []],
                StartedAt = x.StartedAt,
                Heartbeat = x.Heartbeat,
            }).ToList();

            var counts = new BackgroundJobCounts
            {
                Available = true,
                Enqueued = statistics.Enqueued,
                Scheduled = statistics.Scheduled,
                Processing = statistics.Processing,
                Failed = statistics.Failed,
                Succeeded = statistics.Succeeded,
                Workers = servers.Sum(x => x.Workers),
                Servers = servers,
                Queues = monitoring.Queues().Select(x => new JobQueueInfo { Name = x.Name, Length = x.Length }).ToList(),
            };
            counts.LiveInstances = EnvironmentProfileService.CountLiveInstances(counts, timeProvider.GetUtcNow().UtcDateTime);

            return counts;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read the background job statistics");
            return new BackgroundJobCounts { Available = false };
        }
    }

    public virtual IList<FailedJob> GetFailedJobs(DateTime? since, int take)
    {
        var monitoring = GetMonitoringApi();
        if (monitoring == null)
        {
            return [];
        }

        try
        {
            // Hangfire lists failed jobs most recent first.
            return monitoring.FailedJobs(0, MaxFailedJobsRead)
                .Select(x => new FailedJob
                {
                    JobId = x.Key,
                    Job = x.Value.Job != null ? $"{x.Value.Job.Type?.Name}.{x.Value.Job.Method?.Name}" : x.Value.InvocationData?.Type,
                    ExceptionType = x.Value.ExceptionType,
                    ExceptionMessage = redactor.Redact(x.Value.ExceptionMessage).Truncate(500),
                    FailedAt = x.Value.FailedAt,
                })
                .Where(x => since == null || x.FailedAt >= since)
                .Take(Math.Max(1, take))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read the failed background jobs");
            return [];
        }
    }

    // Hangfire names a server "{machine}:{process id}:{guid}" unless the platform sets another name.
    public static string GetHost(string serverName)
    {
        if (string.IsNullOrEmpty(serverName))
        {
            return serverName;
        }

        var colon = serverName.IndexOf(':');
        return colon > 0 ? serverName[..colon] : serverName;
    }

    // "{machine}:{process id}" of a server: one platform process may run several Hangfire servers, one per queue set.
    public static string GetInstanceKey(string serverName)
    {
        var parts = serverName?.Split(':');

        return parts?.Length >= 3 ? $"{parts[0]}:{parts[1]}" : serverName;
    }

    protected virtual IMonitoringApi GetMonitoringApi()
    {
        try
        {
            var storage = serviceProvider.GetService<JobStorage>() ?? JobStorage.Current;
            return storage?.GetMonitoringApi();
        }
        catch (InvalidOperationException)
        {
            // No Hangfire storage: the platform runs another background job engine without legacy Hangfire.
            return null;
        }
    }
}
