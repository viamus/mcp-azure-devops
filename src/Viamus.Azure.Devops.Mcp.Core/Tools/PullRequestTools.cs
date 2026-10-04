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
    private readonly IAzureDevOpsOrganizationContextAccessor _organizationContextAccessor;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PullRequestTools(IAzureDevOpsService azureDevOpsService)
        : this(azureDevOpsService, new AzureDevOpsOrganizationContextAccessor())
    {
    }

    public PullRequestTools(
        IAzureDevOpsService azureDevOpsService,
        IAzureDevOpsOrganizationContextAccessor organizationContextAccessor)
    {
        _azureDevOpsService = azureDevOpsService;
        _organizationContextAccessor = organizationContextAccessor;
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
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
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
    [Description("Gets details of a specific pull request by ID within a repository. Returns full PR information including description, reviewers with their votes, and merge status.")]
    public async Task<string> GetPullRequest(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The pull request ID")] int pullRequestId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
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

    [McpServerTool(Name = "get_pull_request_by_id")]
    [Description("Gets details of a pull request by ID only, without needing to specify the repository. This is a project-level lookup that finds the PR across all repositories. Returns full PR information including description, reviewers with their votes, merge status, and repository details.")]
    public async Task<string> GetPullRequestById(
        [Description("The pull request ID")] int pullRequestId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        if (pullRequestId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Pull request ID must be a positive integer" }, JsonOptions);
        }

        var pullRequest = await _azureDevOpsService.GetPullRequestByIdOnlyAsync(
            pullRequestId, project, cancellationToken);

        if (pullRequest is null)
        {
            return JsonSerializer.Serialize(new { error = $"Pull request {pullRequestId} not found in project" }, JsonOptions);
        }

        return JsonSerializer.Serialize(pullRequest, JsonOptions);
    }

    [McpServerTool(Name = "get_pull_request_threads")]
    [Description("Gets comment threads for a pull request. Returns all discussion threads including inline comments on files with their status and replies.")]
    public async Task<string> GetPullRequestThreads(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The pull request ID")] int pullRequestId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
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

    [McpServerTool(Name = "create_pull_request_thread")]
    [Description("Creates a new comment thread on a pull request. Omit filePath and lineNumber for a general PR discussion, or provide them to create an inline file comment.")]
    public async Task<string> CreatePullRequestThread(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The pull request ID")] int pullRequestId,
        [Description("The initial comment text (Markdown supported)")] string content,
        [Description("Optional file path for an inline thread, e.g. '/src/App.cs'")] string? filePath = null,
        [Description("Optional line number for an inline thread on the right/new file")] int? lineNumber = null,
        [Description("Optional ending line number for an inline thread range on the right/new file")] int? endLineNumber = null,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        if (string.IsNullOrWhiteSpace(repositoryNameOrId))
        {
            return JsonSerializer.Serialize(new { error = "Repository name or ID is required" }, JsonOptions);
        }

        if (pullRequestId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Pull request ID must be a positive integer" }, JsonOptions);
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return JsonSerializer.Serialize(new { error = "Comment content cannot be empty" }, JsonOptions);
        }

        if (!string.IsNullOrWhiteSpace(filePath) && (!lineNumber.HasValue || lineNumber <= 0))
        {
            return JsonSerializer.Serialize(new { error = "Line number must be a positive integer when filePath is provided" }, JsonOptions);
        }

        if (lineNumber.HasValue && string.IsNullOrWhiteSpace(filePath))
        {
            return JsonSerializer.Serialize(new { error = "File path is required when lineNumber is provided" }, JsonOptions);
        }

        if (endLineNumber.HasValue && (!lineNumber.HasValue || endLineNumber < lineNumber))
        {
            return JsonSerializer.Serialize(new { error = "End line number must be greater than or equal to lineNumber" }, JsonOptions);
        }

        var thread = await _azureDevOpsService.CreatePullRequestThreadAsync(
            repositoryNameOrId, pullRequestId, content,
            filePath, lineNumber, endLineNumber, project, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Thread {thread.Id} created on pull request {pullRequestId}",
            thread
        }, JsonOptions);
    }

    [McpServerTool(Name = "add_pull_request_thread_comment")]
    [Description("Adds a comment to an existing comment thread on a pull request. Use this to reply to discussions returned by get_pull_request_threads. Pass parentCommentId to reply to a specific comment within the thread; omit it to add a top-level comment.")]
    public async Task<string> AddPullRequestThreadComment(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The pull request ID")] int pullRequestId,
        [Description("The thread ID (from get_pull_request_threads)")] int threadId,
        [Description("The comment text (Markdown supported)")] string content,
        [Description("Optional parent comment ID when replying to a specific comment in the thread")] int? parentCommentId = null,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        if (string.IsNullOrWhiteSpace(repositoryNameOrId))
        {
            return JsonSerializer.Serialize(new { error = "Repository name or ID is required" }, JsonOptions);
        }

        if (pullRequestId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Pull request ID must be a positive integer" }, JsonOptions);
        }

        if (threadId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Thread ID must be a positive integer" }, JsonOptions);
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return JsonSerializer.Serialize(new { error = "Comment content cannot be empty" }, JsonOptions);
        }

        var comment = await _azureDevOpsService.AddPullRequestThreadCommentAsync(
            repositoryNameOrId, pullRequestId, threadId, content,
            parentCommentId, project, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Comment added to thread {threadId} on pull request {pullRequestId}",
            comment
        }, JsonOptions);
    }

    [McpServerTool(Name = "update_pull_request_thread_status")]
    [Description("Updates the status of an existing pull request comment thread. Supports statuses/aliases such as active/open/reopen, fixed/resolve/resolved, closed/close, wontFix/wont-fix, byDesign/by-design, and pending.")]
    public async Task<string> UpdatePullRequestThreadStatus(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The pull request ID")] int pullRequestId,
        [Description("The thread ID (from get_pull_request_threads)")] int threadId,
        [Description("Target status: Active, Fixed, Closed, WontFix, ByDesign, Pending, or aliases like close/resolve")] string status,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        if (string.IsNullOrWhiteSpace(repositoryNameOrId))
        {
            return JsonSerializer.Serialize(new { error = "Repository name or ID is required" }, JsonOptions);
        }

        if (pullRequestId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Pull request ID must be a positive integer" }, JsonOptions);
        }

        if (threadId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Thread ID must be a positive integer" }, JsonOptions);
        }

        var normalizedStatus = NormalizeThreadStatus(status);
        if (normalizedStatus is null)
        {
            return JsonSerializer.Serialize(new
            {
                error = "Unsupported thread status. Supported statuses are Active, Fixed, Closed, WontFix, ByDesign, and Pending. Aliases include open, reopen, resolve, resolved, close, closed, wont-fix, and by-design."
            }, JsonOptions);
        }

        var thread = await _azureDevOpsService.UpdatePullRequestThreadStatusAsync(
            repositoryNameOrId, pullRequestId, threadId, normalizedStatus,
            project, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Thread {threadId} on pull request {pullRequestId} updated to {thread.Status}",
            thread
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
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
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

    private static string? NormalizeThreadStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        return status.Trim().ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty) switch
        {
            "active" or "open" or "reopen" or "reopened" => "Active",
            "fixed" or "fix" or "resolve" or "resolved" => "Fixed",
            "wontfix" or "wont" => "WontFix",
            "closed" or "close" => "Closed",
            "bydesign" => "ByDesign",
            "pending" => "Pending",
            _ => null
        };
    }

    [McpServerTool(Name = "create_pull_request")]
    [Description("Creates a new pull request in a Git repository. Supports setting title, description, source/target branches, draft status, reviewers, and linked work items.")]
    public async Task<string> CreatePullRequest(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The source branch (e.g., 'refs/heads/feature-branch')")] string sourceRefName,
        [Description("The target branch (e.g., 'refs/heads/main')")] string targetRefName,
        [Description("The pull request title")] string title,
        [Description("The pull request description")] string? description = null,
        [Description("Whether to create as a draft pull request (default: false)")] bool isDraft = false,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("Semicolon-separated reviewer GUIDs (e.g., 'guid1;guid2')")] string? reviewerIds = null,
        [Description("Semicolon-separated work item IDs to link (e.g., '123;456')")] string? workItemIds = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        if (string.IsNullOrWhiteSpace(repositoryNameOrId))
        {
            return JsonSerializer.Serialize(new { error = "Repository name or ID is required" }, JsonOptions);
        }

        if (string.IsNullOrWhiteSpace(sourceRefName))
        {
            return JsonSerializer.Serialize(new { error = "Source branch is required" }, JsonOptions);
        }

        if (string.IsNullOrWhiteSpace(targetRefName))
        {
            return JsonSerializer.Serialize(new { error = "Target branch is required" }, JsonOptions);
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return JsonSerializer.Serialize(new { error = "Title is required" }, JsonOptions);
        }

        var parsedReviewerIds = string.IsNullOrWhiteSpace(reviewerIds)
            ? null
            : reviewerIds.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var parsedWorkItemIds = string.IsNullOrWhiteSpace(workItemIds)
            ? null
            : workItemIds.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(id => int.TryParse(id, out _))
                .Select(int.Parse);

        var pullRequest = await _azureDevOpsService.CreatePullRequestAsync(
            repositoryNameOrId, sourceRefName, targetRefName, title,
            description, isDraft, project, parsedReviewerIds, parsedWorkItemIds,
            cancellationToken);

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Pull request {pullRequest.PullRequestId} created successfully",
            pullRequest
        }, JsonOptions);
    }

    [McpServerTool(Name = "update_pull_request")]
    [Description("Updates an existing pull request. Only specified fields are changed; omitted fields remain unchanged. Supports title, description, target branch, status, and draft flag.")]
    public async Task<string> UpdatePullRequest(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The pull request ID")] int pullRequestId,
        [Description("New pull request title")] string? title = null,
        [Description("New pull request description")] string? description = null,
        [Description("New target branch (e.g., 'refs/heads/main')")] string? targetRefName = null,
        [Description("New status: Active, Abandoned, or Completed. Aliases include open, reopen, abandon, complete, and merge.")] string? status = null,
        [Description("New draft flag. Use true to mark as draft, false to mark ready for review.")] bool? isDraft = null,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        if (string.IsNullOrWhiteSpace(repositoryNameOrId))
        {
            return JsonSerializer.Serialize(new { error = "Repository name or ID is required" }, JsonOptions);
        }

        if (pullRequestId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Pull request ID must be a positive integer" }, JsonOptions);
        }

        if (title is not null && string.IsNullOrWhiteSpace(title))
        {
            return JsonSerializer.Serialize(new { error = "Title cannot be empty" }, JsonOptions);
        }

        if (targetRefName is not null && string.IsNullOrWhiteSpace(targetRefName))
        {
            return JsonSerializer.Serialize(new { error = "Target branch cannot be empty" }, JsonOptions);
        }

        var normalizedStatus = status is null ? null : NormalizePullRequestStatus(status);
        if (status is not null && normalizedStatus is null)
        {
            return JsonSerializer.Serialize(new
            {
                error = "Unsupported pull request status. Supported statuses are Active, Abandoned, and Completed. Aliases include open, reopen, abandon, complete, and merge."
            }, JsonOptions);
        }

        if (title is null &&
            description is null &&
            targetRefName is null &&
            normalizedStatus is null &&
            isDraft is null)
        {
            return JsonSerializer.Serialize(new { error = "At least one pull request field must be provided" }, JsonOptions);
        }

        var pullRequest = await _azureDevOpsService.UpdatePullRequestAsync(
            repositoryNameOrId,
            pullRequestId,
            title,
            description,
            targetRefName,
            normalizedStatus,
            isDraft,
            project,
            cancellationToken);

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Pull request {pullRequest.PullRequestId} updated successfully",
            pullRequest
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
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
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

    private static string? NormalizePullRequestStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        return status.Trim().ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty) switch
        {
            "active" or "open" or "reopen" or "reactivate" or "reactivated" => "Active",
            "abandoned" or "abandon" => "Abandoned",
            "completed" or "complete" or "merge" or "merged" => "Completed",
            _ => null
        };
    }
}
