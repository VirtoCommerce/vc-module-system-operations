using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SystemOperations.Core.Diagnostics;

namespace VirtoCommerce.SystemOperations.Data.Diagnostics;

public class ModuleInventoryService(IModuleService moduleService, ISensitiveDataRedactor redactor) : IModuleInventoryService
{
    private const string VirtoModulePrefix = "VirtoCommerce.";
    private const string PlatformAssemblyPrefix = "VirtoCommerce.Platform.";

    private readonly Lazy<Dictionary<string, ManifestModuleInfo>> _modulesByAssembly = new(() => MapAssemblies(moduleService.GetModules()));

    public virtual IList<ModuleDescription> GetModules(string filter = ModuleFilters.All)
    {
        var failedIds = moduleService.GetFailedModules().Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var modules = moduleService.GetModules()
            .Where(x => x.IsInstalled || failedIds.Contains(x.Id))
            .Select(x => Describe(x, failedIds.Contains(x.Id)));

        modules = filter?.ToLowerInvariant() switch
        {
            ModuleFilters.Own => modules.Where(x => x.Origin == ModuleOrigins.Own),
            ModuleFilters.Virto => modules.Where(x => x.Origin == ModuleOrigins.Virto),
            ModuleFilters.Failed => modules.Where(x => x.Failed),
            _ => modules,
        };

        return modules.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public virtual ModuleDescription FindModuleByAssembly(string assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName) || !_modulesByAssembly.Value.TryGetValue(assemblyName, out var module))
        {
            return null;
        }

        return Describe(module, module.Errors.Count > 0);
    }

    public virtual bool IsPlatformAssembly(AssemblyName assemblyName)
    {
        return assemblyName?.Name?.StartsWith(PlatformAssemblyPrefix, StringComparison.OrdinalIgnoreCase) == true;
    }

    // Virto's modules are named VirtoCommerce.* and list Virto Commerce among their owners or authors. The owners field
    // alone is not enough: both "Virto Commerce" and "VirtoCommerce" occur, and a partner may copy a manifest.
    public static string GetOrigin(ManifestModuleInfo module)
    {
        var isVirtoId = module.Id?.StartsWith(VirtoModulePrefix, StringComparison.OrdinalIgnoreCase) == true;
        var names = (module.Owners ?? []).Concat(module.Authors ?? []);
        var isVirtoOwner = names.Any(x => x?.Replace(" ", string.Empty).Contains("VirtoCommerce", StringComparison.OrdinalIgnoreCase) == true);

        return isVirtoId && isVirtoOwner ? ModuleOrigins.Virto : ModuleOrigins.Own;
    }

    protected virtual ModuleDescription Describe(ManifestModuleInfo module, bool failed)
    {
        return new ModuleDescription
        {
            Id = module.Id,
            Version = module.Version?.ToString(),
            Title = module.Title,
            Origin = GetOrigin(module),
            Failed = failed,
            // A module that fails to load may quote its configuration in the error.
            Errors = module.Errors.Select(redactor.Redact).ToList(),
            Owners = [.. module.Owners ?? []],
            Dependencies = module.Dependencies.Select(x => $"{x.Id} {x.Version}{(x.Optional ? " (optional)" : string.Empty)}").ToList(),
        };
    }

    // An assembly belongs to the module whose folder holds it. A library several modules ship belongs to the module
    // whose own assemblies share its name prefix, or to none: it is a library.
    private static Dictionary<string, ManifestModuleInfo> MapAssemblies(IEnumerable<ManifestModuleInfo> modules)
    {
        var candidates = new Dictionary<string, List<ManifestModuleInfo>>(StringComparer.OrdinalIgnoreCase);

        foreach (var module in modules)
        {
            foreach (var name in GetAssemblyNames(module))
            {
                if (!candidates.TryGetValue(name, out var list))
                {
                    candidates[name] = list = [];
                }

                list.Add(module);
            }
        }

        var result = new Dictionary<string, ManifestModuleInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, list) in candidates)
        {
            var owner = list.Count == 1 ? list[0] : list.FirstOrDefault(x => name.StartsWith(GetAssemblyPrefix(x), StringComparison.OrdinalIgnoreCase));
            if (owner != null)
            {
                result[name] = owner;
            }
        }

        return result;
    }

    private static IEnumerable<string> GetAssemblyNames(ManifestModuleInfo module)
    {
        var names = new List<string>();

        if (module.Assembly != null)
        {
            names.Add(module.Assembly.GetName().Name);
        }

        if (!string.IsNullOrEmpty(module.FullPhysicalPath))
        {
            var bin = Path.Combine(module.FullPhysicalPath, "bin");
            try
            {
                if (Directory.Exists(bin))
                {
                    names.AddRange(Directory.EnumerateFiles(bin, "*.dll").Select(Path.GetFileNameWithoutExtension));
                }
            }
            catch (IOException)
            {
                // A folder that cannot be read maps no assemblies.
            }
            catch (UnauthorizedAccessException)
            {
                // Same.
            }
        }

        return names.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    // VirtoCommerce.OrdersModule.Web.dll -> "VirtoCommerce.OrdersModule."
    private static string GetAssemblyPrefix(ManifestModuleInfo module)
    {
        var name = Path.GetFileNameWithoutExtension(module.AssemblyFile ?? module.Id ?? string.Empty);
        var lastDot = name.LastIndexOf('.');

        return lastDot > 0 ? name[..(lastDot + 1)] : name + ".";
    }
}
