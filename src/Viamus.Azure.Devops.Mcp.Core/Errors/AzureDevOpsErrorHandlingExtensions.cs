using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Viamus.Azure.Devops.Mcp.Server.Tools;

namespace Viamus.Azure.Devops.Mcp.Core.Errors;

public static class AzureDevOpsErrorHandlingExtensions
{
    /// <summary>Wraps registered tools. Call after WithTools or WithToolsFromAssembly.</summary>
    public static IMcpServerBuilder WithAzureDevOpsErrorHandling(this IMcpServerBuilder builder, Assembly? toolAssembly = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var methods = (toolAssembly ?? typeof(WorkItemTools).Assembly).GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Select(method => (Method: method, Attribute: method.GetCustomAttribute<McpServerToolAttribute>()))
            .Where(value => value.Attribute is not null)
            .ToDictionary(value => value.Attribute!.Name ?? value.Method.Name, value => value.Method, StringComparer.Ordinal);
        for (var index = 0; index < builder.Services.Count; index++)
        {
            var descriptor = builder.Services[index];
            if (descriptor.ServiceType != typeof(McpServerTool) || descriptor.IsKeyedService) continue;
            builder.Services[index] = ServiceDescriptor.Describe(typeof(McpServerTool), provider =>
            {
                var inner = ResolveTool(descriptor, provider);
                methods.TryGetValue(inner.ProtocolTool.Name, out var method);
                return inner is AzureDevOpsErrorHandlingTool ? inner : new AzureDevOpsErrorHandlingTool(
                    inner, provider.GetService<ILogger<AzureDevOpsErrorHandlingTool>>(), method);
            }, descriptor.Lifetime);
        }
        return builder;
    }

    private static McpServerTool ResolveTool(ServiceDescriptor descriptor, IServiceProvider provider) =>
        (McpServerTool)(descriptor.ImplementationInstance ?? descriptor.ImplementationFactory?.Invoke(provider) ??
            ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!));
}
