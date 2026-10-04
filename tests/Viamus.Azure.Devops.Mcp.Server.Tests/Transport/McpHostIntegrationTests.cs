using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Common;
using Moq;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Viamus.Azure.Devops.Mcp.Core;
using Viamus.Azure.Devops.Mcp.Server.Services;
using Viamus.Azure.Devops.Mcp.Server.Tools;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Transport;

public sealed class McpHostIntegrationTests
{
    private const string TestPat = "test-only-pat-do-not-expose";

    [Theory]
    [InlineData("stdio")]
    [InlineData("http")]
    public async Task Host_ListsAllTools_ReportsFailures_AndKeepsServing(string transport)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await using var azureDevOps = await StartUnauthorizedServer(timeout.Token);
        var organizationUrl = GetAddress(azureDevOps).ToString();
        await using var httpHost = transport == "http"
            ? await StartHttpMcpServer(organizationUrl, timeout.Token)
            : null;

        IClientTransport clientTransport = transport == "stdio"
            ? new StdioClientTransport(new StdioClientTransportOptions
            {
                Command = "dotnet",
                Arguments = [GetStdioAssemblyPath()],
                WorkingDirectory = Path.GetTempPath(),
                EnvironmentVariables = GetEnvironment(organizationUrl),
                ShutdownTimeout = TimeSpan.FromSeconds(5)
            })
            : new SseClientTransport(new SseClientTransportOptions
            {
                Endpoint = GetAddress(httpHost!),
                UseStreamableHttp = true,
                ConnectionTimeout = TimeSpan.FromSeconds(10)
            });

