using System.Linq;
using VirtoCommerce.SystemOperations.Core.Diagnostics;
using VirtoCommerce.SystemOperations.Data.Diagnostics;
using Xunit;

namespace VirtoCommerce.SystemOperations.Tests.Diagnostics;

[Trait("Category", "Unit")]
public class CapacityEvaluationTests
{
    [Fact]
    public void Evaluate_RecommendsNothing_When_TheInstanceIsQuiet()
    {
        var report = Report();

        CapacityService.Evaluate(report);

        Assert.Equal(CapacityRecommendations.Ok, report.Recommendation);
        Assert.Empty(report.Findings);
    }

    [Fact]
    public void Evaluate_RecommendsScalingUp_When_MemoryIsCloseToTheLimit()
    {
        var report = Report();
        report.MemoryPercent = 92;

        CapacityService.Evaluate(report);

        Assert.Equal(CapacityRecommendations.ScaleUp, report.Recommendation);
        Assert.Equal("memory-high", report.Findings.Single().Id);
    }

    // Starvation is a code problem: more instances only delay it, so it wins over the CPU.
    [Fact]
    public void Evaluate_AsksToFixTheHotspot_When_TheThreadPoolStarves()
    {
        var report = Report();
        report.ThreadPoolQueueLength = 500;
        report.CpuPercent = 90;

        CapacityService.Evaluate(report);

        Assert.Equal(CapacityRecommendations.FixHotspot, report.Recommendation);
        Assert.Equal(CauseClasses.Customization, report.Findings.Single(x => x.Id == "thread-pool-starvation").CauseClass);
        Assert.Contains(report.Findings, x => x.Id == "cpu-high");
    }

    [Fact]
    public void Evaluate_FindsASlowDatabase_And_AJobBacklog()
    {
        var report = Report();
        report.Dependencies = [new HealthCheckEntry { Name = "SQL Server health", Status = HealthStatuses.Healthy, Tags = ["Database"], DurationMs = 1200 }];
        report.Jobs = new BackgroundJobCounts { Available = true, Workers = 10, Enqueued = 900, Servers = [new JobServerInfo()] };

        CapacityService.Evaluate(report);

        Assert.Equal(CapacityRecommendations.Investigate, report.Recommendation);
        Assert.Contains(report.Findings, x => x.Id == "database-slow");
        Assert.Contains(report.Findings, x => x.Id == "job-backlog");
    }

    private static CapacityReport Report()
    {
        return new CapacityReport
        {
            CpuPercent = 20,
            ProcessorCount = 4,
            MemoryPercent = 40,
            GcPausePercent = 1,
            ThreadPoolQueueLength = 0,
            Jobs = new BackgroundJobCounts { Available = true, Workers = 20, Enqueued = 3 },
        };
    }
}
