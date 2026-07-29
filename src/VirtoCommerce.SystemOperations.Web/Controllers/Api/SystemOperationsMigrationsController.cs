using System;
using System.Globalization;
using System.IO;
using System.Linq;
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
        return File(zip, "application/zip", BuildFileName());
    }

    /// <summary>
    /// Builds a download name of the form <c>[host]-migration-scripts-{yyyyMMdd-HHmmss}.zip</c> so exports
    /// are identifiable by source environment and time.
    /// </summary>
    private string BuildFileName()
    {
        var host = Request?.Host.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            host = Environment.MachineName;
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return $"{Sanitize(host)}-migration-scripts-{timestamp}.zip";
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }
}
