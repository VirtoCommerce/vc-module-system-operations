using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VirtoCommerce.SystemOperations.Core.Migrations;

namespace VirtoCommerce.SystemOperations.Data.Migrations;

/// <summary>
/// Enumerates every registered <see cref="DbContext"/> from the running platform's service provider and
/// scripts the EF Core migrations that startup would apply — without applying anything. Output is packaged
/// per context and per target database, with a "which context is in which database" mapping.
/// </summary>
public sealed class MigrationScriptExporter(IServiceProvider serviceProvider, ILogger<MigrationScriptExporter> logger) : IMigrationScriptExporter
{
    private const string PlatformContextTypeName = "PlatformDbContext";
    private const string SecurityContextTypeName = "SecurityDbContext";
    private const string DbContextSuffix = "DbContext";
    private const char Nl = '\n';

    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ILogger<MigrationScriptExporter> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public byte[] ExportZip(MigrationExportMode mode)
    {
        var results = new List<ContextScript>();

        foreach (var (name, type) in DiscoverContexts())
        {
            using var scope = _serviceProvider.CreateScope();
            if (scope.ServiceProvider.GetService(type) is not DbContext context)
            {
                // Base/abstract or unregistered context — skip.
                continue;
            }

            try
            {
                var script = ScriptContext(context, name, mode);
                if (script != null)
                {
                    results.Add(script);
                }
            }
            catch (Exception ex)
            {
                // Never let one problematic context abort the whole export.
                _logger.LogError(ex, "Failed to script migrations for '{Name}' ({Type})", name, type.FullName);
            }
        }

        return BuildZip(results, mode);
    }

    private ContextScript ScriptContext(DbContext context, string name, MigrationExportMode mode)
    {
        var migrator = context.Database.GetService<IMigrator>();
        var provider = context.Database.ProviderName;
        var (server, database) = ReadConnectionTarget(context);

        if (mode == MigrationExportMode.Idempotent)
        {
            var idempotent = migrator.GenerateScript(null, null, MigrationsSqlGenerationOptions.Idempotent);
            return new ContextScript(name, context.GetType().FullName, provider, server, database, idempotent);
        }

        // Pending-only delta against the connected database.
        IReadOnlyList<string> applied;
        try
        {
            applied = context.Database.GetAppliedMigrations().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Could not read applied migrations for '{Name}' ({Message}); falling back to an idempotent script.",
                name, ex.Message);
            var fallback = migrator.GenerateScript(null, null, MigrationsSqlGenerationOptions.Idempotent);
            return new ContextScript(name, context.GetType().FullName, provider, server, database, fallback);
        }

        var all = context.Database.GetMigrations().ToList();
        var appliedSet = new HashSet<string>(applied, StringComparer.OrdinalIgnoreCase);
        var pending = all.Where(m => !appliedSet.Contains(m)).ToList();

        if (pending.Count == 0)
        {
            // Nothing to apply — omit from the archive in pending mode.
            return null;
        }

        // Anchor on the last applied migration that also exists in the assembly, so a database that is
        // ahead of the binaries does not throw "migration was not found".
        var from = "0";
        for (var i = all.Count - 1; i >= 0; i--)
        {
            if (appliedSet.Contains(all[i]))
            {
                from = all[i];
                break;
            }
        }

