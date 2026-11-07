using System;
using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using VirtoCommerce.MCP.Core;
using VirtoCommerce.MCP.Core.Attributes;
using VirtoCommerce.MCP.Core.Models;
using VirtoCommerce.SystemOperations.Core.Diagnostics;
using static VirtoCommerce.Platform.Core.PlatformConstants.Security.Permissions;

namespace VirtoCommerce.SystemOperations.MCP.Tools;

// The back office's Reset cache and Restart, with the permissions the back office asks for them. Resetting the cache
// asks the user in the chat; a restart takes the platform down for a while, so a second person approves it.
[McpServerToolType]
[McpScope(McpScopes.Admin)]
public class MaintenanceTools(
    IPlatformMaintenanceService maintenanceService,
    IBackgroundJobDiagnosticsService jobDiagnostics,
    TimeProvider timeProvider)
{
    [McpServerTool(Name = "reset_platform_cache", ReadOnly = false, Destructive = true, Idempotent = true)]
    [Authorize(ResetCache)]
    [McpRequiresConfirmation("Reset the platform cache now? Every cached catalog, price and setting is read from the database again, so the first requests are slower. Reason: {reason}")]
    [Description("Expire the whole platform memory cache, as the back office's Reset cache does. Use it after a change " +
        "made directly in the database, or when the storefront shows stale data that the cache explains. The first " +
        "requests after it are slower.")]
    public object ResetPlatformCache(
        [Description("Why the cache is reset, for the audit log.")] string reason)
    {
        maintenanceService.ResetCache();

        return new { reset = true, at = timeProvider.GetUtcNow().UtcDateTime, instance = Environment.MachineName, reason };
    }

    [McpServerTool(Name = "restart_platform", ReadOnly = false, Destructive = true, Idempotent = false)]
    [Authorize(ModuleManage)]
    [McpRequiresApproval]
    [Description("Restart the platform application gracefully, as the back office's Restart does, for example after " +
        "installing modules or changing settings that are read at start. The change is submitted for approval: tell the " +
        "user its id and that another person must approve it. The instance that runs the approved change restarts; " +
        "running background jobs on it are interrupted and retried.")]
    public McpChangePreview RestartPlatform(
        [Description("Why the platform is restarted, for the approver and the audit log.")] string reason,
        bool dryRun = false)
    {
        var instance = Environment.MachineName;
        var jobs = jobDiagnostics.GetCounts();

        var preview = new McpChangePreview
        {
            Action = "restart",
            ObjectType = "platform_instance",
            ObjectId = instance,
            ObjectName = $"Platform on {instance}",
            DryRun = dryRun,
        }
        .AddChange("state", "running", dryRun ? "will restart" : "restarting")
        .AddWarning($"Reason: {reason}")
        .AddWarning("The platform does not answer requests while it restarts, usually for one to a few minutes.");

        if (jobs.Available && jobs.Processing > 0)
        {
            preview.AddWarning($"{jobs.Processing} background job(s) are processing in the cluster; those on the restarting instance are interrupted and retried.");
        }

        if (jobs.Available && jobs.LiveInstances > 1)
        {
            preview.AddWarning($"{jobs.LiveInstances} instances are running: only the instance that runs the approved change restarts, not the others.");
        }

        if (!dryRun)
        {
            maintenanceService.Restart();
        }

        return preview;
    }
}
