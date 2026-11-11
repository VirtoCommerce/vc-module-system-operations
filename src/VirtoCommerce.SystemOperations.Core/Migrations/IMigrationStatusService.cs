using System.Collections.Generic;

namespace VirtoCommerce.SystemOperations.Core.Migrations;

// The EF Core migrations of every database context the platform runs with: how many are applied, which are pending.
public interface IMigrationStatusService
{
    IList<MigrationContextStatus> GetStatus();
}

public class MigrationContextStatus
{
    public string Context { get; set; }

    public string Provider { get; set; }

    public string Database { get; set; }

    public int Applied { get; set; }

    public IList<string> Pending { get; set; } = [];

    // Set when the database could not be read.
    public string Error { get; set; }
}
