using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using VirtoCommerce.SystemOperations.Core.Diagnostics;

namespace VirtoCommerce.SystemOperations.Data.Diagnostics;

// Rules for a production deployment, each with its evidence and fix. A rule that depends on the number of instances
// uses the instances the background job engine knows; with one instance, shared storage is only advice.
public class ConfigurationCheckService(
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    IEnvironmentProfileService profileService)
    : IConfigurationCheckService
{
    // The signing certificate the platform ships with for development, and the value its sample configuration unlocks it
    // with: anyone can sign tokens with it.
    private const string DevelopmentCertificateSampleValue = "virto";
    private const string DevelopmentCertificatePath = "certificates/virtocommerce.pfx";

    public virtual async Task<IList<Finding>> CheckAsync(CancellationToken cancellationToken = default)
    {
        var profile = await profileService.GetProfileAsync(cancellationToken);
        var production = !hostEnvironment.IsDevelopment();
        var instances = profile.KnownInstanceCount ?? 1;
        var several = instances > 1;
        var findings = new List<Finding>();

        if (!production)
        {
            findings.Add(Configuration("environment-development", FindingSeverities.Warning,
                "The platform runs in the Development environment",
                $"ASPNETCORE_ENVIRONMENT is {hostEnvironment.EnvironmentName}.",
                "Use Production outside developer machines: Development shows error details to callers and relaxes checks."));
        }

        CheckSigningCertificate(findings, production);
        CheckRedis(findings, profile, several);
        CheckSharedStorage(findings, profile, several, instances);
        CheckBackgroundJobs(findings, profile, production);
        CheckLogging(findings, production);
        CheckSecurity(findings, production);
        CheckModulesAndLicense(findings, profile);

        if (!configuration.GetValue("Caching:CacheEnabled", true))
        {
            findings.Add(Configuration("cache-disabled", FindingSeverities.Warning,
                "The platform memory cache is off",
                "Caching:CacheEnabled is false.",
                "Turn the cache on: without it every request reads the database, and the storefront slows down under load."));
        }

        return findings
            .OrderBy(x => SeverityOrder(x.Severity))
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToList();
    }

    private void CheckSigningCertificate(List<Finding> findings, bool production)
    {
        var path = configuration["Auth:PrivateKeyPath"] ?? string.Empty;
        var password = configuration["Auth:PrivateKeyPassword"];

        if (path.Replace('\\', '/').EndsWith(DevelopmentCertificatePath, StringComparison.OrdinalIgnoreCase) && password == DevelopmentCertificateSampleValue)
        {
            findings.Add(Configuration("signing-certificate-default", production ? FindingSeverities.Critical : FindingSeverities.Info,
                "Tokens are signed with the development certificate",
                "Auth:PrivateKeyPath points to the certificate the platform ships with, with its published password.",
                "Create a certificate for this environment and set Auth:PublicCertPath, Auth:PrivateKeyPath and Auth:PrivateKeyPassword; then sign everyone out once."));
        }
    }

    private static void CheckRedis(List<Finding> findings, EnvironmentProfile profile, bool several)
    {
        if (!profile.RedisConfigured)
        {
            findings.Add(Configuration("redis-missing", several ? FindingSeverities.Critical : FindingSeverities.Info,
                "Redis is not configured",
                several
                    ? $"{profile.KnownInstanceCount} instances run without ConnectionStrings:RedisConnectionString."
                    : "ConnectionStrings:RedisConnectionString is empty; one instance is known.",
                "Set ConnectionStrings:RedisConnectionString before running several instances: it keeps their caches in sync and spreads push notifications."));
        }

        if (several && string.Equals(profile.PushNotificationsScalability, "None", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(Configuration("push-notifications-single-instance", FindingSeverities.Warning,
                "Push notifications reach one instance only",
                $"PushNotifications:ScalabilityMode is None with {profile.KnownInstanceCount} instances.",
                "Set PushNotifications:ScalabilityMode to RedisBackplane or AzureSignalRService, so back-office users see the progress of jobs that run on another instance."));
        }
    }

    private static void CheckSharedStorage(List<Finding> findings, EnvironmentProfile profile, bool several, int instances)
    {
        if (string.Equals(profile.AssetsProvider, "FileSystem", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(Configuration("assets-local-file-system", several ? FindingSeverities.Critical : FindingSeverities.Info,
                "Assets are stored on the local file system",
                $"Assets:Provider is FileSystem with {instances} known instance(s).",
                "Use shared storage, such as Assets:Provider AzureBlobStorage, before running several instances: a file uploaded to one instance is missing on the others."));
        }

        if (string.Equals(profile.SearchProvider, "Lucene", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(Configuration("search-lucene", several ? FindingSeverities.Critical : FindingSeverities.Warning,
                "Search runs on Lucene",
                $"Search:Provider is Lucene with {instances} known instance(s).",
                "Use Elasticsearch, OpenSearch or Azure AI Search in production: the Lucene index is a local folder of one instance and does not scale."));
        }
    }

    private void CheckBackgroundJobs(List<Finding> findings, EnvironmentProfile profile, bool production)
    {
        if (production && string.Equals(profile.HangfireStorage, "Memory", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(Configuration("hangfire-memory-storage", FindingSeverities.Warning,
                "Background jobs are kept in memory",
                "VirtoCommerce:Hangfire:JobStorageType is Memory.",
                "Use Database storage in production: queued and scheduled jobs are lost on every restart, and instances do not share them."));
        }

        if (!configuration.GetValue("VirtoCommerce:Hangfire:UseHangfireServer", true))
        {
            findings.Add(Configuration("hangfire-server-off", FindingSeverities.Info,
                "This instance does not process background jobs",
                "VirtoCommerce:Hangfire:UseHangfireServer is false.",
                "Expected on a storefront-only instance; make sure at least one other instance processes jobs (list_failed_jobs shows the servers)."));
        }
    }

    private void CheckLogging(List<Finding> findings, bool production)
    {
        var level = configuration["Serilog:MinimumLevel:Default"];
        if (production && (string.Equals(level, "Debug", StringComparison.OrdinalIgnoreCase) || string.Equals(level, "Verbose", StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(Configuration("logging-verbose", FindingSeverities.Warning,
                "Logs are written at the debug level",
                $"Serilog:MinimumLevel:Default is {level}.",
                "Use Information or Warning in production; turn debug on for one namespace and only while investigating."));
        }
    }

    private void CheckSecurity(List<Finding> findings, bool production)
    {
        if (production && configuration.GetValue("VirtoCommerce:AllowInsecureHttp", false))
        {
            findings.Add(Configuration("insecure-http", FindingSeverities.Warning,
                "Sign-in is allowed over plain HTTP",
                "VirtoCommerce:AllowInsecureHttp is true.",
                "Set it to false and serve the platform over HTTPS: tokens and passwords travel in clear text otherwise."));
        }

        var connectionString = configuration.GetConnectionString("VirtoCommerce") ?? string.Empty;
        if (production && connectionString.Contains("Password=virto", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(Configuration("database-default-password", FindingSeverities.Critical,
                "The database account uses the sample password",
                "ConnectionStrings:VirtoCommerce uses the password of the platform's sample configuration.",
                "Create a database account with its own password and store the connection string in a secret store."));
        }
    }

    private static void CheckModulesAndLicense(List<Finding> findings, EnvironmentProfile profile)
    {
        if (profile.Modules.Failed > 0)
        {
            findings.Add(Configuration("modules-failed", FindingSeverities.Critical,
                $"{profile.Modules.Failed} module(s) failed to load",
                string.Join(", ", profile.Modules.FailedModuleIds),
                "Call list_modules with filter 'failed' for the errors: install the missing dependency or a version that matches the platform."));
        }

        switch (profile.License?.Status)
        {
            case "expired":
                findings.Add(Configuration("license-expired", FindingSeverities.Critical,
                    "The platform license has expired",
                    $"Expired on {profile.License.ExpirationDate:yyyy-MM-dd}.",
                    "Renew the license with Virto Commerce and upload it in the back office (Platform information)."));
                break;
            case "expiring":
                findings.Add(Configuration("license-expiring", FindingSeverities.Warning,
                    "The platform license expires soon",
                    $"Expires on {profile.License.ExpirationDate:yyyy-MM-dd}.",
                    "Renew the license with Virto Commerce before it expires."));
                break;
            case "missing":
                findings.Add(Configuration("license-missing", FindingSeverities.Info,
                    "No platform license is installed",
                    "The platform has no license.",
                    "Install the license you received from Virto Commerce, if this is a commercial deployment."));
                break;
        }
    }

    private static Finding Configuration(string id, string severity, string title, string evidence, string fix)
    {
        return new Finding
        {
            Id = id,
            Severity = severity,
            CauseClass = CauseClasses.Configuration,
            Title = title,
            Evidence = evidence,
            Fix = fix,
        };
    }

    public static int SeverityOrder(string severity)
    {
        return severity switch
        {
            FindingSeverities.Critical => 0,
            FindingSeverities.Warning => 1,
            _ => 2,
        };
    }
}
