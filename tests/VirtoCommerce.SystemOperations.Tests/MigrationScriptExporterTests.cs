using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.SystemOperations.Core.Migrations;
using VirtoCommerce.SystemOperations.Data.Migrations;
using Xunit;

namespace VirtoCommerce.SystemOperations.Tests
{
    public class MigrationScriptExporterTests
    {
        // Unreachable endpoint + short timeout: idempotent generation is offline, so this never actually connects.
        private const string ConnectionString =
            "Server=127.0.0.1,15;Database=export_test;Connect Timeout=1;TrustServerCertificate=True";

        [Fact]
        public void ExportZip_Idempotent_ContainsPerContextScript_CombinedAndDatabaseMapping()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddDbContext<ExporterTestDbContext>(options =>
                options.UseSqlServer(
                    ConnectionString,
                    sql => sql.MigrationsAssembly(typeof(ExporterTestDbContext).Assembly.GetName().Name)));

            using var provider = services.BuildServiceProvider();
            var exporter = new MigrationScriptExporter(provider, NullLogger<MigrationScriptExporter>.Instance);

            // Act
            var zipBytes = exporter.ExportZip(MigrationExportMode.Idempotent);

            // Assert
            using var archive = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read);
            var names = archive.Entries.Select(e => e.FullName).ToList();

            Assert.Contains("ExporterTest.sql", names);
            Assert.Contains("_databases.md", names);
            Assert.Contains("_databases.json", names);
            Assert.Contains("vc-package.json", names);

            var package = ReadEntry(archive, "vc-package.json");
            Assert.Contains("platformVersion", package);
            Assert.Contains("\"modules\"", package);
            Assert.Contains(names, n => n.StartsWith("_combined.", StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal));

            var script = ReadEntry(archive, "ExporterTest.sql");
            Assert.Contains("ExporterTestItems", script); // the migration's CreateTable
            Assert.Contains("__EFMigrationsHistory", script); // idempotent guard / history insert

            var mapping = ReadEntry(archive, "_databases.json");
            Assert.Contains("export_test", mapping); // database name captured in the mapping
        }

        private static string ReadEntry(ZipArchive archive, string name)
        {
            using var reader = new StreamReader(archive.GetEntry(name).Open());
            return reader.ReadToEnd();
        }
    }
}
