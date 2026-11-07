using System;
using VirtoCommerce.SystemOperations.Core.Diagnostics;
using VirtoCommerce.SystemOperations.Data.Diagnostics;
using Xunit;

namespace VirtoCommerce.SystemOperations.Tests.Diagnostics;

[Trait("Category", "Unit")]
public class InstanceCountTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    // Two platforms on one machine are two instances; a server that stopped reporting is not counted.
    [Fact]
    public void CountLiveInstances_CountsProcesses_WithARecentHeartbeat()
    {
        var jobs = new BackgroundJobCounts
        {
            Servers =
            [
                Server("oz:100:a", Now.AddSeconds(-20)),
                Server("oz:100:b", Now.AddSeconds(-10)),
                Server("oz:200:c", Now.AddSeconds(-40)),
                Server("oz:300:d", Now.AddMinutes(-3)),
                Server("web-2:7:e", Now),
            ],
        };

        Assert.Equal(3, EnvironmentProfileService.CountLiveInstances(jobs, Now));
    }

    [Theory]
    [InlineData("oz:43552:c1559f68", "oz:43552", "oz")]
    [InlineData("custom-name", "custom-name", "custom-name")]
    public void ServerNames_AreReadAsHostAndProcess(string name, string instance, string host)
    {
        Assert.Equal(instance, BackgroundJobDiagnosticsService.GetInstanceKey(name));
        Assert.Equal(host, BackgroundJobDiagnosticsService.GetHost(name));
    }

    [Fact]
    public void GetOrigin_TellsVirtoModulesFromTheSolutionsOwn()
    {
        Assert.Equal(ModuleOrigins.Virto, ModuleInventoryService.GetOrigin(Module("VirtoCommerce.Orders", "Virto Commerce")));
        Assert.Equal(ModuleOrigins.Virto, ModuleInventoryService.GetOrigin(Module("VirtoCommerce.Catalog", "VirtoCommerce")));
        Assert.Equal(ModuleOrigins.Own, ModuleInventoryService.GetOrigin(Module("Contoso.Erp", "Contoso")));
        Assert.Equal(ModuleOrigins.Own, ModuleInventoryService.GetOrigin(Module("VirtoCommerce.ContosoErp", "Contoso")));
        Assert.Equal(ModuleOrigins.Own, ModuleInventoryService.GetOrigin(Module("Contoso.Orders", "Virto Commerce")));
    }

    private static JobServerInfo Server(string name, DateTime heartbeat)
    {
        return new JobServerInfo { Name = name, Host = BackgroundJobDiagnosticsService.GetHost(name), Heartbeat = heartbeat };
    }

    private static VirtoCommerce.Platform.Core.Modularity.ManifestModuleInfo Module(string id, string owner)
    {
        return new VirtoCommerce.Platform.Core.Modularity.ManifestModuleInfo().LoadFromManifest(new VirtoCommerce.Platform.Core.Modularity.ModuleManifest
        {
            Id = id,
            Version = "1.0.0",
            PlatformVersion = "3.1095.0",
            Owners = [owner],
            Authors = [owner],
        });
    }
}
