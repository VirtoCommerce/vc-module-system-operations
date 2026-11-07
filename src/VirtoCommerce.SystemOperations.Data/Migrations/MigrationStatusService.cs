using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VirtoCommerce.SystemOperations.Core.Diagnostics;
using VirtoCommerce.SystemOperations.Core.Migrations;

namespace VirtoCommerce.SystemOperations.Data.Migrations;

// Reads the migration history of every database context, as the platform would before it applies migrations at start.
// It opens each database to read its history; it never applies anything.
public class MigrationStatusService(
    IServiceProvider serviceProvider,
    ISensitiveDataRedactor redactor,
    ILogger<MigrationStatusService> logger)
    : IMigrationStatusService
{
    public virtual IList<MigrationContextStatus> GetStatus()
    {
        var result = new List<MigrationContextStatus>();

        foreach (var (name, type) in MigrationScriptExporter.DiscoverContexts())
        {
            var status = new MigrationContextStatus { Context = name };

            try
            {
                using var scope = serviceProvider.CreateScope();
                if (scope.ServiceProvider.GetService(type) is not DbContext context)
                {
                    continue;
                }

                status.Provider = context.Database.ProviderName;
                status.Database = context.Database.GetDbConnection().Database;

                // The modules share one migration history table, so the history holds the migrations of every context;
                // only this context's own migrations are counted.
                var applied = context.Database.GetAppliedMigrations().ToHashSet(StringComparer.OrdinalIgnoreCase);
                var own = context.Database.GetMigrations().ToList();
                status.Applied = own.Count(applied.Contains);
                status.Pending = own.Where(x => !applied.Contains(x)).ToList();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not read the migrations of '{Name}' ({Type})", name, type.FullName);
                // A connection error may quote the connection string.
                status.Error = redactor.Redact(ex.GetBaseException().Message);
            }

            result.Add(status);
        }

        return result;
    }
}
