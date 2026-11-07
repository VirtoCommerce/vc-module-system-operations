using System;
using System.Collections.Generic;

namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

// Read-only view of the background job engine (Hangfire): counts, servers, queues and failed jobs.
public interface IBackgroundJobDiagnosticsService
{
    BackgroundJobCounts GetCounts();

    // Failed jobs, most recent first.
    IList<FailedJob> GetFailedJobs(DateTime? since, int take);
}
