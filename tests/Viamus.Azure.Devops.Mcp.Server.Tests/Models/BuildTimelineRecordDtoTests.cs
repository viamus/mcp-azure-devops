using System.Text.Json;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Models;

public class BuildTimelineRecordDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void BuildTimelineRecordDto_ShouldSerializeToJson()
    {
        var dto = new BuildTimelineRecordDto
        {
            Id = "record-123",
            ParentId = "parent-456",
            Type = "Stage",
            Name = "Build Stage",
            State = "Completed",
            Result = "Succeeded",
            Order = 1,
            StartTime = new DateTime(2024, 1, 15, 10, 0, 0),
            FinishTime = new DateTime(2024, 1, 15, 10, 5, 0),
            ErrorCount = 0,
            WarningCount = 2,
            LogUrl = "https://example.com/logs/record-123",
            PercentComplete = 100
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"id\":\"record-123\"", json);
        Assert.Contains("\"parentId\":\"parent-456\"", json);
        Assert.Contains("\"type\":\"Stage\"", json);
        Assert.Contains("\"name\":\"Build Stage\"", json);
        Assert.Contains("\"state\":\"Completed\"", json);
        Assert.Contains("\"result\":\"Succeeded\"", json);
        Assert.Contains("\"errorCount\":0", json);
        Assert.Contains("\"warningCount\":2", json);
    }

    [Fact]
    public void BuildTimelineRecordDto_ShouldDeserializeFromJson()
    {
        var json = """
        {
            "id": "task-789",
            "parentId": "job-456",
            "type": "Task",
            "name": "npm install",
            "state": "InProgress",
            "result": null,
            "order": 3,
            "errorCount": 0,
            "warningCount": 0,
            "percentComplete": 50
        }
        """;

        var dto = JsonSerializer.Deserialize<BuildTimelineRecordDto>(json, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal("task-789", dto.Id);
        Assert.Equal("job-456", dto.ParentId);
        Assert.Equal("Task", dto.Type);
        Assert.Equal("npm install", dto.Name);
        Assert.Equal("InProgress", dto.State);
        Assert.Null(dto.Result);
        Assert.Equal(3, dto.Order);
        Assert.Equal(50, dto.PercentComplete);
    }

    [Theory]
    [InlineData("Stage")]
    [InlineData("Job")]
    [InlineData("Task")]
    [InlineData("Phase")]
    public void BuildTimelineRecordDto_TypeValues_ShouldBeValid(string type)
    {
        var dto = new BuildTimelineRecordDto { Type = type };

        Assert.Equal(type, dto.Type);
    }

    [Fact]
    public void BuildTimelineRecordDto_HierarchicalStructure_ShouldWork()
    {
        var stage = new BuildTimelineRecordDto
        {
            Id = "stage-1",
            ParentId = null,
            Type = "Stage",
            Name = "Build"
        };

        var job = new BuildTimelineRecordDto
        {
            Id = "job-1",
            ParentId = "stage-1",
            Type = "Job",
            Name = "Build Job"
        };

        var task = new BuildTimelineRecordDto
        {
            Id = "task-1",
            ParentId = "job-1",
            Type = "Task",
            Name = "Compile"
        };

        Assert.Null(stage.ParentId);
        Assert.Equal("stage-1", job.ParentId);
        Assert.Equal("job-1", task.ParentId);
    }

    [Fact]
    public void BuildTimelineRecordDto_RecordEquality_ShouldWork()
    {
        var dto1 = new BuildTimelineRecordDto
        {
            Id = "123",
            Type = "Task",
            Name = "Build"
        };

        var dto2 = new BuildTimelineRecordDto
        {
            Id = "123",
            Type = "Task",
            Name = "Build"
        };

        Assert.Equal(dto1, dto2);
    }

    [Fact]
    public void BuildTimelineRecordDto_RecordInequality_ShouldWork()
    {
        var dto1 = new BuildTimelineRecordDto { Id = "1", Name = "Task 1" };
        var dto2 = new BuildTimelineRecordDto { Id = "2", Name = "Task 2" };

        Assert.NotEqual(dto1, dto2);
    }

    [Fact]
    public void BuildTimelineRecordDto_NullableProperties_ShouldAllowNull()
    {
        var dto = new BuildTimelineRecordDto
        {
            Id = null,
            ParentId = null,
            Type = null,
            Name = null,
            State = null,
            Result = null,
            StartTime = null,
            FinishTime = null,
            ErrorCount = null,
            WarningCount = null,
            LogUrl = null,
            PercentComplete = null
        };

        Assert.Null(dto.Id);
        Assert.Null(dto.ParentId);
        Assert.Null(dto.Type);
        Assert.Null(dto.Name);
        Assert.Null(dto.State);
        Assert.Null(dto.Result);
        Assert.Null(dto.StartTime);
        Assert.Null(dto.FinishTime);
        Assert.Null(dto.ErrorCount);
        Assert.Null(dto.WarningCount);
        Assert.Null(dto.LogUrl);
        Assert.Null(dto.PercentComplete);
    }
}
