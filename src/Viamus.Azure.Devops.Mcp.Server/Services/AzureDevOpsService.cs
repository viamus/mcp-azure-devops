using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Viamus.Azure.Devops.Mcp.Server.Configuration;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Services;

/// <summary>
/// Implementation of Azure DevOps service for work item operations.
/// </summary>
public sealed class AzureDevOpsService : IAzureDevOpsService, IDisposable
{
    private readonly AzureDevOpsOptions _options;
    private readonly ILogger<AzureDevOpsService> _logger;
    private readonly VssConnection _connection;
    private readonly WorkItemTrackingHttpClient _witClient;
    private readonly GitHttpClient _gitClient;
    private readonly BuildHttpClient _buildClient;
    private bool _disposed;

    private static readonly string[] DefaultFields =
    [
        "System.Id",
        "System.Title",
        "System.WorkItemType",
        "System.State",
        "System.AssignedTo",
        "System.Description",
        "System.AreaPath",
        "System.IterationPath",
        "Microsoft.VSTS.Common.Priority",
        "Microsoft.VSTS.Common.Severity",
        "System.CreatedDate",
        "System.ChangedDate",
        "System.CreatedBy",
        "System.ChangedBy",
        "System.Reason",
        "System.Parent"
    ];

    private static readonly string[] SummaryFields =
    [
        "System.Id",
        "System.Title",
        "System.WorkItemType",
        "System.State",
        "System.AssignedTo",
        "Microsoft.VSTS.Common.Priority",
        "System.ChangedDate",
        "System.Parent"
    ];

    public AzureDevOpsService(IOptions<AzureDevOpsOptions> options, ILogger<AzureDevOpsService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var credentials = new VssBasicCredential(string.Empty, _options.PersonalAccessToken);
        _connection = new VssConnection(new Uri(_options.OrganizationUrl), credentials);
        _witClient = _connection.GetClient<WorkItemTrackingHttpClient>();
        _gitClient = _connection.GetClient<GitHttpClient>();
        _buildClient = _connection.GetClient<BuildHttpClient>();

        _logger.LogInformation("Azure DevOps service initialized for organization: {OrganizationUrl}", _options.OrganizationUrl);
    }

    public async Task<WorkItemDto?> GetWorkItemAsync(int workItemId, string? project = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting work item {WorkItemId}", workItemId);

            var workItem = await _witClient.GetWorkItemAsync(
                project: project ?? _options.DefaultProject,
                id: workItemId,
                expand: WorkItemExpand.All,
                cancellationToken: cancellationToken);

            return MapToDto(workItem, includeAllFields: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work item {WorkItemId}", workItemId);
            throw;
        }
    }

