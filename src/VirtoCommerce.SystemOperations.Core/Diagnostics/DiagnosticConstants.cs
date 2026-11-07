namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

// The classes a diagnosis names as the cause of a problem, so a person knows who fixes it: the data, the configuration,
// the solution's own code, Virto's code, the hosting, or a service the platform calls.
public static class CauseClasses
{
    public const string Data = "data";
    public const string Configuration = "configuration";
    public const string Customization = "customization";
    public const string PlatformCode = "platform_code";
    public const string Infrastructure = "infrastructure";
    public const string ExternalService = "external_service";
}

public static class FindingSeverities
{
    public const string Critical = "critical";
    public const string Warning = "warning";
    public const string Info = "info";
}

public static class HealthStatuses
{
    public const string Healthy = "healthy";
    public const string Degraded = "degraded";
    public const string Unhealthy = "unhealthy";
    public const string Unknown = "unknown";
}

// Who maintains the code of a module: Virto Commerce, or the solution (a partner, the client, a third party).
public static class ModuleOrigins
{
    public const string Virto = "virto";
    public const string Own = "own";
}
