using System.Text.Json;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Models;

public class WorkItemSummaryDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void WorkItemSummaryDto_ShouldSerializeToJson()
    {
        var dto = new WorkItemSummaryDto
        {
            Id = 123,
            Title = "Test Task",
            WorkItemType = "Task",
            State = "Active",
            AssignedTo = "John Doe",
            Priority = "2",
            ChangedDate = new DateTime(2024, 1, 20, 14, 45, 0, DateTimeKind.Utc),
            ParentId = 100
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"id\":123", json);
        Assert.Contains("\"title\":\"Test Task\"", json);
        Assert.Contains("\"workItemType\":\"Task\"", json);
        Assert.Contains("\"state\":\"Active\"", json);
        Assert.Contains("\"parentId\":100", json);
    }

    [Fact]
    public void WorkItemSummaryDto_ShouldDeserializeFromJson()
    {
        var json = """
        {
            "id": 456,
            "title": "Bug Fix",
            "workItemType": "Bug",
            "state": "Resolved",
            "assignedTo": "Alice",
            "priority": "1",
            "parentId": 200
        }
        """;

        var dto = JsonSerializer.Deserialize<WorkItemSummaryDto>(json, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal(456, dto.Id);
        Assert.Equal("Bug Fix", dto.Title);
        Assert.Equal("Bug", dto.WorkItemType);
        Assert.Equal("Resolved", dto.State);
        Assert.Equal("Alice", dto.AssignedTo);
        Assert.Equal("1", dto.Priority);
        Assert.Equal(200, dto.ParentId);
    }

    [Fact]
    public void WorkItemSummaryDto_RecordEquality_ShouldWork()
    {
        var dto1 = new WorkItemSummaryDto
        {
            Id = 123,
            Title = "Test",
            State = "Active"
        };

        var dto2 = new WorkItemSummaryDto
        {
            Id = 123,
            Title = "Test",
            State = "Active"
        };

        Assert.Equal(dto1, dto2);
    }

    [Fact]
    public void WorkItemSummaryDto_RecordInequality_ShouldWork()
    {
        var dto1 = new WorkItemSummaryDto { Id = 1, Title = "Test" };
        var dto2 = new WorkItemSummaryDto { Id = 2, Title = "Test" };

        Assert.NotEqual(dto1, dto2);
    }

    [Fact]
    public void WorkItemSummaryDto_WithRecord_ShouldSupportWith()
    {
        var original = new WorkItemSummaryDto
        {
            Id = 123,
            Title = "Original",
            State = "New"
        };

        var modified = original with { State = "Active", Priority = "1" };

        Assert.Equal(123, modified.Id);
        Assert.Equal("Original", modified.Title);
        Assert.Equal("Active", modified.State);
        Assert.Equal("1", modified.Priority);
        Assert.NotEqual(original, modified);
    }

    [Fact]
    public void WorkItemSummaryDto_NullableProperties_ShouldAllowNull()
    {
        var dto = new WorkItemSummaryDto
        {
            Id = 1,
            Title = null,
            WorkItemType = null,
            AssignedTo = null,
            Priority = null,
            ChangedDate = null,
            ParentId = null
        };

        Assert.Null(dto.Title);
        Assert.Null(dto.WorkItemType);
        Assert.Null(dto.AssignedTo);
        Assert.Null(dto.Priority);
        Assert.Null(dto.ChangedDate);
        Assert.Null(dto.ParentId);
    }

    [Fact]
    public void WorkItemSummaryDto_HasFewerPropertiesThanFullDto()
    {
        var summaryProperties = typeof(WorkItemSummaryDto).GetProperties();
        var fullProperties = typeof(WorkItemDto).GetProperties();

        Assert.True(summaryProperties.Length < fullProperties.Length);
    }

    [Fact]
    public void WorkItemSummaryDto_ContainsEssentialProperties()
    {
        var properties = typeof(WorkItemSummaryDto).GetProperties().Select(p => p.Name).ToList();

        Assert.Contains("Id", properties);
        Assert.Contains("Title", properties);
        Assert.Contains("WorkItemType", properties);
        Assert.Contains("State", properties);
        Assert.Contains("AssignedTo", properties);
        Assert.Contains("Priority", properties);
        Assert.Contains("ChangedDate", properties);
        Assert.Contains("ParentId", properties);
    }
}
