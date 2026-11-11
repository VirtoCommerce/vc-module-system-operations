using System;
using System.Collections.Generic;

namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

// The errors this platform instance logged since it started, grouped by fingerprint and kept in memory, so the
// diagnosis works without an external telemetry backend.
public interface IErrorJournal
{
    DateTime StartedAt { get; }

    void Record(ErrorOccurrence occurrence);

    // Groups most recent first, with counts limited to the window when Since is set.
    IList<ErrorGroup> Search(ErrorSearchCriteria criteria);

    ErrorGroup Get(string fingerprint);
}

public class ErrorOccurrence
{
    public DateTime Timestamp { get; set; }

    public string Level { get; set; }

    public string Source { get; set; }

    public string MessageTemplate { get; set; }

    public string Message { get; set; }

    public Exception Exception { get; set; }

    public string TraceId { get; set; }
}
