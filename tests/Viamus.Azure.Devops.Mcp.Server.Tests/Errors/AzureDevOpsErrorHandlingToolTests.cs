using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Common;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;
using Viamus.Azure.Devops.Mcp.Core.Errors;
using Viamus.Azure.Devops.Mcp.Server.Services;
using Viamus.Azure.Devops.Mcp.Server.Tools;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Errors;

public class AzureDevOpsErrorHandlingToolTests
{
    [Fact]
    public async Task RegisteredRealTool_AuthenticationFailure_IsMcpErrorAndLogsNoSecret()
    {
        var service = new Mock<IAzureDevOpsService>();
        service.Setup(value => value.GetWorkItemAsync(123, null, false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new VssUnauthorizedException("PAT-secret upstream response"));
        var logger = new RecordingLogger<AzureDevOpsErrorHandlingTool>();
        var services = new ServiceCollection();
        services.AddSingleton(service.Object);
        services.AddSingleton<ILogger<AzureDevOpsErrorHandlingTool>>(logger);
        services.AddMcpServer().WithTools<WorkItemTools>().WithAzureDevOpsErrorHandling();
        using var provider = services.BuildServiceProvider();
        var tool = provider.GetServices<McpServerTool>().Single(value => value.ProtocolTool.Name == "get_work_item");
        var response = await tool.InvokeAsync(Request(provider, "get_work_item", new() { ["workItemId"] = JsonSerializer.SerializeToElement(123) }));
        Assert.True(response.IsError);
        using var payload = JsonDocument.Parse(Assert.Single(response.Content).Text!);
        Assert.Equal("AZURE_DEVOPS_AUTHENTICATION_FAILED", payload.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal(401, payload.RootElement.GetProperty("httpStatusCode").GetInt32());
        Assert.DoesNotContain("PAT-secret", response.Content[0].Text);
        Assert.DoesNotContain("PAT-secret", string.Join(" ", logger.Messages));
        Assert.All(logger.Exceptions, Assert.Null);
    }

    [Fact]
    public async Task RegisteredRealTool_MissingRequiredArgument_IsInvalidArgument()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Mock<IAzureDevOpsService>().Object);
        services.AddMcpServer().WithTools<WorkItemTools>().WithAzureDevOpsErrorHandling();
        using var provider = services.BuildServiceProvider();
        var tool = provider.GetServices<McpServerTool>().Single(value => value.ProtocolTool.Name == "get_work_item");
        var response = await tool.InvokeAsync(Request(provider, "get_work_item"));
        Assert.True(response.IsError);
        Assert.Contains("INVALID_ARGUMENT", Assert.Single(response.Content).Text);
    }

