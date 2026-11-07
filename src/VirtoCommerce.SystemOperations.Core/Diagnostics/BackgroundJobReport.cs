using System;
using System.Collections.Generic;

namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

public class BackgroundJobCounts
{
    // False when the platform runs without Hangfire storage, so nothing below is known.
    public bool Available { get; set; }

    public long Enqueued { get; set; }

    public long Scheduled { get; set; }

    public long Processing { get; set; }

    public long Failed { get; set; }

    public long Succeeded { get; set; }

    public int Workers { get; set; }

    // Platform processes whose job servers reported in the last two minutes: the known instances of the cluster.
    public int LiveInstances { get; set; }

    public IList<JobServerInfo> Servers { get; set; } = [];

    public IList<JobQueueInfo> Queues { get; set; } = [];
}

public class JobServerInfo
{
    public string Name { get; set; }

    public string Host { get; set; }

    public int Workers { get; set; }

    public IList<string> Queues { get; set; } = [];

    public DateTime? StartedAt { get; set; }

    public DateTime? Heartbeat { get; set; }
}

public class JobQueueInfo
{
    public string Name { get; set; }

    public long Length { get; set; }
}

public class FailedJob
{
    public string JobId { get; set; }

    // Type and method of the job.
    public string Job { get; set; }

    public string ExceptionType { get; set; }

    public string ExceptionMessage { get; set; }

    public DateTime? FailedAt { get; set; }
}
