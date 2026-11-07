using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using VirtoCommerce.MCP.Core;
using VirtoCommerce.MCP.Core.Attributes;
using VirtoCommerce.SystemOperations.Core.Diagnostics;
using static VirtoCommerce.SystemOperations.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.SystemOperations.MCP.Tools;

[McpServerToolType]
[McpScope(McpScopes.Admin)]
[Authorize(DiagnosticsRead)]
public class SupportTools(ISupportCaseBuilder supportCaseBuilder)
{
    [McpServerTool(Name = "prepare_support_case", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Prepare a problem report for Virto Commerce support in Markdown: the problem, steps and what was tried, " +
        "the environment and the solution's own modules, health, configuration findings, the errors with trace ids, " +
        "where they are thrown and their stack traces, failed jobs and the timeline. Secrets, emails and card numbers " +
        "are replaced with ***. Nothing is sent: show the report to the user to review and send.")]
    public async Task<object> PrepareSupportCase(
        [Description("What users see, in a sentence or two.")] string symptom,
        [Description("How to reproduce it, if known.")] string steps_to_reproduce = null,
        [Description("What was already tried.")] string what_was_tried = null,
        [Description("Fingerprint of the error the case is about, from search_errors. Omit it to include the most frequent errors.")] string fingerprint = null,
        [Description("Hours of errors, failed jobs and events to include, 1 to 336. Default 24.")] int hours = 24,
        CancellationToken cancellationToken = default)
    {
        var report = await supportCaseBuilder.BuildAsync(new SupportCaseRequest
        {
            Symptom = symptom,
            StepsToReproduce = steps_to_reproduce,
            WhatWasTried = what_was_tried,
            Fingerprint = fingerprint,
            Hours = hours,
        }, cancellationToken);

        return new
        {
            format = "markdown",
            note = "Review before sending. It was not sent anywhere.",
            report,
        };
    }
}
