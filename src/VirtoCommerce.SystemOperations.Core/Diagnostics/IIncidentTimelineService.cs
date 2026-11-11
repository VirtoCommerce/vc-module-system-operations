using System;
using System.Collections.Generic;

namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

// What happened in a window, in time order: instance starts, new errors, failed jobs.
public interface IIncidentTimelineService
{
    IList<TimelineEvent> GetTimeline(DateTime since, DateTime? until = null, int take = 100);
}
