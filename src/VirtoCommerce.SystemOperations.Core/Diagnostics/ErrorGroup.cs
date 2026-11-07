using System;
using System.Collections.Generic;

namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

// Errors with the same fingerprint (exception type, message template and top frame) counted as one problem.
public class ErrorGroup
{
    public string Fingerprint { get; set; }

    public string Level { get; set; }

    public string Source { get; set; }

    public string MessageTemplate { get; set; }

    // The last message as it was written, with personal data and secrets removed.
    public string LastMessage { get; set; }

    public string ExceptionType { get; set; }

    public string TopFrame { get; set; }

    // The last stack trace, with personal data and secrets removed, at most a few kilobytes.
    public string StackTrace { get; set; }

    public long Count { get; set; }

    public DateTime FirstSeen { get; set; }

    public DateTime LastSeen { get; set; }

    public string LastTraceId { get; set; }

    public string MachineName { get; set; }

    // The occurrences the journal keeps a time for, the latest last, to count them in a window.
    public IList<DateTime> RecentOccurrences { get; set; } = [];

    public ErrorGroup Clone()
    {
        var clone = (ErrorGroup)MemberwiseClone();
        clone.RecentOccurrences = [.. RecentOccurrences];
        return clone;
    }
}

public class ErrorSearchCriteria
{
    public DateTime? Since { get; set; }

    // Text in the message, the source, the exception type or the top frame.
    public string Keyword { get; set; }

    public string Fingerprint { get; set; }

    public int Take { get; set; } = 20;
}
