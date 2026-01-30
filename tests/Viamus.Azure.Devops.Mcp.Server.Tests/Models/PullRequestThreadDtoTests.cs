using System.Text.Json;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Models;

public class PullRequestThreadDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void PullRequestThreadDto_ShouldSerializeToJson()
    {
        var dto = new PullRequestThreadDto
        {
            Id = 123,
            Status = "Active",
            FilePath = "/src/Program.cs",
            LineNumber = 42,
            PublishedDate = new DateTime(2024, 1, 15, 10, 30, 0),
            LastUpdatedDate = new DateTime(2024, 1, 16, 14, 0, 0)
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"id\":123", json);
        Assert.Contains("\"status\":\"Active\"", json);
        Assert.Contains("\"filePath\":\"/src/Program.cs\"", json);
        Assert.Contains("\"lineNumber\":42", json);
    }

    [Fact]
    public void PullRequestThreadDto_ShouldDeserializeFromJson()
    {
        var json = """
        {
            "id": 456,
            "status": "Fixed",
            "filePath": "/tests/Test.cs",
            "lineNumber": 100
        }
        """;

        var dto = JsonSerializer.Deserialize<PullRequestThreadDto>(json, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal(456, dto.Id);
        Assert.Equal("Fixed", dto.Status);
        Assert.Equal("/tests/Test.cs", dto.FilePath);
        Assert.Equal(100, dto.LineNumber);
    }

    [Fact]
    public void PullRequestThreadDto_WithComments_ShouldSerializeCorrectly()
    {
        var dto = new PullRequestThreadDto
        {
            Id = 789,
            Status = "Active",
            Comments = new List<PullRequestCommentDto>
            {
                new() { Id = 1, Content = "Please fix this", Author = "Alice" },
                new() { Id = 2, Content = "Done!", Author = "Bob", ParentCommentId = 1 }
            }
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"comments\":", json);
        Assert.Contains("\"content\":\"Please fix this\"", json);
        Assert.Contains("\"content\":\"Done!\"", json);
        Assert.Contains("\"parentCommentId\":1", json);
    }

    [Fact]
    public void PullRequestThreadDto_RecordEquality_ShouldWork()
    {
        var dto1 = new PullRequestThreadDto
        {
            Id = 123,
            Status = "Active",
            FilePath = "/file.cs"
        };

        var dto2 = new PullRequestThreadDto
        {
            Id = 123,
            Status = "Active",
            FilePath = "/file.cs"
        };

        Assert.Equal(dto1, dto2);
    }

    [Fact]
    public void PullRequestThreadDto_NullableProperties_ShouldAllowNull()
    {
        var dto = new PullRequestThreadDto
        {
            Id = 1,
            Status = null,
            FilePath = null,
            LineNumber = null,
            Comments = null
        };

        Assert.Null(dto.Status);
        Assert.Null(dto.FilePath);
        Assert.Null(dto.LineNumber);
        Assert.Null(dto.Comments);
    }
}
