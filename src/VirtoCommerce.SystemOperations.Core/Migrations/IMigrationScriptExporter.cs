namespace VirtoCommerce.SystemOperations.Core.Migrations;

/// <summary>
/// Produces a ZIP of EF Core migration SQL scripts for every registered DbContext
/// (platform + security + all installed modules), grouped by target database.
/// </summary>
public interface IMigrationScriptExporter
{
    /// <summary>
    /// Builds the ZIP archive contents in memory. Never applies migrations and never opens the database
    /// in <see cref="MigrationExportMode.Idempotent"/> mode.
    /// </summary>
    byte[] ExportZip(MigrationExportMode mode);
}
