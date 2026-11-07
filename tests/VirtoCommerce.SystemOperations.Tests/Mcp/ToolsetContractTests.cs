using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using VirtoCommerce.MCP.Core;
using VirtoCommerce.MCP.Core.Attributes;
using VirtoCommerce.SystemOperations.Core.Diagnostics;
using VirtoCommerce.SystemOperations.MCP;
using VirtoCommerce.SystemOperations.MCP.Prompts;
using Xunit;
using ModuleConstantsPermissions = VirtoCommerce.SystemOperations.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.SystemOperations.Tests.Mcp;

// The rules every tool of the toolset keeps, checked on the attributes the MCP module reads.
[Trait("Category", "Unit")]
public partial class ToolsetContractTests
{
    private static readonly Assembly McpAssembly = typeof(SystemOperationsMcp).Assembly;

    private static readonly MethodInfo[] Tools = McpAssembly.GetTypes()
        .Where(x => x.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
        .SelectMany(x => x.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        .Where(x => x.GetCustomAttribute<McpServerToolAttribute>() != null)
        .ToArray();

    [Fact]
    public void Toolset_HasTheOperationsTools()
    {
        var names = Tools.Select(ToolName).Order().ToArray();

        Assert.Equal(
        [
            "check_capacity", "check_configuration", "check_pending_migrations", "explain_error", "get_environment_profile",
            "get_incident_timeline", "get_platform_health", "list_failed_jobs", "list_modules", "prepare_support_case",
            "reset_platform_cache", "restart_platform", "search_errors",
        ], names);
    }

    [Fact]
    public void EveryTool_IsOnTheAdminScope_AndSaysWhoMayUseIt()
    {
        foreach (var tool in Tools)
        {
            var scope = tool.GetCustomAttribute<McpScopeAttribute>() ?? tool.DeclaringType.GetCustomAttribute<McpScopeAttribute>();
            Assert.True(scope != null, $"{ToolName(tool)} has no scope");
            Assert.Contains(McpScopes.Admin, scope.Scopes);

            var authorize = tool.GetCustomAttributes<AuthorizeAttribute>().Concat(tool.DeclaringType.GetCustomAttributes<AuthorizeAttribute>());
            Assert.True(authorize.Any(x => !string.IsNullOrEmpty(x.Policy)), $"{ToolName(tool)} has no permission");
        }
    }

    [Fact]
    public void ReadOnlyTools_ChangeNothing_AndActionsAskAPerson()
    {
        foreach (var tool in Tools.Where(x => x.GetCustomAttribute<McpServerToolAttribute>().ReadOnly))
        {
            Assert.Null(tool.GetCustomAttribute<McpRequiresApprovalAttribute>());
            Assert.DoesNotContain(tool.GetParameters(), x => x.Name == "dryRun");
        }

        var reset = Tools.Single(x => ToolName(x) == "reset_platform_cache");
        Assert.NotNull(reset.GetCustomAttribute<McpRequiresConfirmationAttribute>());
        Assert.True(reset.GetCustomAttribute<McpServerToolAttribute>().Destructive);

        var restart = Tools.Single(x => ToolName(x) == "restart_platform");
        Assert.NotNull(restart.GetCustomAttribute<McpRequiresApprovalAttribute>());
        Assert.Contains(restart.GetParameters(), x => x.Name == "dryRun");
    }

    [Fact]
    public void Prompts_NameOnlyToolsOfTheToolset()
    {
        var toolNames = Tools.Select(ToolName).ToHashSet();
        var prompts = typeof(OperationsPrompts).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(x => x.GetCustomAttribute<McpServerPromptAttribute>() != null)
            .ToArray();

        Assert.Equal(6, prompts.Length);
        foreach (var prompt in prompts)
        {
            var arguments = prompt.GetParameters().Select(x => x.HasDefaultValue ? x.DefaultValue : "x").ToArray();
            var text = (string)prompt.Invoke(null, arguments);

            foreach (Match match in CallRegex().Matches(text))
            {
                Assert.True(toolNames.Contains(match.Groups["name"].Value), $"Prompt {prompt.Name} calls unknown tool {match.Groups["name"].Value}");
            }
        }
    }

    [Fact]
    public void AddSystemOperationsMcp_RegistersEveryToolAndPrompt()
    {
        // Act
        using var provider = new ServiceCollection().AddSystemOperationsMcp().BuildServiceProvider();

        // Assert
        Assert.Equal(Tools.Length, provider.GetServices<McpServerTool>().Count());
        Assert.Equal(6, provider.GetServices<McpServerPrompt>().Count());
    }

    // The MCP module hides a prompt whose [McpPermission] the user lacks; a prompt has no [Authorize] check.
    [Fact]
    public void Prompts_RequireTheDiagnosticsPermission()
    {
        // Act
        using var provider = new ServiceCollection().AddSystemOperationsMcp().BuildServiceProvider();

        // Assert
        Assert.All(provider.GetServices<McpServerPrompt>(), prompt => Assert.Contains(
            ModuleConstantsPermissions.DiagnosticsRead,
            prompt.Metadata.OfType<McpPermissionAttribute>().SelectMany(x => x.Permissions)));
    }

    [Fact]
    public void SerializerOptions_WriteSnakeCase_AndSkipNulls()
    {
        var json = JsonSerializer.Serialize(new Finding { Id = "redis-missing", CauseClass = CauseClasses.Configuration }, SystemOperationsMcp.SerializerOptions);

        Assert.Equal("""{"id":"redis-missing","cause_class":"configuration"}""", json);
    }

    private static string ToolName(MethodInfo method)
    {
        return method.GetCustomAttribute<McpServerToolAttribute>().Name;
    }

    [GeneratedRegex(@"(?:Call|call)\s+(?<name>[a-z]+_[a-z_]+)")]
    private static partial Regex CallRegex();
}
