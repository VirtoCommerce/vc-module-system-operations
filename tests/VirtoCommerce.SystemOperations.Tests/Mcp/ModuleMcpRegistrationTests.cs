using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SystemOperations.Core.Diagnostics;
using Xunit;

namespace VirtoCommerce.SystemOperations.Tests.Mcp;

// The MCP contracts ship with the module, so they always load; the toolset is registered only when the MCP module, which
// serves it, is installed.
[Trait("Category", "Unit")]
public class ModuleMcpRegistrationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Initialize_RegistersTheToolset_OnlyWithTheMcpModule(bool mcpInstalled)
    {
        // Arrange
        var services = new ServiceCollection();
        var module = new Web.Module { ModuleService = new StubModuleService(mcpInstalled ? ["VirtoCommerce.MCP"] : []) };

        // Act
        module.Initialize(services);

        // Assert
        Assert.Equal(mcpInstalled, services.Any(x => x.ServiceType == typeof(McpServerTool)));
        Assert.Contains(services, x => x.ServiceType == typeof(IPlatformHealthService));
    }

    private sealed class StubModuleService(string[] installed) : IModuleService
    {
        public IList<ManifestModuleInfo> GetModules() => [];

        public IList<ManifestModuleInfo> GetInstalledModules() => [];

        public IList<ManifestModuleInfo> GetFailedModules() => [];

        public bool IsInstalled(string moduleId) => installed.Contains(moduleId, StringComparer.OrdinalIgnoreCase);

        public bool IsInstalled(string moduleId, string minVersion) => IsInstalled(moduleId);

        public ManifestModuleInfo GetModule(string moduleId) => null;
    }
}
