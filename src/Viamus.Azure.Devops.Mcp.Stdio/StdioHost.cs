using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Viamus.Azure.Devops.Mcp.Core;

namespace Viamus.Azure.Devops.Mcp.Stdio;

/// <summary>Creates a console host whose stdout is reserved for MCP protocol messages.</summary>
public static class StdioHost
{
    public static HostApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services.AddAzureDevOpsMcp(builder.Configuration).WithStdioServerTransport();
        return builder;
    }
}
