using System.Text.Json;
using Moq;
using Viamus.Azure.Devops.Mcp.Server.Models;
using Viamus.Azure.Devops.Mcp.Server.Services;
using Viamus.Azure.Devops.Mcp.Server.Tools;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Tools;

public class GitHistoryToolsTests
{
    private const string Sha = "1111111111111111111111111111111111111111";
    private readonly Mock<IAzureDevOpsService> _service = new(MockBehavior.Strict);
    private readonly AzureDevOpsOrganizationContextAccessor _accessor = new();
    private GitHistoryTools Tools => new(_service.Object, _accessor);

    [Fact]
    public async Task Refs_ForwardsOpaquePagingAndOrganization_AndReturnsContinuation()
    {
        using var previous = _accessor.Use("previous");
        using var cancellation = new CancellationTokenSource();
        _service.Setup(service => service.GetRefsAsync("repo", "tags/v", 10, "opaque+/=", "project", cancellation.Token))
            .Callback(() => Assert.Equal("secondary", _accessor.CurrentOrganization))
            .ReturnsAsync(new GitRefsPageDto
            {
                Refs = [new() { Name = "refs/tags/v1", ObjectId = "tag-object", ResolvedCommitId = Sha }],
                ContinuationToken = "next"
            });

        using var json = JsonDocument.Parse(await Tools.GetRefs("repo", "tags/v", 10, "opaque+/=", "project", "secondary", cancellation.Token));

        Assert.True(json.RootElement.GetProperty("hasMore").GetBoolean());
        Assert.Equal("next", json.RootElement.GetProperty("continuationToken").GetString());
        Assert.Equal(Sha, json.RootElement.GetProperty("refs")[0].GetProperty("resolvedCommitId").GetString());
        Assert.Equal("previous", _accessor.CurrentOrganization);
        _service.VerifyAll();
    }

    [Fact]
    public async Task Commits_ReturnsPinnedRevisionAndNextPage()
    {
        _service.Setup(service => service.GetCommitsAsync("repo", "main", "branch", 1, 5, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitCommitsPageDto { ResolvedVersion = Sha, Commits = [new() { CommitId = Sha }], Skip = 5, HasMore = true });

        using var json = JsonDocument.Parse(await Tools.GetCommits("repo", "main", top: 1, skip: 5));

        Assert.Equal(Sha, json.RootElement.GetProperty("resolvedVersion").GetString());
        Assert.Equal(6, json.RootElement.GetProperty("nextSkip").GetInt32());
        _service.VerifyAll();
    }

    [Fact]
    public async Task Diff_ForwardsExplicitVersionTypesAndComparisonMode()
    {
        _service.Setup(service => service.GetCommitDiffAsync("repo", "v1", "main", "tag", "branch", true, 2, 4, "project", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitCommitDiffDto { BaseCommit = Sha, Changes = [], Skip = 4, HasMore = false });

        using var json = JsonDocument.Parse(await Tools.GetCommitDiff("repo", "v1", "main", "tag", "branch", true, 2, 4, "project"));

        Assert.Equal(Sha, json.RootElement.GetProperty("baseCommit").GetString());
        Assert.False(json.RootElement.GetProperty("hasMore").GetBoolean());
        _service.VerifyAll();
    }

    [Fact]
    public async Task Commit_ReturnsParents()
    {
        _service.Setup(service => service.GetCommitAsync("repo", Sha, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitCommitDto { CommitId = Sha, Parents = [Sha] });

        using var json = JsonDocument.Parse(await Tools.GetCommit("repo", Sha));

        Assert.Equal(Sha, json.RootElement.GetProperty("parents")[0].GetString());
        _service.VerifyAll();
    }

    [Fact]
    public async Task FileAtVersion_ReturnsContentAndResolvedCommit()
    {
        _service.Setup(service => service.GetFileAtVersionAsync("repo", "/file.yml", "v1", "tag", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitFileContentDto { Path = "/file.yml", CommitId = Sha, Content = "ref: v1\n" });

        using var json = JsonDocument.Parse(await Tools.GetFileAtVersion("repo", "/file.yml", "v1", "tag"));

        Assert.Equal(Sha, json.RootElement.GetProperty("file").GetProperty("commitId").GetString());
        Assert.Equal("ref: v1\n", json.RootElement.GetProperty("file").GetProperty("content").GetString());
        _service.VerifyAll();
    }

    [Fact]
    public async Task FileAtVersion_NotFound_ReturnsError()
    {
        _service.Setup(service => service.GetFileAtVersionAsync("repo", "/missing", Sha, "commit", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GitFileContentDto?)null);
        using var json = JsonDocument.Parse(await Tools.GetFileAtVersion("repo", "/missing", Sha));
        Assert.Contains("not found", json.RootElement.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1001, 0)]
    [InlineData(5, -1)]
    [InlineData(5, int.MaxValue)]
    public async Task InvalidPagination_DoesNotInvokeService(int top, int skip)
    {
        using var json = JsonDocument.Parse(await Tools.GetCommits("repo", "main", top: top, skip: skip));
        Assert.True(json.RootElement.TryGetProperty("error", out _));
        _service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("main", "0")]
    [InlineData("main", "unknown")]
    [InlineData("", "branch")]
    [InlineData("short-sha", "commit")]
    public async Task InvalidVersion_DoesNotInvokeService(string version, string type)
    {
        using var json = JsonDocument.Parse(await Tools.GetCommits("repo", version, type));
        Assert.True(json.RootElement.TryGetProperty("error", out _));
        _service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EveryTool_RejectsEmptyRepository()
    {
        var results = await Task.WhenAll(Tools.GetRefs(""), Tools.GetCommits("", "main"), Tools.GetCommit("", Sha),
            Tools.GetCommitDiff("", Sha, Sha), Tools.GetFileAtVersion("", "/file", Sha));
        Assert.All(results, result => Assert.Contains("Repository name or ID is required", result));
        _service.VerifyNoOtherCalls();
    }
}
