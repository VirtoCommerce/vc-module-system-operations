using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.SystemOperations.Data.Diagnostics;
using Xunit;

namespace VirtoCommerce.SystemOperations.Tests.Diagnostics;

[Trait("Category", "Unit")]
public class BackgroundJobDiagnosticsServiceTests
{
    // Hangfire comes with the Background Jobs module, not with the platform. The test host has no Hangfire assembly,
    // as a platform without that module has none.
    [Fact]
    public void WithoutHangfire_JobsAreUnavailable()
    {
        Assert.Null(Type.GetType("Hangfire.JobStorage, Hangfire.Core", throwOnError: false));

        var service = new BackgroundJobDiagnosticsService(
            new ServiceCollection().BuildServiceProvider(),
            new SensitiveDataRedactor(),
            TimeProvider.System,
            NullLogger<BackgroundJobDiagnosticsService>.Instance);

        Assert.False(service.GetCounts().Available);
        Assert.Empty(service.GetFailedJobs(since: null, take: 10));
    }
}
