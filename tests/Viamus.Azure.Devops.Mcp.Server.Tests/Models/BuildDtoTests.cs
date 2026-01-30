using System.Text.Json;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Models;

public class BuildDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void BuildDto_ShouldSerializeToJson()
    {
        var dto = new BuildDto
        {
            Id = 12345,
            BuildNumber = "20240115.1",
            Status = "Completed",
            Result = "Succeeded",
            SourceBranch = "refs/heads/main",
            SourceVersion = "abc123def456",
            RequestedBy = "John Doe",
            RequestedFor = "Jane Smith",
            QueueTime = new DateTime(2024, 1, 15, 10, 0, 0),
            StartTime = new DateTime(2024, 1, 15, 10, 1, 0),
            FinishTime = new DateTime(2024, 1, 15, 10, 15, 0),
            DefinitionId = 123,
            DefinitionName = "CI-Build",
            ProjectId = "project-123",
            ProjectName = "MyProject",
            Url = "https://dev.azure.com/org/project/_apis/build/builds/12345",
            LogsUrl = "https://dev.azure.com/org/project/_apis/build/builds/12345/logs",
            Reason = "Manual",
            Priority = "Normal"
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"id\":12345", json);
        Assert.Contains("\"buildNumber\":\"20240115.1\"", json);
        Assert.Contains("\"status\":\"Completed\"", json);
        Assert.Contains("\"result\":\"Succeeded\"", json);
        Assert.Contains("\"sourceBranch\":\"refs/heads/main\"", json);
        Assert.Contains("\"reason\":\"Manual\"", json);
    }

    [Fact]
    public void BuildDto_ShouldDeserializeFromJson()
    {
        var json = """
        {
            "id": 67890,
            "buildNumber": "20240116.5",
            "status": "InProgress",
            "result": null,
            "sourceBranch": "refs/heads/feature",
            "sourceVersion": "xyz789",
            "definitionId": 456,
            "definitionName": "PR-Build",
            "reason": "PullRequest"
        }
        """;

        var dto = JsonSerializer.Deserialize<BuildDto>(json, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal(67890, dto.Id);
        Assert.Equal("20240116.5", dto.BuildNumber);
        Assert.Equal("InProgress", dto.Status);
        Assert.Null(dto.Result);
        Assert.Equal("refs/heads/feature", dto.SourceBranch);
        Assert.Equal("xyz789", dto.SourceVersion);
        Assert.Equal(456, dto.DefinitionId);
        Assert.Equal("PR-Build", dto.DefinitionName);
        Assert.Equal("PullRequest", dto.Reason);
    }

    [Fact]
    public void BuildDto_RecordEquality_ShouldWork()
    {
        var dto1 = new BuildDto
        {
            Id = 123,
            BuildNumber = "20240115.1",
            Status = "Completed"
        };

        var dto2 = new BuildDto
        {
            Id = 123,
            BuildNumber = "20240115.1",
            Status = "Completed"
        };

        Assert.Equal(dto1, dto2);
    }

    [Fact]
    public void BuildDto_RecordInequality_ShouldWork()
    {
        var dto1 = new BuildDto { Id = 123, BuildNumber = "1" };
        var dto2 = new BuildDto { Id = 456, BuildNumber = "2" };

        Assert.NotEqual(dto1, dto2);
    }

    [Fact]
    public void BuildDto_WithRecord_ShouldSupportWith()
    {
        var original = new BuildDto
        {
            Id = 123,
            BuildNumber = "1",
            Status = "InProgress"
        };

        var modified = original with { Status = "Completed", Result = "Succeeded" };

        Assert.Equal(123, modified.Id);
        Assert.Equal("1", modified.BuildNumber);
        Assert.Equal("Completed", modified.Status);
        Assert.Equal("Succeeded", modified.Result);
    }

    [Fact]
    public void BuildDto_NullableProperties_ShouldAllowNull()
    {
        var dto = new BuildDto
        {
            Id = 1,
            BuildNumber = null,
            Status = null,
            Result = null,
            SourceBranch = null,
            RequestedBy = null,
            DefinitionId = null,
            LogsUrl = null
        };

        Assert.Null(dto.BuildNumber);
        Assert.Null(dto.Status);
        Assert.Null(dto.Result);
        Assert.Null(dto.SourceBranch);
        Assert.Null(dto.RequestedBy);
        Assert.Null(dto.DefinitionId);
        Assert.Null(dto.LogsUrl);
    }
}
