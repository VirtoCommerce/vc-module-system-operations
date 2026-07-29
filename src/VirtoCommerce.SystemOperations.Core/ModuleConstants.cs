namespace VirtoCommerce.SystemOperations.Core;

public static class ModuleConstants
{
    public static class Security
    {
        public static class Permissions
        {
            public const string Export = "systemoperations:migrations:export";

            public static readonly string[] AllPermissions = [Export];
        }
    }
}
