using System.Text.Json;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Models;

public class WorkItemDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void WorkItemDto_ShouldSerializeToJson()
    {
        var dto = new WorkItemDto
        {
            Id = 123,
            Title = "Test Bug",
            WorkItemType = "Bug",
            State = "Active",
            AssignedTo = "John Doe",
            Description = "Test description",
            AreaPath = "Project\\Team",
            IterationPath = "Project\\Sprint 1",
            Priority = "2",
            Severity = "2 - High",
            CreatedDate = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            ChangedDate = new DateTime(2024, 1, 20, 14, 45, 0, DateTimeKind.Utc),
            CreatedBy = "Jane Doe",
            ChangedBy = "John Doe",
            Reason = "New",
            ParentId = 100,
            Url = "https://dev.azure.com/org/project/_workitems/123"
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"id\":123", json);
        Assert.Contains("\"title\":\"Test Bug\"", json);
        Assert.Contains("\"workItemType\":\"Bug\"", json);
        Assert.Contains("\"state\":\"Active\"", json);
    }

    [Fact]
    public void WorkItemDto_ShouldDeserializeFromJson()
    {
        var json = """
        {
            "id": 456,
            "title": "Feature Request",
            "workItemType": "User Story",
            "state": "New",
            "assignedTo": "Alice",
            "priority": "1"
        }
        """;

        var dto = JsonSerializer.Deserialize<WorkItemDto>(json, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal(456, dto.Id);
        Assert.Equal("Feature Request", dto.Title);
        Assert.Equal("User Story", dto.WorkItemType);
        Assert.Equal("New", dto.State);
        Assert.Equal("Alice", dto.AssignedTo);
        Assert.Equal("1", dto.Priority);
    }

    [Fact]
    public void WorkItemDto_WithCustomFields_ShouldSerializeCorrectly()
    {
        var customFields = new Dictionary<string, object?>
        {
            { "Custom.Field1", "Value1" },
            { "Custom.Field2", 42 },
            { "Custom.Field3", true }
        };

        var dto = new WorkItemDto
        {
            Id = 789,
            Title = "With Custom Fields",
            CustomFields = customFields
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"customFields\"", json);
        Assert.Contains("Custom.Field1", json);
    }

    [Fact]
    public void WorkItemDto_RecordEquality_ShouldWork()
    {
        var dto1 = new WorkItemDto
        {
            Id = 123,
            Title = "Test",
            State = "Active"
        };

        var dto2 = new WorkItemDto
        {
            Id = 123,
            Title = "Test",
            State = "Active"
        };

        Assert.Equal(dto1, dto2);
    }

    [Fact]
    public void WorkItemDto_RecordInequality_ShouldWork()
    {
        var dto1 = new WorkItemDto { Id = 1, Title = "Test" };
        var dto2 = new WorkItemDto { Id = 2, Title = "Test" };

        Assert.NotEqual(dto1, dto2);
    }

    [Fact]
    public void WorkItemDto_WithRecord_ShouldSupportWith()
    {
        var original = new WorkItemDto
        {
            Id = 123,
            Title = "Original",
            State = "New"
        };

        var modified = original with { State = "Active" };

        Assert.Equal(123, modified.Id);
        Assert.Equal("Original", modified.Title);
        Assert.Equal("Active", modified.State);
        Assert.NotEqual(original, modified);
    }

    [Fact]
    public void WorkItemDto_NullableProperties_ShouldAllowNull()
    {
        var dto = new WorkItemDto
        {
            Id = 1,
            Title = null,
            Description = null,
            AssignedTo = null,
            ParentId = null,
            CustomFields = null
        };

        Assert.Null(dto.Title);
        Assert.Null(dto.Description);
        Assert.Null(dto.AssignedTo);
        Assert.Null(dto.ParentId);
        Assert.Null(dto.CustomFields);
    }
}
