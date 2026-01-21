using Viamus.Azure.Devops.Mcp.Server.Configuration;
using Viamus.Azure.Devops.Mcp.Server.Services;
using Viamus.Azure.Devops.Mcp.Server.Tools;

var builder = WebApplication.CreateBuilder(args);

// Configure Azure DevOps options
builder.Services.Configure<AzureDevOpsOptions>(
    builder.Configuration.GetSection(AzureDevOpsOptions.SectionName));

// Validate configuration on startup
var azureDevOpsConfig = builder.Configuration.GetSection(AzureDevOpsOptions.SectionName).Get<AzureDevOpsOptions>();
if (string.IsNullOrWhiteSpace(azureDevOpsConfig?.OrganizationUrl))
{
    throw new InvalidOperationException("AzureDevOps:OrganizationUrl configuration is required.");
}
if (string.IsNullOrWhiteSpace(azureDevOpsConfig?.PersonalAccessToken))
{
    throw new InvalidOperationException("AzureDevOps:PersonalAccessToken configuration is required.");
}

// Register services
builder.Services.AddSingleton<IAzureDevOpsService, AzureDevOpsService>();

// Configure MCP Server
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// Map MCP endpoints
app.MapMcp();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
