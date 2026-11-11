using System;
using System.Collections.Generic;

namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

// What the solution runs on: versions, providers, the instance and the modules, without connection strings or secrets.
public class EnvironmentProfile
{
    public string PlatformVersion { get; set; }

    public string EnvironmentName { get; set; }

    public string DotNetVersion { get; set; }

    public string OperatingSystem { get; set; }

    public string RuntimeIdentifier { get; set; }

    public string DatabaseProvider { get; set; }

    public string SearchProvider { get; set; }

    public string AssetsProvider { get; set; }

    public string BackgroundJobsProvider { get; set; }

    public string HangfireStorage { get; set; }

    public bool RedisConfigured { get; set; }

    public string PushNotificationsScalability { get; set; }

    public InstanceInfo Instance { get; set; }

    // The platform instances that run background jobs and reported in the last minutes; null when it is not known.
    public int? KnownInstanceCount { get; set; }

    public LicenseInfo License { get; set; }

    public ModuleCounts Modules { get; set; }
}

public class InstanceInfo
{
    public string MachineName { get; set; }

    public int ProcessId { get; set; }

    public DateTime StartedAt { get; set; }

    public TimeSpan Uptime { get; set; }

    public int ProcessorCount { get; set; }

    // The memory the runtime may use: the container limit when there is one, otherwise the machine's memory.
    public long MemoryLimitBytes { get; set; }

    public bool ServerGarbageCollector { get; set; }
}

public class LicenseInfo
{
    public string Type { get; set; }

    public DateTime? ExpirationDate { get; set; }

    // "active", "expiring" (within 30 days), "expired", "missing" or "unknown".
    public string Status { get; set; }
}

public class ModuleCounts
{
    public int Installed { get; set; }

    public int Virto { get; set; }

    public int Own { get; set; }

    public int Failed { get; set; }

    public IList<string> OwnModuleIds { get; set; } = [];

    public IList<string> FailedModuleIds { get; set; } = [];
}
