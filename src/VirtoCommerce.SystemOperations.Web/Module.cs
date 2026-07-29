using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.SystemOperations.Core;
using VirtoCommerce.SystemOperations.Core.Migrations;
using VirtoCommerce.SystemOperations.Data.Migrations;

namespace VirtoCommerce.SystemOperations.Web
{
    public class Module : IModule
    {
        public ManifestModuleInfo ModuleInfo { get; set; }

        public void Initialize(IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<IMigrationScriptExporter, MigrationScriptExporter>();
        }

        public void PostInitialize(IApplicationBuilder appBuilder)
        {
            var permissionsRegistrar = appBuilder.ApplicationServices.GetRequiredService<IPermissionsRegistrar>();
            permissionsRegistrar.RegisterPermissions(ModuleInfo.Id, "System Operations", ModuleConstants.Security.Permissions.AllPermissions);
        }

        public void Uninstall()
        {
            // Nothing to do.
        }
    }
}
