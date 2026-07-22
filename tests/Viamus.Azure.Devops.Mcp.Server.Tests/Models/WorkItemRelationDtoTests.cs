using System.Text.Json;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Models;

public class WorkItemRelationDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void WorkItemRelationDto_ShouldSerializeToJson()
    {
        var relation = new WorkItemRelationDto
        {
            RelationType = "Related",
            RawRel = "System.LinkTypes.Related",
            TargetId = 456,
            TargetUrl = "https://dev.azure.com/org/proj/_apis/wit/workItems/456",
            Comment = "defect origin tracking",
            TargetSummary = new WorkItemSummaryDto
            {
                Id = 456,
                Title = "Original Bug",
                State = "Active",
                WorkItemType = "Bug"
            }
        };

        var json = JsonSerializer.Serialize(relation, JsonOptions);

        Assert.Contains("\"relationType\":\"Related\"", json);
        Assert.Contains("\"rawRel\":\"System.LinkTypes.Related\"", json);
        Assert.Contains("\"targetId\":456", json);
        Assert.Contains("\"comment\":\"defect origin tracking\"", json);
        Assert.Contains("\"targetSummary\":", json);
        Assert.Contains("\"title\":\"Original Bug\"", json);
    }

    [Fact]
    public void WorkItemRelationsResultDto_ShouldSerializeToJson()
    {
        var result = new WorkItemRelationsResultDto
        {
            WorkItemId = 123,
            Count = 1,
            Relations = new List<WorkItemRelationDto>
            {
                new()
                {
                    RelationType = "Parent",
                    RawRel = "System.LinkTypes.Hierarchy-Reverse",
                    TargetId = 789
                }
            }
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);

        Assert.Contains("\"workItemId\":123", json);
        Assert.Contains("\"count\":1", json);
        Assert.Contains("\"relations\":", json);
        Assert.Contains("\"relationType\":\"Parent\"", json);
    }

    [Fact]
    public void WorkItemTreeNodeDto_ShouldSerializeToJson()
    {
        var treeNode = new WorkItemTreeNodeDto
        {
            WorkItem = new WorkItemDto { Id = 1, Title = "Epic" },
            Children = new List<WorkItemTreeNodeDto>
            {
                new()
                {
                    WorkItem = new WorkItemDto { Id = 2, Title = "Feature" },
                    Children = []
                }
            }
        };

        var json = JsonSerializer.Serialize(treeNode, JsonOptions);

        Assert.Contains("\"workItem\":", json);
        Assert.Contains("\"title\":\"Epic\"", json);
        Assert.Contains("\"children\":", json);
        Assert.Contains("\"title\":\"Feature\"", json);
    }
}
