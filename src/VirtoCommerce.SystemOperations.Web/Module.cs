using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Platform.Core.Logger;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.SystemOperations.Core;
using VirtoCommerce.SystemOperations.Core.Diagnostics;
using VirtoCommerce.SystemOperations.Core.Migrations;
using VirtoCommerce.SystemOperations.Data.Diagnostics;
using VirtoCommerce.SystemOperations.Data.Migrations;
using VirtoCommerce.SystemOperations.MCP;

namespace VirtoCommerce.SystemOperations.Web
{
    public class Module : IModule
    {
        // The MCP module is an optional dependency: when it is installed, its assemblies are loaded and it initializes
        // before this module.
        private const string McpModuleAssemblyName = "VirtoCommerce.MCP.Web";

        public ManifestModuleInfo ModuleInfo { get; set; }

        public void Initialize(IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<IMigrationScriptExporter, MigrationScriptExporter>();
            serviceCollection.AddTransient<IMigrationStatusService, MigrationStatusService>();

            // Diagnostics of the platform. The error journal keeps the errors of this instance from its start, through a
            // Serilog sink the platform adds next to the configured ones.
            serviceCollection.AddSingleton(TimeProvider.System);
            serviceCollection.AddSingleton<ISensitiveDataRedactor, SensitiveDataRedactor>();
            serviceCollection.AddSingleton<IErrorJournal, ErrorJournal>();
            serviceCollection.AddSingleton<ErrorJournalSink>();
            serviceCollection.AddSingleton<ILoggerConfigurationService, ErrorJournalLoggerConfigurationService>();
            serviceCollection.AddSingleton<IModuleInventoryService, ModuleInventoryService>();
            serviceCollection.AddSingleton<IErrorExplainer, ErrorExplainer>();
            serviceCollection.AddTransient<IBackgroundJobDiagnosticsService, BackgroundJobDiagnosticsService>();
            serviceCollection.AddTransient<IEnvironmentProfileService, EnvironmentProfileService>();
            serviceCollection.AddTransient<IPlatformHealthService, PlatformHealthService>();
            serviceCollection.AddTransient<IConfigurationCheckService, ConfigurationCheckService>();
            serviceCollection.AddTransient<ICapacityService, CapacityService>();
            serviceCollection.AddTransient<IIncidentTimelineService, IncidentTimelineService>();
            serviceCollection.AddTransient<ISupportCaseBuilder, SupportCaseBuilder>();
            serviceCollection.AddTransient<IPlatformMaintenanceService, PlatformMaintenanceService>();

            if (IsMcpModuleLoaded())
            {
                AddMcp(serviceCollection);
            }
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

        private static bool IsMcpModuleLoaded()
        {
            return AppDomain.CurrentDomain.GetAssemblies().Any(x => string.Equals(x.GetName().Name, McpModuleAssemblyName, StringComparison.OrdinalIgnoreCase));
        }

        // A method of its own, so the MCP types are resolved only when the MCP module is installed.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AddMcp(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSystemOperationsMcp();
        }
    }
}