        await using var client = await McpClientFactory.CreateAsync(
            clientTransport, cancellationToken: timeout.Token);
        var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
        var expectedTools = typeof(GitTools).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods())
            .Select(method => method.GetCustomAttribute<ModelContextProtocol.Server.McpServerToolAttribute>())
            .Where(attribute => attribute != null)
            .Select(attribute => attribute!.Name)
            .OrderBy(name => name)
            .ToArray();
        Assert.Equal(expectedTools, tools.Select(tool => tool.Name).OrderBy(name => name).ToArray());

        var invalid = await client.CallToolAsync("get_repository",
            new Dictionary<string, object?> { ["repositoryNameOrId"] = "" },
            cancellationToken: timeout.Token);
        Assert.True(invalid.IsError);
        Assert.Contains("required", Assert.Single(invalid.Content).Text!, StringComparison.OrdinalIgnoreCase);

        var unknownOrganization = await client.CallToolAsync("get_repositories",
            new Dictionary<string, object?> { ["project"] = "test-project", ["organization"] = "unconfigured" },
            cancellationToken: timeout.Token);
        AssertError(unknownOrganization, "CONFIGURATION_ERROR");

        var unauthorized = await client.CallToolAsync("get_repositories",
            new Dictionary<string, object?> { ["project"] = "test-project" },
            cancellationToken: timeout.Token);
        AssertError(unauthorized, "UNEXPECTED_ERROR");
        Assert.DoesNotContain("Basic authentication requires", Assert.Single(unauthorized.Content).Text!);
        Assert.DoesNotContain(TestPat, Assert.Single(unauthorized.Content).Text!);

        var toolsAfterFailure = await client.ListToolsAsync(cancellationToken: timeout.Token);
        Assert.Equal(tools.Count, toolsAfterFailure.Count);
    }

    [Fact]
    public async Task Http_AuthenticationFailure_ReturnsActionableMcpErrorWithoutSecret()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var service = new Mock<IAzureDevOpsService>();
        service.Setup(value => value.GetRepositoriesAsync("test-project", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new VssUnauthorizedException(TestPat + " upstream diagnostic"));
        await using var host = await StartHttpMcpServer("https://dev.azure.com/test", timeout.Token, service.Object);
        await using var client = await McpClientFactory.CreateAsync(new SseClientTransport(new SseClientTransportOptions
        {
            Endpoint = GetAddress(host),
            UseStreamableHttp = true
        }), cancellationToken: timeout.Token);

        var response = await client.CallToolAsync("get_repositories",
            new Dictionary<string, object?> { ["project"] = "test-project" },
            cancellationToken: timeout.Token);
        AssertError(response, "AZURE_DEVOPS_AUTHENTICATION_FAILED");
        var text = Assert.Single(response.Content).Text!;
        Assert.DoesNotContain(TestPat, text);
        Assert.Contains("PAT may be expired, revoked, or invalid", text);
        using var payload = JsonDocument.Parse(text);
        Assert.Equal(401, payload.RootElement.GetProperty("httpStatusCode").GetInt32());
        service.Verify(value => value.GetRepositoriesAsync("test-project", It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotEmpty(await client.ListToolsAsync(cancellationToken: timeout.Token));
    }
    [Fact]
    public async Task Stdio_MissingConfiguration_ExitsWithErrorOnStderrAndEmptyStdout()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetTempPath()
        };
        startInfo.ArgumentList.Add(GetStdioAssemblyPath());
        foreach (var entry in GetEnvironment("https://dev.azure.com/test"))
        {
            if (entry.Value == null) startInfo.Environment.Remove(entry.Key);
            else startInfo.Environment[entry.Key] = entry.Value;
        }
        startInfo.Environment["AzureDevOps__PersonalAccessToken"] = "";

        using var process = Process.Start(startInfo)!;
        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            Assert.NotEqual(0, process.ExitCode);
            Assert.Equal("", await stdoutTask);
            Assert.Contains("PersonalAccessToken", await stderrTask);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    private static void AssertError(CallToolResponse response, string expectedCode)
    {
        Assert.True(response.IsError);
        using var document = JsonDocument.Parse(Assert.Single(response.Content).Text!);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("errorCode").GetString());
        Assert.False(document.RootElement.GetProperty("retryable").GetBoolean());
    }

    private static Dictionary<string, string?> GetEnvironment(string organizationUrl)
    {
        var result = Environment.GetEnvironmentVariables().Keys.Cast<string>()
            .Where(name => name.StartsWith("AzureDevOps__", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(name => name, _ => (string?)null, StringComparer.OrdinalIgnoreCase);
        result["AzureDevOps__OrganizationUrl"] = organizationUrl;
        result["AzureDevOps__PersonalAccessToken"] = TestPat;
        result["AzureDevOps__DefaultProject"] = "test-project";
        result["Logging__LogLevel__Default"] = "Trace";
        return result;
    }

    private static string GetStdioAssemblyPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Solution.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var configuration = typeof(McpHostIntegrationTests).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var assemblyPath = Path.Combine(directory!.FullName, "src", "Viamus.Azure.Devops.Mcp.Stdio",
            "bin", configuration, "net10.0", "Viamus.Azure.Devops.Mcp.Stdio.dll");
        Assert.True(File.Exists(assemblyPath), $"STDIO host was not built: {assemblyPath}");
        return assemblyPath;
    }

    private static async Task<WebApplication> StartUnauthorizedServer(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.Run(context =>
        {
            context.Response.StatusCode = 401;
            context.Response.Headers.WWWAuthenticate = "Basic realm=AzureDevOps";
            return Task.CompletedTask;
        });
        await app.StartAsync(cancellationToken);
        return app;
    }

    private static async Task<WebApplication> StartHttpMcpServer(string organizationUrl, CancellationToken cancellationToken, IAzureDevOpsService? service = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureDevOps:OrganizationUrl"] = organizationUrl,
            ["AzureDevOps:PersonalAccessToken"] = TestPat,
            ["AzureDevOps:DefaultProject"] = "test-project"
        });
        if (service != null) builder.Services.AddSingleton(service);
        builder.Services.AddAzureDevOpsMcp(builder.Configuration).WithHttpTransport();
        var app = builder.Build();
        app.MapMcp();
        await app.StartAsync(cancellationToken);
        return app;
    }

    private static Uri GetAddress(WebApplication app) => new(
        app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
}
