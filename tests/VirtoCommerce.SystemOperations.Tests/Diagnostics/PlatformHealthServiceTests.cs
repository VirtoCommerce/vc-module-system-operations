using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VirtoCommerce.SystemOperations.Core.Diagnostics;
using VirtoCommerce.SystemOperations.Data.Diagnostics;
using Xunit;

namespace VirtoCommerce.SystemOperations.Tests.Diagnostics;

[Trait("Category", "Unit")]
public class PlatformHealthServiceTests
{
    [Fact]
    public async Task CheckAsync_TurnsFailedChecksIntoFindings_WithMaskedErrors()
    {
        // Arrange
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddHealthChecks()
            .AddCheck("database", () => HealthCheckResult.Healthy())
            .AddCheck("cache", () => HealthCheckResult.Unhealthy("Redis is down", new InvalidOperationException("No connection to cache:6380,password=hunter2")))
            .AddCheck("modules", () => HealthCheckResult.Degraded("One module failed"), tags: ["Modules"])
            .Services
            .BuildServiceProvider();
        var service = new PlatformHealthService(provider.GetRequiredService<HealthCheckService>(), new SensitiveDataRedactor(), TimeProvider.System);

        // Act
        var report = await service.CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatuses.Unhealthy, report.Status);
        Assert.Equal(["cache", "database", "modules"], report.Checks.Select(x => x.Name));
        Assert.Equal("No connection to cache:6380,password=***", report.Checks[0].Error);

        var cache = Assert.Single(report.Findings, x => x.Id == "health:cache");
        Assert.Equal(FindingSeverities.Critical, cache.Severity);
        Assert.Equal(CauseClasses.Infrastructure, cache.CauseClass);
        Assert.DoesNotContain("hunter2", cache.Evidence);

        var modules = Assert.Single(report.Findings, x => x.Id == "health:modules");
        Assert.Equal(FindingSeverities.Warning, modules.Severity);
        Assert.Equal(CauseClasses.Configuration, modules.CauseClass);
    }
}
