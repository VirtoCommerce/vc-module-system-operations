using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SystemOperations.Core.Diagnostics;

namespace VirtoCommerce.SystemOperations.Data.Diagnostics;

// Puts together what Virto support asks for first: versions, own modules, configuration findings, health, the errors
// with their trace ids and where they are thrown, failed jobs and the timeline. Every value passes the redactor, and
// nothing is sent: the person reads the text and sends it.
public class SupportCaseBuilder(
    IEnvironmentProfileService profileService,
    IPlatformHealthService healthService,
    IConfigurationCheckService configurationCheckService,
    IErrorJournal errorJournal,
    IErrorExplainer errorExplainer,
    IBackgroundJobDiagnosticsService jobDiagnostics,
    IIncidentTimelineService timelineService,
    ISensitiveDataRedactor redactor,
    TimeProvider timeProvider)
    : ISupportCaseBuilder
{
    private const int MaxErrors = 5;
    private const int MaxFailedJobs = 10;
    private const int MaxTimelineEvents = 30;

    public virtual async Task<string> BuildAsync(SupportCaseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var since = now.AddHours(-Math.Clamp(request.Hours, 1, 24 * 14));
        var profile = await profileService.GetProfileAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("# Support case");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Prepared {now:yyyy-MM-dd HH:mm} UTC on instance {profile.Instance.MachineName}, covering the last {(now - since).TotalHours:0} hours. Review it before sending: secrets, emails and card numbers are replaced with ***, but the text may still name your customers or products.");
        sb.AppendLine();

        AppendProblem(sb, request);
        AppendEnvironment(sb, profile);
        AppendHealth(sb, await healthService.CheckAsync(cancellationToken));
        AppendFindings(sb, await configurationCheckService.CheckAsync(cancellationToken));
        AppendErrors(sb, GetErrors(request, since));
        AppendFailedJobs(sb, jobDiagnostics.GetFailedJobs(since, MaxFailedJobs));
        AppendTimeline(sb, timelineService.GetTimeline(since, now, MaxTimelineEvents));

        return redactor.Redact(sb.ToString());
    }

    // The error the case is about, or the most frequent errors of the window.
    protected virtual IList<ErrorGroup> GetErrors(SupportCaseRequest request, DateTime since)
    {
        return string.IsNullOrEmpty(request.Fingerprint)
            ? errorJournal.Search(new ErrorSearchCriteria { Since = since, Take = 50 }).OrderByDescending(x => x.Count).Take(MaxErrors).ToList()
            : errorJournal.Search(new ErrorSearchCriteria { Fingerprint = request.Fingerprint, Take = 1 });
    }

    private void AppendProblem(StringBuilder sb, SupportCaseRequest request)
    {
        sb.AppendLine("## Problem");
        sb.AppendLine(Text(request.Symptom, "(describe what users see)"));
        sb.AppendLine();
        sb.AppendLine("**Steps to reproduce**");
        sb.AppendLine(Text(request.StepsToReproduce, "(not given)"));
        sb.AppendLine();
        sb.AppendLine("**What was tried**");
        sb.AppendLine(Text(request.WhatWasTried, "(not given)"));
        sb.AppendLine();
    }

    private static void AppendEnvironment(StringBuilder sb, EnvironmentProfile profile)
    {
        sb.AppendLine("## Environment");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Platform {profile.PlatformVersion}, .NET {profile.DotNetVersion}, {profile.OperatingSystem} ({profile.RuntimeIdentifier}), environment {profile.EnvironmentName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Database {profile.DatabaseProvider}; search {profile.SearchProvider}; assets {profile.AssetsProvider}; background jobs {profile.BackgroundJobsProvider} ({profile.HangfireStorage}); Redis {(profile.RedisConfigured ? "configured" : "not configured")}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Instances known: {profile.KnownInstanceCount?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}; this one has {profile.Instance.ProcessorCount} processors, {profile.Instance.MemoryLimitBytes / 1024 / 1024} MB memory limit, up {profile.Instance.Uptime:d\\.hh\\:mm}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Modules: {profile.Modules.Installed} installed, {profile.Modules.Virto} by Virto, {profile.Modules.Own} own, {profile.Modules.Failed} failed");
        if (profile.Modules.OwnModuleIds.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Own modules (customizations): {string.Join(", ", profile.Modules.OwnModuleIds)}");
        }

        if (profile.Modules.FailedModuleIds.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Failed modules: {string.Join(", ", profile.Modules.FailedModuleIds)}");
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"- License: {profile.License?.Status}");
        sb.AppendLine();
    }

    private static void AppendHealth(StringBuilder sb, PlatformHealthReport health)
    {
        sb.AppendLine("## Health");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Overall: {health.Status}.");
        foreach (var check in health.Checks)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- {check.Name}: {check.Status}, {check.DurationMs} ms{Suffix(check.Error)}");
        }

        sb.AppendLine();
    }

    private void AppendFindings(StringBuilder sb, IList<Finding> findings)
    {
        sb.AppendLine("## Configuration findings");
        if (findings.Count == 0)
        {
            sb.AppendLine("None.");
        }

        foreach (var finding in findings)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- [{finding.Severity}] {finding.Title}: {redactor.Redact(finding.Evidence)}");
        }

        sb.AppendLine();
    }

    private void AppendErrors(StringBuilder sb, IList<ErrorGroup> errors)
    {
        sb.AppendLine("## Errors");
        if (errors.Count == 0)
        {
            sb.AppendLine("No errors were logged on this instance in the window.");
        }

        foreach (var error in errors)
        {
            var explanation = errorExplainer.Explain(error.ExceptionType, error.LastMessage, error.StackTrace);
            sb.AppendLine(CultureInfo.InvariantCulture, $"### {error.ExceptionType ?? error.Level} ({error.Fingerprint})");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- {error.Count} time(s), first {error.FirstSeen:yyyy-MM-dd HH:mm:ss}, last {error.LastSeen:yyyy-MM-dd HH:mm:ss} UTC; last trace id {error.LastTraceId ?? "none"}; source {error.Source}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Message: {error.LastMessage}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Thrown in: {Describe(explanation.FirstOwnedFrame)}; own code on the stack: {(explanation.OwnCodeOnStack ? "yes" : "no")}; likely cause: {explanation.LikelyCauseClass} ({explanation.Confidence})");
            if (!string.IsNullOrEmpty(error.StackTrace))
            {
                sb.AppendLine("```");
                sb.AppendLine(error.StackTrace);
                sb.AppendLine("```");
            }
        }

        sb.AppendLine();
    }

    private static void AppendFailedJobs(StringBuilder sb, IList<FailedJob> failedJobs)
    {
        sb.AppendLine("## Failed background jobs");
        if (failedJobs.Count == 0)
        {
            sb.AppendLine("None in the window.");
        }

        foreach (var job in failedJobs)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- {job.FailedAt:yyyy-MM-dd HH:mm:ss} {job.Job} ({job.JobId}): {job.ExceptionType} {job.ExceptionMessage}");
        }

        sb.AppendLine();
    }

    private static void AppendTimeline(StringBuilder sb, IList<TimelineEvent> timeline)
    {
        sb.AppendLine("## Timeline (UTC)");
        foreach (var item in timeline)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- {item.Time:yyyy-MM-dd HH:mm:ss} {item.Kind}: {item.Title}");
        }
    }

    private string Text(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : redactor.Redact(value.Trim());
    }

    private static string Suffix(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : $" ({value})";
    }

    private static string Describe(StackFrameOwner frame)
    {
        if (frame == null)
        {
            return "unknown";
        }

        return frame.ModuleId != null ? $"{frame.ModuleId} {frame.ModuleVersion} ({frame.Owner}), {frame.Method}" : $"{frame.Owner}, {frame.Method}";
    }
}
