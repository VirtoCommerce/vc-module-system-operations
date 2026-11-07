using System;
using System.Collections.Generic;

namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

// Resource use of this instance against its limits, and of the background job workers of the cluster.
public class CapacityReport
{
    public string MachineName { get; set; }

    public DateTime MeasuredAt { get; set; }

    public double SampleSeconds { get; set; }

    // CPU used by the process over the sample, in percent of the processors it may use.
    public double CpuPercent { get; set; }

    public int ProcessorCount { get; set; }

    public long WorkingSetBytes { get; set; }

    public long ManagedHeapBytes { get; set; }

    public long MemoryLimitBytes { get; set; }

    public double MemoryPercent { get; set; }

    // Time spent in garbage collection pauses since the process started, in percent of its lifetime.
    public double GcPausePercent { get; set; }

    public int Gen2Collections { get; set; }

    public int ThreadPoolThreads { get; set; }

    public long ThreadPoolQueueLength { get; set; }

    public long LockContentionCount { get; set; }

    public BackgroundJobCounts Jobs { get; set; }

    public IList<HealthCheckEntry> Dependencies { get; set; } = [];

    // CapacityRecommendations: what to do about the capacity.
    public string Recommendation { get; set; }

    public IList<Finding> Findings { get; set; } = [];
}

public static class CapacityRecommendations
{
    public const string Ok = "ok";
    public const string ScaleUp = "scale_up";
    public const string ScaleOut = "scale_out";
    public const string FixHotspot = "fix_hotspot";
    public const string Investigate = "investigate";
}
