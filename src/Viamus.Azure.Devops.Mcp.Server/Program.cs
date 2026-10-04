using Viamus.Azure.Devops.Mcp.Core;
using Viamus.Azure.Devops.Mcp.Server.Configuration;
using Viamus.Azure.Devops.Mcp.Server.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ServerSecurityOptions>(
    builder.Configuration.GetSection(ServerSecurityOptions.SectionName));

builder.Services.AddAzureDevOpsMcp(builder.Configuration).WithHttpTransport();

var app = builder.Build();
app.UseApiKeyAuthentication();
app.MapMcp();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
app.Run();
