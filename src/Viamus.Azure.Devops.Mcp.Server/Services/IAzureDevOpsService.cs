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
}