        var sql = migrator.GenerateScript(from, null, MigrationsSqlGenerationOptions.Default);
        return new ContextScript(name, context.GetType().FullName, provider, server, database, sql, pending);
    }

    private static (string Server, string Database) ReadConnectionTarget(DbContext context)
    {
        // DataSource + Database are base DbConnection properties populated the same way for SqlConnection,
        // NpgsqlConnection and MySqlConnection — provider-agnostic, and no secret-bearing string is read.
        try
        {
            var connection = context.Database.GetDbConnection();
            return (connection.DataSource ?? string.Empty, connection.Database ?? string.Empty);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    private static byte[] BuildZip(IReadOnlyList<ContextScript> results, MigrationExportMode mode)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Per-context scripts.
            foreach (var r in results)
            {
                r.FileName = UniqueName(r.Name + ".sql", usedNames);
                WriteEntry(archive, r.FileName, Header(r, mode) + r.Sql);
            }

            // Per-target-database combined scripts.
            var byDatabase = results
                .GroupBy(r => DbKey(r.Server, r.Database), StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var group in byDatabase)
            {
                var label = SanitizeFileName(string.IsNullOrEmpty(group.First().Database) ? "database" : group.First().Database);
                var combinedName = UniqueName($"_combined.{label}.sql", usedNames);

                var sb = new StringBuilder();
                sb.Append("-- Combined migration script").Append(Nl);
                sb.Append($"-- Target database: server={Show(group.First().Server)}; database={Show(group.First().Database)}").Append(Nl);
                sb.Append($"-- Mode: {mode}").Append(Nl).Append(Nl);
                foreach (var r in group)
                {
                    sb.Append("-- ============================================================").Append(Nl);
                    sb.Append($"-- {r.Name}").Append(Nl);
                    sb.Append("-- ============================================================").Append(Nl);
                    sb.Append(r.Sql).Append(Nl).Append(Nl);
                }

                WriteEntry(archive, combinedName, sb.ToString());
                foreach (var r in group)
                {
                    r.CombinedFileName = combinedName;
                }
            }

            // "Which context is in which database" mapping.
            WriteEntry(archive, "_databases.md", BuildDatabasesMarkdown(results, mode));
            WriteEntry(archive, "_databases.json", BuildDatabasesJson(results, mode));
        }

        return memory.ToArray();
    }

    private static string Header(ContextScript r, MigrationExportMode mode)
    {
        var sb = new StringBuilder();
        sb.Append($"-- Migration script for '{r.Name}' ({r.ContextType})").Append(Nl);
        sb.Append($"-- Provider: {Show(r.Provider)}").Append(Nl);
        sb.Append($"-- Target: server={Show(r.Server)}; database={Show(r.Database)}").Append(Nl);
        sb.Append($"-- Mode: {mode}").Append(Nl);
        if (r.Pending is { Count: > 0 })
        {
            sb.Append($"-- Pending migrations ({r.Pending.Count}): {string.Join(", ", r.Pending)}").Append(Nl);
        }
        return sb.ToString();
    }

    private static string BuildDatabasesMarkdown(IReadOnlyList<ContextScript> results, MigrationExportMode mode)
    {
        var sb = new StringBuilder();
        sb.Append("# Migration export — database mapping").Append(Nl).Append(Nl);
        sb.Append($"Mode: **{mode}**. One combined script per target database; apply each to the matching database.").Append(Nl).Append(Nl);
        sb.Append("| Context | Provider | Server | Database | Script | Combined |").Append(Nl);
        sb.Append("|---|---|---|---|---|---|").Append(Nl);
        foreach (var r in results.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append($"| {r.Name} | {Show(r.Provider)} | {Show(r.Server)} | {Show(r.Database)} | {r.FileName} | {r.CombinedFileName} |").Append(Nl);
        }
        return sb.ToString();
    }

    private static string BuildDatabasesJson(IReadOnlyList<ContextScript> results, MigrationExportMode mode)
    {
        // Hand-written JSON to avoid taking a serializer dependency and to keep credential-free output explicit.
        var sb = new StringBuilder();
        sb.Append('{').Append(Nl);
        sb.Append($"  \"mode\": \"{mode}\",").Append(Nl);
        sb.Append("  \"contexts\": [").Append(Nl);
        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            sb.Append("    {").Append(Nl);
            sb.Append($"      \"context\": {JsonString(r.Name)},").Append(Nl);
            sb.Append($"      \"contextType\": {JsonString(r.ContextType)},").Append(Nl);
            sb.Append($"      \"provider\": {JsonString(r.Provider)},").Append(Nl);
            sb.Append($"      \"server\": {JsonString(r.Server)},").Append(Nl);
            sb.Append($"      \"database\": {JsonString(r.Database)},").Append(Nl);
            sb.Append($"      \"script\": {JsonString(r.FileName)},").Append(Nl);
            sb.Append($"      \"combined\": {JsonString(r.CombinedFileName)}").Append(Nl);
            sb.Append("    }").Append(i < results.Count - 1 ? "," : string.Empty).Append(Nl);
        }
        sb.Append("  ]").Append(Nl);
        sb.Append('}').Append(Nl);
        return sb.ToString();
    }

    private static List<(string Name, Type Type)> DiscoverContexts()
    {
        var contextTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .Where(IsConcreteDbContext)
            .Distinct();

        var ranked = new List<(string Name, Type Type, int Sort)>();
        foreach (var type in contextTypes)
        {
            var (name, sort) = Classify(type);
            ranked.Add((name, type, sort));
        }

        return ranked
            .OrderBy(x => x.Sort)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => (x.Name, x.Type))
            .ToList();
    }

    private static (string Name, int Sort) Classify(Type type)
    {
        if (type.Name == PlatformContextTypeName)
        {
            return ("Platform", 0);
        }

        if (type.Name == SecurityContextTypeName)
        {
            return ("Security", 1);
        }

        var name = type.Name;
        if (name.EndsWith(DbContextSuffix, StringComparison.Ordinal) && name.Length > DbContextSuffix.Length)
        {
            name = name.Substring(0, name.Length - DbContextSuffix.Length);
        }

        return (name, 2);
    }

    private static bool IsConcreteDbContext(Type type)
    {
        return type != null
            && type.IsClass
            && !type.IsAbstract
            && !type.ContainsGenericParameters
            && type != typeof(DbContext)
            && typeof(DbContext).IsAssignableFrom(type);
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null);
        }
        catch
        {
            return [];
        }
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string DbKey(string server, string database)
    {
        return $"{server}||{database}";
    }

    private static string UniqueName(string name, HashSet<string> used)
    {
        if (used.Add(name))
        {
            return name;
        }

        var extension = Path.GetExtension(name);
        var baseName = Path.GetFileNameWithoutExtension(name);
        var counter = 1;
        string candidate;
        do
        {
            candidate = $"{baseName}.{counter++}{extension}";
        }
        while (!used.Add(candidate));

        return candidate;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }

    private static string Show(string value)
    {
        return string.IsNullOrEmpty(value) ? "(unknown)" : value;
    }

    private static string JsonString(string value)
    {
        if (value == null)
        {
            return "null";
        }

        var sb = new StringBuilder("\"");
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    private sealed class ContextScript
    {
        public ContextScript(string name, string contextType, string provider, string server, string database, string sql, IReadOnlyList<string> pending = null)
        {
            Name = name;
            ContextType = contextType;
            Provider = provider;
            Server = server;
            Database = database;
            Sql = sql;
            Pending = pending;
        }

        public string Name { get; }
        public string ContextType { get; }
        public string Provider { get; }
        public string Server { get; }
        public string Database { get; }
        public string Sql { get; }
        public IReadOnlyList<string> Pending { get; }
        public string FileName { get; set; }
        public string CombinedFileName { get; set; }
    }
}
