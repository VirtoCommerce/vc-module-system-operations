using System.ComponentModel;
using ModelContextProtocol.Server;
using VirtoCommerce.MCP.Core;
using VirtoCommerce.MCP.Core.Attributes;
using static VirtoCommerce.SystemOperations.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.SystemOperations.MCP.Prompts;

// The routines of the people who run a Virto Commerce solution, offered by MCP clients as slash commands, for example
// /daily_health_check in Claude. They only describe what to do; the tools decide what the user may see and change. A
// prompt has no [Authorize] check of its own, so [McpPermission] hides the prompts from a user who cannot run their tools.
[McpServerPromptType]
[McpScope(McpScopes.Admin)]
[McpPermission(DiagnosticsRead)]
public static class OperationsPrompts
{
    private const string Rules = """
        Rules:
        - Base every statement on a tool result and quote the evidence (a number, a finding, a fingerprint, a trace id).
        - Name the instance each measurement comes from: a call reaches one instance of the cluster.
        - Do not change anything unless I ask. reset_platform_cache asks me to confirm where my client can ask, otherwise it runs at once, so call it only when I ask for it; restart_platform needs a second person's approval.
        """;

    [McpServerPrompt(Name = "daily_health_check")]
    [Description("Morning check of the platform: health, new errors, failed jobs, capacity and configuration, with a short action list.")]
    public static string DailyHealthCheck()
    {
        return $"""
            Run my daily health check of the Virto Commerce platform.

            1. Call get_platform_health and get_environment_profile.
            2. Call search_errors with hours 24 and list_failed_jobs with hours 24.
            3. Call check_capacity and check_configuration.

            Then write the check for me:
            - One line first: green, yellow or red, and why.
            - Health: every check that is not healthy, with its latency.
            - Errors: the five most frequent groups of the last 24 hours, with count, source and fingerprint; mark the ones first seen today.
            - Jobs: failed jobs and the backlog.
            - Capacity: CPU, memory and thread pool against the limits, and the recommendation.
            - Configuration: critical findings only.
            - Actions: at most five, most urgent first, each with the tool or the person who does it.

            {Rules}
            """;
    }

    [McpServerPrompt(Name = "production_readiness")]
    [Description("Is this environment ready for production traffic: configuration, capacity, migrations, modules and license.")]
    public static string ProductionReadiness()
    {
        return $"""
            Check whether this Virto Commerce environment is ready for production.

            1. Call get_environment_profile and check_configuration.
            2. Call check_pending_migrations and list_modules with filter failed.
            3. Call get_platform_health and check_capacity.

            Then give me a go or no-go:
            - Blockers: every critical finding, pending migration and failed module, each with the fix.
            - Risks: warnings that matter under load or with several instances.
            - What is fine, in one paragraph.
            - A checklist I can hand to the person who deploys, in the order to do it.

            {Rules}
            """;
    }

    [McpServerPrompt(Name = "capacity_review")]
    [Description("Are the resources enough, where is the bottleneck, and should we scale up, scale out or fix a hotspot.")]
    public static string CapacityReview(
        [Description("When the problem happens, for example 'every day 10:00-12:00' or 'during imports'. Optional.")] string when = null)
    {
        var timing = string.IsNullOrWhiteSpace(when) ? string.Empty : $" The problem happens {when}.";

        return $"""
            Review the capacity of the Virto Commerce platform.{timing}

            1. Call get_environment_profile for the instances, processors and memory limits.
            2. Call check_capacity with sample_seconds 5. Call it a second time a minute later and compare.
            3. Call list_failed_jobs for the job backlog, and search_errors with hours 24 and keyword 'timeout'.

            Then answer:
            - Are the resources enough today? Yes or no, with the numbers.
            - Where is the bottleneck: CPU, memory, thread pool, database, job workers or an external service.
            - The recommendation: scale up, scale out, or fix a hotspot first, and why; for a hotspot, which module or operation.
            - What to measure next, and when, if one sample is not enough to be sure.

            {Rules}
            """;
    }

    [McpServerPrompt(Name = "incident_rca")]
    [Description("Root-cause analysis of an incident: timeline, errors, whose code, cause class and next steps.")]
    public static string IncidentRca(
        [Description("What users see, for example 'checkout fails with an error'.")] string symptom,
        [Description("How many hours back the incident started. Default 6.")] string hours = "6")
    {
        return $"""
            Help me find the root cause of an incident: "{symptom}". It started within the last {hours} hours.

            1. Call get_incident_timeline with hours {hours}: what changed just before (an instance start, a deployment, new errors, failed jobs)?
            2. Call search_errors with hours {hours}, and with a keyword from the symptom.
            3. Call explain_error for the error groups that match the symptom.
            4. Call get_platform_health and check_capacity, to rule infrastructure in or out.

            Then write the analysis:
            - What happened, in time order, with the evidence.
            - The root cause, as one of the cause classes: data, configuration, customization, platform_code, infrastructure, external_service. Say how sure you are and what would prove it.
            - Who fixes it: our team (and which module), the hosting, the external service, or Virto support.
            - Immediate mitigation and the permanent fix.
            - If Virto should look at it, offer to run prepare_support_case.

            {Rules}
            """;
    }

    [McpServerPrompt(Name = "is_it_us_or_virto")]
    [Description("For one error: is it the solution's own code, Virto's code, the data, the configuration or the infrastructure.")]
    public static string IsItUsOrVirto(
        [Description("An error fingerprint from search_errors, or a keyword of the error message.")] string error)
    {
        return $"""
            Tell me whether this error is caused by our code or by Virto's: "{error}".

            1. If it looks like a fingerprint, call explain_error with it. Otherwise call search_errors with it as the keyword and explain_error for the best match.
            2. Call list_modules with filter own, so you know our customizations.
            3. If a module of ours is on the stack, say which and at which frame.

            Then answer in three parts:
            - Verdict: our code, Virto's code, data, configuration, infrastructure or an external service, and how sure you are.
            - Evidence: the frames and the facts it rests on.
            - Next step: who should act and what they should check; if it is Virto's code, offer to run prepare_support_case.

            {Rules}
            """;
    }

    [McpServerPrompt(Name = "prepare_support_case")]
    [Description("Collect the evidence and prepare a problem report for Virto Commerce support, for you to review and send.")]
    public static string PrepareSupportCase(
        [Description("What users see.")] string symptom)
    {
        return $"""
            Prepare a support case for Virto Commerce about: "{symptom}".

            1. Ask me in one message for the steps to reproduce and what we already tried, if I have not said it.
            2. Call search_errors with hours 24 and pick the error group that matches the symptom; call explain_error for it.
            3. If the error is in our own module, tell me so first: support cannot fix our code.
            4. Call prepare_support_case with the symptom, the steps, what was tried and the fingerprint.

            Show me the report as it is, and tell me what to check before sending it. Do not send it anywhere.

            {Rules}
            """;
    }
}
