using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.SystemOperations.Core;
using VirtoCommerce.SystemOperations.Core.Migrations;

namespace VirtoCommerce.SystemOperations.Web.Controllers.Api;

[ApiController]
[Route("api/system-operations/migrations")]
[Authorize(ModuleConstants.Security.Permissions.Export)]
public class SystemOperationsMigrationsController : ControllerBase
{
    private readonly IMigrationScriptExporter _exporter;

    public SystemOperationsMigrationsController(IMigrationScriptExporter exporter)
    {
        _exporter = exporter;
    }

    /// <summary>
    /// Downloads a ZIP of the EF Core migration SQL that startup would apply — platform, security and every
    /// installed module — grouped per target database, plus a database mapping. Applies nothing.
    /// </summary>
    /// <param name="mode">"idempotent" (default, safe for any target state) or "pending" (delta vs the connected DB).</param>
    [HttpGet("export")]
    public IActionResult Export([FromQuery] string mode = "idempotent")
    {
        var exportMode = string.Equals(mode, "pending", StringComparison.OrdinalIgnoreCase)
            ? MigrationExportMode.Pending
            : MigrationExportMode.Idempotent;

        var zip = _exporter.ExportZip(exportMode);
        return File(zip, "application/zip", "migration-scripts.zip");
    }
}
