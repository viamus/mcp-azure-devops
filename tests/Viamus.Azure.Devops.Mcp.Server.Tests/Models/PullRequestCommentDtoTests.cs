using System.Text.Json;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Models;

public class PullRequestCommentDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void PullRequestCommentDto_ShouldSerializeToJson()
    {
        var dto = new PullRequestCommentDto
        {
            Id = 123,
            ParentCommentId = null,
            Content = "This is a great improvement!",
            Author = "John Doe",
            PublishedDate = new DateTime(2024, 1, 15, 10, 30, 0),
            LastUpdatedDate = new DateTime(2024, 1, 15, 11, 0, 0),
            CommentType = "Text"
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"id\":123", json);
        Assert.Contains("\"content\":\"This is a great improvement!\"", json);
        Assert.Contains("\"author\":\"John Doe\"", json);
        Assert.Contains("\"commentType\":\"Text\"", json);
    }

    [Fact]
    public void PullRequestCommentDto_ShouldDeserializeFromJson()
    {
        var json = """
        {
            "id": 456,
            "parentCommentId": 123,
            "content": "I agree with the above",
            "author": "Jane Smith",
            "commentType": "Text"
        }
        """;

        var dto = JsonSerializer.Deserialize<PullRequestCommentDto>(json, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal(456, dto.Id);
        Assert.Equal(123, dto.ParentCommentId);
        Assert.Equal("I agree with the above", dto.Content);
        Assert.Equal("Jane Smith", dto.Author);
        Assert.Equal("Text", dto.CommentType);
    }

    [Fact]
    public void PullRequestCommentDto_WithParentComment_ShouldRepresentReply()
    {
        var parentComment = new PullRequestCommentDto
        {
            Id = 1,
            ParentCommentId = null,
            Content = "Original comment"
        };

        var replyComment = new PullRequestCommentDto
        {
            Id = 2,
            ParentCommentId = 1,
            Content = "Reply to original"
        };

        Assert.Null(parentComment.ParentCommentId);
        Assert.Equal(1, replyComment.ParentCommentId);
    }

    [Fact]
    public void PullRequestCommentDto_RecordEquality_ShouldWork()
    {
        var dto1 = new PullRequestCommentDto
        {
            Id = 123,
            Content = "Test comment",
            Author = "User"
        };

        var dto2 = new PullRequestCommentDto
        {
            Id = 123,
            Content = "Test comment",
            Author = "User"
        };

        Assert.Equal(dto1, dto2);
    }

    [Fact]
    public void PullRequestCommentDto_RecordInequality_ShouldWork()
    {
        var dto1 = new PullRequestCommentDto { Id = 1, Content = "Comment 1" };
        var dto2 = new PullRequestCommentDto { Id = 2, Content = "Comment 2" };

        Assert.NotEqual(dto1, dto2);
    }

    [Fact]
    public void PullRequestCommentDto_NullableProperties_ShouldAllowNull()
    {
        var dto = new PullRequestCommentDto
        {
            Id = 1,
            ParentCommentId = null,
            Content = null,
            Author = null,
            CommentType = null
        };

        Assert.Null(dto.ParentCommentId);
        Assert.Null(dto.Content);
        Assert.Null(dto.Author);
        Assert.Null(dto.CommentType);
    }
}
