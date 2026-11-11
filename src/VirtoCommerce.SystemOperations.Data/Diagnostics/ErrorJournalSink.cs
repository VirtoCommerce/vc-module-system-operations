using System;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using VirtoCommerce.Platform.Core.Logger;
using VirtoCommerce.SystemOperations.Core.Diagnostics;

namespace VirtoCommerce.SystemOperations.Data.Diagnostics;

// Errors, and warnings that carry an exception, of every logger of the platform go to the error journal. The platform
// logs through Serilog, so the journal is a Serilog sink added through ILoggerConfigurationService, next to the sinks
// the configuration declares, and it sees what they see.
public class ErrorJournalSink(IErrorJournal journal) : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        if (logEvent == null || (logEvent.Level < LogEventLevel.Error && logEvent.Exception == null))
        {
            return;
        }

        try
        {
            journal.Record(new ErrorOccurrence
            {
                Timestamp = logEvent.Timestamp.UtcDateTime,
                Level = logEvent.Level.ToString(),
                Source = logEvent.Properties.TryGetValue(Constants.SourceContextPropertyName, out var source) && source is ScalarValue { Value: string name }
                    ? name
                    : null,
                MessageTemplate = logEvent.MessageTemplate.Text,
                Message = logEvent.RenderMessage(),
                Exception = logEvent.Exception,
                TraceId = logEvent.TraceId?.ToHexString(),
            });
        }
        catch (Exception)
        {
            // A sink must never throw into the logger, and logging this failure would come back here.
        }
    }
}

public class ErrorJournalLoggerConfigurationService(ErrorJournalSink sink) : ILoggerConfigurationService
{
    public void Configure(LoggerConfiguration loggerConfiguration)
    {
        loggerConfiguration.WriteTo.Sink(sink, LogEventLevel.Warning);
    }
}
