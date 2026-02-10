using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Services;

/// <summary>
/// Interface for Azure DevOps operations.
/// </summary>
public interface IAzureDevOpsService
{
    /// <summary>
    /// Gets a work item by its ID.
    /// </summary>
    /// <param name="workItemId">The work item ID.</param>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The work item details.</returns>
    Task<WorkItemDto?> GetWorkItemAsync(int workItemId, string? project = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets multiple work items by their IDs.
    /// </summary>
    /// <param name="workItemIds">The work item IDs.</param>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of work items.</returns>
    Task<IReadOnlyList<WorkItemDto>> GetWorkItemsAsync(IEnumerable<int> workItemIds, string? project = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries work items using WIQL (Work Item Query Language).
    /// </summary>
    /// <param name="wiqlQuery">The WIQL query string.</param>
    /// <param name="project">The project name (optional).</param>
    /// <param name="top">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of work items matching the query.</returns>
    Task<IReadOnlyList<WorkItemDto>> QueryWorkItemsAsync(string wiqlQuery, string? project = null, int top = 200, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets child work items of a parent work item.
    /// </summary>
    /// <param name="parentWorkItemId">The parent work item ID.</param>
    /// <param name="project">The project name (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of child work items.</returns>
    Task<IReadOnlyList<WorkItemDto>> GetChildWorkItemsAsync(int parentWorkItemId, string? project = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries work items using WIQL and returns paginated summary results.
    /// This is optimized for large result sets with reduced payload size.
    /// </summary>
    /// <param name="wiqlQuery">The WIQL query string.</param>
    /// <param name="project">The project name (optional).</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of work item summaries.</returns>
    Task<PaginatedResult<WorkItemSummaryDto>> QueryWorkItemsSummaryAsync(
        string wiqlQuery,
        string? project = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a comment to a work item.
    /// </summary>
    /// <param name="workItemId">The work item ID.</param>
    /// <param name="comment">The comment text.</param>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created comment details.</returns>
    Task<WorkItemCommentDto> AddWorkItemCommentAsync(
        int workItemId,
        string comment,
        string? project = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new work item in the specified project.
    /// </summary>
    /// <param name="project">The project name.</param>
    /// <param name="workItemType">The work item type (e.g., Bug, Task, User Story).</param>
    /// <param name="title">The work item title.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="assignedTo">Optional user to assign to.</param>
    /// <param name="areaPath">Optional area path.</param>
    /// <param name="iterationPath">Optional iteration path.</param>
    /// <param name="state">Optional initial state.</param>
    /// <param name="priority">Optional priority (1-4).</param>
    /// <param name="parentId">Optional parent work item ID to link as child.</param>
    /// <param name="tags">Optional semicolon-separated tags.</param>
    /// <param name="additionalFields">Optional dictionary of additional field reference names and values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created work item.</returns>
    Task<WorkItemDto> CreateWorkItemAsync(
        string project,
        string workItemType,
        string title,
        string? description = null,
        string? assignedTo = null,
        string? areaPath = null,
        string? iterationPath = null,
        string? state = null,
        int? priority = null,
        int? parentId = null,
        string? tags = null,
        Dictionary<string, string>? additionalFields = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing work item.
    /// </summary>
    /// <param name="workItemId">The work item ID to update.</param>
    /// <param name="title">Optional new title.</param>
    /// <param name="description">Optional new description.</param>
    /// <param name="assignedTo">Optional new assignee.</param>
    /// <param name="state">Optional new state.</param>
    /// <param name="areaPath">Optional new area path.</param>
    /// <param name="iterationPath">Optional new iteration path.</param>
    /// <param name="priority">Optional new priority (1-4).</param>
    /// <param name="tags">Optional new tags.</param>
    /// <param name="additionalFields">Optional dictionary of additional field reference names and values.</param>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated work item.</returns>
    Task<WorkItemDto> UpdateWorkItemAsync(
        int workItemId,
        string? title = null,
        string? description = null,
        string? assignedTo = null,
        string? state = null,
        string? areaPath = null,
        string? iterationPath = null,
        int? priority = null,
        string? tags = null,
        Dictionary<string, string>? additionalFields = null,
        string? project = null,
        CancellationToken cancellationToken = default);

    #region Git Operations

    /// <summary>
    /// Gets all repositories in a project.
    /// </summary>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of repositories.</returns>
    Task<IReadOnlyList<RepositoryDto>> GetRepositoriesAsync(string? project = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific repository by name or ID.
    /// </summary>
    /// <param name="repositoryNameOrId">The repository name or ID.</param>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The repository details.</returns>
    Task<RepositoryDto?> GetRepositoryAsync(string repositoryNameOrId, string? project = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all branches in a repository.
    /// </summary>
    /// <param name="repositoryNameOrId">The repository name or ID.</param>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of branches.</returns>
    Task<IReadOnlyList<BranchDto>> GetBranchesAsync(string repositoryNameOrId, string? project = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets items (files and folders) at a specific path in a repository.
    /// </summary>
    /// <param name="repositoryNameOrId">The repository name or ID.</param>
    /// <param name="path">The path to browse (default is root "/").</param>
    /// <param name="branchName">The branch name (optional, uses default branch if not specified).</param>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="recursionLevel">How deep to recurse (None, OneLevel, Full). Default is OneLevel.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of git items.</returns>
    Task<IReadOnlyList<GitItemDto>> GetItemsAsync(
        string repositoryNameOrId,
        string path = "/",
        string? branchName = null,
        string? project = null,
        string recursionLevel = "OneLevel",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the content of a specific file in a repository.
    /// </summary>
    /// <param name="repositoryNameOrId">The repository name or ID.</param>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="branchName">The branch name (optional, uses default branch if not specified).</param>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file content.</returns>
    Task<GitFileContentDto?> GetFileContentAsync(
        string repositoryNameOrId,
        string filePath,
        string? branchName = null,
        string? project = null,
        CancellationToken cancellationToken = default);

    #endregion

    #region Pull Request Operations

    /// <summary>
    /// Gets pull requests for a repository with optional filters.
    /// </summary>
    /// <param name="repositoryNameOrId">The repository name or ID.</param>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="status">Filter by status: active, completed, abandoned, or all.</param>
    /// <param name="creatorId">Filter by creator's unique name or ID.</param>
    /// <param name="reviewerId">Filter by reviewer's unique name or ID.</param>
    /// <param name="sourceRefName">Filter by source branch (e.g., refs/heads/feature).</param>
    /// <param name="targetRefName">Filter by target branch (e.g., refs/heads/main).</param>
    /// <param name="top">Maximum number of results to return.</param>
    /// <param name="skip">Number of results to skip for pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of pull requests matching the filters.</returns>
    Task<IReadOnlyList<PullRequestDto>> GetPullRequestsAsync(
        string repositoryNameOrId,
        string? project = null,
        string? status = null,
        string? creatorId = null,
        string? reviewerId = null,
        string? sourceRefName = null,
        string? targetRefName = null,
        int top = 50,
        int skip = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific pull request by ID within a repository.
    /// </summary>
    /// <param name="repositoryNameOrId">The repository name or ID.</param>
    /// <param name="pullRequestId">The pull request ID.</param>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pull request details.</returns>
    Task<PullRequestDto?> GetPullRequestByIdAsync(
        string repositoryNameOrId,
        int pullRequestId,
        string? project = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific pull request by ID only (project-level lookup, no repository required).
    /// </summary>
    /// <param name="pullRequestId">The pull request ID.</param>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pull request details.</returns>
    Task<PullRequestDto?> GetPullRequestByIdOnlyAsync(
        int pullRequestId,
        string? project = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets comment threads for a pull request.
    /// </summary>
    /// <param name="repositoryNameOrId">The repository name or ID.</param>
    /// <param name="pullRequestId">The pull request ID.</param>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of comment threads.</returns>
    Task<IReadOnlyList<PullRequestThreadDto>> GetPullRequestThreadsAsync(
        string repositoryNameOrId,
        int pullRequestId,
        string? project = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches pull requests by title or description.
    /// </summary>
    /// <param name="repositoryNameOrId">The repository name or ID.</param>
    /// <param name="searchText">Text to search for in title or description.</param>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="status">Filter by status: active, completed, abandoned, or all.</param>
    /// <param name="top">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching pull requests.</returns>
    Task<IReadOnlyList<PullRequestDto>> SearchPullRequestsAsync(
        string repositoryNameOrId,
        string searchText,
        string? project = null,
        string? status = null,
        int top = 50,
        CancellationToken cancellationToken = default);

    #endregion

    #region Pipeline/Build Operations

    /// <summary>
    /// Gets all pipelines (build definitions) in a project.
    /// </summary>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="name">Optional filter by pipeline name (supports wildcards).</param>
    /// <param name="folder">Optional filter by folder path.</param>
    /// <param name="top">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of pipelines.</returns>
    Task<IReadOnlyList<PipelineDto>> GetPipelinesAsync(
        string? project = null,
        string? name = null,
        string? folder = null,
        int top = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific pipeline by ID.
    /// </summary>
    /// <param name="pipelineId">The pipeline ID.</param>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pipeline details.</returns>
    Task<PipelineDto?> GetPipelineAsync(
        int pipelineId,
        string? project = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets builds for a project with optional filters.
    /// </summary>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="definitions">Optional list of pipeline definition IDs to filter by.</param>
    /// <param name="branchName">Optional filter by source branch.</param>
    /// <param name="statusFilter">Optional filter by status: all, inProgress, completed, cancelling, postponed, notStarted, none.</param>
    /// <param name="resultFilter">Optional filter by result: succeeded, partiallySucceeded, failed, canceled, none.</param>
    /// <param name="requestedFor">Optional filter by who requested the build.</param>
    /// <param name="top">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of builds.</returns>
    Task<IReadOnlyList<BuildDto>> GetBuildsAsync(
        string? project = null,
        IEnumerable<int>? definitions = null,
        string? branchName = null,
        string? statusFilter = null,
        string? resultFilter = null,
        string? requestedFor = null,
        int top = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific build by ID.
    /// </summary>
    /// <param name="buildId">The build ID.</param>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The build details.</returns>
    Task<BuildDto?> GetBuildAsync(
        int buildId,
        string? project = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the logs for a build.
    /// </summary>
    /// <param name="buildId">The build ID.</param>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of build logs.</returns>
    Task<IReadOnlyList<BuildLogDto>> GetBuildLogsAsync(
        int buildId,
        string? project = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the content of a specific build log.
    /// </summary>
    /// <param name="buildId">The build ID.</param>
    /// <param name="logId">The log ID.</param>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The log content as a string.</returns>
    Task<string?> GetBuildLogContentAsync(
        int buildId,
        int logId,
        string? project = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the timeline (stages, jobs, tasks) for a build.
    /// </summary>
    /// <param name="buildId">The build ID.</param>
    /// <param name="project">The project name (optional if default project is configured).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of timeline records.</returns>
    Task<IReadOnlyList<BuildTimelineRecordDto>> GetBuildTimelineAsync(
        int buildId,
        string? project = null,
        CancellationToken cancellationToken = default);

    #endregion
}
