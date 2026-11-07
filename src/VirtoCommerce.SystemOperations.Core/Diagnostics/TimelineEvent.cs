using System;

namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

public class TimelineEvent
{
    public DateTime Time { get; set; }

    // TimelineEventKinds.
    public string Kind { get; set; }

    public string Title { get; set; }

    // The error fingerprint, job id or server name the event refers to.
    public string Reference { get; set; }
}

public static class TimelineEventKinds
{
    // An instance started: a restart, a scale-out or a deployment.
    public const string InstanceStart = "instance_start";
    public const string ErrorFirstSeen = "error_first_seen";
    public const string JobFailed = "job_failed";
}
