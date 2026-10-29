using System.Text.Json;
using Moq;
using Viamus.Azure.Devops.Mcp.Server.Models;
using Viamus.Azure.Devops.Mcp.Server.Services;
using Viamus.Azure.Devops.Mcp.Server.Tools;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Tools;

public class PullRequestAutoCompleteToolsTests
{
    private readonly Mock<IAzureDevOpsService> _service = new(MockBehavior.Strict);

    [Theory]
    [InlineData("noFastForward", "NoFastForward")]
    [InlineData(" SQUASH ", "Squash")]
    [InlineData("rebase", "Rebase")]
    [InlineData("rebaseMerge", "RebaseMerge")]
    public async Task Enable_PassesOptionsOrganizationAndCancellation_AndReturnsState(string strategy, string normalizedStrategy)
    {
        var accessor = new AzureDevOpsOrganizationContextAccessor();
        var tools = new PullRequestTools(_service.Object, accessor);
        using var originalScope = accessor.Use("original");
        using var cancellation = new CancellationTokenSource();
        _service.Setup(service => service.SetPullRequestAutoCompleteAsync(
                "repo", 123, true, normalizedStrategy, true, "project", cancellation.Token))
            .Callback(() => Assert.Equal("secondary", accessor.CurrentOrganization))
            .ReturnsAsync(new PullRequestDto
            {
                PullRequestId = 123,
                Status = "Active",
                AutoCompleteEnabled = true,
                AutoCompleteSetBy = "Authenticated User",
                AutoCompleteSetById = "user-id"
            });

        var result = await tools.SetPullRequestAutoComplete(
            "repo", 123, true, strategy, true, "project", "secondary", cancellation.Token);

        using var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        var pullRequest = json.RootElement.GetProperty("pullRequest");
        Assert.True(pullRequest.GetProperty("autoCompleteEnabled").GetBoolean());
        Assert.Equal("user-id", pullRequest.GetProperty("autoCompleteSetById").GetString());
        Assert.Equal("original", accessor.CurrentOrganization);
        _service.VerifyAll();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExplicitChoice_WithNoOptions_PreservesDefaults(bool enabled)
    {
        _service.Setup(service => service.SetPullRequestAutoCompleteAsync(
                "repo", 123, enabled, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequestDto { PullRequestId = 123, AutoCompleteEnabled = enabled });

        var result = await new PullRequestTools(_service.Object).SetPullRequestAutoComplete("repo", 123, enabled);

        using var json = JsonDocument.Parse(result);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(enabled, json.RootElement.GetProperty("pullRequest").GetProperty("autoCompleteEnabled").GetBoolean());
        _service.VerifyAll();
    }

    [Theory]
    [InlineData("", 123, true, null, null, "Repository name or ID is required")]
    [InlineData(" ", 123, false, null, null, "Repository name or ID is required")]
    [InlineData("repo", 0, true, null, null, "Pull request ID must be a positive integer")]
    [InlineData("repo", -1, false, null, null, "Pull request ID must be a positive integer")]
    [InlineData("repo", 123, true, "invalid", null, "Unsupported merge strategy")]
    [InlineData("repo", 123, true, "1", null, "Unsupported merge strategy")]
    [InlineData("repo", 123, true, "", null, "Unsupported merge strategy")]
    [InlineData("repo", 123, false, "squash", null, "only valid when enabling")]
    [InlineData("repo", 123, false, null, false, "only valid when enabling")]
    public async Task InvalidArguments_ReturnErrorWithoutCallingService(
        string repository, int id, bool enabled, string? strategy, bool? deleteSourceBranch, string error)
    {
        var result = await new PullRequestTools(_service.Object).SetPullRequestAutoComplete(
            repository, id, enabled, strategy, deleteSourceBranch);

        using var json = JsonDocument.Parse(result);
        Assert.Contains(error, json.RootElement.GetProperty("error").GetString());
        _service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ServiceFailure_PropagatesToErrorFilter_AndRestoresOrganization()
    {
        var accessor = new AzureDevOpsOrganizationContextAccessor();
        using var originalScope = accessor.Use("original");
        var tools = new PullRequestTools(_service.Object, accessor);
        var failure = new HttpRequestException("Failed to update");
        _service.Setup(service => service.SetPullRequestAutoCompleteAsync(
                "repo", 123, true, null, null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var actual = await Assert.ThrowsAsync<HttpRequestException>(() =>
            tools.SetPullRequestAutoComplete("repo", 123, true, organization: "secondary"));

        Assert.Same(failure, actual);
        Assert.Equal("original", accessor.CurrentOrganization);
        _service.VerifyAll();
    }
}
