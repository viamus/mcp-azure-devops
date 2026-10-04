using Microsoft.Extensions.Hosting;
using Viamus.Azure.Devops.Mcp.Server.Configuration;
using Viamus.Azure.Devops.Mcp.Stdio;

try
{
    var builder = StdioHost.CreateBuilder(args);
    using var host = builder.Build();
    await host.RunAsync();
    return 0;
}
catch (AzureDevOpsConfigurationException exception)
{
    await Console.Error.WriteLineAsync(exception.Message);
    return 1;
}
catch (Exception)
{
    // Startup exceptions may contain setting values, file content, or SDK diagnostics.
    await Console.Error.WriteLineAsync("Azure DevOps MCP STDIO could not start or continue. Check appsettings.json beside the executable and AzureDevOps__ environment settings; ensure the MCP client provides valid standard input/output streams.");
    return 1;
}
