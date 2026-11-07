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
using static VirtoCommerce.SystemOperations.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.SystemOperations.MCP.Tools;

// Orientation: where am I and what is the state. Every answer names the instance it was measured on, because a call
// reaches one instance of the cluster.
[McpServerToolType]
[McpScope(McpScopes.Admin)]
[Authorize(DiagnosticsRead)]
public class OrientationTools(
    IEnvironmentProfileService profileService,
    IPlatformHealthService healthService,
    IModuleInventoryService moduleInventory,
    IIncidentTimelineService timelineService,
    TimeProvider timeProvider)
{
    [McpServerTool(Name = "get_environment_profile", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("What this Virto Commerce solution runs on: platform and .NET versions, environment, database, search, " +
        "assets and background job providers, Redis, the instance (processors, memory limit, uptime), the number of known " +
        "instances, the license status, and the modules split into Virto's and the solution's own (customizations) and " +
        "failed. No connection strings or secrets. Start here before any diagnosis.")]
    public Task<EnvironmentProfile> GetEnvironmentProfile(CancellationToken cancellationToken)
    {
        return profileService.GetProfileAsync(cancellationToken);
    }

    [McpServerTool(Name = "get_platform_health", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Run the platform's health checks on this instance: database, cache, Redis, module loading and the " +
        "checks modules add (search and others). Returns the status and latency of each check, and findings with the " +
        "cause class and the fix for every check that is not healthy.")]
    public Task<PlatformHealthReport> GetPlatformHealth(CancellationToken cancellationToken)
    {
        return healthService.CheckAsync(cancellationToken);
    }

    [McpServerTool(Name = "list_modules", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("List the installed modules with version and origin: 'virto' for Virto Commerce's modules, 'own' for " +
        "the solution's customizations and third-party modules. Filter 'own' answers 'what did we customize', 'failed' " +
        "returns the modules that did not load with their errors.")]
    public async Task<object> ListModules(
        IMcpPageSizeResolver pageSizeResolver,
        [Description("all, own, virto or failed. Default all.")] string filter = ModuleFilters.All,
        [Description("Number of rows to return. Omit it for the default page size.")] int? take = null)
    {
        var modules = moduleInventory.GetModules(filter);
        var size = await pageSizeResolver.GetPageSizeAsync(take);

        return new
        {
            total_count = modules.Count,
            modules = modules.Take(size).Select(x => new
            {
                id = x.Id,
                version = x.Version,
                origin = x.Origin,
                failed = x.Failed,
                errors = x.Failed ? x.Errors : null,
                dependencies = x.Failed ? x.Dependencies : null,
            }),
        };
    }

    [McpServerTool(Name = "get_incident_timeline", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("What happened in a window, in time order: instance starts (restarts, deployments, scale-out), the " +
        "first occurrence of each error with its count, and failed background jobs. Use it to see what changed just " +
        "before a problem started. Errors are those of this instance since it started.")]
    public object GetIncidentTimeline(
        [Description("How many hours back to look. Default 24.")] int hours = 24,
        [Description("Maximum number of events, the latest kept. Default 100.")] int take = 100)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var since = now.AddHours(-Math.Clamp(hours, 1, 24 * 14));

        return new
        {
            since,
            until = now,
            events = timelineService.GetTimeline(since, now, Math.Clamp(take, 1, 500)),
        };
    }
}
