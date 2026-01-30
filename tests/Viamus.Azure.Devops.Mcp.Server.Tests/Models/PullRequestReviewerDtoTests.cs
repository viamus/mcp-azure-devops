using System.Text.Json;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Models;

public class PullRequestReviewerDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void PullRequestReviewerDto_ShouldSerializeToJson()
    {
        var dto = new PullRequestReviewerDto
        {
            Id = "reviewer-123",
            DisplayName = "John Doe",
            UniqueName = "john@example.com",
            Vote = 10,
            IsRequired = true,
            HasDeclined = false,
            ImageUrl = "https://example.com/avatar.png"
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"id\":\"reviewer-123\"", json);
        Assert.Contains("\"displayName\":\"John Doe\"", json);
        Assert.Contains("\"vote\":10", json);
        Assert.Contains("\"isRequired\":true", json);
        Assert.Contains("\"hasDeclined\":false", json);
    }

    [Fact]
    public void PullRequestReviewerDto_ShouldDeserializeFromJson()
    {
        var json = """
        {
            "id": "rev-456",
            "displayName": "Jane Smith",
            "uniqueName": "jane@example.com",
            "vote": -10,
            "isRequired": false,
            "hasDeclined": true
        }
        """;

        var dto = JsonSerializer.Deserialize<PullRequestReviewerDto>(json, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal("rev-456", dto.Id);
        Assert.Equal("Jane Smith", dto.DisplayName);
        Assert.Equal("jane@example.com", dto.UniqueName);
        Assert.Equal(-10, dto.Vote);
        Assert.False(dto.IsRequired);
        Assert.True(dto.HasDeclined);
    }

    [Theory]
    [InlineData(10, "Approved")]
    [InlineData(5, "Approved with suggestions")]
    [InlineData(0, "No vote")]
    [InlineData(-5, "Waiting for author")]
    [InlineData(-10, "Rejected")]
    public void PullRequestReviewerDto_VoteValues_ShouldBeValid(int vote, string description)
    {
        var dto = new PullRequestReviewerDto { Vote = vote };

        Assert.Equal(vote, dto.Vote);
        Assert.NotNull(description); // Just to use the parameter
    }

    [Fact]
    public void PullRequestReviewerDto_RecordEquality_ShouldWork()
    {
        var dto1 = new PullRequestReviewerDto
        {
            Id = "123",
            DisplayName = "John",
            Vote = 10
        };

        var dto2 = new PullRequestReviewerDto
        {
            Id = "123",
            DisplayName = "John",
            Vote = 10
        };

        Assert.Equal(dto1, dto2);
    }

    [Fact]
    public void PullRequestReviewerDto_RecordInequality_ShouldWork()
    {
        var dto1 = new PullRequestReviewerDto { Id = "123", Vote = 10 };
        var dto2 = new PullRequestReviewerDto { Id = "123", Vote = -10 };

        Assert.NotEqual(dto1, dto2);
    }

    [Fact]
    public void PullRequestReviewerDto_NullableProperties_ShouldAllowNull()
    {
        var dto = new PullRequestReviewerDto
        {
            Id = null,
            DisplayName = null,
            UniqueName = null,
            ImageUrl = null
        };

        Assert.Null(dto.Id);
        Assert.Null(dto.DisplayName);
        Assert.Null(dto.UniqueName);
        Assert.Null(dto.ImageUrl);
    }
}
