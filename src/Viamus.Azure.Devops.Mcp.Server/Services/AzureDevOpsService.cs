using Microsoft.Extensions.Options;
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
                fields: DefaultFields,
                cancellationToken: cancellationToken);

            return workItems.Select(wi => MapToDto(wi)).ToList();
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
            return await GetWorkItemsAsync(workItemIds, project, cancellationToken);
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

        return dto;
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

    public void Dispose()
    {
        if (_disposed) return;

        _witClient.Dispose();
        _gitClient.Dispose();
        _connection.Dispose();
        _disposed = true;
    }
}
