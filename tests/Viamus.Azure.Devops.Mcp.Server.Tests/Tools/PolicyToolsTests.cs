using System.Text.Json;
using Moq;
using Viamus.Azure.Devops.Mcp.Server.Models;
using Viamus.Azure.Devops.Mcp.Server.Services;
using Viamus.Azure.Devops.Mcp.Server.Tools;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Tools;

public class PolicyToolsTests
{
    [Fact]
    public async Task Configurations_ForwardScopePaginationAndCancellation_AndRestoreOrganization()
    {
        var accessor = new AzureDevOpsOrganizationContextAccessor();
        using var scope = accessor.Use("original");
        using var cancellation = new CancellationTokenSource();
        using var settings = JsonDocument.Parse("{\"minimumApproverCount\":1,\"creatorVoteCounts\":false,\"scope\":[{\"repositoryId\":null,\"refName\":\"refs/heads/main\",\"matchKind\":\"Exact\"}]}");
        var service = new Mock<IAzureDevOpsService>(MockBehavior.Strict);
        service.Setup(value => value.GetPolicyConfigurationsAsync("repo", "refs/heads/main", "project", 25, "next-token", cancellation.Token))
            .Callback(() => Assert.Equal("secondary", accessor.CurrentOrganization))
            .ReturnsAsync(new PolicyConfigurationPageDto
            {
                RepositoryId = "repo-id",
                ProjectId = "project-id",
                RefName = "refs/heads/main",
                Configurations = [new PolicyConfigurationDto { Id = 27, IsEnabled = true, IsBlocking = true, Settings = settings.RootElement.Clone() }],
                ContinuationToken = "another-token"
            });

        var result = await new PolicyTools(service.Object, accessor).GetPolicyConfigurations("repo", "refs/heads/main", "project", 25, "next-token", "secondary", cancellation.Token);

        using var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.GetProperty("hasMore").GetBoolean());
        Assert.Equal("another-token", json.RootElement.GetProperty("continuationToken").GetString());
        var serializedSettings = json.RootElement.GetProperty("configurations")[0].GetProperty("settings");
        Assert.Equal(1, serializedSettings.GetProperty("minimumApproverCount").GetInt32());
        Assert.False(serializedSettings.GetProperty("creatorVoteCounts").GetBoolean());
        Assert.Equal(JsonValueKind.Null, serializedSettings.GetProperty("scope")[0].GetProperty("repositoryId").ValueKind);
        Assert.Equal("original", accessor.CurrentOrganization);
        service.VerifyAll();
    }

    [Fact]
    public async Task Evaluations_ForwardOptionsAndCancellation_AndRestoreOrganizationOnFailure()
    {
        var accessor = new AzureDevOpsOrganizationContextAccessor();
        using var scope = accessor.Use("original");
        using var cancellation = new CancellationTokenSource();
        var service = new Mock<IAzureDevOpsService>(MockBehavior.Strict);
        service.Setup(value => value.GetPullRequestPolicyEvaluationsAsync("repo", 123, "project", false, 25, 50, cancellation.Token))
            .Callback(() => Assert.Equal("secondary", accessor.CurrentOrganization))
            .ThrowsAsync(new HttpRequestException("test failure"));

        await Assert.ThrowsAsync<HttpRequestException>(() => new PolicyTools(service.Object, accessor).GetPullRequestPolicyEvaluations("repo", 123, "project", false, 25, 50, "secondary", cancellation.Token));

        Assert.Equal("original", accessor.CurrentOrganization);
        service.VerifyAll();
    }

    [Theory]
    [InlineData("", "refs/heads/main", 100)]
    [InlineData("repo", "main", 100)]
    [InlineData("repo", "refs/tags/1.0", 100)]
    [InlineData("repo", "refs/heads/", 100)]
    [InlineData("repo", "refs/heads/main", 0)]
    [InlineData("repo", "refs/heads/main", 1001)]
    public async Task InvalidConfigurationsRequest_DoesNotCallService(string repository, string branch, int top)
    {
        var service = new Mock<IAzureDevOpsService>(MockBehavior.Strict);

        var result = await new PolicyTools(service.Object).GetPolicyConfigurations(repository, branch, top: top);

        using var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.TryGetProperty("error", out _));
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("", 123, 100, 0)]
    [InlineData("repo", 0, 100, 0)]
    [InlineData("repo", 123, 0, 0)]
    [InlineData("repo", 123, 1001, 0)]
    [InlineData("repo", 123, 100, -1)]
    public async Task InvalidEvaluationsRequest_DoesNotCallService(string repository, int id, int top, int skip)
    {
        var service = new Mock<IAzureDevOpsService>(MockBehavior.Strict);

        var result = await new PolicyTools(service.Object).GetPullRequestPolicyEvaluations(repository, id, top: top, skip: skip);

        using var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.TryGetProperty("error", out _));
        service.VerifyNoOtherCalls();
    }
}
