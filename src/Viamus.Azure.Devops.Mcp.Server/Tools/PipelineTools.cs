using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Viamus.Azure.Devops.Mcp.Server.Services;

namespace Viamus.Azure.Devops.Mcp.Server.Tools;

/// <summary>
/// MCP tools for Azure DevOps Pipeline and Build operations.
/// </summary>
[McpServerToolType]
public sealed class PipelineTools
{
    private readonly IAzureDevOpsService _azureDevOpsService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PipelineTools(IAzureDevOpsService azureDevOpsService)
    {
        _azureDevOpsService = azureDevOpsService;
    }

    [McpServerTool(Name = "get_pipelines")]
    [Description("Gets all pipelines (build definitions) in an Azure DevOps project. Returns pipeline details including name, folder, and configuration.")]
    public async Task<string> GetPipelines(
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("Optional filter by pipeline name (supports wildcards like 'MyPipeline*')")] string? name = null,
        [Description("Optional filter by folder path (e.g., '\\folder\\subfolder')")] string? folder = null,
        [Description("Maximum number of results to return (default: 100)")] int top = 100,
        CancellationToken cancellationToken = default)
    {
        var pipelines = await _azureDevOpsService.GetPipelinesAsync(project, name, folder, top, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            count = pipelines.Count,
            pipelines
        }, JsonOptions);
    }

    [McpServerTool(Name = "get_pipeline")]
    [Description("Gets details of a specific pipeline (build definition) by ID. Returns pipeline configuration including triggers and settings.")]
    public async Task<string> GetPipeline(
        [Description("The pipeline (build definition) ID")] int pipelineId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (pipelineId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Pipeline ID must be a positive integer" }, JsonOptions);
        }

        var pipeline = await _azureDevOpsService.GetPipelineAsync(pipelineId, project, cancellationToken);

        if (pipeline is null)
        {
            return JsonSerializer.Serialize(new { error = $"Pipeline {pipelineId} not found" }, JsonOptions);
        }

        return JsonSerializer.Serialize(pipeline, JsonOptions);
    }

    [McpServerTool(Name = "get_pipeline_runs")]
    [Description("Gets recent runs (builds) for a specific pipeline. Returns build history including status, result, and timing information.")]
    public async Task<string> GetPipelineRuns(
        [Description("The pipeline (build definition) ID")] int pipelineId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("Filter by source branch (e.g., 'refs/heads/main')")] string? branchName = null,
        [Description("Filter by status: 'all', 'inProgress', 'completed', 'cancelling', 'postponed', 'notStarted', 'none'")] string? statusFilter = null,
        [Description("Filter by result: 'succeeded', 'partiallySucceeded', 'failed', 'canceled', 'none'")] string? resultFilter = null,
        [Description("Maximum number of results to return (default: 20)")] int top = 20,
        CancellationToken cancellationToken = default)
    {
        if (pipelineId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Pipeline ID must be a positive integer" }, JsonOptions);
        }

        var builds = await _azureDevOpsService.GetBuildsAsync(
            project, [pipelineId], branchName, statusFilter, resultFilter, null, top, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            pipelineId,
            count = builds.Count,
            builds
        }, JsonOptions);
    }

    [McpServerTool(Name = "get_build")]
    [Description("Gets details of a specific build by ID. Returns comprehensive build information including status, timing, source details, and logs URL.")]
    public async Task<string> GetBuild(
        [Description("The build ID")] int buildId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (buildId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Build ID must be a positive integer" }, JsonOptions);
        }

        var build = await _azureDevOpsService.GetBuildAsync(buildId, project, cancellationToken);

        if (build is null)
        {
            return JsonSerializer.Serialize(new { error = $"Build {buildId} not found" }, JsonOptions);
        }

        return JsonSerializer.Serialize(build, JsonOptions);
    }

    [McpServerTool(Name = "get_builds")]
    [Description("Gets builds with optional filters. Returns build list with status, results, and timing. Useful for monitoring CI/CD health.")]
    public async Task<string> GetBuilds(
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("Comma-separated list of pipeline definition IDs to filter by (e.g., '1,2,3')")] string? definitions = null,
        [Description("Filter by source branch (e.g., 'refs/heads/main')")] string? branchName = null,
        [Description("Filter by status: 'all', 'inProgress', 'completed', 'cancelling', 'postponed', 'notStarted', 'none'")] string? statusFilter = null,
        [Description("Filter by result: 'succeeded', 'partiallySucceeded', 'failed', 'canceled', 'none'")] string? resultFilter = null,
        [Description("Filter by who requested the build")] string? requestedFor = null,
        [Description("Maximum number of results to return (default: 50)")] int top = 50,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<int>? definitionIds = null;
        if (!string.IsNullOrWhiteSpace(definitions))
        {
            var ids = new List<int>();
            foreach (var idStr in definitions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(idStr, out var id))
                {
                    ids.Add(id);
                }
            }
            if (ids.Count > 0)
            {
                definitionIds = ids;
            }
        }

        var builds = await _azureDevOpsService.GetBuildsAsync(
            project, definitionIds, branchName, statusFilter, resultFilter, requestedFor, top, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            filters = new
            {
                definitions,
                branchName,
                statusFilter,
                resultFilter,
                requestedFor
            },
            count = builds.Count,
            builds
        }, JsonOptions);
    }

    [McpServerTool(Name = "get_build_logs")]
    [Description("Gets the list of log files for a build. Returns log metadata including line counts. Use get_build_log_content to retrieve actual log content.")]
    public async Task<string> GetBuildLogs(
        [Description("The build ID")] int buildId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (buildId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Build ID must be a positive integer" }, JsonOptions);
        }

        var logs = await _azureDevOpsService.GetBuildLogsAsync(buildId, project, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            buildId,
            count = logs.Count,
            logs
        }, JsonOptions);
    }

    [McpServerTool(Name = "get_build_log_content")]
    [Description("Gets the content of a specific build log. Returns the full text of the log file. Use get_build_logs first to find available log IDs.")]
    public async Task<string> GetBuildLogContent(
        [Description("The build ID")] int buildId,
        [Description("The log ID (from get_build_logs)")] int logId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (buildId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Build ID must be a positive integer" }, JsonOptions);
        }

        if (logId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Log ID must be a positive integer" }, JsonOptions);
        }

        var content = await _azureDevOpsService.GetBuildLogContentAsync(buildId, logId, project, cancellationToken);

        if (content is null)
        {
            return JsonSerializer.Serialize(new { error = $"Log {logId} not found for build {buildId}" }, JsonOptions);
        }

        return JsonSerializer.Serialize(new
        {
            buildId,
            logId,
            content
        }, JsonOptions);
    }

    [McpServerTool(Name = "get_build_timeline")]
    [Description("Gets the timeline (stages, jobs, and tasks) for a build. Returns hierarchical structure of build execution with status and timing for each step.")]
    public async Task<string> GetBuildTimeline(
        [Description("The build ID")] int buildId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (buildId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Build ID must be a positive integer" }, JsonOptions);
        }

        var timeline = await _azureDevOpsService.GetBuildTimelineAsync(buildId, project, cancellationToken);

        // Organize by type for easier consumption
        var stages = timeline.Where(r => r.Type == "Stage").ToList();
        var jobs = timeline.Where(r => r.Type == "Job").ToList();
        var tasks = timeline.Where(r => r.Type == "Task").ToList();

        return JsonSerializer.Serialize(new
        {
            buildId,
            summary = new
            {
                stageCount = stages.Count,
                jobCount = jobs.Count,
                taskCount = tasks.Count,
                totalRecords = timeline.Count
            },
            records = timeline
        }, JsonOptions);
    }

    [McpServerTool(Name = "query_builds")]
    [Description("Advanced query for builds with multiple combined filters. Allows filtering by definitions, branch, status, result, and requester simultaneously.")]
    public async Task<string> QueryBuilds(
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("Comma-separated list of pipeline definition IDs to filter by")] string? definitions = null,
        [Description("Filter by source branch (e.g., 'refs/heads/main')")] string? branchName = null,
        [Description("Filter by status: 'all', 'inProgress', 'completed', 'cancelling', 'postponed', 'notStarted', 'none'")] string? statusFilter = null,
        [Description("Filter by result: 'succeeded', 'partiallySucceeded', 'failed', 'canceled', 'none'")] string? resultFilter = null,
        [Description("Filter by who requested the build")] string? requestedFor = null,
        [Description("Maximum number of results to return (default: 50)")] int top = 50,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<int>? definitionIds = null;
        if (!string.IsNullOrWhiteSpace(definitions))
        {
            var ids = new List<int>();
            foreach (var idStr in definitions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(idStr, out var id))
                {
                    ids.Add(id);
                }
            }
            if (ids.Count > 0)
            {
                definitionIds = ids;
            }
        }

        var builds = await _azureDevOpsService.GetBuildsAsync(
            project, definitionIds, branchName, statusFilter, resultFilter, requestedFor, top, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            query = new
            {
                definitions,
                branchName,
                statusFilter,
                resultFilter,
                requestedFor,
                top
            },
            count = builds.Count,
            builds
        }, JsonOptions);
    }
}
