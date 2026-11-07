using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using VirtoCommerce.MCP.Core;
using VirtoCommerce.MCP.Core.Attributes;
using VirtoCommerce.MCP.Core.Services;
using VirtoCommerce.SystemOperations.Core.Diagnostics;
using VirtoCommerce.SystemOperations.Core.Migrations;
using static VirtoCommerce.SystemOperations.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.SystemOperations.MCP.Tools;

// Answers the platform computes and the model cannot: what is misconfigured, where the bottleneck is, what the job
// engine fails on, what a deployment has not migrated.
[McpServerToolType]
[McpScope(McpScopes.Admin)]
[Authorize(DiagnosticsRead)]
public class AnalysisTools(
    IConfigurationCheckService configurationCheckService,
    ICapacityService capacityService,
    IBackgroundJobDiagnosticsService jobDiagnostics,
    IMigrationStatusService migrationStatusService,
    TimeProvider timeProvider)
{
    private const int MaxSampleSeconds = 10;

    [McpServerTool(Name = "check_configuration", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Check the configuration against production-readiness rules: Development environment, the development " +
        "signing certificate, Redis and push notifications with several instances, local file storage for assets, Lucene " +
        "search, in-memory background jobs, debug logging, plain HTTP, the sample database password, failed modules and " +
        "the license. Each finding has a severity, the evidence and the fix. Rules about several instances use the " +
        "instances the job engine knows.")]
    public async Task<object> CheckConfiguration(CancellationToken cancellationToken)
    {
        var findings = await configurationCheckService.CheckAsync(cancellationToken);

        return new
        {
            critical = findings.Count(x => x.Severity == FindingSeverities.Critical),
            warnings = findings.Count(x => x.Severity == FindingSeverities.Warning),
            findings,
        };
    }

    [McpServerTool(Name = "check_capacity", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Are the resources enough, and where is the bottleneck. Measures this instance over a short sample: CPU " +
        "against its processors, memory against its limit (the container limit when there is one), garbage collection, " +
        "the thread pool queue (starvation), database latency, and the background job backlog of the cluster. Returns " +
        "findings with evidence and one recommendation: ok, scale_up, scale_out, fix_hotspot or investigate. Measure " +
        "again during the problem and at a quiet time to compare.")]
    public Task<CapacityReport> CheckCapacity(
        [Description("Seconds to sample the CPU, 1 to 10. Default 2.")] int sample_seconds = 2,
        CancellationToken cancellationToken = default)
    {
        return capacityService.MeasureAsync(TimeSpan.FromSeconds(Math.Clamp(sample_seconds, 1, MaxSampleSeconds)), cancellationToken);
    }

    [McpServerTool(Name = "list_failed_jobs", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("The background job engine at a glance: enqueued, scheduled, processing and failed counts, the job " +
        "servers (one per instance that processes jobs, with workers, start time and heartbeat), the queues, and the " +
        "most recent failed jobs with the exception. Use it when imports, exports, indexing, emails or integrations do " +
        "not finish.")]
    public async Task<object> ListFailedJobs(
        IMcpPageSizeResolver pageSizeResolver,
        [Description("How many hours back to list failed jobs. Omit it for the most recent ones.")] int? hours = null,
        [Description("Number of failed jobs to return. Omit it for the default page size.")] int? take = null)
    {
        var since = hours == null ? (DateTime?)null : timeProvider.GetUtcNow().UtcDateTime.AddHours(-Math.Clamp(hours.Value, 1, 24 * 14));
        var counts = jobDiagnostics.GetCounts();

        return new
        {
            available = counts.Available,
            counts = counts.Available
                ? new { counts.Enqueued, counts.Scheduled, counts.Processing, counts.Failed, counts.Succeeded, counts.Workers }
                : null,
            servers = counts.Servers,
            queues = counts.Queues,
            failed_jobs = jobDiagnostics.GetFailedJobs(since, await pageSizeResolver.GetPageSizeAsync(take)),
        };
    }

    [McpServerTool(Name = "check_pending_migrations", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("For every database context of the platform and its modules: how many EF Core migrations are applied " +
        "and which are pending. Pending migrations after a deployment mean the database is older than the code. Reads " +
        "the migration history of each database; applies nothing. The back office's Export migration scripts gives " +
        "the SQL.")]
    public object CheckPendingMigrations()
    {
        var contexts = migrationStatusService.GetStatus();

        return new
        {
            pending_total = contexts.Sum(x => x.Pending.Count),
            failed_to_read = contexts.Count(x => x.Error != null),
            contexts = contexts.Select(x => new
            {
                context = x.Context,
                provider = x.Provider,
                database = x.Database,
                applied = x.Applied,
                pending = x.Pending,
                error = x.Error,
            }),
        };
    }
}
