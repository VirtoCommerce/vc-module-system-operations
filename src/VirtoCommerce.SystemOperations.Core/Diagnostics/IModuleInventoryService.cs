using System.Collections.Generic;
using System.Reflection;

namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

// The installed modules, who maintains each one, and which module an assembly belongs to.
public interface IModuleInventoryService
{
    IList<ModuleDescription> GetModules(string filter = ModuleFilters.All);

    // The module whose folder holds the assembly, or null for the platform, the runtime and third-party libraries.
    ModuleDescription FindModuleByAssembly(string assemblyName);

    bool IsPlatformAssembly(AssemblyName assemblyName);
}
