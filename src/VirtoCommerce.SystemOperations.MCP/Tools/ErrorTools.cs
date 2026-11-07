using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using VirtoCommerce.MCP.Core;
using VirtoCommerce.MCP.Core.Attributes;
using VirtoCommerce.MCP.Core.Services;
using VirtoCommerce.SystemOperations.Core.Diagnostics;
using static VirtoCommerce.SystemOperations.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.SystemOperations.MCP.Tools;

// Errors grouped by fingerprint instead of raw log lines, and "is it our code or Virto's" for one of them.
[McpServerToolType]
[McpScope(McpScopes.Admin)]
[Authorize(DiagnosticsRead)]
public class ErrorTools(
    IErrorJournal errorJournal,
    IErrorExplainer errorExplainer,
    ISensitiveDataRedactor redactor,
    TimeProvider timeProvider)
{
    [McpServerTool(Name = "search_errors", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Errors this instance logged since it started, grouped by fingerprint (exception type, message template " +
        "and the frame that threw), most recent first, with the count in the window, first and last time, the source, " +
        "the last message and trace id. Messages have secrets and personal data removed. Pass a fingerprint to " +
        "explain_error to find out whose code throws it.")]
    public async Task<object> SearchErrors(
        IMcpPageSizeResolver pageSizeResolver,
        [Description("How many hours back to count. Omit it for everything since the instance started.")] int? hours = null,
        [Description("Text in the message, the source, the exception type or the frame, for example 'Order' or 'Timeout'.")] string keyword = null,
        [Description("Number of groups to return. Omit it for the default page size.")] int? take = null)
    {
        var since = hours == null ? (DateTime?)null : timeProvider.GetUtcNow().UtcDateTime.AddHours(-Math.Clamp(hours.Value, 1, 24 * 14));
        var groups = errorJournal.Search(new ErrorSearchCriteria
        {
            Since = since,
            Keyword = keyword,
            Take = await pageSizeResolver.GetPageSizeAsync(take),
        });

        return new
        {
            instance = Environment.MachineName,
            journal_started_at = errorJournal.StartedAt,
            since,
            errors = groups.Select(x => new
            {
                fingerprint = x.Fingerprint,
                count = x.Count,
                level = x.Level,
                exception_type = x.ExceptionType,
                message = x.LastMessage,
                source = x.Source,
                top_frame = x.TopFrame,
                first_seen = x.FirstSeen,
                last_seen = x.LastSeen,
                last_trace_id = x.LastTraceId,
            }),
        };
    }

    [McpServerTool(Name = "explain_error", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Answer 'is it our code or Virto's' for one error: maps every frame of its stack to the platform, a " +
        "Virto module or one of the solution's own modules, with versions, and names the likely cause class (data, " +
        "configuration, customization, platform_code, infrastructure, external_service) with the reason and the next " +
        "checks. Pass the fingerprint from search_errors, or a stack trace the user pasted.")]
    public object ExplainError(
        [Description("Fingerprint from search_errors.")] string fingerprint = null,
        [Description("A stack trace to explain instead, as text, for example from a log file or a support ticket.")][McpSensitive] string stack_trace = null)
    {
        if (!string.IsNullOrEmpty(fingerprint))
        {
            var error = errorJournal.Get(fingerprint) ?? throw new McpException($"No error with fingerprint '{fingerprint}' on this instance. Errors are kept per instance since it started; call search_errors.");
            return new
            {
                fingerprint,
                count = error.Count,
                last_seen = error.LastSeen,
                last_trace_id = error.LastTraceId,
                explanation = errorExplainer.Explain(error.ExceptionType, error.LastMessage, error.StackTrace),
            };
        }

        if (string.IsNullOrWhiteSpace(stack_trace))
        {
            throw new McpException("Pass a fingerprint from search_errors or a stack trace.");
        }

        var text = redactor.Redact(stack_trace);
        var firstLine = text.Split('\n')[0].Trim();
        var colon = firstLine.IndexOf(':');
        var message = colon > 0 ? firstLine[(colon + 1)..].Trim() : null;

        return new { explanation = errorExplainer.Explain(null, message, text) };
    }
}
