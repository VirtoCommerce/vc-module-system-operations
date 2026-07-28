namespace VirtoCommerce.SystemOperations.Core.Migrations;

/// <summary>
/// Controls what SQL the migration export produces.
/// </summary>
public enum MigrationExportMode
{
    /// <summary>
    /// Full, self-guarding script per context (each migration wrapped in an
    /// <c>__EFMigrationsHistory</c> existence check). Safe to run against a database at any migration
    /// state and needs no database connection to generate. Recommended for applying to another environment.
    /// </summary>
    Idempotent,

    /// <summary>
    /// Only the migrations not yet applied to the connected database (computed from its history).
    /// Requires a reachable database; falls back to <see cref="Idempotent"/> when it cannot be read.
    /// </summary>
    Pending,
}
