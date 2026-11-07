using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
    private const int FingerprintLength = 12;
    // How much more than the kept length is redacted, so a secret across the cut is still recognized.
    private const int RedactedLengthFactor = 2;

    private readonly ConcurrentDictionary<string, ErrorGroup> _groups = new(StringComparer.Ordinal);
    // ConcurrentDictionary.Count takes every lock of the dictionary, too much for each logged error.
    private int _groupCount;

    public DateTime StartedAt { get; } = timeProvider.GetUtcNow().UtcDateTime;

    public virtual void Record(ErrorOccurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);

        var (fingerprint, exceptionType, topFrame) = Identify(occurrence);
        var group = GetOrAddGroup(fingerprint, occurrence, exceptionType, topFrame);

        // Redacted outside the lock: the same error logged by many threads at once waits for the lock only to store it.
        // Some loggers write the whole exception into the message; the stack is kept apart, so the message is its first
        // line.
        var message = Redact(FirstLine(occurrence.Message), MaxMessageLength);
        var stackTrace = Redact(occurrence.Exception?.ToString(), MaxStackTraceLength);

        lock (group)
        {
            group.Count++;
            group.Level = occurrence.Level;
            group.LastSeen = occurrence.Timestamp > group.LastSeen ? occurrence.Timestamp : group.LastSeen;
            group.LastMessage = message;
            group.LastTraceId = occurrence.TraceId ?? group.LastTraceId;
            group.StackTrace = stackTrace;

            group.RecentOccurrences.Add(occurrence.Timestamp);
            if (group.RecentOccurrences.Count > MaxOccurrences)
            {
                group.RecentOccurrences.RemoveAt(0);
            }
        }

        if (Volatile.Read(ref _groupCount) > MaxGroups)
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
            groups = groups.Where(x => Matches(x, keyword));
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

    private ErrorGroup GetOrAddGroup(string fingerprint, ErrorOccurrence occurrence, string exceptionType, string topFrame)
    {
        if (_groups.TryGetValue(fingerprint, out var group))
        {
            return group;
        }

        var created = new ErrorGroup
        {
            Fingerprint = fingerprint,
            Source = occurrence.Source,
            MessageTemplate = Redact(FirstLine(occurrence.MessageTemplate), MaxMessageLength),
            ExceptionType = exceptionType,
            TopFrame = topFrame,
            FirstSeen = occurrence.Timestamp,
            MachineName = Environment.MachineName,
        };

        group = _groups.GetOrAdd(fingerprint, created);
        if (ReferenceEquals(group, created))
        {
            Interlocked.Increment(ref _groupCount);
        }

        return group;
    }

    // The text is cut before it is redacted, so a long exception costs no more than a short one, but with a margin: a
    // secret cut in half at the end may not be recognized, and the margin is cut off after the redaction.
    private string Redact(string value, int maxLength)
    {
        return redactor.Redact(value.Truncate(maxLength * RedactedLengthFactor)).Truncate(maxLength);
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
        return ErrorExplainer.ParseFrames(stackTrace).FirstOrDefault();
    }

    public static string GetFingerprint(string exceptionType, string messageTemplate, string topFrame)
    {
        var text = $"{exceptionType}|{messageTemplate}|{topFrame}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));

        return Convert.ToHexStringLower(hash)[..FingerprintLength];
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

    private static bool Matches(ErrorGroup group, string keyword)
    {
        return new[] { group.LastMessage, group.MessageTemplate, group.Source, group.ExceptionType, group.TopFrame }
            .Any(x => Contains(x, keyword));
    }

    private static bool Contains(string value, string keyword)
    {
        return value?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;
    }

    private void Trim()
    {
        foreach (var stale in _groups.Values.OrderBy(x => x.LastSeen).Take(_groups.Count - MaxGroups).ToList())
        {
            if (_groups.TryRemove(stale.Fingerprint, out _))
            {
                Interlocked.Decrement(ref _groupCount);
            }
        }
    }
}
