using System;
using System.Collections.Generic;

namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

public class PlatformHealthReport
{
    // HealthStatuses: the worst status of the checks.
    public string Status { get; set; }

    public string MachineName { get; set; }

    public DateTime CheckedAt { get; set; }

    public TimeSpan Duration { get; set; }

    public IList<HealthCheckEntry> Checks { get; set; } = [];

    public IList<Finding> Findings { get; set; } = [];
}

public class HealthCheckEntry
{
    public string Name { get; set; }

    public string Status { get; set; }

    public IList<string> Tags { get; set; } = [];

    public double DurationMs { get; set; }

    public string Description { get; set; }

    public string Error { get; set; }
}
