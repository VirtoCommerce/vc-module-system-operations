namespace VirtoCommerce.SystemOperations.Core;

public static class ModuleConstants
{
    public static class Security
    {
        public static class Permissions
        {
            public const string Export = "systemoperations:migrations:export";

            // Read the diagnostics of the platform: environment, health, errors, configuration, capacity, jobs.
            public const string DiagnosticsRead = "systemoperations:diagnostics:read";

            public static readonly string[] AllPermissions = [Export, DiagnosticsRead];
        }
    }
}
