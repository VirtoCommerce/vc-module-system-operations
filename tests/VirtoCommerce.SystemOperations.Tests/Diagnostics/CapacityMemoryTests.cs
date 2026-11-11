using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SystemOperations.Core.Diagnostics;
using VirtoCommerce.SystemOperations.Data.Diagnostics;
using Xunit;

namespace VirtoCommerce.SystemOperations.Tests.Diagnostics;

// The memory of an instance is measured against the container's limit, and the managed heap against the GC heap limit,
// which in a container is 75% of the container's limit.
[Trait("Category", "Unit")]
public class CapacityMemoryTests
{
    private const long Gib = 1024L * 1024 * 1024;
    private const long Mib = 1024L * 1024;

    [Theory]
    [InlineData("1073741824\n", Gib)]
    [InlineData(" 536870912 ", 512 * Mib)]
    [InlineData("max\n", null)]
    [InlineData("9223372036854771712", null)] // cgroup v1 without a limit
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("-1", null)]
    [InlineData("abc", null)]
    public void Parse_ReadsTheLimit_OrNoneForNoLimit(string value, long? expected)
    {
        Assert.Equal(expected, ContainerMemoryLimit.Parse(value));
    }

    [Fact]
    public void Read_PrefersCgroupV2_AndFallsBackToV1()
    {
        var v2 = new Dictionary<string, string> { [ContainerMemoryLimit.CgroupV2File] = "1073741824", [ContainerMemoryLimit.CgroupV1File] = "536870912" };
        var v1 = new Dictionary<string, string> { [ContainerMemoryLimit.CgroupV2File] = "max", [ContainerMemoryLimit.CgroupV1File] = "536870912" };

        Assert.Equal(Gib, ContainerMemoryLimit.Read(x => v2.GetValueOrDefault(x)));
        Assert.Equal(512 * Mib, ContainerMemoryLimit.Read(x => v1.GetValueOrDefault(x)));
        Assert.Null(ContainerMemoryLimit.Read(_ => null));
    }

    // The healthy instance of the QA report: 774 MB working set in a 1 GiB container, 175 MB heap of a 768 MB heap limit.
    // Measured against the heap limit it read 100.8% and asked to scale up.
    [Fact]
    public void Evaluate_FindsNothing_ForAHealthyInstanceInA1GibContainer()
    {
        var report = Report(workingSet: 774 * Mib, memoryLimit: Gib, heap: 175 * Mib, heapLimit: 768 * Mib);

        CapacityService.Evaluate(report);

        Assert.Equal(75.6, report.MemoryPercent);
        Assert.Equal(22.8, report.ManagedHeapPercent);
        Assert.Equal(CapacityRecommendations.Ok, report.Recommendation);
        Assert.Empty(report.Findings);
    }

    [Fact]
    public void Evaluate_FindsTheHeapCloseToItsLimit()
    {
        var report = Report(workingSet: 800 * Mib, memoryLimit: Gib, heap: 700 * Mib, heapLimit: 768 * Mib);

        CapacityService.Evaluate(report);

        var finding = Assert.Single(report.Findings);
        Assert.Equal("heap-high", finding.Id);
        Assert.Equal(FindingSeverities.Critical, finding.Severity);
        Assert.Equal(CapacityRecommendations.ScaleUp, report.Recommendation);
    }

    [Fact]
    public void Evaluate_FindsTheWorkingSetCloseToTheContainerLimit()
    {
        var report = Report(workingSet: 950 * Mib, memoryLimit: Gib, heap: 300 * Mib, heapLimit: 768 * Mib);

        CapacityService.Evaluate(report);

        Assert.Equal("memory-high", Assert.Single(report.Findings).Id);
    }

    [Fact]
    public async Task MeasureAsync_MeasuresMemoryAgainstTheContainerLimit()
    {
        var service = new TestCapacityService(containerLimit: 64 * Gib);

        var report = await service.MeasureAsync(TimeSpan.Zero, TestContext.Current.CancellationToken);

        Assert.Equal(64 * Gib, report.MemoryLimitBytes);
        Assert.Equal(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes, report.ManagedHeapLimitBytes);
        Assert.Equal(Math.Round(report.WorkingSetBytes * 100.0 / (64 * Gib), 1), report.MemoryPercent);
    }

    [Fact]
    public async Task MeasureAsync_UsesTheGcFigure_WithoutAContainerLimit()
    {
        var service = new TestCapacityService(containerLimit: null);

        var report = await service.MeasureAsync(TimeSpan.Zero, TestContext.Current.CancellationToken);

        Assert.Equal(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes, report.MemoryLimitBytes);
    }

    private static CapacityReport Report(long workingSet, long memoryLimit, long heap, long heapLimit)
    {
        return new CapacityReport
        {
            CpuPercent = 2,
            ProcessorCount = 1,
            WorkingSetBytes = workingSet,
            MemoryLimitBytes = memoryLimit,
            MemoryPercent = Math.Round(workingSet * 100.0 / memoryLimit, 1),
            ManagedHeapBytes = heap,
            ManagedHeapLimitBytes = heapLimit,
            ManagedHeapPercent = Math.Round(heap * 100.0 / heapLimit, 1),
            Jobs = new BackgroundJobCounts { Available = true, Workers = 5 },
        };
    }

    private sealed class TestCapacityService(long? containerLimit)
        : CapacityService(new HealthyPlatform(), new NoJobs(), TimeProvider.System)
    {
        protected override long? GetContainerMemoryLimitBytes() => containerLimit;
    }

    private sealed class HealthyPlatform : IPlatformHealthService
    {
        public Task<PlatformHealthReport> CheckAsync(CancellationToken cancellationToken = default) => Task.FromResult(new PlatformHealthReport());
    }

    private sealed class NoJobs : IBackgroundJobDiagnosticsService
    {
        public BackgroundJobCounts GetCounts() => new() { Available = false };

        public IList<FailedJob> GetFailedJobs(DateTime? since, int take) => [];
    }
}
