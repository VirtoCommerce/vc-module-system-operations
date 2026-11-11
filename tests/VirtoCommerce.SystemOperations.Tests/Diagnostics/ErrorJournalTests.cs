using System;
using System.Linq;
using System.Threading.Tasks;
using Serilog.Events;
using Serilog.Parsing;
using VirtoCommerce.SystemOperations.Core.Diagnostics;
using VirtoCommerce.SystemOperations.Data.Diagnostics;
using Xunit;

namespace VirtoCommerce.SystemOperations.Tests.Diagnostics;

[Trait("Category", "Unit")]
public class ErrorJournalTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    private readonly ErrorJournal _journal = new(new SensitiveDataRedactor(), new FixedTimeProvider(Now));

    [Fact]
    public void Record_GroupsErrorsByTheirExceptionAndFrame_OrByTheirTemplateWithoutAnException()
    {
        // Arrange
        var exception = Throw(() => throw new InvalidOperationException("Order 1 has no lines"));

        // Act
        _journal.Record(Occurrence(Now.AddMinutes(-3), "Order {OrderId} has no lines", "Order 1 has no lines", exception));
        _journal.Record(Occurrence(Now.AddMinutes(-1), "Order {OrderId} has no lines", "Order 2 has no lines", exception));
        _journal.Record(Occurrence(Now.AddMinutes(-1), "Payment {Id} declined", "Payment 7 declined"));
        _journal.Record(Occurrence(Now, "Payment {Id} declined", "Payment 8 declined"));
        _journal.Record(Occurrence(Now, "Refund {Id} failed", "Refund 9 failed"));

        // Assert
        var groups = _journal.Search(new ErrorSearchCriteria());
        Assert.Equal(3, groups.Count);
        var orders = groups.Single(x => x.ExceptionType != null);
        Assert.Equal(2, orders.Count);
        Assert.Equal(Now.AddMinutes(-3), orders.FirstSeen);
        Assert.Equal("Order 2 has no lines", orders.LastMessage);
        Assert.Equal(typeof(InvalidOperationException).FullName, orders.ExceptionType);
        Assert.Contains(nameof(ErrorJournalTests), orders.TopFrame);
        Assert.Equal(2, groups.Single(x => x.MessageTemplate == "Payment {Id} declined").Count);
    }

    // A logger that writes the exception into its message (the platform's API error middleware does) still groups by the
    // place the error is thrown, and the message keeps its first line only.
    [Fact]
    public void Record_GroupsByTheThrowingFrame_When_TheMessageCarriesTheInputAndTheStack()
    {
        // Arrange
        var first = Throw(() => Fail("/assets/a.zip"));
        var second = Throw(() => Fail("/assets/b.zip"));

        // Act
        _journal.Record(Occurrence(Now, $"Request failed: {first}", $"Request failed: {first}", first));
        _journal.Record(Occurrence(Now, $"Request failed: {second}", $"Request failed: {second}", second));

        // Assert
        var group = Assert.Single(_journal.Search(new ErrorSearchCriteria()));
        Assert.Equal(2, group.Count);
        Assert.Equal("Request failed: System.InvalidOperationException: Invalid path /assets/b.zip", group.LastMessage);
        Assert.Contains(nameof(Fail), group.TopFrame);
        Assert.Contains(nameof(Fail), group.StackTrace);
    }

    [Fact]
    public void Search_CountsTheOccurrencesOfTheWindow()
    {
        // Arrange
        _journal.Record(Occurrence(Now.AddHours(-5), "Timeout", "Timeout"));
        _journal.Record(Occurrence(Now.AddMinutes(-30), "Timeout", "Timeout"));
        _journal.Record(Occurrence(Now.AddMinutes(-10), "Timeout", "Timeout"));
        _journal.Record(Occurrence(Now.AddHours(-6), "Old", "Old"));

        // Act
        var groups = _journal.Search(new ErrorSearchCriteria { Since = Now.AddHours(-1) });

        // Assert
        var group = Assert.Single(groups);
        Assert.Equal("Timeout", group.MessageTemplate);
        Assert.Equal(2, group.Count);
    }

    [Fact]
    public void Search_FindsByKeyword_And_RedactsMessages()
    {
        // Arrange
        _journal.Record(Occurrence(Now, "Sending to {Email} failed", "Sending to jane@example.com failed"));
        _journal.Record(Occurrence(Now, "Index rebuild failed", "Index rebuild failed"));

        // Act
        var groups = _journal.Search(new ErrorSearchCriteria { Keyword = "sending" });

        // Assert
        var group = Assert.Single(groups);
        Assert.Equal("Sending to ***@*** failed", group.LastMessage);
    }

    [Fact]
    public void Record_KeepsAtMostMaxGroups_DroppingTheLeastRecentlySeen()
    {
        // Act
        for (var i = 0; i <= ErrorJournal.MaxGroups; i++)
        {
            _journal.Record(Occurrence(Now.AddSeconds(i), $"Error {i}", $"Error {i}"));
        }

        // Assert
        var groups = _journal.Search(new ErrorSearchCriteria { Take = 1000 });
        Assert.Equal(ErrorJournal.MaxGroups, groups.Count);
        Assert.DoesNotContain(groups, x => x.MessageTemplate == "Error 0");
    }

    [Fact]
    public void Sink_RecordsErrorsAndWarningsWithAnException_Only()
    {
        // Arrange
        var sink = new ErrorJournalSink(_journal);
        var exception = Throw(() => throw new TimeoutException("slow"));

        // Act
        sink.Emit(LogEvent(LogEventLevel.Information, "Started", null));
        sink.Emit(LogEvent(LogEventLevel.Warning, "Slow query", null));
        sink.Emit(LogEvent(LogEventLevel.Warning, "Retrying", exception));
        sink.Emit(LogEvent(LogEventLevel.Error, "Failed", null));

        // Assert
        var templates = _journal.Search(new ErrorSearchCriteria()).Select(x => x.MessageTemplate).Order().ToArray();
        Assert.Equal(["Failed", "Retrying"], templates);
    }

    [Fact]
    public void Record_CountsEveryOccurrence_And_KeepsTheGroupLimit_UnderConcurrentLogging()
    {
        // Arrange
        var exception = Throw(() => throw new InvalidOperationException("Storm"));
        const int threads = 8;
        const int perThread = 500;

        // Act: one error repeated by every thread, the most recent, among more distinct errors than the journal keeps.
        Parallel.For(0, threads, thread =>
        {
            for (var i = 0; i < perThread; i++)
            {
                _journal.Record(Occurrence(Now.AddHours(1), "Storm", "Storm", exception));
                _journal.Record(Occurrence(Now.AddSeconds(i), $"Unique {thread}-{i}", $"Unique {thread}-{i}"));
            }
        });

        // Assert
        Assert.True(_journal.Search(new ErrorSearchCriteria { Take = 10_000 }).Count <= ErrorJournal.MaxGroups + threads);
        var storm = Assert.Single(_journal.Search(new ErrorSearchCriteria { Keyword = "Storm" }));
        Assert.Equal(threads * perThread, storm.Count);
    }

    // A long exception is cut before it is redacted, with a margin: a card number across the kept length is still
    // recognized as one and masked, not cut into digits that no longer look like a card.
    [Fact]
    public void Record_MasksACardNumberAcrossTheKeptLength()
    {
        // Arrange
        const string prefix = "System.InvalidOperationException: ";
        var padding = new string('x', 5990 - prefix.Length - 1);
        var exception = new InvalidOperationException($"{padding} 4111 1111 1111 1111 declined");

        // Act
        _journal.Record(Occurrence(Now, "Long", "Long", exception));

        // Assert
        var group = Assert.Single(_journal.Search(new ErrorSearchCriteria()));
        Assert.StartsWith(prefix, group.StackTrace);
        Assert.Contains("[card]", group.StackTrace);
        Assert.DoesNotContain("4111 1", group.StackTrace);
    }

    [Theory]
    [InlineData("   at A.B.C(Int32 x) in C:\\src\\B.cs:line 12\n   at D.E.F()", "A.B.C(Int32 x)")]
    [InlineData("System.Exception: boom\n   at A.B.C()", "A.B.C()")]
    [InlineData("no frames", null)]
    [InlineData(null, null)]
    public void GetTopFrame_ReadsTheFirstFrameWithoutItsLocation(string stackTrace, string expected)
    {
        Assert.Equal(expected, ErrorJournal.GetTopFrame(stackTrace));
    }

    private static ErrorOccurrence Occurrence(DateTime time, string template, string message, Exception exception = null)
    {
        return new ErrorOccurrence { Timestamp = time, Level = "Error", MessageTemplate = template, Message = message, Exception = exception, Source = "Tests" };
    }

    private static LogEvent LogEvent(LogEventLevel level, string template, Exception exception)
    {
        return new LogEvent(Now, level, exception, new MessageTemplateParser().Parse(template), []);
    }

    private static void Fail(string path)
    {
        throw new InvalidOperationException($"Invalid path {path}");
    }

    private static Exception Throw(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            return ex;
        }

        return null;
    }
}

internal sealed class FixedTimeProvider(DateTime now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
    {
        return new DateTimeOffset(now);
    }
}