    public async Task<IReadOnlyList<WorkItemDto>> GetWorkItemsAsync(IEnumerable<int> workItemIds, string? project = null, CancellationToken cancellationToken = default)
    {
        var ids = workItemIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        try
        {
            _logger.LogDebug("Getting {Count} work items", ids.Count);

            var workItems = await _witClient.GetWorkItemsAsync(
                project: project ?? _options.DefaultProject,
                ids: ids,
                expand: WorkItemExpand.All,
                cancellationToken: cancellationToken);

            return workItems.Select(wi => MapToDto(wi, includeAllFields: true)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work items");
            throw;
        }
    }

    public async Task<IReadOnlyList<WorkItemDto>> QueryWorkItemsAsync(string wiqlQuery, string? project = null, int top = 200, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing WIQL query");

            var wiql = new Wiql { Query = wiqlQuery };
            var queryResult = await _witClient.QueryByWiqlAsync(
                wiql: wiql,
                project: project ?? _options.DefaultProject,
                top: top,
                cancellationToken: cancellationToken);

            if (queryResult.WorkItems == null || !queryResult.WorkItems.Any())
            {
                return [];
            }

            var workItemIds = queryResult.WorkItems.Select(wi => wi.Id).ToList();

            // Process in batches to avoid API limits
            const int batchSize = 100;
            var results = new List<WorkItemDto>();

            for (var i = 0; i < workItemIds.Count; i += batchSize)
            {
                var batchIds = workItemIds.Skip(i).Take(batchSize).ToList();
                var batchResults = await GetWorkItemsAsync(batchIds, project, cancellationToken);
                results.AddRange(batchResults);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing WIQL query");
            throw;
        }
    }

    public async Task<IReadOnlyList<WorkItemDto>> GetChildWorkItemsAsync(int parentWorkItemId, string? project = null, CancellationToken cancellationToken = default)
    {
        var projectName = project ?? _options.DefaultProject;
        var wiqlQuery = $@"
            SELECT [System.Id]
            FROM WorkItemLinks
            WHERE ([Source].[System.Id] = {parentWorkItemId})
            AND ([System.Links.LinkType] = 'System.LinkTypes.Hierarchy-Forward')
            MODE (MustContain)";

        try
        {
            _logger.LogDebug("Getting child work items for parent {ParentWorkItemId}", parentWorkItemId);

            var wiql = new Wiql { Query = wiqlQuery };
            var queryResult = await _witClient.QueryByWiqlAsync(
                wiql: wiql,
                project: projectName,
                cancellationToken: cancellationToken);

            if (queryResult.WorkItemRelations == null || !queryResult.WorkItemRelations.Any())
            {
                return [];
            }

            var childIds = queryResult.WorkItemRelations
                .Where(r => r.Target != null && r.Source != null)
                .Select(r => r.Target!.Id)
                .Distinct()
                .ToList();

            if (childIds.Count == 0)
            {
                return [];
            }

            return await GetWorkItemsAsync(childIds, projectName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting child work items for parent {ParentWorkItemId}", parentWorkItemId);
            throw;
        }
    }

    public async Task<PaginatedResult<WorkItemSummaryDto>> QueryWorkItemsSummaryAsync(
        string wiqlQuery,
        string? project = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 20);

        try
        {
            _logger.LogDebug("Executing paginated WIQL query (page: {Page}, pageSize: {PageSize})", page, pageSize);

            var wiql = new Wiql { Query = wiqlQuery };

            // First, get all matching IDs to determine total count
            var queryResult = await _witClient.QueryByWiqlAsync(
                wiql: wiql,
                project: project ?? _options.DefaultProject,
                cancellationToken: cancellationToken);

            if (queryResult.WorkItems == null || !queryResult.WorkItems.Any())
            {
                return new PaginatedResult<WorkItemSummaryDto>
                {
                    Items = [],
                    TotalCount = 0,
                    Page = page,
                    PageSize = pageSize
                };
            }

            var allIds = queryResult.WorkItems.Select(wi => wi.Id).ToList();
            var totalCount = allIds.Count;

            // Get only the IDs for the requested page
            var pageIds = allIds
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            if (pageIds.Count == 0)
            {
                return new PaginatedResult<WorkItemSummaryDto>
                {
                    Items = [],
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize
                };
            }

            // Fetch only summary fields for the page items
            var workItems = await _witClient.GetWorkItemsAsync(
                project: project ?? _options.DefaultProject,
                ids: pageIds,
                fields: SummaryFields,
                cancellationToken: cancellationToken);

            var summaries = workItems.Select(MapToSummaryDto).ToList();

            return new PaginatedResult<WorkItemSummaryDto>
            {
                Items = summaries,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing paginated WIQL query");
            throw;
        }
    }

    private static WorkItemSummaryDto MapToSummaryDto(WorkItem workItem)
    {
        var fields = workItem.Fields;

        return new WorkItemSummaryDto
        {
            Id = workItem.Id ?? 0,
            Title = GetFieldValue<string>(fields, "System.Title"),
            WorkItemType = GetFieldValue<string>(fields, "System.WorkItemType"),
            State = GetFieldValue<string>(fields, "System.State"),
            AssignedTo = GetIdentityFieldValue(fields, "System.AssignedTo"),
            Priority = GetFieldValue<object>(fields, "Microsoft.VSTS.Common.Priority")?.ToString(),
            ChangedDate = GetFieldValue<DateTime?>(fields, "System.ChangedDate"),
            ParentId = GetFieldValue<int?>(fields, "System.Parent")
        };
    }

    private static WorkItemDto MapToDto(WorkItem workItem, bool includeAllFields = false)
    {
        var fields = workItem.Fields;

        var dto = new WorkItemDto
        {
            Id = workItem.Id ?? 0,
            Title = GetFieldValue<string>(fields, "System.Title"),
            WorkItemType = GetFieldValue<string>(fields, "System.WorkItemType"),
            State = GetFieldValue<string>(fields, "System.State"),
            AssignedTo = GetIdentityFieldValue(fields, "System.AssignedTo"),
            Description = GetFieldValue<string>(fields, "System.Description"),
            AreaPath = GetFieldValue<string>(fields, "System.AreaPath"),
            IterationPath = GetFieldValue<string>(fields, "System.IterationPath"),
            Priority = GetFieldValue<object>(fields, "Microsoft.VSTS.Common.Priority")?.ToString(),
            Severity = GetFieldValue<string>(fields, "Microsoft.VSTS.Common.Severity"),
            CreatedDate = GetFieldValue<DateTime?>(fields, "System.CreatedDate"),
            ChangedDate = GetFieldValue<DateTime?>(fields, "System.ChangedDate"),
            CreatedBy = GetIdentityFieldValue(fields, "System.CreatedBy"),
            ChangedBy = GetIdentityFieldValue(fields, "System.ChangedBy"),
            Reason = GetFieldValue<string>(fields, "System.Reason"),
            ParentId = GetFieldValue<int?>(fields, "System.Parent"),
            Url = workItem.Url
        };

        if (includeAllFields && fields.Count > 0)
        {
            var customFields = new Dictionary<string, object?>();
            foreach (var field in fields)
            {
                if (!field.Key.StartsWith("System.") && !field.Key.StartsWith("Microsoft.VSTS.Common."))
                {
                    var value = field.Value;
                    if (value is IdentityRef identity)
                    {
                        customFields[field.Key] = identity.DisplayName;
                    }
                    else
                    {
                        customFields[field.Key] = value;
                    }
                }
            }
            if (customFields.Count > 0)
            {
                dto = dto with { CustomFields = customFields };
            }
        }

        // Extract linked commits and pull requests from relations
        if (workItem.Relations != null && workItem.Relations.Count > 0)
        {
            var linkedCommits = new List<WorkItemCommitLinkDto>();
            var linkedPullRequests = new List<WorkItemPullRequestLinkDto>();

            foreach (var relation in workItem.Relations)
            {
                if (relation.Rel == "ArtifactLink" && !string.IsNullOrEmpty(relation.Url))
                {
                    // Commit URL format: vstfs:///Git/Commit/{projectId}%2F{repoId}%2F{commitId}
                    // Pull Request URL format: vstfs:///Git/PullRequestId/{projectId}%2F{repoId}%2F{prId}
                    var decodedUrl = Uri.UnescapeDataString(relation.Url);

                    if (decodedUrl.Contains("/Git/Commit/"))
                    {
                        var commitInfo = ExtractGitArtifactInfo(decodedUrl, "/Git/Commit/");
                        if (commitInfo != null)
                        {
                            linkedCommits.Add(new WorkItemCommitLinkDto
                            {
                                CommitId = commitInfo.Value.artifactId,
                                RepositoryId = commitInfo.Value.repositoryId,
                                Url = relation.Url
                            });
                        }
                    }
                    else if (decodedUrl.Contains("/Git/PullRequestId/"))
                    {
                        var prInfo = ExtractGitArtifactInfo(decodedUrl, "/Git/PullRequestId/");
                        if (prInfo != null && int.TryParse(prInfo.Value.artifactId, out var prId))
                        {
                            linkedPullRequests.Add(new WorkItemPullRequestLinkDto
                            {
                                PullRequestId = prId,
                                RepositoryId = prInfo.Value.repositoryId,
                                Url = relation.Url
                            });
                        }
                    }
                }
            }

            if (linkedCommits.Count > 0)
            {
                dto = dto with { LinkedCommits = linkedCommits };
            }
            if (linkedPullRequests.Count > 0)
            {
                dto = dto with { LinkedPullRequests = linkedPullRequests };
            }
        }

        return dto;
    }

    /// <summary>
    /// Extracts repository ID and artifact ID from a Git artifact URL.
    /// URL format: vstfs:///Git/{Type}/{projectId}/{repoId}/{artifactId}
    /// </summary>
    private static (string? repositoryId, string? artifactId)? ExtractGitArtifactInfo(string url, string typeSegment)
    {
        try
        {
            var startIndex = url.IndexOf(typeSegment, StringComparison.OrdinalIgnoreCase);
            if (startIndex < 0) return null;

            var pathPart = url[(startIndex + typeSegment.Length)..];
            var segments = pathPart.Split('/');

            // Expected format: {projectId}/{repoId}/{artifactId}
            if (segments.Length >= 3)
            {
                return (segments[1], segments[2]);
            }
            // Some formats may be: {repoId}/{artifactId}
            else if (segments.Length >= 2)
            {
                return (segments[0], segments[1]);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static T? GetFieldValue<T>(IDictionary<string, object> fields, string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    private static string? GetIdentityFieldValue(IDictionary<string, object> fields, string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var value))
        {
            if (value is IdentityRef identity)
            {
                return identity.DisplayName;
            }
            return value?.ToString();
        }
        return null;
    }

    public async Task<WorkItemCommentDto> AddWorkItemCommentAsync(
        int workItemId,
        string comment,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Adding comment to work item {WorkItemId}", workItemId);

            var projectName = project ?? _options.DefaultProject;
            var request = new CommentCreate { Text = comment };

            var createdComment = await _witClient.AddCommentAsync(
                request: request,
                project: projectName,
                workItemId: workItemId,
                cancellationToken: cancellationToken);

            return new WorkItemCommentDto
            {
                Id = createdComment.Id,
                WorkItemId = workItemId,
                Text = createdComment.Text,
                CreatedBy = createdComment.CreatedBy?.DisplayName,
                CreatedDate = createdComment.CreatedDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment to work item {WorkItemId}", workItemId);
            throw;
        }
    }

    #region Git Operations

    public async Task<IReadOnlyList<RepositoryDto>> GetRepositoriesAsync(string? project = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? _options.DefaultProject;
            _logger.LogDebug("Getting repositories for project {Project}", projectName);

            var repositories = await _gitClient.GetRepositoriesAsync(
                project: projectName,
                cancellationToken: cancellationToken);

            return repositories.Select(MapToRepositoryDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting repositories");
            throw;
        }
    }

    public async Task<RepositoryDto?> GetRepositoryAsync(string repositoryNameOrId, string? project = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? _options.DefaultProject;
            _logger.LogDebug("Getting repository {Repository} for project {Project}", repositoryNameOrId, projectName);

            var repository = await _gitClient.GetRepositoryAsync(
                project: projectName,
                repositoryId: repositoryNameOrId,
                cancellationToken: cancellationToken);

            return MapToRepositoryDto(repository);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting repository {Repository}", repositoryNameOrId);
            throw;
        }
    }

    public async Task<IReadOnlyList<BranchDto>> GetBranchesAsync(string repositoryNameOrId, string? project = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? _options.DefaultProject;
            _logger.LogDebug("Getting branches for repository {Repository}", repositoryNameOrId);

            var branches = await _gitClient.GetBranchesAsync(
                project: projectName,
                repositoryId: repositoryNameOrId,
                cancellationToken: cancellationToken);

            return branches.Select(MapToBranchDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting branches for repository {Repository}", repositoryNameOrId);
            throw;
        }
    }

    public async Task<IReadOnlyList<GitItemDto>> GetItemsAsync(
        string repositoryNameOrId,
        string path = "/",
        string? branchName = null,
        string? project = null,
        string recursionLevel = "OneLevel",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? _options.DefaultProject;
            _logger.LogDebug("Getting items at path {Path} in repository {Repository}", path, repositoryNameOrId);

            var versionDescriptor = string.IsNullOrEmpty(branchName)
                ? null
                : new GitVersionDescriptor
                {
                    VersionType = GitVersionType.Branch,
                    Version = branchName
                };

            var recursion = recursionLevel.ToLowerInvariant() switch
            {
                "none" => VersionControlRecursionType.None,
                "full" => VersionControlRecursionType.Full,
                _ => VersionControlRecursionType.OneLevel
            };

            var items = await _gitClient.GetItemsAsync(
                project: projectName,
                repositoryId: repositoryNameOrId,
                scopePath: path,
                recursionLevel: recursion,
                versionDescriptor: versionDescriptor,
                cancellationToken: cancellationToken);

            return items.Select(MapToGitItemDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting items at path {Path} in repository {Repository}", path, repositoryNameOrId);
            throw;
        }
    }

    public async Task<GitFileContentDto?> GetFileContentAsync(
        string repositoryNameOrId,
        string filePath,
        string? branchName = null,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? _options.DefaultProject;
            _logger.LogDebug("Getting file content at path {Path} in repository {Repository}", filePath, repositoryNameOrId);

            var versionDescriptor = string.IsNullOrEmpty(branchName)
                ? null
                : new GitVersionDescriptor
                {
                    VersionType = GitVersionType.Branch,
                    Version = branchName
                };

            // First get the item metadata
            var item = await _gitClient.GetItemAsync(
                project: projectName,
                repositoryId: repositoryNameOrId,
                path: filePath,
                versionDescriptor: versionDescriptor,
                includeContent: false,
                cancellationToken: cancellationToken);

            if (item == null)
            {
                return null;
            }

            // Check if it's a folder
            if (item.IsFolder)
            {
                return new GitFileContentDto
                {
                    Path = item.Path,
                    CommitId = item.CommitId,
                    IsBinary = false,
                    Content = null,
                    Size = 0
                };
            }

            // Get the content stream
            using var contentStream = await _gitClient.GetItemContentAsync(
                project: projectName,
                repositoryId: repositoryNameOrId,
                path: filePath,
                versionDescriptor: versionDescriptor,
                cancellationToken: cancellationToken);

            // Read content as text
            using var reader = new StreamReader(contentStream);
            var content = await reader.ReadToEndAsync(cancellationToken);

            // Simple binary detection - check for null bytes in first portion
            var isBinary = content.Take(8000).Any(c => c == '\0');

            return new GitFileContentDto
            {
                Path = item.Path,
                CommitId = item.CommitId,
                Content = isBinary ? "[Binary file content not shown]" : content,
                IsBinary = isBinary,
                Encoding = "UTF-8",
                Size = content.Length
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file content at path {Path} in repository {Repository}", filePath, repositoryNameOrId);
            throw;
        }
    }

    private static RepositoryDto MapToRepositoryDto(GitRepository repository)
    {
        return new RepositoryDto
        {
            Id = repository.Id.ToString(),
            Name = repository.Name,
            Url = repository.Url,
            DefaultBranch = repository.DefaultBranch,
            Size = repository.Size,
            RemoteUrl = repository.RemoteUrl,
            SshUrl = repository.SshUrl,
            WebUrl = repository.WebUrl,
            ProjectId = repository.ProjectReference?.Id.ToString(),
            ProjectName = repository.ProjectReference?.Name,
            IsDisabled = repository.IsDisabled ?? false,
            IsFork = repository.IsFork
        };
    }

    private static BranchDto MapToBranchDto(GitBranchStats branch)
    {
        return new BranchDto
        {
            Name = branch.Name,
            ObjectId = branch.Commit?.CommitId,
            CreatorName = branch.Commit?.Author?.Name,
            CreatorEmail = branch.Commit?.Author?.Email,
            IsBaseVersion = branch.IsBaseVersion
        };
    }

    private static GitItemDto MapToGitItemDto(GitItem item)
    {
        return new GitItemDto
        {
            ObjectId = item.ObjectId,
            GitObjectType = item.GitObjectType.ToString(),
            CommitId = item.CommitId,
            Path = item.Path,
            IsFolder = item.IsFolder,
            Url = item.Url
        };
    }

    #endregion

    #region Pull Request Operations

    public async Task<IReadOnlyList<PullRequestDto>> GetPullRequestsAsync(
        string repositoryNameOrId,
        string? project = null,
        string? status = null,
        string? creatorId = null,
        string? reviewerId = null,
        string? sourceRefName = null,
        string? targetRefName = null,
        int top = 50,
        int skip = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? _options.DefaultProject;
            _logger.LogDebug("Getting pull requests for repository {Repository}", repositoryNameOrId);

            var searchCriteria = new GitPullRequestSearchCriteria
            {
                Status = ParsePullRequestStatus(status),
                CreatorId = string.IsNullOrEmpty(creatorId) ? null : Guid.TryParse(creatorId, out var cid) ? cid : null,
                ReviewerId = string.IsNullOrEmpty(reviewerId) ? null : Guid.TryParse(reviewerId, out var rid) ? rid : null,
                SourceRefName = sourceRefName,
                TargetRefName = targetRefName
            };

            var pullRequests = await _gitClient.GetPullRequestsAsync(
                project: projectName,
                repositoryId: repositoryNameOrId,
                searchCriteria: searchCriteria,
                top: top,
                skip: skip,
                cancellationToken: cancellationToken);

            return pullRequests.Select(MapToPullRequestDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pull requests for repository {Repository}", repositoryNameOrId);
            throw;
        }
    }

    public async Task<PullRequestDto?> GetPullRequestByIdAsync(
        string repositoryNameOrId,
        int pullRequestId,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? _options.DefaultProject;
            _logger.LogDebug("Getting pull request {PullRequestId} for repository {Repository}", pullRequestId, repositoryNameOrId);

            var pullRequest = await _gitClient.GetPullRequestAsync(
                project: projectName,
                repositoryId: repositoryNameOrId,
                pullRequestId: pullRequestId,
                cancellationToken: cancellationToken);

            return MapToPullRequestDto(pullRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pull request {PullRequestId} for repository {Repository}", pullRequestId, repositoryNameOrId);
            throw;
        }
    }

    public async Task<PullRequestDto?> GetPullRequestByIdOnlyAsync(
        int pullRequestId,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? _options.DefaultProject;
            _logger.LogDebug("Getting pull request {PullRequestId} at project level", pullRequestId);

            var pullRequest = await _gitClient.GetPullRequestByIdAsync(
                pullRequestId: pullRequestId,
                project: projectName,
                cancellationToken: cancellationToken);

            return MapToPullRequestDto(pullRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pull request {PullRequestId} at project level", pullRequestId);
            throw;
        }
    }

    public async Task<IReadOnlyList<PullRequestThreadDto>> GetPullRequestThreadsAsync(
        string repositoryNameOrId,
        int pullRequestId,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? _options.DefaultProject;
            _logger.LogDebug("Getting threads for pull request {PullRequestId}", pullRequestId);

            var threads = await _gitClient.GetThreadsAsync(
                project: projectName,
                repositoryId: repositoryNameOrId,
                pullRequestId: pullRequestId,
                cancellationToken: cancellationToken);

            return threads.Select(MapToPullRequestThreadDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting threads for pull request {PullRequestId}", pullRequestId);
            throw;
        }
    }

    public async Task<IReadOnlyList<PullRequestDto>> SearchPullRequestsAsync(
        string repositoryNameOrId,
        string searchText,
        string? project = null,
        string? status = null,
        int top = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? _options.DefaultProject;
            _logger.LogDebug("Searching pull requests with text '{SearchText}' in repository {Repository}", searchText, repositoryNameOrId);

            // Get pull requests with status filter
            var searchCriteria = new GitPullRequestSearchCriteria
            {
                Status = ParsePullRequestStatus(status)
            };

            var pullRequests = await _gitClient.GetPullRequestsAsync(
                project: projectName,
                repositoryId: repositoryNameOrId,
                searchCriteria: searchCriteria,
                top: 200, // Get more to filter locally
                cancellationToken: cancellationToken);

            // Filter by search text in title or description
            var searchLower = searchText.ToLowerInvariant();
            var filtered = pullRequests
                .Where(pr =>
                    (pr.Title?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                    (pr.Description?.ToLowerInvariant().Contains(searchLower) ?? false))
                .Take(top)
                .Select(MapToPullRequestDto)
                .ToList();

            return filtered;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching pull requests with text '{SearchText}'", searchText);
            throw;
        }
    }

    private static PullRequestStatus? ParsePullRequestStatus(string? status)
    {
        if (string.IsNullOrEmpty(status))
            return null;

        return status.ToLowerInvariant() switch
        {
            "active" => PullRequestStatus.Active,
            "completed" => PullRequestStatus.Completed,
            "abandoned" => PullRequestStatus.Abandoned,
            "all" => PullRequestStatus.All,
            _ => null
        };
    }

    private static PullRequestDto MapToPullRequestDto(GitPullRequest pr)
    {
        return new PullRequestDto
        {
            PullRequestId = pr.PullRequestId,
            Title = pr.Title,
            Description = pr.Description,
            SourceBranch = pr.SourceRefName,
            TargetBranch = pr.TargetRefName,
            Status = pr.Status.ToString(),
            CreatedBy = pr.CreatedBy?.DisplayName,
            CreationDate = pr.CreationDate,
            ClosedDate = pr.ClosedDate,
            MergeStatus = pr.MergeStatus.ToString(),
            IsDraft = pr.IsDraft ?? false,
            RepositoryName = pr.Repository?.Name,
            RepositoryId = pr.Repository?.Id.ToString(),
            ProjectName = pr.Repository?.ProjectReference?.Name,
            Url = pr.Url,
            Reviewers = pr.Reviewers?.Select(r => new PullRequestReviewerDto
            {
                Id = r.Id,
                DisplayName = r.DisplayName,
                UniqueName = r.UniqueName,
                Vote = r.Vote,
                IsRequired = r.IsRequired,
                HasDeclined = r.HasDeclined ?? false,
                ImageUrl = r.ImageUrl
            }).ToList()
        };
    }

    private static PullRequestThreadDto MapToPullRequestThreadDto(GitPullRequestCommentThread thread)
    {
        return new PullRequestThreadDto
        {
            Id = thread.Id,
            Status = thread.Status.ToString(),
            FilePath = thread.ThreadContext?.FilePath,
            LineNumber = thread.ThreadContext?.RightFileStart?.Line,
            PublishedDate = thread.PublishedDate,
            LastUpdatedDate = thread.LastUpdatedDate,
            Comments = thread.Comments?.Select(c => new PullRequestCommentDto
            {
                Id = c.Id,
                ParentCommentId = c.ParentCommentId,
                Content = c.Content,
                Author = c.Author?.DisplayName,
                PublishedDate = c.PublishedDate,
                LastUpdatedDate = c.LastUpdatedDate,
                CommentType = c.CommentType.ToString()
            }).ToList()
        };
    }

    #endregion

    #region Pipeline/Build Operations

    public async Task<IReadOnlyList<PipelineDto>> GetPipelinesAsync(
        string? project = null,
        string? name = null,
        string? folder = null,
        int top = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? _options.DefaultProject;
            _logger.LogDebug("Getting pipelines for project {Project}", projectName);

            var definitions = await _buildClient.GetDefinitionsAsync(
                project: projectName,
                name: name,
                path: folder,
                top: top,
                cancellationToken: cancellationToken);

            return definitions.Select(MapToPipelineDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pipelines for project");
            throw;
        }
    }

    public async Task<PipelineDto?> GetPipelineAsync(
        int pipelineId,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? _options.DefaultProject;
            _logger.LogDebug("Getting pipeline {PipelineId}", pipelineId);

            var definition = await _buildClient.GetDefinitionAsync(
                project: projectName,
                definitionId: pipelineId,
                cancellationToken: cancellationToken);

            return MapToPipelineDto(definition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pipeline {PipelineId}", pipelineId);
            throw;
        }
    }

    public async Task<IReadOnlyList<BuildDto>> GetBuildsAsync(
        string? project = null,
        IEnumerable<int>? definitions = null,
        string? branchName = null,
        string? statusFilter = null,
        string? resultFilter = null,
        string? requestedFor = null,
        int top = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? _options.DefaultProject;
            _logger.LogDebug("Getting builds for project {Project}", projectName);

            var builds = await _buildClient.GetBuildsAsync(
                project: projectName,
                definitions: definitions?.ToList(),
                branchName: branchName,
                statusFilter: ParseBuildStatus(statusFilter),
                resultFilter: ParseBuildResult(resultFilter),
                requestedFor: requestedFor,
                top: top,
                cancellationToken: cancellationToken);

            return builds.Select(MapToBuildDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting builds for project");
            throw;
        }
    }

    public async Task<BuildDto?> GetBuildAsync(
        int buildId,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? _options.DefaultProject;
            _logger.LogDebug("Getting build {BuildId}", buildId);

            var build = await _buildClient.GetBuildAsync(
                project: projectName,
                buildId: buildId,
                cancellationToken: cancellationToken);

            return MapToBuildDto(build);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting build {BuildId}", buildId);
            throw;
        }
    }

    public async Task<IReadOnlyList<BuildLogDto>> GetBuildLogsAsync(
        int buildId,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? _options.DefaultProject;
            _logger.LogDebug("Getting logs for build {BuildId}", buildId);

            var logs = await _buildClient.GetBuildLogsAsync(
                project: projectName,
                buildId: buildId,
                cancellationToken: cancellationToken);

            return logs.Select(MapToBuildLogDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting logs for build {BuildId}", buildId);
            throw;
        }
    }

    public async Task<string?> GetBuildLogContentAsync(
        int buildId,
        int logId,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? _options.DefaultProject;
            _logger.LogDebug("Getting log content for build {BuildId}, log {LogId}", buildId, logId);

            var logLines = await _buildClient.GetBuildLogLinesAsync(
                project: projectName,
                buildId: buildId,
                logId: logId,
                cancellationToken: cancellationToken);

            return logLines != null ? string.Join(Environment.NewLine, logLines) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting log content for build {BuildId}, log {LogId}", buildId, logId);
            throw;
        }
    }

    public async Task<IReadOnlyList<BuildTimelineRecordDto>> GetBuildTimelineAsync(
        int buildId,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? _options.DefaultProject;
            _logger.LogDebug("Getting timeline for build {BuildId}", buildId);

            var timeline = await _buildClient.GetBuildTimelineAsync(
                project: projectName,
                buildId: buildId,
                cancellationToken: cancellationToken);

            if (timeline?.Records == null)
            {
                return [];
            }

            return timeline.Records.Select(MapToBuildTimelineRecordDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting timeline for build {BuildId}", buildId);
            throw;
        }
    }

    private static BuildStatus? ParseBuildStatus(string? status)
    {
        if (string.IsNullOrEmpty(status))
            return null;

        return status.ToLowerInvariant() switch
        {
            "all" => BuildStatus.All,
            "inprogress" => BuildStatus.InProgress,
            "completed" => BuildStatus.Completed,
            "cancelling" => BuildStatus.Cancelling,
            "postponed" => BuildStatus.Postponed,
            "notstarted" => BuildStatus.NotStarted,
            "none" => BuildStatus.None,
            _ => null
        };
    }

    private static BuildResult? ParseBuildResult(string? result)
    {
        if (string.IsNullOrEmpty(result))
            return null;

        return result.ToLowerInvariant() switch
        {
            "succeeded" => BuildResult.Succeeded,
            "partiallysucceeded" => BuildResult.PartiallySucceeded,
            "failed" => BuildResult.Failed,
            "canceled" => BuildResult.Canceled,
            "none" => BuildResult.None,
            _ => null
        };
    }

    private static PipelineDto MapToPipelineDto(BuildDefinitionReference definition)
    {
        return new PipelineDto
        {
            Id = definition.Id,
            Name = definition.Name,
            Folder = definition.Path,
            Path = definition.Path,
            QueueStatus = definition.QueueStatus.ToString(),
            Revision = definition.Revision,
            Url = definition.Url,
            ProjectId = definition.Project?.Id.ToString(),
            ProjectName = definition.Project?.Name,
            CreatedDate = definition.CreatedDate
        };
    }

    private static BuildDto MapToBuildDto(Build build)
    {
        return new BuildDto
        {
            Id = build.Id,
            BuildNumber = build.BuildNumber,
            Status = build.Status?.ToString(),
            Result = build.Result?.ToString(),
            SourceBranch = build.SourceBranch,
            SourceVersion = build.SourceVersion,
            RequestedBy = build.RequestedBy?.DisplayName,
            RequestedFor = build.RequestedFor?.DisplayName,
            QueueTime = build.QueueTime,
            StartTime = build.StartTime,
            FinishTime = build.FinishTime,
            DefinitionId = build.Definition?.Id,
            DefinitionName = build.Definition?.Name,
            ProjectId = build.Project?.Id.ToString(),
            ProjectName = build.Project?.Name,
            Url = build.Url,
            LogsUrl = build.Logs?.Url,
            Reason = build.Reason.ToString(),
            Priority = build.Priority.ToString(),
            RepositoryId = build.Repository?.Id,
            RepositoryName = build.Repository?.Name
        };
    }

    private static BuildLogDto MapToBuildLogDto(BuildLog log)
    {
        return new BuildLogDto
        {
            Id = log.Id,
            Type = log.Type,
            Url = log.Url,
            LineCount = (int)log.LineCount,
            CreatedOn = log.CreatedOn,
            LastChangedOn = log.LastChangedOn
        };
    }

    private static BuildTimelineRecordDto MapToBuildTimelineRecordDto(TimelineRecord record)
    {
        return new BuildTimelineRecordDto
        {
            Id = record.Id.ToString(),
            ParentId = record.ParentId?.ToString(),
            Type = record.RecordType,
            Name = record.Name,
            State = record.State?.ToString(),
            Result = record.Result?.ToString(),
            Order = record.Order ?? 0,
            StartTime = record.StartTime,
            FinishTime = record.FinishTime,
            ErrorCount = record.ErrorCount,
            WarningCount = record.WarningCount,
            LogUrl = record.Log?.Url,
            PercentComplete = record.PercentComplete
        };
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;

        _witClient.Dispose();
        _gitClient.Dispose();
        _buildClient.Dispose();
        _connection.Dispose();
        _disposed = true;
    }
}
