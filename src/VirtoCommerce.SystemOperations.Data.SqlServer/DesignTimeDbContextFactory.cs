using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using VirtoCommerce.SystemOperations.Data.Repositories;

namespace VirtoCommerce.SystemOperations.Data.SqlServer;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SystemOperationsDbContext>
{
    public SystemOperationsDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<SystemOperationsDbContext>();
        var connectionString = args.Length != 0 ? args[0] : "Server=(local);User=virto;Password=virto;Database=VirtoCommerce3;";

        builder.UseSqlServer(
            connectionString,
            options => options.MigrationsAssembly(typeof(SqlServerDataAssemblyMarker).Assembly.GetName().Name));

        return new SystemOperationsDbContext(builder.Options);
    }
}
