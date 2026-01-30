using System.Text.Json;
using Moq;
using Viamus.Azure.Devops.Mcp.Server.Models;
using Viamus.Azure.Devops.Mcp.Server.Services;
using Viamus.Azure.Devops.Mcp.Server.Tools;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Tools;

public class PipelineToolsTests
{
    private readonly Mock<IAzureDevOpsService> _mockService;
    private readonly PipelineTools _tools;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PipelineToolsTests()
    {
        _mockService = new Mock<IAzureDevOpsService>();
        _tools = new PipelineTools(_mockService.Object);
    }

    #region GetPipelines Tests

    [Fact]
    public async Task GetPipelines_ShouldReturnPipelineList()
    {
        var pipelines = new List<PipelineDto>
        {
            new() { Id = 1, Name = "CI-Build", Folder = "\\Builds", QueueStatus = "Enabled" },
            new() { Id = 2, Name = "CD-Deploy", Folder = "\\Deploys", QueueStatus = "Enabled" }
        };

        _mockService
            .Setup(s => s.GetPipelinesAsync(null, null, null, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipelines);

        var result = await _tools.GetPipelines();

        Assert.Contains("\"count\": 2", result);
        Assert.Contains("CI-Build", result);
        Assert.Contains("CD-Deploy", result);
    }

    [Fact]
    public async Task GetPipelines_WithNameFilter_ShouldPassNameToService()
    {
        var pipelines = new List<PipelineDto>
        {
            new() { Id = 1, Name = "CI-Build" }
        };

        _mockService
            .Setup(s => s.GetPipelinesAsync(null, "CI*", null, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipelines);

        await _tools.GetPipelines(name: "CI*");

        _mockService.Verify(s => s.GetPipelinesAsync(null, "CI*", null, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPipelines_WithFolderFilter_ShouldPassFolderToService()
    {
        var pipelines = new List<PipelineDto>();

        _mockService
            .Setup(s => s.GetPipelinesAsync(null, null, "\\Builds", 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipelines);

        await _tools.GetPipelines(folder: "\\Builds");

        _mockService.Verify(s => s.GetPipelinesAsync(null, null, "\\Builds", 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPipelines_WhenEmpty_ShouldReturnEmptyList()
    {
        _mockService
            .Setup(s => s.GetPipelinesAsync(null, null, null, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PipelineDto>());

        var result = await _tools.GetPipelines();

        Assert.Contains("\"count\": 0", result);
    }

    #endregion

    #region GetPipeline Tests

    [Fact]
    public async Task GetPipeline_WhenExists_ShouldReturnPipeline()
    {
        var pipeline = new PipelineDto
        {
            Id = 123,
            Name = "CI-Build",
            Folder = "\\Builds\\Production",
            QueueStatus = "Enabled",
            Revision = 5
        };

        _mockService
            .Setup(s => s.GetPipelineAsync(123, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipeline);

        var result = await _tools.GetPipeline(123);

        Assert.Contains("\"id\": 123", result);
        Assert.Contains("CI-Build", result);
        Assert.Contains("\"queueStatus\": \"Enabled\"", result);
    }

    [Fact]
    public async Task GetPipeline_WhenNotFound_ShouldReturnError()
    {
        _mockService
            .Setup(s => s.GetPipelineAsync(999, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PipelineDto?)null);

        var result = await _tools.GetPipeline(999);

        Assert.Contains("error", result);
        Assert.Contains("not found", result);
    }

    [Fact]
    public async Task GetPipeline_WithInvalidId_ShouldReturnError()
    {
        var result = await _tools.GetPipeline(0);

        Assert.Contains("error", result);
        Assert.Contains("Pipeline ID must be a positive integer", result);
    }

    [Fact]
    public async Task GetPipeline_WithProject_ShouldPassProjectToService()
    {
        var pipeline = new PipelineDto { Id = 123 };

        _mockService
            .Setup(s => s.GetPipelineAsync(123, "MyProject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipeline);

        await _tools.GetPipeline(123, "MyProject");

        _mockService.Verify(s => s.GetPipelineAsync(123, "MyProject", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetPipelineRuns Tests

    [Fact]
    public async Task GetPipelineRuns_ShouldReturnBuilds()
    {
        var builds = new List<BuildDto>
        {
            new() { Id = 1, BuildNumber = "20240115.1", Status = "Completed", Result = "Succeeded" },
            new() { Id = 2, BuildNumber = "20240115.2", Status = "InProgress", Result = null }
        };

        _mockService
            .Setup(s => s.GetBuildsAsync(null, It.Is<IEnumerable<int>>(d => d.Contains(123)), null, null, null, null, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(builds);

        var result = await _tools.GetPipelineRuns(123);

        Assert.Contains("\"count\": 2", result);
        Assert.Contains("20240115.1", result);
        Assert.Contains("\"result\": \"Succeeded\"", result);
    }

    [Fact]
    public async Task GetPipelineRuns_WithInvalidId_ShouldReturnError()
    {
        var result = await _tools.GetPipelineRuns(-1);

        Assert.Contains("error", result);
        Assert.Contains("Pipeline ID must be a positive integer", result);
    }

    [Fact]
    public async Task GetPipelineRuns_WithFilters_ShouldPassFiltersToService()
    {
        var builds = new List<BuildDto>();

        _mockService
            .Setup(s => s.GetBuildsAsync(null, It.IsAny<IEnumerable<int>>(), "refs/heads/main", "completed", "failed", null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(builds);

        await _tools.GetPipelineRuns(123, branchName: "refs/heads/main", statusFilter: "completed", resultFilter: "failed", top: 10);

        _mockService.Verify(s => s.GetBuildsAsync(null, It.IsAny<IEnumerable<int>>(), "refs/heads/main", "completed", "failed", null, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetBuild Tests

    [Fact]
    public async Task GetBuild_WhenExists_ShouldReturnBuild()
    {
        var build = new BuildDto
        {
            Id = 12345,
            BuildNumber = "20240115.1",
            Status = "Completed",
            Result = "Succeeded",
            SourceBranch = "refs/heads/main",
            RequestedBy = "John Doe",
            DefinitionName = "CI-Build"
        };

        _mockService
            .Setup(s => s.GetBuildAsync(12345, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(build);

        var result = await _tools.GetBuild(12345);

        Assert.Contains("\"id\": 12345", result);
        Assert.Contains("20240115.1", result);
        Assert.Contains("\"result\": \"Succeeded\"", result);
        Assert.Contains("John Doe", result);
    }

    [Fact]
    public async Task GetBuild_WhenNotFound_ShouldReturnError()
    {
        _mockService
            .Setup(s => s.GetBuildAsync(99999, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BuildDto?)null);

        var result = await _tools.GetBuild(99999);

        Assert.Contains("error", result);
        Assert.Contains("not found", result);
    }

    [Fact]
    public async Task GetBuild_WithInvalidId_ShouldReturnError()
    {
        var result = await _tools.GetBuild(0);

        Assert.Contains("error", result);
        Assert.Contains("Build ID must be a positive integer", result);
    }

    #endregion

    #region GetBuilds Tests

    [Fact]
    public async Task GetBuilds_ShouldReturnBuildList()
    {
        var builds = new List<BuildDto>
        {
            new() { Id = 1, BuildNumber = "1", Status = "Completed", Result = "Succeeded" },
            new() { Id = 2, BuildNumber = "2", Status = "Completed", Result = "Failed" }
        };

        _mockService
            .Setup(s => s.GetBuildsAsync(null, null, null, null, null, null, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(builds);

        var result = await _tools.GetBuilds();

        Assert.Contains("\"count\": 2", result);
    }

    [Fact]
    public async Task GetBuilds_WithDefinitions_ShouldParseAndPassDefinitions()
    {
        var builds = new List<BuildDto>();

        _mockService
            .Setup(s => s.GetBuildsAsync(null, It.Is<IEnumerable<int>>(d => d.Contains(1) && d.Contains(2) && d.Contains(3)), null, null, null, null, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(builds);

        await _tools.GetBuilds(definitions: "1,2,3");

        _mockService.Verify(s => s.GetBuildsAsync(null, It.Is<IEnumerable<int>>(d => d.Count() == 3), null, null, null, null, 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetBuilds_WithAllFilters_ShouldPassFiltersToService()
    {
        var builds = new List<BuildDto>();

        _mockService
            .Setup(s => s.GetBuildsAsync("MyProject", It.IsAny<IEnumerable<int>>(), "refs/heads/main", "completed", "failed", "john@example.com", 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(builds);

        await _tools.GetBuilds(
            project: "MyProject",
            definitions: "123",
            branchName: "refs/heads/main",
            statusFilter: "completed",
            resultFilter: "failed",
            requestedFor: "john@example.com",
            top: 25);

        _mockService.Verify(s => s.GetBuildsAsync("MyProject", It.IsAny<IEnumerable<int>>(), "refs/heads/main", "completed", "failed", "john@example.com", 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetBuildLogs Tests

    [Fact]
    public async Task GetBuildLogs_ShouldReturnLogList()
    {
        var logs = new List<BuildLogDto>
        {
            new() { Id = 1, Type = "Container", LineCount = 100, Url = "https://example.com/logs/1" },
            new() { Id = 2, Type = "TaskLog", LineCount = 500, Url = "https://example.com/logs/2" }
        };

        _mockService
            .Setup(s => s.GetBuildLogsAsync(12345, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var result = await _tools.GetBuildLogs(12345);

        Assert.Contains("\"count\": 2", result);
        Assert.Contains("\"lineCount\": 100", result);
        Assert.Contains("\"lineCount\": 500", result);
    }

    [Fact]
    public async Task GetBuildLogs_WithInvalidBuildId_ShouldReturnError()
    {
        var result = await _tools.GetBuildLogs(0);

        Assert.Contains("error", result);
        Assert.Contains("Build ID must be a positive integer", result);
    }

    #endregion

    #region GetBuildLogContent Tests

    [Fact]
    public async Task GetBuildLogContent_ShouldReturnLogContent()
    {
        var logContent = "Step 1: Restore packages\nStep 2: Build\nStep 3: Test\nBuild succeeded.";

        _mockService
            .Setup(s => s.GetBuildLogContentAsync(12345, 1, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logContent);

        var result = await _tools.GetBuildLogContent(12345, 1);

        Assert.Contains("Step 1: Restore packages", result);
        Assert.Contains("Build succeeded", result);
    }

    [Fact]
    public async Task GetBuildLogContent_WhenNotFound_ShouldReturnError()
    {
        _mockService
            .Setup(s => s.GetBuildLogContentAsync(12345, 999, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _tools.GetBuildLogContent(12345, 999);

        Assert.Contains("error", result);
        Assert.Contains("not found", result);
    }

    [Fact]
    public async Task GetBuildLogContent_WithInvalidBuildId_ShouldReturnError()
    {
        var result = await _tools.GetBuildLogContent(0, 1);

        Assert.Contains("error", result);
        Assert.Contains("Build ID must be a positive integer", result);
    }

    [Fact]
    public async Task GetBuildLogContent_WithInvalidLogId_ShouldReturnError()
    {
        var result = await _tools.GetBuildLogContent(123, 0);

        Assert.Contains("error", result);
        Assert.Contains("Log ID must be a positive integer", result);
    }

    #endregion

    #region GetBuildTimeline Tests

    [Fact]
    public async Task GetBuildTimeline_ShouldReturnTimelineRecords()
    {
        var records = new List<BuildTimelineRecordDto>
        {
            new() { Id = "stage-1", Type = "Stage", Name = "Build", State = "Completed", Result = "Succeeded" },
            new() { Id = "job-1", ParentId = "stage-1", Type = "Job", Name = "Build Job", State = "Completed" },
            new() { Id = "task-1", ParentId = "job-1", Type = "Task", Name = "Restore", State = "Completed" },
            new() { Id = "task-2", ParentId = "job-1", Type = "Task", Name = "Build", State = "Completed" }
        };

        _mockService
            .Setup(s => s.GetBuildTimelineAsync(12345, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

        var result = await _tools.GetBuildTimeline(12345);

        Assert.Contains("\"stageCount\": 1", result);
        Assert.Contains("\"jobCount\": 1", result);
        Assert.Contains("\"taskCount\": 2", result);
        Assert.Contains("\"totalRecords\": 4", result);
    }

    [Fact]
    public async Task GetBuildTimeline_WithInvalidBuildId_ShouldReturnError()
    {
        var result = await _tools.GetBuildTimeline(-1);

        Assert.Contains("error", result);
        Assert.Contains("Build ID must be a positive integer", result);
    }

    [Fact]
    public async Task GetBuildTimeline_WhenEmpty_ShouldReturnEmptySummary()
    {
        _mockService
            .Setup(s => s.GetBuildTimelineAsync(12345, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BuildTimelineRecordDto>());

        var result = await _tools.GetBuildTimeline(12345);

        Assert.Contains("\"stageCount\": 0", result);
        Assert.Contains("\"jobCount\": 0", result);
        Assert.Contains("\"taskCount\": 0", result);
    }

    #endregion

    #region QueryBuilds Tests

    [Fact]
    public async Task QueryBuilds_ShouldReturnFilteredResults()
    {
        var builds = new List<BuildDto>
        {
            new() { Id = 1, BuildNumber = "1", Status = "Completed", Result = "Failed" }
        };

        _mockService
            .Setup(s => s.GetBuildsAsync(null, null, "refs/heads/main", "completed", "failed", null, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(builds);

        var result = await _tools.QueryBuilds(branchName: "refs/heads/main", statusFilter: "completed", resultFilter: "failed");

        Assert.Contains("\"count\": 1", result);
        Assert.Contains("\"query\":", result);
        Assert.Contains("\"branchName\": \"refs/heads/main\"", result);
    }

    [Fact]
    public async Task QueryBuilds_WithAllFilters_ShouldPassAllToService()
    {
        var builds = new List<BuildDto>();

        _mockService
            .Setup(s => s.GetBuildsAsync("MyProject", It.IsAny<IEnumerable<int>>(), "refs/heads/main", "completed", "succeeded", "john@example.com", 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(builds);

        await _tools.QueryBuilds(
            project: "MyProject",
            definitions: "1,2",
            branchName: "refs/heads/main",
            statusFilter: "completed",
            resultFilter: "succeeded",
            requestedFor: "john@example.com",
            top: 25);

        _mockService.Verify(s => s.GetBuildsAsync("MyProject", It.IsAny<IEnumerable<int>>(), "refs/heads/main", "completed", "succeeded", "john@example.com", 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
