using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SystemOperations.Core.Diagnostics;
using VirtoCommerce.SystemOperations.Data.Diagnostics;
using Xunit;

namespace VirtoCommerce.SystemOperations.Tests.Diagnostics;

// The support case is the one report that leaves the platform: what it contains, and that nothing in it is unmasked.
[Trait("Category", "Unit")]
public class SupportCaseTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    // Before the start of the test process, which the timeline adds as an event of its own.
    private static readonly DateTime Earlier = EnvironmentProfileService.GetInstance(Now).StartedAt.AddHours(-2);

    private readonly SensitiveDataRedactor _redactor = new();
    private readonly ErrorJournal _journal;
    private readonly StubJobs _jobs = new();

    public SupportCaseTests()
    {
        _journal = new ErrorJournal(_redactor, TimeProvider.System);
    }

    [Fact]
    public async Task BuildAsync_PutsEverySection_AndMasksWhatTheSectionsQuote()
    {
        // Arrange
        _journal.Record(Occurrence("Mail to {Email} failed", "Mail to jane@example.com failed"));
        _jobs.FailedJobs.Add(new FailedJob { JobId = "42", Job = "IndexJob.Run", ExceptionType = "TimeoutException", ExceptionMessage = "token=abc123 expired", FailedAt = Now.AddHours(-1) });
        var builder = CreateBuilder(new Finding { Id = "redis-missing", Severity = FindingSeverities.Warning, Title = "Redis is not configured", Evidence = "Password=hunter2;" });

        // Act
        var text = await builder.BuildAsync(new SupportCaseRequest { Symptom = "Checkout fails for bob@example.com", Hours = 24 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.StartsWith("# Support case", text);
        Assert.Contains("instance web-1", text);
        Assert.Contains("## Problem", text);
        Assert.Contains("Checkout fails for ***@***", text);
        Assert.Contains($"[{FindingSeverities.Warning}] Redis is not configured: Password=***;", text);
        Assert.Contains("Mail to ***@*** failed", text);
        Assert.Contains("IndexJob.Run (42): TimeoutException token=*** expired", text);
        Assert.Contains("job_failed", text);
        Assert.DoesNotContain("hunter2", text);
        Assert.DoesNotContain("abc123", text);
        Assert.DoesNotContain("example.com", text);
    }

    [Fact]
    public async Task BuildAsync_TakesTheErrorOfTheFingerprint_Only()
    {
        // Arrange
        _journal.Record(Occurrence("Payment {Id} declined", "Payment 7 declined"));
        _journal.Record(Occurrence("Index {Name} rebuild failed", "Index products rebuild failed"));
        var fingerprint = _journal.Search(new ErrorSearchCriteria { Keyword = "Payment" }).Single().Fingerprint;

        // Act
        var text = await CreateBuilder().BuildAsync(new SupportCaseRequest { Fingerprint = fingerprint }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains($"({fingerprint})", text);
        Assert.Contains("Payment 7 declined", text);
        Assert.DoesNotContain("Index products rebuild failed", text);
        Assert.Contains("(describe what users see)", text);
    }

    [Fact]
    public async Task BuildAsync_SaysSo_When_ThereAreNoErrorsOrJobs()
    {
        // Act
        var text = await CreateBuilder().BuildAsync(new SupportCaseRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("No errors were logged on this instance in the window.", text);
        Assert.Contains("## Failed background jobs\r\nNone in the window.".Replace("\r\n", Environment.NewLine), text);
        Assert.Contains("## Configuration findings" + Environment.NewLine + "None.", text);
    }

    [Fact]
    public void BuildAsync_RejectsAMissingRequest_BeforeItStarts()
    {
        var builder = CreateBuilder();

        // Thrown by the call, not by the returned task.
        Assert.Throws<ArgumentNullException>(() => { _ = builder.BuildAsync(null, TestContext.Current.CancellationToken); });
    }

    [Fact]
    public void GetTimeline_OrdersTheEvents_AndLeavesOutTheJobServerOfThisProcess()
    {
        // Arrange
        var instance = EnvironmentProfileService.GetInstance(Now);
        _jobs.Counts.Servers.Add(new JobServerInfo { Name = $"{instance.MachineName}:{instance.ProcessId}:a1", StartedAt = Earlier.AddMinutes(-50) });
        _jobs.Counts.Servers.Add(new JobServerInfo { Name = "web-2:7:b2", Host = "web-2", Workers = 4, StartedAt = Earlier.AddMinutes(-40) });
        _journal.Record(Occurrence("Timeout", "Timeout", Earlier.AddMinutes(-30)));
        _jobs.FailedJobs.Add(new FailedJob { JobId = "1", Job = "Export.Run", FailedAt = Earlier.AddMinutes(-20) });
        var timeline = new IncidentTimelineService(_journal, _jobs, TimeProvider.System);

        // Act
        var events = timeline.GetTimeline(Earlier.AddHours(-1), Earlier);

        // Assert
        Assert.Equal([TimelineEventKinds.InstanceStart, TimelineEventKinds.ErrorFirstSeen, TimelineEventKinds.JobFailed], events.Select(x => x.Kind));
        Assert.Equal("web-2:7:b2", events[0].Reference);
    }

    [Fact]
    public void GetTimeline_KeepsTheMostRecentEvents()
    {
        // Arrange
        for (var i = 1; i <= 5; i++)
        {
            _jobs.FailedJobs.Add(new FailedJob { JobId = $"{i}", Job = "Export.Run", FailedAt = Earlier.AddMinutes(-i) });
        }

        var timeline = new IncidentTimelineService(_journal, _jobs, TimeProvider.System);

        // Act
        var events = timeline.GetTimeline(Earlier.AddMinutes(-10), Earlier, take: 2);

        // Assert
        Assert.Equal(["2", "1"], events.Select(x => x.Reference));
    }

    private SupportCaseBuilder CreateBuilder(params Finding[] findings)
    {
        var profile = new EnvironmentProfile
        {
            PlatformVersion = "3.1095.0",
            Instance = new InstanceInfo { MachineName = "web-1" },
            License = new LicenseInfo { Status = "active" },
            Modules = new ModuleCounts(),
        };
        var health = new PlatformHealthReport { Status = "Healthy", Checks = [new HealthCheckEntry { Name = "database", Status = "Healthy", DurationMs = 3 }] };

        return new SupportCaseBuilder(
            new StubProfile(profile),
            new StubHealth(health),
            new StubConfiguration(findings),
            _journal,
            new StubExplainer(),
            _jobs,
            new IncidentTimelineService(_journal, _jobs, TimeProvider.System),
            _redactor,
            TimeProvider.System);
    }

    private static ErrorOccurrence Occurrence(string template, string message, DateTime? time = null)
    {
        return new ErrorOccurrence { Timestamp = time ?? Now.AddMinutes(-5), Level = "Error", MessageTemplate = template, Message = message, Source = "Tests" };
    }

    private sealed class StubJobs : IBackgroundJobDiagnosticsService
    {
        public BackgroundJobCounts Counts { get; } = new() { Available = true };

        public List<FailedJob> FailedJobs { get; } = [];

        public BackgroundJobCounts GetCounts()
        {
            return Counts;
        }

        public IList<FailedJob> GetFailedJobs(DateTime? since, int take)
        {
            return FailedJobs.Where(x => since == null || x.FailedAt >= since).OrderByDescending(x => x.FailedAt).Take(take).ToList();
        }
    }

    private sealed class StubProfile(EnvironmentProfile profile) : IEnvironmentProfileService
    {
        public Task<EnvironmentProfile> GetProfileAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(profile);
        }
    }

    private sealed class StubHealth(PlatformHealthReport report) : IPlatformHealthService
    {
        public Task<PlatformHealthReport> CheckAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(report);
        }
    }

    private sealed class StubConfiguration(IList<Finding> findings) : IConfigurationCheckService
    {
        public Task<IList<Finding>> CheckAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(findings);
        }
    }

    private sealed class StubExplainer : IErrorExplainer
    {
        public ErrorExplanation Explain(string exceptionType, string message, string stackTrace)
        {
            return new ErrorExplanation { LikelyCauseClass = CauseClasses.Configuration, Confidence = "low" };
        }
    }
}
