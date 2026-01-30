using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Viamus.Azure.Devops.Mcp.Server.Services;

namespace Viamus.Azure.Devops.Mcp.Server.Tools;

/// <summary>
/// MCP tools for Azure DevOps Pull Request operations.
/// </summary>
[McpServerToolType]
public sealed class PullRequestTools
{
    private readonly IAzureDevOpsService _azureDevOpsService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PullRequestTools(IAzureDevOpsService azureDevOpsService)
    {
        _azureDevOpsService = azureDevOpsService;
    }

    [McpServerTool(Name = "get_pull_requests")]
    [Description("Gets pull requests for a Git repository with optional filters. Returns PR details including title, source/target branches, status, reviewers, and merge status.")]
    public async Task<string> GetPullRequests(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("Filter by status: 'active', 'completed', 'abandoned', or 'all' (default: all)")] string? status = null,
        [Description("Filter by creator's unique name or GUID")] string? creatorId = null,
        [Description("Filter by reviewer's unique name or GUID")] string? reviewerId = null,
        [Description("Filter by source branch (e.g., 'refs/heads/feature-branch')")] string? sourceRefName = null,
        [Description("Filter by target branch (e.g., 'refs/heads/main')")] string? targetRefName = null,
        [Description("Maximum number of results to return (default: 50)")] int top = 50,
        [Description("Number of results to skip for pagination (default: 0)")] int skip = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryNameOrId))
        {
            return JsonSerializer.Serialize(new { error = "Repository name or ID is required" }, JsonOptions);
        }

        var pullRequests = await _azureDevOpsService.GetPullRequestsAsync(
            repositoryNameOrId, project, status, creatorId, reviewerId,
            sourceRefName, targetRefName, top, skip, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            repository = repositoryNameOrId,
            count = pullRequests.Count,
            pullRequests
        }, JsonOptions);
    }

    [McpServerTool(Name = "get_pull_request")]
    [Description("Gets details of a specific pull request by ID. Returns full PR information including description, reviewers with their votes, and merge status.")]
    public async Task<string> GetPullRequest(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The pull request ID")] int pullRequestId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryNameOrId))
        {
            return JsonSerializer.Serialize(new { error = "Repository name or ID is required" }, JsonOptions);
        }

        if (pullRequestId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Pull request ID must be a positive integer" }, JsonOptions);
        }

        var pullRequest = await _azureDevOpsService.GetPullRequestByIdAsync(
            repositoryNameOrId, pullRequestId, project, cancellationToken);

        if (pullRequest is null)
        {
            return JsonSerializer.Serialize(new { error = $"Pull request {pullRequestId} not found in repository '{repositoryNameOrId}'" }, JsonOptions);
        }

        return JsonSerializer.Serialize(pullRequest, JsonOptions);
    }

    [McpServerTool(Name = "get_pull_request_threads")]
    [Description("Gets comment threads for a pull request. Returns all discussion threads including inline comments on files with their status and replies.")]
    public async Task<string> GetPullRequestThreads(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The pull request ID")] int pullRequestId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryNameOrId))
        {
            return JsonSerializer.Serialize(new { error = "Repository name or ID is required" }, JsonOptions);
        }

        if (pullRequestId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Pull request ID must be a positive integer" }, JsonOptions);
        }

        var threads = await _azureDevOpsService.GetPullRequestThreadsAsync(
            repositoryNameOrId, pullRequestId, project, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            repository = repositoryNameOrId,
            pullRequestId,
            count = threads.Count,
            threads
        }, JsonOptions);
    }

    [McpServerTool(Name = "search_pull_requests")]
    [Description("Searches pull requests by text in title or description. Useful for finding PRs related to specific features or bugs.")]
    public async Task<string> SearchPullRequests(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("Text to search for in PR title or description")] string searchText,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("Filter by status: 'active', 'completed', 'abandoned', or 'all' (default: all)")] string? status = null,
        [Description("Maximum number of results to return (default: 50)")] int top = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryNameOrId))
        {
            return JsonSerializer.Serialize(new { error = "Repository name or ID is required" }, JsonOptions);
        }

        if (string.IsNullOrWhiteSpace(searchText))
        {
            return JsonSerializer.Serialize(new { error = "Search text is required" }, JsonOptions);
        }

        var pullRequests = await _azureDevOpsService.SearchPullRequestsAsync(
            repositoryNameOrId, searchText, project, status, top, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            repository = repositoryNameOrId,
            searchText,
            count = pullRequests.Count,
            pullRequests
        }, JsonOptions);
    }

    [McpServerTool(Name = "query_pull_requests")]
    [Description("Advanced query for pull requests with multiple combined filters. Allows filtering by status, branches, dates, creator, and reviewer simultaneously.")]
    public async Task<string> QueryPullRequests(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("Filter by status: 'active', 'completed', 'abandoned', or 'all'")] string? status = null,
        [Description("Filter by creator's unique name or GUID")] string? creatorId = null,
        [Description("Filter by reviewer's unique name or GUID")] string? reviewerId = null,
        [Description("Filter by source branch (e.g., 'refs/heads/feature-branch')")] string? sourceRefName = null,
        [Description("Filter by target branch (e.g., 'refs/heads/main')")] string? targetRefName = null,
        [Description("Maximum number of results to return (default: 50)")] int top = 50,
        [Description("Number of results to skip for pagination (default: 0)")] int skip = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryNameOrId))
        {
            return JsonSerializer.Serialize(new { error = "Repository name or ID is required" }, JsonOptions);
        }

        var pullRequests = await _azureDevOpsService.GetPullRequestsAsync(
            repositoryNameOrId, project, status, creatorId, reviewerId,
            sourceRefName, targetRefName, top, skip, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            repository = repositoryNameOrId,
            filters = new
            {
                status = status ?? "all",
                creatorId,
                reviewerId,
                sourceRefName,
                targetRefName
            },
            pagination = new { top, skip },
            count = pullRequests.Count,
            pullRequests
        }, JsonOptions);
    }
}
