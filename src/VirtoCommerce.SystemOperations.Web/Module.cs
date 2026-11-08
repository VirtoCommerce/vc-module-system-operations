using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    public class Module : IModule, IHasModuleService
    {
        // The MCP module is an optional dependency: when it is installed, it initializes before this module. Its contracts
        // (VirtoCommerce.MCP.Core and the MCP SDK) are packaged with this module (module.keep), so they load without it.
        private const string McpModuleId = "VirtoCommerce.MCP";

        public ManifestModuleInfo ModuleInfo { get; set; }

        public IModuleService ModuleService { get; set; }

        public void Initialize(IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<IMigrationScriptExporter, MigrationScriptExporter>();
            serviceCollection.AddTransient<IMigrationStatusService, MigrationStatusService>();

            // Diagnostics of the platform. The error journal keeps the errors of this instance from its start, through a
            // Serilog sink the platform adds next to the configured ones.
            serviceCollection.TryAddSingleton(TimeProvider.System);
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

            if (ModuleService.IsInstalled(McpModuleId))
            {
                serviceCollection.AddSystemOperationsMcp();
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
    }
}
