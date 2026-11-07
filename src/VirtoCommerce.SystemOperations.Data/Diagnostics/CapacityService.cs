using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SystemOperations.Core.Diagnostics;

namespace VirtoCommerce.SystemOperations.Data.Diagnostics;

// Measures this instance and reads the cluster's job workers, then names the bottleneck with its evidence. The
// thresholds are rules of thumb for a .NET web application; the evidence is returned, so a person can judge.
public class CapacityService(
    IPlatformHealthService healthService,
    IBackgroundJobDiagnosticsService jobDiagnostics,
    TimeProvider timeProvider)
    : ICapacityService
{
    private const double HighCpuPercent = 80;
    private const double HighMemoryPercent = 85;
    private const double HighGcPausePercent = 10;
    private const double SlowDatabaseMs = 500;
    private const int QueuedItemsPerProcessor = 10;
    private const int BacklogPerWorker = 50;

    public virtual async Task<CapacityReport> MeasureAsync(TimeSpan sample, CancellationToken cancellationToken = default)
    {
        sample = sample < TimeSpan.FromMilliseconds(200) ? TimeSpan.FromMilliseconds(200) : sample;

        using var process = Process.GetCurrentProcess();
        var cpuBefore = process.TotalProcessorTime;
        var stopwatch = Stopwatch.StartNew();

        // The health checks run during the sample, so their latency is measured under the same load.
        var health = await healthService.CheckAsync(cancellationToken);
        var remaining = sample - stopwatch.Elapsed;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, timeProvider, cancellationToken);
        }

        process.Refresh();
        var elapsed = stopwatch.Elapsed;
        var cpuPercent = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds / (elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100;

        var memoryInfo = GC.GetGCMemoryInfo();
        var uptime = timeProvider.GetUtcNow().UtcDateTime - process.StartTime.ToUniversalTime();

        var report = new CapacityReport
        {
            MachineName = Environment.MachineName,
            MeasuredAt = timeProvider.GetUtcNow().UtcDateTime,
            SampleSeconds = Math.Round(elapsed.TotalSeconds, 2),
            CpuPercent = Math.Round(Math.Clamp(cpuPercent, 0, 100), 1),
            ProcessorCount = Environment.ProcessorCount,
            WorkingSetBytes = process.WorkingSet64,
            ManagedHeapBytes = GC.GetTotalMemory(forceFullCollection: false),
            MemoryLimitBytes = memoryInfo.TotalAvailableMemoryBytes,
            GcPausePercent = uptime.TotalMilliseconds > 0 ? Math.Round(GC.GetTotalPauseDuration().TotalMilliseconds / uptime.TotalMilliseconds * 100, 2) : 0,
            Gen2Collections = GC.CollectionCount(2),
            ThreadPoolThreads = ThreadPool.ThreadCount,
            ThreadPoolQueueLength = ThreadPool.PendingWorkItemCount,
            LockContentionCount = Monitor.LockContentionCount,
            Jobs = jobDiagnostics.GetCounts(),
            Dependencies = health.Checks,
        };
        report.MemoryPercent = report.MemoryLimitBytes > 0 ? Math.Round(report.WorkingSetBytes * 100.0 / report.MemoryLimitBytes, 1) : 0;

        Evaluate(report);

        return report;
    }

    public static void Evaluate(CapacityReport report)
    {
        report.Recommendation = CapacityRecommendations.Ok;

        EvaluateProcess(report);
        EvaluateDatabase(report);
        EvaluateJobs(report);
    }

    // Memory, garbage collection, the thread pool and the CPU of this instance.
    private static void EvaluateProcess(CapacityReport report)
    {
        if (report.MemoryPercent >= HighMemoryPercent)
        {
            report.Findings.Add(Infrastructure("memory-high", FindingSeverities.Critical,
                "Memory is close to the limit",
                $"Working set {ToMb(report.WorkingSetBytes)} MB of {ToMb(report.MemoryLimitBytes)} MB ({report.MemoryPercent}%); managed heap {ToMb(report.ManagedHeapBytes)} MB.",
                "Give the instance more memory. If the managed heap keeps growing between measurements, look for a leak or an oversized cache before scaling."));
            Recommend(report, CapacityRecommendations.ScaleUp);
        }

        if (report.GcPausePercent >= HighGcPausePercent)
        {
            report.Findings.Add(Infrastructure("gc-pressure", FindingSeverities.Warning,
                "Garbage collection takes a large share of the time",
                $"{report.GcPausePercent}% of the process lifetime in GC pauses; {report.Gen2Collections} full collections.",
                "Usually large allocations: big result sets loaded at once, large exports, images. Find the heavy operations before adding memory."));
            Recommend(report, CapacityRecommendations.FixHotspot);
        }

        if (report.ThreadPoolQueueLength > report.ProcessorCount * QueuedItemsPerProcessor)
        {
            report.Findings.Add(new Finding
            {
                Id = "thread-pool-starvation",
                Severity = FindingSeverities.Critical,
                CauseClass = CauseClasses.Customization,
                Title = "Requests wait for a free thread",
                Evidence = $"{report.ThreadPoolQueueLength} work items queued with {report.ThreadPoolThreads} threads on {report.ProcessorCount} processors.",
                Fix = "Thread pool starvation, usually blocking calls in async code (.Result, .Wait(), synchronous I/O) in a module. Find the module in search_errors and explain_error; more instances only delay it.",
            });
            Recommend(report, CapacityRecommendations.FixHotspot);
        }

        if (report.CpuPercent >= HighCpuPercent)
        {
            report.Findings.Add(Infrastructure("cpu-high", FindingSeverities.Warning,
                "CPU is busy",
                $"{report.CpuPercent}% of {report.ProcessorCount} processors over {report.SampleSeconds} s.",
                "Add instances if the load is traffic; if one operation dominates, fix it first. Measure again at a quiet time to compare."));
            Recommend(report, CapacityRecommendations.ScaleOut);
        }
    }

    private static void EvaluateDatabase(CapacityReport report)
    {
        foreach (var dependency in report.Dependencies.Where(x => x.Tags.Contains("Database", StringComparer.OrdinalIgnoreCase)))
        {
            if (dependency.Status != HealthStatuses.Healthy)
            {
                report.Findings.Add(Infrastructure("database-unhealthy", FindingSeverities.Critical,
                    "The database does not answer the health check",
                    $"{dependency.Name}: {dependency.Status}. {dependency.Error}",
                    "Check the database server, its limits and the network between it and the platform."));
                Recommend(report, CapacityRecommendations.Investigate);
            }
            else if (dependency.DurationMs >= SlowDatabaseMs)
            {
                report.Findings.Add(Infrastructure("database-slow", FindingSeverities.Warning,
                    "The database answers slowly",
                    $"{dependency.Name} took {dependency.DurationMs} ms for a trivial query.",
                    "The database tier is saturated or far away: check its CPU or DTU, blocking sessions and long-running queries."));
                Recommend(report, CapacityRecommendations.Investigate);
            }
        }
    }

    private static void EvaluateJobs(CapacityReport report)
    {
        var jobs = report.Jobs;
        if (jobs?.Available == true && jobs.Workers > 0 && jobs.Enqueued > jobs.Workers * BacklogPerWorker)
        {
            report.Findings.Add(Infrastructure("job-backlog", FindingSeverities.Warning,
                "Background jobs pile up",
                $"{jobs.Enqueued} jobs enqueued for {jobs.Workers} workers on {jobs.Servers.Count} server(s).",
                "Add job workers (VirtoCommerce:Hangfire:WorkerCount) or an instance that processes jobs; check list_failed_jobs for jobs that retry in a loop."));
            Recommend(report, CapacityRecommendations.ScaleOut);
        }
    }

    // The first problem decides, except that "ok" always gives way.
    private static void Recommend(CapacityReport report, string recommendation)
    {
        if (report.Recommendation == CapacityRecommendations.Ok)
        {
            report.Recommendation = recommendation;
        }
    }

    private static Finding Infrastructure(string id, string severity, string title, string evidence, string fix)
    {
        return new Finding { Id = id, Severity = severity, CauseClass = CauseClasses.Infrastructure, Title = title, Evidence = evidence, Fix = fix };
    }

    private static long ToMb(long bytes)
    {
        return bytes / 1024 / 1024;
    }
}
