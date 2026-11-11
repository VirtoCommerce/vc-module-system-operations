using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VirtoCommerce.SystemOperations.Core.Diagnostics;

namespace VirtoCommerce.SystemOperations.Data.Diagnostics;

public class PlatformHealthService(
    HealthCheckService healthCheckService,
    ISensitiveDataRedactor redactor,
    TimeProvider timeProvider)
    : IPlatformHealthService
{
    public virtual async Task<PlatformHealthReport> CheckAsync(CancellationToken cancellationToken = default)
    {
        var report = await healthCheckService.CheckHealthAsync(cancellationToken);

        var result = new PlatformHealthReport
        {
            Status = ToStatus(report.Status),
            MachineName = Environment.MachineName,
            CheckedAt = timeProvider.GetUtcNow().UtcDateTime,
            Duration = report.TotalDuration,
            Checks = report.Entries
                .Select(x => new HealthCheckEntry
                {
                    Name = x.Key,
                    Status = ToStatus(x.Value.Status),
                    Tags = [.. x.Value.Tags],
                    DurationMs = Math.Round(x.Value.Duration.TotalMilliseconds, 1),
                    Description = redactor.Redact(x.Value.Description).Truncate(500),
                    Error = redactor.Redact(x.Value.Exception?.GetBaseException().Message).Truncate(500),
                })
                .OrderBy(x => x.Name)
                .ToList(),
        };

        foreach (var check in result.Checks.Where(x => x.Status != HealthStatuses.Healthy))
        {
            result.Findings.Add(new Finding
            {
                Id = $"health:{check.Name}",
                Severity = check.Status == HealthStatuses.Unhealthy ? FindingSeverities.Critical : FindingSeverities.Warning,
                CauseClass = GetCauseClass(check),
                Title = $"{check.Name} is {check.Status}",
                Evidence = string.Join(" ", new[] { check.Description, check.Error }.Where(x => !string.IsNullOrEmpty(x))),
                Fix = GetFix(check),
            });
        }

        return result;
    }

    public static string ToStatus(HealthStatus status)
    {
        return status switch
        {
            HealthStatus.Healthy => HealthStatuses.Healthy,
            HealthStatus.Degraded => HealthStatuses.Degraded,
            HealthStatus.Unhealthy => HealthStatuses.Unhealthy,
            _ => HealthStatuses.Unknown,
        };
    }

    // A failed module is a deployment or configuration problem (a missing dependency, a version the platform does not
    // support); a failed database, cache or search check is the infrastructure the platform depends on.
    private static string GetCauseClass(HealthCheckEntry check)
    {
        return check.Tags.Contains("Modules", StringComparer.OrdinalIgnoreCase)
            ? CauseClasses.Configuration
            : CauseClasses.Infrastructure;
    }

    private static string GetFix(HealthCheckEntry check)
    {
        if (check.Tags.Contains("Modules", StringComparer.OrdinalIgnoreCase))
        {
            return "Call list_modules with filter 'failed' to see the module errors: usually a missing dependency or a version that needs a newer platform.";
        }

        if (check.Tags.Contains("Database", StringComparer.OrdinalIgnoreCase))
        {
            return "Check that the database server is reachable from the platform and not overloaded: connections, DTU or CPU, blocking sessions.";
        }

        if (check.Tags.Contains("Cache", StringComparer.OrdinalIgnoreCase))
        {
            return "Check the Redis server and ConnectionStrings:RedisConnectionString; with several instances the caches go out of sync while Redis is down.";
        }

        return "Check the service the health check names and its settings.";
    }
}
