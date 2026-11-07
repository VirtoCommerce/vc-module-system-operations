using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using VirtoCommerce.MCP.Core.Extensions;

namespace VirtoCommerce.SystemOperations.MCP;

public static class SystemOperationsMcp
{
    // The diagnostics models are returned as they are, with snake_case names, the convention of MCP tool results.
    public static readonly JsonSerializerOptions SerializerOptions = new(McpJsonUtilities.DefaultOptions)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // Adds the operations toolset and its prompts to the MCP module's admin scope. Without the MCP module installed
    // the registrations stay unused: nothing serves them.
    public static IServiceCollection AddSystemOperationsMcp(this IServiceCollection services)
    {
        services.AddMcpToolsFromAssembly(typeof(SystemOperationsMcp).Assembly, SerializerOptions);
        services.AddMcpPromptsFromAssembly(typeof(SystemOperationsMcp).Assembly, SerializerOptions);

        return services;
    }
}
