using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Viamus.Azure.Devops.Mcp.Core.Errors;
using Viamus.Azure.Devops.Mcp.Server.Configuration;
using Viamus.Azure.Devops.Mcp.Server.Services;
using Viamus.Azure.Devops.Mcp.Server.Tools;

namespace Viamus.Azure.Devops.Mcp.Core;

/// <summary>Registers the same Azure DevOps tools and services for every MCP transport.</summary>
public static class AzureDevOpsMcpServiceCollectionExtensions
{
    public static IMcpServerBuilder AddAzureDevOpsMcp(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(AzureDevOpsOptions.SectionName);
        var options = section.Get<AzureDevOpsOptions>() ?? new AzureDevOpsOptions();
        var errors = AzureDevOpsConfigurationValidator.Validate(options);
        if (errors.Count > 0)
        {
            throw new AzureDevOpsConfigurationException(errors);
        }

        services.Configure<AzureDevOpsOptions>(section);
        services.TryAddSingleton<IAzureDevOpsOrganizationContextAccessor, AzureDevOpsOrganizationContextAccessor>();
        // The service and its Azure SDK clients are constructed only when a tool uses them.
        services.TryAddSingleton<IAzureDevOpsService, AzureDevOpsService>();

        return services.AddMcpServer()
            .WithToolsFromAssembly(typeof(GitTools).Assembly)
            .WithAzureDevOpsErrorHandling();
    }
}
