using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using VirtoCommerce.SystemOperations.Core.Diagnostics;

namespace VirtoCommerce.SystemOperations.Data.Diagnostics;

// Keeps the errors of this instance in memory: at most MaxGroups fingerprints, the least recently seen dropped first,
// and the times of the last MaxOccurrences of each. Nothing is written to the database, so the journal costs no
// migration and no storage, and it starts empty on every start of the instance.
public class ErrorJournal(ISensitiveDataRedactor redactor, TimeProvider timeProvider) : IErrorJournal
{
    public const int MaxGroups = 500;
    public const int MaxOccurrences = 200;
    private const int MaxMessageLength = 1000;
    private const int MaxStackTraceLength = 6000;

    private readonly ConcurrentDictionary<string, ErrorGroup> _groups = new(StringComparer.Ordinal);

    public DateTime StartedAt { get; } = timeProvider.GetUtcNow().UtcDateTime;

    public virtual void Record(ErrorOccurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);

        var (fingerprint, exceptionType, topFrame) = Identify(occurrence);

        var group = _groups.GetOrAdd(fingerprint, key => new ErrorGroup
        {
            Fingerprint = key,
            Source = occurrence.Source,
            MessageTemplate = redactor.Redact(FirstLine(occurrence.MessageTemplate)).Truncate(MaxMessageLength),
            ExceptionType = exceptionType,
            TopFrame = topFrame,
            FirstSeen = occurrence.Timestamp,
            MachineName = Environment.MachineName,
        });

        lock (group)
        {
            group.Count++;
            group.Level = occurrence.Level;
            group.LastSeen = occurrence.Timestamp > group.LastSeen ? occurrence.Timestamp : group.LastSeen;
            // Some loggers write the whole exception into the message; the stack is kept apart, so the message is its
            // first line.
            group.LastMessage = redactor.Redact(FirstLine(occurrence.Message)).Truncate(MaxMessageLength);
            group.LastTraceId = occurrence.TraceId ?? group.LastTraceId;
            group.StackTrace = redactor.Redact(occurrence.Exception?.ToString()).Truncate(MaxStackTraceLength);

            group.RecentOccurrences.Add(occurrence.Timestamp);
            if (group.RecentOccurrences.Count > MaxOccurrences)
            {
                group.RecentOccurrences.RemoveAt(0);
            }
        }

        if (_groups.Count > MaxGroups)
        {
            Trim();
        }
    }

    public virtual IList<ErrorGroup> Search(ErrorSearchCriteria criteria)
    {
        criteria ??= new ErrorSearchCriteria();

        var groups = _groups.Values.Select(Snapshot);

        if (!string.IsNullOrEmpty(criteria.Fingerprint))
        {
            groups = groups.Where(x => x.Fingerprint == criteria.Fingerprint);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            var keyword = criteria.Keyword.Trim();
            groups = groups.Where(x => Contains(x.LastMessage, keyword) || Contains(x.MessageTemplate, keyword) ||
                Contains(x.Source, keyword) || Contains(x.ExceptionType, keyword) || Contains(x.TopFrame, keyword));
        }

        if (criteria.Since != null)
        {
            var since = criteria.Since.Value;
            groups = groups
                .Where(x => x.LastSeen >= since)
                .Select(x =>
                {
                    // The count of the window; a group that overflowed its kept times counts at least those.
                    x.Count = x.RecentOccurrences.Count(t => t >= since);
                    return x;
                });
        }

        return groups
            .OrderByDescending(x => x.LastSeen)
            .ThenByDescending(x => x.Count)
            .Take(Math.Max(1, criteria.Take))
            .ToList();
    }

    public virtual ErrorGroup Get(string fingerprint)
    {
        return fingerprint != null && _groups.TryGetValue(fingerprint, out var group) ? Snapshot(group) : null;
    }

    // An error with an exception is grouped by where it is thrown, as error trackers do: the same failure with other
    // input data, or logged by another caller with the exception text in its message, is one problem.
    private static (string Fingerprint, string ExceptionType, string TopFrame) Identify(ErrorOccurrence occurrence)
    {
        var exception = occurrence.Exception?.GetBaseException();
        var exceptionType = exception?.GetType().FullName;
        var topFrame = GetTopFrame(exception?.StackTrace);

        var fingerprint = topFrame != null
            ? GetFingerprint(exceptionType, null, topFrame)
            : GetFingerprint(exceptionType, FirstLine(occurrence.MessageTemplate ?? occurrence.Message), null);

        return (fingerprint, exceptionType, topFrame);
    }

    // "   at Namespace.Type.Method(Args) in C:\path\File.cs:line 12" -> "Namespace.Type.Method(Args)"
    public static string GetTopFrame(string stackTrace)
    {
        if (string.IsNullOrEmpty(stackTrace))
        {
            return null;
        }

        var line = stackTrace.Split('\n').Select(x => x.Trim()).FirstOrDefault(x => x.StartsWith("at ", StringComparison.Ordinal));
        if (line == null)
        {
            return null;
        }

        line = line[3..];
        var location = line.IndexOf(" in ", StringComparison.Ordinal);

        return location > 0 ? line[..location] : line;
    }

    public static string GetFingerprint(string exceptionType, string messageTemplate, string topFrame)
    {
        var text = $"{exceptionType}|{messageTemplate}|{topFrame}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));

        return Convert.ToHexStringLower(hash)[..12];
    }

    public static string FirstLine(string value)
    {
        if (value == null)
        {
            return null;
        }

        var end = value.IndexOfAny(['\r', '\n']);
        return end >= 0 ? value[..end] : value;
    }

    private static ErrorGroup Snapshot(ErrorGroup group)
    {
        lock (group)
        {
            return group.Clone();
        }
    }

    private static bool Contains(string value, string keyword)
    {
        return value?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;
    }

    private void Trim()
    {
        foreach (var stale in _groups.Values.OrderBy(x => x.LastSeen).Take(_groups.Count - MaxGroups).ToList())
        {
            _groups.TryRemove(stale.Fingerprint, out _);
        }
    }
}
