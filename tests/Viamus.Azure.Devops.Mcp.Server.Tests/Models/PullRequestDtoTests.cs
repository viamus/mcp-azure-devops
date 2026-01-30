using System.Text.Json;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Models;

public class PullRequestDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void PullRequestDto_ShouldSerializeToJson()
    {
        var dto = new PullRequestDto
        {
            PullRequestId = 123,
            Title = "Add new feature",
            Description = "This PR adds a new feature",
            SourceBranch = "refs/heads/feature-branch",
            TargetBranch = "refs/heads/main",
            Status = "Active",
            CreatedBy = "John Doe",
            CreationDate = new DateTime(2024, 1, 15, 10, 30, 0),
            MergeStatus = "Succeeded",
            IsDraft = false,
            RepositoryName = "my-repo",
            RepositoryId = "repo-123",
            ProjectName = "MyProject",
            Url = "https://dev.azure.com/org/project/_git/my-repo/pullrequest/123"
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"pullRequestId\":123", json);
        Assert.Contains("\"title\":\"Add new feature\"", json);
        Assert.Contains("\"sourceBranch\":\"refs/heads/feature-branch\"", json);
        Assert.Contains("\"targetBranch\":\"refs/heads/main\"", json);
        Assert.Contains("\"status\":\"Active\"", json);
        Assert.Contains("\"isDraft\":false", json);
    }

    [Fact]
    public void PullRequestDto_ShouldDeserializeFromJson()
    {
        var json = """
        {
            "pullRequestId": 456,
            "title": "Bug fix",
            "description": "Fixes critical bug",
            "sourceBranch": "refs/heads/bugfix",
            "targetBranch": "refs/heads/develop",
            "status": "Completed",
            "isDraft": true
        }
        """;

        var dto = JsonSerializer.Deserialize<PullRequestDto>(json, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal(456, dto.PullRequestId);
        Assert.Equal("Bug fix", dto.Title);
        Assert.Equal("Fixes critical bug", dto.Description);
        Assert.Equal("refs/heads/bugfix", dto.SourceBranch);
        Assert.Equal("refs/heads/develop", dto.TargetBranch);
        Assert.Equal("Completed", dto.Status);
        Assert.True(dto.IsDraft);
    }

    [Fact]
    public void PullRequestDto_WithReviewers_ShouldSerializeCorrectly()
    {
        var dto = new PullRequestDto
        {
            PullRequestId = 789,
            Title = "Feature with reviewers",
            Reviewers = new List<PullRequestReviewerDto>
            {
                new() { DisplayName = "Alice", Vote = 10, IsRequired = true },
                new() { DisplayName = "Bob", Vote = 0, IsRequired = false }
            }
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"reviewers\":", json);
        Assert.Contains("\"displayName\":\"Alice\"", json);
        Assert.Contains("\"vote\":10", json);
        Assert.Contains("\"displayName\":\"Bob\"", json);
    }

    [Fact]
    public void PullRequestDto_RecordEquality_ShouldWork()
    {
        var dto1 = new PullRequestDto
        {
            PullRequestId = 123,
            Title = "Test PR",
            SourceBranch = "feature"
        };

        var dto2 = new PullRequestDto
        {
            PullRequestId = 123,
            Title = "Test PR",
            SourceBranch = "feature"
        };

        Assert.Equal(dto1, dto2);
    }

    [Fact]
    public void PullRequestDto_RecordInequality_ShouldWork()
    {
        var dto1 = new PullRequestDto { PullRequestId = 123, Title = "PR 1" };
        var dto2 = new PullRequestDto { PullRequestId = 456, Title = "PR 2" };

        Assert.NotEqual(dto1, dto2);
    }

    [Fact]
    public void PullRequestDto_WithRecord_ShouldSupportWith()
    {
        var original = new PullRequestDto
        {
            PullRequestId = 123,
            Title = "Original Title",
            Status = "Active"
        };

        var modified = original with { Status = "Completed" };

        Assert.Equal(123, modified.PullRequestId);
        Assert.Equal("Original Title", modified.Title);
        Assert.Equal("Completed", modified.Status);
        Assert.NotEqual(original, modified);
    }

    [Fact]
    public void PullRequestDto_NullableProperties_ShouldAllowNull()
    {
        var dto = new PullRequestDto
        {
            PullRequestId = 1,
            Title = null,
            Description = null,
            SourceBranch = null,
            CreatedBy = null,
            Reviewers = null
        };

        Assert.Null(dto.Title);
        Assert.Null(dto.Description);
        Assert.Null(dto.SourceBranch);
        Assert.Null(dto.CreatedBy);
        Assert.Null(dto.Reviewers);
    }
}
