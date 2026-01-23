using System.Text.Json;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Models;

public class WorkItemCommentDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void WorkItemCommentDto_ShouldSerializeToJson()
    {
        var dto = new WorkItemCommentDto
        {
            Id = 1,
            WorkItemId = 123,
            Text = "This is a test comment",
            CreatedBy = "John Doe",
            CreatedDate = new DateTime(2024, 1, 20, 14, 45, 0, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"id\":1", json);
        Assert.Contains("\"workItemId\":123", json);
        Assert.Contains("\"text\":\"This is a test comment\"", json);
        Assert.Contains("\"createdBy\":\"John Doe\"", json);
    }

    [Fact]
    public void WorkItemCommentDto_ShouldDeserializeFromJson()
    {
        var json = """
        {
            "id": 5,
            "workItemId": 456,
            "text": "Comment from JSON",
            "createdBy": "Alice",
            "createdDate": "2024-01-20T10:30:00Z"
        }
        """;

        var dto = JsonSerializer.Deserialize<WorkItemCommentDto>(json, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal(5, dto.Id);
        Assert.Equal(456, dto.WorkItemId);
        Assert.Equal("Comment from JSON", dto.Text);
        Assert.Equal("Alice", dto.CreatedBy);
        Assert.NotNull(dto.CreatedDate);
    }

    [Fact]
    public void WorkItemCommentDto_RecordEquality_ShouldWork()
    {
        var dto1 = new WorkItemCommentDto
        {
            Id = 1,
            WorkItemId = 123,
            Text = "Test comment"
        };

        var dto2 = new WorkItemCommentDto
        {
            Id = 1,
            WorkItemId = 123,
            Text = "Test comment"
        };

        Assert.Equal(dto1, dto2);
    }

    [Fact]
    public void WorkItemCommentDto_RecordInequality_ShouldWork()
    {
        var dto1 = new WorkItemCommentDto { Id = 1, WorkItemId = 123, Text = "Comment 1" };
        var dto2 = new WorkItemCommentDto { Id = 2, WorkItemId = 123, Text = "Comment 2" };

        Assert.NotEqual(dto1, dto2);
    }

    [Fact]
    public void WorkItemCommentDto_WithRecord_ShouldSupportWith()
    {
        var original = new WorkItemCommentDto
        {
            Id = 1,
            WorkItemId = 123,
            Text = "Original comment"
        };

        var modified = original with { Text = "Modified comment" };

        Assert.Equal(1, modified.Id);
        Assert.Equal(123, modified.WorkItemId);
        Assert.Equal("Modified comment", modified.Text);
        Assert.NotEqual(original, modified);
    }

    [Fact]
    public void WorkItemCommentDto_NullableProperties_ShouldAllowNull()
    {
        var dto = new WorkItemCommentDto
        {
            Id = 1,
            WorkItemId = 123,
            Text = null,
            CreatedBy = null,
            CreatedDate = null
        };

        Assert.Null(dto.Text);
        Assert.Null(dto.CreatedBy);
        Assert.Null(dto.CreatedDate);
    }

    [Fact]
    public void WorkItemCommentDto_ContainsExpectedProperties()
    {
        var properties = typeof(WorkItemCommentDto).GetProperties().Select(p => p.Name).ToList();

        Assert.Contains("Id", properties);
        Assert.Contains("WorkItemId", properties);
        Assert.Contains("Text", properties);
        Assert.Contains("CreatedBy", properties);
        Assert.Contains("CreatedDate", properties);
        Assert.Equal(5, properties.Count);
    }
}