    [Fact]
    public async Task RegisteredRealTool_WrappedRequestedCancellation_IsRethrownWithOriginalToken()
    {
        using var cancellation = new CancellationTokenSource();
        CancellationTools.Source = cancellation;
        var services = new ServiceCollection();
        services.AddMcpServer().WithTools<CancellationTools>().WithAzureDevOpsErrorHandling(typeof(CancellationTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tool = Assert.Single(provider.GetServices<McpServerTool>());
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await tool.InvokeAsync(Request(provider, "cancelled"), cancellation.Token));
        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    public sealed class CancellationTools
    {
        public static CancellationTokenSource? Source;
        [McpServerTool(Name = "cancelled")]
        public static string Run(CancellationToken cancellationToken)
        {
            Source!.Cancel();
            throw new TargetInvocationException(new OperationCanceledException(cancellationToken));
        }
    }
    [Fact]
    public async Task ToolConstructorFailure_IsCaughtOnInvokeAndDoesNotPreventDiscovery()
    {
        ConstructorFailureTools.ConstructionCount = 0;
        var services = new ServiceCollection();
        services.AddMcpServer().WithTools<ConstructorFailureTools>().WithAzureDevOpsErrorHandling(typeof(ConstructorFailureTools).Assembly);
        using var provider = services.BuildServiceProvider();
        var tool = Assert.Single(provider.GetServices<McpServerTool>());
        Assert.Equal("constructor_failure", tool.ProtocolTool.Name);
        Assert.Equal(0, ConstructorFailureTools.ConstructionCount);
        var response = await tool.InvokeAsync(Request(provider, "constructor_failure"));
        Assert.True(response.IsError);
        Assert.Contains("AZURE_DEVOPS_AUTHENTICATION_FAILED", Assert.Single(response.Content).Text);
        Assert.Equal(1, ConstructorFailureTools.ConstructionCount);
    }

    [Theory]
    [InlineData("{\"error\":\"Project name is required\"}", "INVALID_ARGUMENT")]
    [InlineData("{\"error\":\"Wiki not found\"}", "AZURE_DEVOPS_NOT_FOUND")]
    public async Task LegacyValidationAndNotFoundJson_AreReportedAsMcpFailures(string result, string code)
    {
        var inner = McpServerTool.Create(() => result, new() { Name = "legacy" });
        var tool = new AzureDevOpsErrorHandlingTool(inner);
        using var provider = new ServiceCollection().BuildServiceProvider();
        var response = await tool.InvokeAsync(Request(provider, "legacy"));
        Assert.True(response.IsError);
        Assert.Contains(code, Assert.Single(response.Content).Text);
    }

    [Fact]
    public async Task SuccessJsonAndNestedErrorFields_ArePreserved()
    {
        const string result = "{\"workItem\":{\"error\":\"customer field value\"},\"success\":true}";
        var tool = new AzureDevOpsErrorHandlingTool(McpServerTool.Create(() => result, new() { Name = "success" }));
        using var provider = new ServiceCollection().BuildServiceProvider();
        var response = await tool.InvokeAsync(Request(provider, "success"));
        Assert.False(response.IsError);
        Assert.Equal(result, Assert.Single(response.Content).Text);
    }

    [Fact]
    public async Task UnexpectedFailure_ProducesSafeGenericToolError()
    {
        Func<string> operation = () => throw new InvalidOperationException("PAT-secret raw response");
        var tool = new AzureDevOpsErrorHandlingTool(McpServerTool.Create(operation, new() { Name = "unknown" }));
        using var provider = new ServiceCollection().BuildServiceProvider();
        var response = await tool.InvokeAsync(Request(provider, "unknown"));
        Assert.True(response.IsError);
        Assert.Contains("UNEXPECTED_ERROR", Assert.Single(response.Content).Text);
        Assert.DoesNotContain("PAT-secret", response.Content[0].Text);
    }

    [Fact]
    public async Task RequestedWrappedCancellation_IsRethrown()
    {
        using var cancellation = new CancellationTokenSource();
        Func<string> operation = () =>
        {
            cancellation.Cancel();
            throw new TargetInvocationException(new OperationCanceledException(cancellation.Token));
        };
        var tool = new AzureDevOpsErrorHandlingTool(McpServerTool.Create(operation, new() { Name = "cancelled" }));
        using var provider = new ServiceCollection().BuildServiceProvider();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await tool.InvokeAsync(Request(provider, "cancelled"), cancellation.Token));
        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    private static RequestContext<CallToolRequestParams> Request(IServiceProvider provider, string name,
        Dictionary<string, JsonElement>? arguments = null) => new(new Mock<IMcpServer>().Object)
        {
            Services = provider,
            Params = new CallToolRequestParams { Name = name, Arguments = arguments }
        };

    public sealed class ConstructorFailureTools
    {
        public static int ConstructionCount;
        public ConstructorFailureTools()
        {
            ConstructionCount++;
            throw new VssAuthenticationException("PAT-secret constructor response");
        }

        [McpServerTool(Name = "constructor_failure")]
        public string Run() => "unreachable";
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public List<Exception?> Exceptions { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            Exceptions.Add(exception);
        }
    }
}
