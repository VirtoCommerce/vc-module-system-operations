using System.Collections.Generic;

namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

public class ModuleDescription
{
    public string Id { get; set; }

    public string Version { get; set; }

    public string Title { get; set; }

    // ModuleOrigins: who maintains the module.
    public string Origin { get; set; }

    public bool Failed { get; set; }

    public IList<string> Errors { get; set; } = [];

    public IList<string> Owners { get; set; } = [];

    public IList<string> Dependencies { get; set; } = [];
}

public static class ModuleFilters
{
    public const string All = "all";
    public const string Own = "own";
    public const string Virto = "virto";
    public const string Failed = "failed";
}
