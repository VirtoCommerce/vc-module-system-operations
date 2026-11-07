using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SystemOperations.Core.Diagnostics;

namespace VirtoCommerce.SystemOperations.Data.Diagnostics;

public class EnvironmentProfileService(
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    IModuleInventoryService moduleInventory,
    IBackgroundJobDiagnosticsService jobDiagnostics,
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    ILogger<EnvironmentProfileService> logger)
    : IEnvironmentProfileService
{
    // A Hangfire server reports every 30 seconds; one that has not reported for this long is gone, its instance stopped
    // or scaled in, although Hangfire removes it from the list only later.
    public static readonly TimeSpan LiveServerHeartbeat = TimeSpan.FromMinutes(2);

    private const int ExpiringLicenseDays = 30;

    public virtual Task<EnvironmentProfile> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var modules = moduleInventory.GetModules();
        var jobs = jobDiagnostics.GetCounts();

        var profile = new EnvironmentProfile
        {
            PlatformVersion = PlatformVersion.CurrentVersion?.ToString(),
            EnvironmentName = hostEnvironment.EnvironmentName,
            DotNetVersion = Environment.Version.ToString(),
            OperatingSystem = RuntimeInformation.OSDescription,
            RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
            DatabaseProvider = configuration.GetValue("DatabaseProvider", "SqlServer"),
            SearchProvider = configuration["Search:Provider"],
            AssetsProvider = configuration["Assets:Provider"],
            BackgroundJobsProvider = configuration.GetValue("VirtoCommerce:BackgroundJobs:Provider", "Hangfire"),
            HangfireStorage = configuration.GetValue("VirtoCommerce:Hangfire:JobStorageType", "Database"),
            RedisConfigured = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("RedisConnectionString")),
            PushNotificationsScalability = configuration.GetValue("PushNotifications:ScalabilityMode", "None"),
            Instance = GetInstance(now),
            KnownInstanceCount = jobs.Available ? jobs.LiveInstances : null,
            License = GetLicense(now),
            Modules = new ModuleCounts
            {
                Installed = modules.Count(x => !x.Failed),
                Virto = modules.Count(x => x.Origin == ModuleOrigins.Virto),
                Own = modules.Count(x => x.Origin == ModuleOrigins.Own),
                Failed = modules.Count(x => x.Failed),
                OwnModuleIds = modules.Where(x => x.Origin == ModuleOrigins.Own).Select(x => $"{x.Id} {x.Version}").ToList(),
                FailedModuleIds = modules.Where(x => x.Failed).Select(x => $"{x.Id} {x.Version}").ToList(),
            },
        };

        return Task.FromResult(profile);
    }

    // An instance is a process, not a host: several platforms on one machine or one container host share the host name.
    public static int CountLiveInstances(BackgroundJobCounts jobs, DateTime now)
    {
        return jobs.Servers
            .Where(x => x.Heartbeat == null || now - x.Heartbeat.Value <= LiveServerHeartbeat)
            .Select(x => BackgroundJobDiagnosticsService.GetInstanceKey(x.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    public static InstanceInfo GetInstance(DateTime now)
    {
        using var process = Process.GetCurrentProcess();
        var startedAt = process.StartTime.ToUniversalTime();

        return new InstanceInfo
        {
            MachineName = Environment.MachineName,
            ProcessId = Environment.ProcessId,
            StartedAt = startedAt,
            Uptime = now - startedAt,
            ProcessorCount = Environment.ProcessorCount,
            MemoryLimitBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            ServerGarbageCollector = GCSettings.IsServerGC,
        };
    }

    // The license is read by the platform's LicenseProvider, which lives in the platform host and not in a package a
    // module can reference, so it is found by name. Only the type and the expiration date are read: the license also
    // names the customer, which is personal data.
    protected virtual LicenseInfo GetLicense(DateTime now)
    {
        try
        {
            var providerType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(x => x.GetType("VirtoCommerce.Platform.Web.Licensing.LicenseProvider", throwOnError: false))
                .FirstOrDefault(x => x != null);
            var provider = providerType == null ? null : serviceProvider.GetService(providerType);
            if (provider == null)
            {
                return new LicenseInfo { Status = "unknown" };
            }

            var license = providerType.GetMethod("GetLicense")?.Invoke(provider, null);
            if (license == null)
            {
                return new LicenseInfo { Status = "missing" };
            }

            var licenseType = license.GetType();
            var expiration = licenseType.GetProperty("ExpirationDate")?.GetValue(license) as DateTime?;

            return new LicenseInfo
            {
                Type = licenseType.GetProperty("Type")?.GetValue(license) as string,
                ExpirationDate = expiration,
                Status = GetLicenseStatus(expiration, now),
            };
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not read the platform license");
            return new LicenseInfo { Status = "unknown" };
        }
    }

    public static string GetLicenseStatus(DateTime? expiration, DateTime now)
    {
        if (expiration == null)
        {
            return "unknown";
        }

        if (expiration < now)
        {
            return "expired";
        }

        return expiration < now.AddDays(ExpiringLicenseDays) ? "expiring" : "active";
    }
}
