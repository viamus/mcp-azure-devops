using System.Text.Json;
using Moq;
using Viamus.Azure.Devops.Mcp.Server.Models;
using Viamus.Azure.Devops.Mcp.Server.Services;
using Viamus.Azure.Devops.Mcp.Server.Tools;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Tools;

public class PullRequestToolsTests
{
    private readonly Mock<IAzureDevOpsService> _mockService;
    private readonly PullRequestTools _tools;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PullRequestToolsTests()
    {
        _mockService = new Mock<IAzureDevOpsService>();
        _tools = new PullRequestTools(_mockService.Object);
    }

    #region GetPullRequests Tests

    [Fact]
    public async Task GetPullRequests_ShouldReturnPullRequestList()
    {
        var pullRequests = new List<PullRequestDto>
        {
            new() { PullRequestId = 1, Title = "Feature A", Status = "Active", SourceBranch = "refs/heads/feature-a" },
            new() { PullRequestId = 2, Title = "Bug Fix", Status = "Completed", SourceBranch = "refs/heads/bugfix" }
        };

        _mockService
            .Setup(s => s.GetPullRequestsAsync("my-repo", null, null, null, null, null, null, 50, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pullRequests);

        var result = await _tools.GetPullRequests("my-repo");

        Assert.Contains("\"count\": 2", result);
        Assert.Contains("Feature A", result);
        Assert.Contains("Bug Fix", result);
    }

    [Fact]
    public async Task GetPullRequests_WithStatusFilter_ShouldPassStatusToService()
    {
        var pullRequests = new List<PullRequestDto>
        {
            new() { PullRequestId = 1, Title = "Active PR", Status = "Active" }
        };

        _mockService
            .Setup(s => s.GetPullRequestsAsync("repo", null, "active", null, null, null, null, 50, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pullRequests);

        await _tools.GetPullRequests("repo", status: "active");

        _mockService.Verify(s => s.GetPullRequestsAsync("repo", null, "active", null, null, null, null, 50, 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPullRequests_WithBranchFilters_ShouldPassBranchesToService()
    {
        var pullRequests = new List<PullRequestDto>();

        _mockService
            .Setup(s => s.GetPullRequestsAsync("repo", null, null, null, null, "refs/heads/feature", "refs/heads/main", 50, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pullRequests);

        await _tools.GetPullRequests("repo", sourceRefName: "refs/heads/feature", targetRefName: "refs/heads/main");

        _mockService.Verify(s => s.GetPullRequestsAsync("repo", null, null, null, null, "refs/heads/feature", "refs/heads/main", 50, 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPullRequests_WithEmptyRepoName_ShouldReturnError()
    {
        var result = await _tools.GetPullRequests("");

        Assert.Contains("error", result);
        Assert.Contains("Repository name or ID is required", result);
    }

    [Fact]
    public async Task GetPullRequests_WhenEmpty_ShouldReturnEmptyList()
    {
        _mockService
            .Setup(s => s.GetPullRequestsAsync("repo", null, null, null, null, null, null, 50, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestDto>());

        var result = await _tools.GetPullRequests("repo");

        Assert.Contains("\"count\": 0", result);
    }

    #endregion

    #region GetPullRequest Tests

    [Fact]
    public async Task GetPullRequest_WhenExists_ShouldReturnPullRequest()
    {
        var pullRequest = new PullRequestDto
        {
            PullRequestId = 123,
            Title = "Add new feature",
            Description = "This PR adds a new feature",
            SourceBranch = "refs/heads/feature",
            TargetBranch = "refs/heads/main",
            Status = "Active",
            CreatedBy = "John Doe",
            Reviewers = new List<PullRequestReviewerDto>
            {
                new() { DisplayName = "Alice", Vote = 10 }
            }
        };

        _mockService
            .Setup(s => s.GetPullRequestByIdAsync("repo", 123, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pullRequest);

        var result = await _tools.GetPullRequest("repo", 123);

        Assert.Contains("\"pullRequestId\": 123", result);
        Assert.Contains("Add new feature", result);
        Assert.Contains("refs/heads/feature", result);
        Assert.Contains("Alice", result);
    }

    [Fact]
    public async Task GetPullRequest_WhenNotFound_ShouldReturnError()
    {
        _mockService
            .Setup(s => s.GetPullRequestByIdAsync("repo", 999, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PullRequestDto?)null);

        var result = await _tools.GetPullRequest("repo", 999);

        Assert.Contains("error", result);
        Assert.Contains("not found", result);
    }

    [Fact]
    public async Task GetPullRequest_WithEmptyRepoName_ShouldReturnError()
    {
        var result = await _tools.GetPullRequest("", 123);

        Assert.Contains("error", result);
        Assert.Contains("Repository name or ID is required", result);
    }

    [Fact]
    public async Task GetPullRequest_WithInvalidId_ShouldReturnError()
    {
        var result = await _tools.GetPullRequest("repo", 0);

        Assert.Contains("error", result);
        Assert.Contains("Pull request ID must be a positive integer", result);
    }

    [Fact]
    public async Task GetPullRequest_WithProject_ShouldPassProjectToService()
    {
        var pullRequest = new PullRequestDto { PullRequestId = 123 };

        _mockService
            .Setup(s => s.GetPullRequestByIdAsync("repo", 123, "MyProject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pullRequest);

        await _tools.GetPullRequest("repo", 123, "MyProject");

        _mockService.Verify(s => s.GetPullRequestByIdAsync("repo", 123, "MyProject", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetPullRequestThreads Tests

    [Fact]
    public async Task GetPullRequestThreads_ShouldReturnThreads()
    {
        var threads = new List<PullRequestThreadDto>
        {
            new()
            {
                Id = 1,
                Status = "Active",
                FilePath = "/src/Program.cs",
                LineNumber = 42,
                Comments = new List<PullRequestCommentDto>
                {
                    new() { Id = 1, Content = "Please fix this", Author = "Alice" }
                }
            },
            new()
            {
                Id = 2,
                Status = "Fixed",
                FilePath = "/src/Service.cs",
                LineNumber = 100
            }
        };

        _mockService
            .Setup(s => s.GetPullRequestThreadsAsync("repo", 123, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(threads);

        var result = await _tools.GetPullRequestThreads("repo", 123);

        Assert.Contains("\"count\": 2", result);
        Assert.Contains("/src/Program.cs", result);
        Assert.Contains("Please fix this", result);
        Assert.Contains("\"status\": \"Fixed\"", result);
    }

    [Fact]
    public async Task GetPullRequestThreads_WithEmptyRepoName_ShouldReturnError()
    {
        var result = await _tools.GetPullRequestThreads("", 123);

        Assert.Contains("error", result);
        Assert.Contains("Repository name or ID is required", result);
    }

    [Fact]
    public async Task GetPullRequestThreads_WithInvalidPRId_ShouldReturnError()
    {
        var result = await _tools.GetPullRequestThreads("repo", -1);

        Assert.Contains("error", result);
        Assert.Contains("Pull request ID must be a positive integer", result);
    }

    [Fact]
    public async Task GetPullRequestThreads_WhenEmpty_ShouldReturnEmptyList()
    {
        _mockService
            .Setup(s => s.GetPullRequestThreadsAsync("repo", 123, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PullRequestThreadDto>());

        var result = await _tools.GetPullRequestThreads("repo", 123);

        Assert.Contains("\"count\": 0", result);
    }

    #endregion

    #region SearchPullRequests Tests

    [Fact]
    public async Task SearchPullRequests_ShouldReturnMatchingPRs()
    {
        var pullRequests = new List<PullRequestDto>
        {
            new() { PullRequestId = 1, Title = "Add authentication feature", Status = "Active" },
            new() { PullRequestId = 2, Title = "Fix auth bug", Status = "Completed" }
        };

        _mockService
            .Setup(s => s.SearchPullRequestsAsync("repo", "auth", null, null, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pullRequests);

        var result = await _tools.SearchPullRequests("repo", "auth");

        Assert.Contains("\"count\": 2", result);
        Assert.Contains("authentication", result);
        Assert.Contains("auth bug", result);
    }

    [Fact]
    public async Task SearchPullRequests_WithEmptyRepoName_ShouldReturnError()
    {
        var result = await _tools.SearchPullRequests("", "search");

        Assert.Contains("error", result);
        Assert.Contains("Repository name or ID is required", result);
    }

    [Fact]
    public async Task SearchPullRequests_WithEmptySearchText_ShouldReturnError()
    {
        var result = await _tools.SearchPullRequests("repo", "");

        Assert.Contains("error", result);
        Assert.Contains("Search text is required", result);
    }

    [Fact]
    public async Task SearchPullRequests_WithStatus_ShouldPassStatusToService()
    {
        var pullRequests = new List<PullRequestDto>();

        _mockService
            .Setup(s => s.SearchPullRequestsAsync("repo", "feature", null, "active", 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pullRequests);

        await _tools.SearchPullRequests("repo", "feature", status: "active");

        _mockService.Verify(s => s.SearchPullRequestsAsync("repo", "feature", null, "active", 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region QueryPullRequests Tests

    [Fact]
    public async Task QueryPullRequests_ShouldReturnFilteredResults()
    {
        var pullRequests = new List<PullRequestDto>
        {
            new() { PullRequestId = 1, Title = "PR 1", Status = "Active", TargetBranch = "refs/heads/main" }
        };

        _mockService
            .Setup(s => s.GetPullRequestsAsync("repo", null, "active", null, null, null, "refs/heads/main", 50, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pullRequests);

        var result = await _tools.QueryPullRequests("repo", status: "active", targetRefName: "refs/heads/main");

        Assert.Contains("\"count\": 1", result);
        Assert.Contains("\"filters\":", result);
        Assert.Contains("\"status\": \"active\"", result);
    }

    [Fact]
    public async Task QueryPullRequests_WithEmptyRepoName_ShouldReturnError()
    {
        var result = await _tools.QueryPullRequests("");

        Assert.Contains("error", result);
        Assert.Contains("Repository name or ID is required", result);
    }

    [Fact]
    public async Task QueryPullRequests_WithAllFilters_ShouldPassAllToService()
    {
        var pullRequests = new List<PullRequestDto>();

        _mockService
            .Setup(s => s.GetPullRequestsAsync(
                "repo", "MyProject", "active", "creator-guid", "reviewer-guid",
                "refs/heads/feature", "refs/heads/main", 25, 10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pullRequests);

        await _tools.QueryPullRequests(
            "repo",
            project: "MyProject",
            status: "active",
            creatorId: "creator-guid",
            reviewerId: "reviewer-guid",
            sourceRefName: "refs/heads/feature",
            targetRefName: "refs/heads/main",
            top: 25,
            skip: 10);

        _mockService.Verify(s => s.GetPullRequestsAsync(
            "repo", "MyProject", "active", "creator-guid", "reviewer-guid",
            "refs/heads/feature", "refs/heads/main", 25, 10,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
