using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.Wiki.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
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
    private readonly IAzureDevOpsOrganizationContextAccessor _organizationContextAccessor;
    private readonly IReadOnlyDictionary<string, AzureDevOpsOrganizationContext> _organizationContexts;
    private readonly AzureDevOpsOrganizationContext _defaultOrganizationContext;
    private bool _disposed;

    private WorkItemTrackingHttpClient WitClient => GetOrganizationContext().WitClient;
    private GitHttpClient GitClient => GetOrganizationContext().GitClient;
    private BuildHttpClient BuildClient => GetOrganizationContext().BuildClient;
    private WikiHttpClient WikiClient => GetOrganizationContext().WikiClient;
    private string? DefaultProject => GetOrganizationContext().DefaultProject;
    private string OrganizationUrl => GetOrganizationContext().OrganizationUrl;

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
        "Microsoft.VSTS.Common.ActivatedDate",
        "Microsoft.VSTS.Common.ClosedDate",
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
        "Microsoft.VSTS.Common.ActivatedDate",
        "Microsoft.VSTS.Common.ClosedDate",
        "System.Parent"
    ];

    public AzureDevOpsService(
        IOptions<AzureDevOpsOptions> options,
        ILogger<AzureDevOpsService> logger,
        IAzureDevOpsOrganizationContextAccessor organizationContextAccessor)
    {
        _options = options.Value;
        _logger = logger;
        _organizationContextAccessor = organizationContextAccessor;

        var contexts = _options.GetConfiguredOrganizations()
            .Select(CreateOrganizationContext)
            .ToList();

        if (contexts.Count == 0)
        {
            throw new InvalidOperationException("At least one Azure DevOps organization must be configured.");
        }

        var lookup = new Dictionary<string, AzureDevOpsOrganizationContext>(StringComparer.OrdinalIgnoreCase);
        foreach (var context in contexts)
        {
            RegisterOrganizationKey(lookup, context.Name, context);
            RegisterOrganizationKey(lookup, context.OrganizationUrl, context);

            var organizationNameFromUrl = GetOrganizationNameFromUrl(context.OrganizationUrl);
            if (!string.IsNullOrWhiteSpace(organizationNameFromUrl))
            {
                RegisterOrganizationKey(lookup, organizationNameFromUrl, context);
            }
        }

        _organizationContexts = lookup;
        _defaultOrganizationContext = ResolveDefaultOrganization(contexts);

        _logger.LogInformation(
            "Azure DevOps service initialized for {OrganizationCount} organization(s); default organization: {DefaultOrganization}",
            contexts.Count,
            _defaultOrganizationContext.Name);
    }

    private AzureDevOpsOrganizationContext GetOrganizationContext()
    {
        var organization = _organizationContextAccessor.CurrentOrganization;
        if (string.IsNullOrWhiteSpace(organization))
        {
            return _defaultOrganizationContext;
        }

        var key = NormalizeOrganizationKey(organization);
        if (_organizationContexts.TryGetValue(key, out var context))
        {
            return context;
        }

        throw new InvalidOperationException(
            $"Azure DevOps organization '{organization}' is not configured. Configure it under AzureDevOps:Organizations or use the default organization.");
    }

    private AzureDevOpsOrganizationContext ResolveDefaultOrganization(IReadOnlyList<AzureDevOpsOrganizationContext> contexts)
    {
        if (string.IsNullOrWhiteSpace(_options.DefaultOrganization))
        {
            return contexts[0];
        }

        var key = NormalizeOrganizationKey(_options.DefaultOrganization);
        if (_organizationContexts.TryGetValue(key, out var context))
        {
            return context;
        }

        throw new InvalidOperationException(
            $"AzureDevOps:DefaultOrganization '{_options.DefaultOrganization}' does not match any configured organization.");
    }

    private static AzureDevOpsOrganizationContext CreateOrganizationContext(AzureDevOpsOrganizationOptions organization)
    {
        var organizationUrl = organization.OrganizationUrl?.Trim()
            ?? throw new InvalidOperationException("Azure DevOps organization URL is required.");
        var personalAccessToken = organization.PersonalAccessToken
            ?? throw new InvalidOperationException($"Azure DevOps organization '{organizationUrl}' requires a PAT.");
        var organizationName = string.IsNullOrWhiteSpace(organization.Name)
            ? GetOrganizationNameFromUrl(organizationUrl) ?? organizationUrl
            : organization.Name.Trim();

        var credentials = new VssBasicCredential(string.Empty, personalAccessToken);
        var connection = new VssConnection(new Uri(organizationUrl), credentials);

        return new AzureDevOpsOrganizationContext
        {
            Name = organizationName,
            OrganizationUrl = organizationUrl.TrimEnd('/'),
            DefaultProject = string.IsNullOrWhiteSpace(organization.DefaultProject)
                ? null
                : organization.DefaultProject.Trim(),
            Connection = connection,
            WitClient = connection.GetClient<WorkItemTrackingHttpClient>(),
            GitClient = connection.GetClient<GitHttpClient>(),
            BuildClient = connection.GetClient<BuildHttpClient>(),
            WikiClient = connection.GetClient<WikiHttpClient>()
        };
    }

    private static void RegisterOrganizationKey(
        IDictionary<string, AzureDevOpsOrganizationContext> lookup,
        string? key,
        AzureDevOpsOrganizationContext context)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var normalizedKey = NormalizeOrganizationKey(key);
        if (lookup.TryGetValue(normalizedKey, out var existing) && !ReferenceEquals(existing, context))
        {
            throw new InvalidOperationException($"Duplicate Azure DevOps organization key '{key}'.");
        }

        lookup[normalizedKey] = context;
    }

    private static string NormalizeOrganizationKey(string organization) =>
        organization.Trim().TrimEnd('/').ToLowerInvariant();

    private static string? GetOrganizationNameFromUrl(string? organizationUrl)
    {
        if (string.IsNullOrWhiteSpace(organizationUrl) ||
            !Uri.TryCreate(organizationUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            return uri.Segments
                .Select(segment => segment.Trim('/'))
                .FirstOrDefault(segment => !string.IsNullOrWhiteSpace(segment));
        }

        const string visualStudioSuffix = ".visualstudio.com";
        if (uri.Host.EndsWith(visualStudioSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return uri.Host[..^visualStudioSuffix.Length];
        }

        return uri.Host;
    }

    public Task<WorkItemDto?> GetWorkItemAsync(int workItemId, string? project = null, CancellationToken cancellationToken = default)
        => GetWorkItemAsync(workItemId, project, includeRelations: false, cancellationToken);

    public async Task<WorkItemDto?> GetWorkItemAsync(int workItemId, string? project = null, bool includeRelations = false, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting work item {WorkItemId}", workItemId);

            var workItem = await WitClient.GetWorkItemAsync(
                project: project ?? DefaultProject,
                id: workItemId,
                expand: WorkItemExpand.All,
                cancellationToken: cancellationToken);

            return MapToDto(workItem, includeAllFields: true, includeRelations: includeRelations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work item {WorkItemId}", workItemId);
            throw;
        }
    }

    public Task<IReadOnlyList<WorkItemDto>> GetWorkItemsAsync(IEnumerable<int> workItemIds, string? project = null, CancellationToken cancellationToken = default)
        => GetWorkItemsAsync(workItemIds, project, includeRelations: false, cancellationToken);

    public async Task<IReadOnlyList<WorkItemDto>> GetWorkItemsAsync(IEnumerable<int> workItemIds, string? project = null, bool includeRelations = false, CancellationToken cancellationToken = default)
    {
        var ids = workItemIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        try
        {
            _logger.LogDebug("Getting {Count} work items", ids.Count);

            var workItems = await WitClient.GetWorkItemsAsync(
                project: project ?? DefaultProject,
                ids: ids,
                expand: WorkItemExpand.All,
                cancellationToken: cancellationToken);

            return workItems.Select(wi => MapToDto(wi, includeAllFields: true, includeRelations: includeRelations)).ToList();
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
            var queryResult = await WitClient.QueryByWiqlAsync(
                wiql: wiql,
                project: project ?? DefaultProject,
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
        var projectName = project ?? DefaultProject;
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
            var queryResult = await WitClient.QueryByWiqlAsync(
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

    public async Task<WorkItemDto> LinkWorkItemsAsync(
        int sourceWorkItemId,
        IEnumerable<int> targetWorkItemIds,
        string relationType,
        string? comment = null,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        var targetIds = targetWorkItemIds.Distinct().ToList();
        if (targetIds.Count == 0)
        {
            throw new ArgumentException("At least one target work item ID is required.", nameof(targetWorkItemIds));
        }

        try
        {
            _logger.LogDebug(
                "Linking work item {SourceWorkItemId} to {TargetCount} work item(s) with relation {RelationType}",
                sourceWorkItemId,
                targetIds.Count,
                relationType);

            var patchDocument = new JsonPatchDocument();
            var relationComment = string.IsNullOrWhiteSpace(comment) ? relationType : comment.Trim();

            foreach (var targetId in targetIds)
            {
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/relations/-",
                    Value = new
                    {
                        rel = relationType,
                        url = BuildWorkItemUrl(targetId),
                        attributes = new { comment = relationComment }
                    }
                });
            }

            var result = await WitClient.UpdateWorkItemAsync(
                document: patchDocument,
                id: sourceWorkItemId,
                project: project ?? DefaultProject,
                cancellationToken: cancellationToken);

            return MapToDto(result, includeAllFields: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error linking work item {SourceWorkItemId} with relation {RelationType}",
                sourceWorkItemId,
                relationType);
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
            var queryResult = await WitClient.QueryByWiqlAsync(
                wiql: wiql,
                project: project ?? DefaultProject,
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
            var workItems = await WitClient.GetWorkItemsAsync(
                project: project ?? DefaultProject,
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
            ActivatedDate = GetFieldValue<DateTime?>(fields, "Microsoft.VSTS.Common.ActivatedDate"),
            ClosedDate = GetFieldValue<DateTime?>(fields, "Microsoft.VSTS.Common.ClosedDate"),
            ParentId = GetFieldValue<int?>(fields, "System.Parent")
        };
    }

    private static WorkItemDto MapToDto(WorkItem workItem, bool includeAllFields = false, bool includeRelations = false)
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
            ActivatedDate = GetFieldValue<DateTime?>(fields, "Microsoft.VSTS.Common.ActivatedDate"),
            ClosedDate = GetFieldValue<DateTime?>(fields, "Microsoft.VSTS.Common.ClosedDate"),
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
                if (relation.Rel == "System.LinkTypes.Hierarchy-Reverse" && !string.IsNullOrEmpty(relation.Url))
                {
                    var lastSlash = relation.Url.LastIndexOf('/');
                    if (lastSlash >= 0 && int.TryParse(relation.Url[(lastSlash + 1)..], out var parentIdFromRelation))
                    {
                        dto = dto with { ParentId = parentIdFromRelation };
                    }
                }
                else if (relation.Rel == "ArtifactLink" && !string.IsNullOrEmpty(relation.Url))
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

            if (includeRelations)
            {
                var relationsList = new List<WorkItemRelationDto>();
                foreach (var relation in workItem.Relations)
                {
                    var relType = NormalizeRelationType(relation.Rel);
                    var targetId = ExtractTargetWorkItemId(relation.Url);
                    string? comment = null;

                    if (relation.Attributes != null && relation.Attributes.TryGetValue("comment", out var commentVal))
                    {
                        comment = commentVal?.ToString();
                    }

                    relationsList.Add(new WorkItemRelationDto
                    {
                        RelationType = relType,
                        RawRel = relation.Rel,
                        TargetId = targetId,
                        TargetUrl = relation.Url,
                        Comment = comment
                    });
                }
                dto = dto with { Relations = relationsList };
            }
        }

        return dto;
    }

    private static readonly Dictionary<string, string> RelationTypeMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["System.LinkTypes.Hierarchy-Reverse"] = "Parent",
        ["System.LinkTypes.Hierarchy-Forward"] = "Child",
        ["System.LinkTypes.Related"] = "Related",
        ["System.LinkTypes.Dependency-Reverse"] = "Predecessor",
        ["System.LinkTypes.Dependency-Forward"] = "Successor",
        ["Microsoft.VSTS.Common.TestedBy-Forward"] = "Tested By",
        ["Microsoft.VSTS.Common.TestedBy-Reverse"] = "Tests",
        ["Hyperlink"] = "Hyperlink",
        ["AttachedFile"] = "Attachment"
    };

    private static string NormalizeRelationType(string rawRel)
    {
        if (string.IsNullOrWhiteSpace(rawRel)) return "Unknown";
        if (RelationTypeMappings.TryGetValue(rawRel, out var normalized))
        {
            return normalized;
        }
        var lastDot = rawRel.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < rawRel.Length - 1)
        {
            return rawRel[(lastDot + 1)..];
        }
        return rawRel;
    }

    private static int? ExtractTargetWorkItemId(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var lastSlash = url.LastIndexOf('/');
        if (lastSlash >= 0 && lastSlash < url.Length - 1 && int.TryParse(url[(lastSlash + 1)..], out var parsedId))
        {
            return parsedId;
        }
        return null;
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

    private string BuildWorkItemUrl(int workItemId) =>
        $"{OrganizationUrl.TrimEnd('/')}/_apis/wit/workItems/{workItemId}";

    public async Task<WorkItemCommentDto> AddWorkItemCommentAsync(
        int workItemId,
        string comment,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Adding comment to work item {WorkItemId}", workItemId);

            var projectName = project ?? DefaultProject;
            var request = new CommentCreate { Text = comment };

            var createdComment = await WitClient.AddCommentAsync(
                request: request,
                project: projectName,
                workItemId: workItemId,
                cancellationToken: cancellationToken);

            return MapToCommentDto(createdComment, workItemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment to work item {WorkItemId}", workItemId);
            throw;
        }
    }

    public async Task<WorkItemCommentsResultDto> GetWorkItemCommentsAsync(
        int workItemId,
        string? project = null,
        int? top = null,
        string? continuationToken = null,
        bool includeDeleted = false,
        string? order = null,
        bool includeRenderedText = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting comments for work item {WorkItemId}", workItemId);

            CommentSortOrder? sortOrder = order?.Trim().ToLowerInvariant() switch
            {
                "asc" or "ascending" or "oldest" => CommentSortOrder.Asc,
                "desc" or "descending" or "newest" => CommentSortOrder.Desc,
                null or "" => null,
                _ => throw new ArgumentException($"Invalid order '{order}'. Use 'asc' or 'desc'.", nameof(order))
            };

            CommentExpandOptions? expand = includeRenderedText ? CommentExpandOptions.RenderedText : null;

            var list = await WitClient.GetCommentsAsync(
                project: project ?? DefaultProject,
                workItemId: workItemId,
                top: top,
                continuationToken: continuationToken,
                includeDeleted: includeDeleted,
                expand: expand,
                order: sortOrder,
                cancellationToken: cancellationToken);

            var mapped = (list.Comments ?? new List<Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.Comment>())
                .Select(c => MapToCommentDto(c, workItemId))
                .ToList();

            return new WorkItemCommentsResultDto
            {
                Comments = mapped,
                TotalCount = list.TotalCount,
                Count = list.Count,
                ContinuationToken = list.ContinuationToken,
                NextPage = list.NextPage?.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting comments for work item {WorkItemId}", workItemId);
            throw;
        }
    }

    private static WorkItemCommentDto MapToCommentDto(Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.Comment comment, int workItemId) => new()
    {
        Id = comment.Id,
        WorkItemId = comment.WorkItemId != 0 ? comment.WorkItemId : workItemId,
        Text = comment.Text,
        CreatedBy = comment.CreatedBy?.DisplayName,
        CreatedDate = comment.CreatedDate,
        ModifiedBy = comment.ModifiedBy?.DisplayName,
        ModifiedDate = comment.ModifiedDate,
        Version = comment.Version,
        IsDeleted = comment.IsDeleted,
        Url = comment.Url,
        Format = comment.Format.ToString()
    };

    public async Task<IReadOnlyList<WorkItemAttachmentDto>> GetWorkItemAttachmentsAsync(
        int workItemId,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting attachments for work item {WorkItemId}", workItemId);

            var workItem = await WitClient.GetWorkItemAsync(
                project: project ?? DefaultProject,
                id: workItemId,
                expand: WorkItemExpand.Relations,
                cancellationToken: cancellationToken);

            if (workItem.Relations is null || workItem.Relations.Count == 0)
            {
                return [];
            }

            var attachments = new List<WorkItemAttachmentDto>();

            foreach (var relation in workItem.Relations)
            {
                if (!string.Equals(relation.Rel, "AttachedFile", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                attachments.Add(MapToAttachmentDto(relation));
            }

            return attachments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting attachments for work item {WorkItemId}", workItemId);
            throw;
        }
    }

    public async Task<WorkItemAttachmentContentDto?> GetWorkItemAttachmentContentAsync(
        Guid attachmentId,
        string? fileName = null,
        string? project = null,
        long maxBytes = 10 * 1024 * 1024,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Downloading attachment {AttachmentId}", attachmentId);

            using var stream = await WitClient.GetAttachmentContentAsync(
                project: project ?? DefaultProject,
                id: attachmentId,
                cancellationToken: cancellationToken);

            if (stream is null)
            {
                return null;
            }

            var bytes = await ReadStreamWithLimitAsync(stream, maxBytes, cancellationToken);
            var isBinary = LooksBinary(bytes);

            return new WorkItemAttachmentContentDto
            {
                Id = attachmentId,
                FileName = fileName,
                Size = bytes.LongLength,
                IsBinary = isBinary,
                Encoding = isBinary ? "base64" : "utf-8",
                Content = isBinary ? Convert.ToBase64String(bytes) : System.Text.Encoding.UTF8.GetString(bytes)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading attachment {AttachmentId}", attachmentId);
            throw;
        }
    }

    private static async Task<byte[]> ReadStreamWithLimitAsync(Stream stream, long maxBytes, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new InvalidOperationException(
                    $"Attachment exceeds the {maxBytes:N0}-byte limit. Use the URL from get_work_item_attachments to download it directly.");
            }

            ms.Write(buffer, 0, read);
        }

        return ms.ToArray();
    }

    private static bool LooksBinary(byte[] bytes)
    {
        var sample = Math.Min(bytes.Length, 8000);
        for (var i = 0; i < sample; i++)
        {
            if (bytes[i] == 0)
            {
                return true;
            }
        }
        return false;
    }

    private static WorkItemAttachmentDto MapToAttachmentDto(WorkItemRelation relation)
    {
        var attributes = relation.Attributes;

        string? name = null;
        long? size = null;
        string? comment = null;
        DateTime? createdDate = null;
        DateTime? modifiedDate = null;

        if (attributes != null)
        {
            if (attributes.TryGetValue("name", out var nameValue))
            {
                name = nameValue?.ToString();
            }

            if (attributes.TryGetValue("resourceSize", out var sizeValue) && sizeValue != null)
            {
                size = Convert.ToInt64(sizeValue);
            }

            if (attributes.TryGetValue("comment", out var commentValue))
            {
                comment = commentValue?.ToString();
            }

            if (attributes.TryGetValue("resourceCreatedDate", out var createdValue)
                && DateTime.TryParse(createdValue?.ToString(), out var parsedCreated))
            {
                createdDate = parsedCreated;
            }

            if (attributes.TryGetValue("resourceModifiedDate", out var modifiedValue)
                && DateTime.TryParse(modifiedValue?.ToString(), out var parsedModified))
            {
                modifiedDate = parsedModified;
            }
        }

        return new WorkItemAttachmentDto
        {
            Id = ExtractAttachmentId(relation.Url),
            Name = name,
            Size = size,
            Comment = comment,
            CreatedDate = createdDate,
            ModifiedDate = modifiedDate,
            Url = relation.Url
        };
    }

    private static Guid? ExtractAttachmentId(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        // URL format: https://{host}/{org}/{project}/_apis/wit/attachments/{guid}[?...]
        var lastSlash = url.LastIndexOf('/');
        if (lastSlash < 0 || lastSlash == url.Length - 1)
        {
            return null;
        }

        var idPart = url[(lastSlash + 1)..];
        var queryStart = idPart.IndexOf('?');
        if (queryStart >= 0)
        {
            idPart = idPart[..queryStart];
        }

        return Guid.TryParse(idPart, out var id) ? id : null;
    }

    public async Task<WorkItemDto> CreateWorkItemAsync(
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
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Creating work item of type {WorkItemType} in project {Project}", workItemType, project);

            var patchDocument = new JsonPatchDocument();

            patchDocument.Add(new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/System.Title",
                Value = title
            });

            if (!string.IsNullOrEmpty(description))
            {
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.Description",
                    Value = description
                });
            }

            if (!string.IsNullOrEmpty(assignedTo))
            {
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.AssignedTo",
                    Value = assignedTo
                });
            }

            if (!string.IsNullOrEmpty(areaPath))
            {
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.AreaPath",
                    Value = areaPath
                });
            }

            if (!string.IsNullOrEmpty(iterationPath))
            {
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.IterationPath",
                    Value = iterationPath
                });
            }

            if (!string.IsNullOrEmpty(state))
            {
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.State",
                    Value = state
                });
            }

            if (priority.HasValue)
            {
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/fields/Microsoft.VSTS.Common.Priority",
                    Value = priority.Value
                });
            }

            if (!string.IsNullOrEmpty(tags))
            {
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.Tags",
                    Value = tags
                });
            }

            if (parentId.HasValue)
            {
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/relations/-",
                    Value = new
                    {
                        rel = "System.LinkTypes.Hierarchy-Reverse",
                        url = BuildWorkItemUrl(parentId.Value),
                        attributes = new { comment = "Parent" }
                    }
                });
            }

            if (additionalFields != null)
            {
                foreach (var field in additionalFields)
                {
                    var path = field.Key.StartsWith("/fields/")
                        ? field.Key
                        : $"/fields/{field.Key}";

                    patchDocument.Add(new JsonPatchOperation
                    {
                        Operation = Operation.Add,
                        Path = path,
                        Value = field.Value
                    });
                }
            }

            var result = await WitClient.CreateWorkItemAsync(
                document: patchDocument,
                project: project,
                type: workItemType,
                cancellationToken: cancellationToken);

            return MapToDto(result, includeAllFields: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating work item of type {WorkItemType} in project {Project}", workItemType, project);
            throw;
        }
    }

    public async Task<WorkItemDto> UpdateWorkItemAsync(
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
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Updating work item {WorkItemId}", workItemId);

            var patchDocument = new JsonPatchDocument();

            if (title != null)
            {
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Replace,
                    Path = "/fields/System.Title",
                    Value = title
                });
            }

            if (description != null)
            {
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Replace,
                    Path = "/fields/System.Description",
                    Value = description
                });
            }

            if (assignedTo != null)
            {
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Replace,
                    Path = "/fields/System.AssignedTo",
                    Value = assignedTo
                });
            }

            if (state != null)
            {
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Replace,
                    Path = "/fields/System.State",
                    Value = state
                });
            }

            if (areaPath != null)
            {
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Replace,
                    Path = "/fields/System.AreaPath",
                    Value = areaPath
                });
            }

            if (iterationPath != null)
            {
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Replace,
                    Path = "/fields/System.IterationPath",
                    Value = iterationPath
                });
            }

            if (priority.HasValue)
            {
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Replace,
                    Path = "/fields/Microsoft.VSTS.Common.Priority",
                    Value = priority.Value
                });
            }

            if (tags != null)
            {
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Replace,
                    Path = "/fields/System.Tags",
                    Value = tags
                });
            }

            if (additionalFields != null)
            {
                foreach (var field in additionalFields)
                {
                    var path = field.Key.StartsWith("/fields/")
                        ? field.Key
                        : $"/fields/{field.Key}";

                    patchDocument.Add(new JsonPatchOperation
                    {
                        Operation = Operation.Replace,
                        Path = path,
                        Value = field.Value
                    });
                }
            }

            // If no fields to update, just return the current work item
            if (patchDocument.Count == 0)
            {
                var currentWorkItem = await GetWorkItemAsync(workItemId, project, cancellationToken);
                return currentWorkItem!;
            }

            var result = await WitClient.UpdateWorkItemAsync(
                document: patchDocument,
                id: workItemId,
                project: project ?? DefaultProject,
                cancellationToken: cancellationToken);

            return MapToDto(result, includeAllFields: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating work item {WorkItemId}", workItemId);
            throw;
        }
    }

    public async Task<WorkItemHistoryResultDto?> GetWorkItemHistoryAsync(
        int workItemId,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting activity history for work item {WorkItemId}", workItemId);

            var projectName = project ?? DefaultProject;
            var updates = await WitClient.GetUpdatesAsync(
                project: projectName,
                id: workItemId,
                cancellationToken: cancellationToken);

            if (updates == null || updates.Count == 0)
            {
                return new WorkItemHistoryResultDto
                {
                    WorkItemId = workItemId,
                    TotalTransitions = 0,
                    Transitions = Array.Empty<WorkItemStateTransitionDto>()
                };
            }

            var rawTransitions = new List<WorkItemStateTransitionDto>();

            foreach (var update in updates)
            {
                if (update.Fields == null)
                {
                    continue;
                }

                string? newState = null;
                string? oldState = null;
                string? newBoardColumn = null;
                string? oldBoardColumn = null;

                if (update.Fields.TryGetValue("System.State", out var stateUpdate))
                {
                    newState = stateUpdate.NewValue?.ToString();
                    oldState = stateUpdate.OldValue?.ToString();
                }

                foreach (var kvp in update.Fields)
                {
                    if (kvp.Key.Equals("System.BoardColumn", StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.EndsWith("_Kanban.Column", StringComparison.OrdinalIgnoreCase))
                    {
                        newBoardColumn = kvp.Value.NewValue?.ToString();
                        oldBoardColumn = kvp.Value.OldValue?.ToString();
                        break;
                    }
                }

                if (newState != null || newBoardColumn != null)
                {
                    var movedBy = update.RevisedBy?.DisplayName
                        ?? update.RevisedBy?.UniqueName
                        ?? update.RevisedBy?.Name
                        ?? "Unknown";

                    var timestamp = update.RevisedDate;

                    rawTransitions.Add(new WorkItemStateTransitionDto
                    {
                        Revision = update.Rev != 0 ? update.Rev : update.Id,
                        State = newState ?? string.Empty,
                        PreviousState = oldState,
                        BoardColumn = newBoardColumn,
                        PreviousBoardColumn = oldBoardColumn,
                        MovedBy = movedBy,
                        Timestamp = timestamp
                    });
                }
            }

            var orderedTransitions = rawTransitions
                .OrderBy(t => t.Timestamp)
                .ToList();

            for (var i = 0; i < orderedTransitions.Count; i++)
            {
                if (string.IsNullOrEmpty(orderedTransitions[i].State) && i > 0)
                {
                    orderedTransitions[i] = orderedTransitions[i] with
                    {
                        State = orderedTransitions[i - 1].State
                    };
                }

                var nextTime = i < orderedTransitions.Count - 1
                    ? orderedTransitions[i + 1].Timestamp
                    : DateTime.UtcNow;

                var durationHours = (nextTime - orderedTransitions[i].Timestamp).TotalHours;
                if (durationHours >= 0)
                {
                    orderedTransitions[i] = orderedTransitions[i] with
                    {
                        DurationInHours = Math.Round(durationHours, 2)
                    };
                }
            }

            return new WorkItemHistoryResultDto
            {
                WorkItemId = workItemId,
                TotalTransitions = orderedTransitions.Count,
                Transitions = orderedTransitions
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting activity history for work item {WorkItemId}", workItemId);
            throw;
        }
    }

    public async Task<IReadOnlyList<WorkItemHistoryResultDto>> GetWorkItemsHistoryAsync(
        IEnumerable<int> workItemIds,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var ids = workItemIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return Array.Empty<WorkItemHistoryResultDto>();
            }

            _logger.LogDebug("Getting batch activity history for {Count} work items", ids.Count);

            using var semaphore = new SemaphoreSlim(10, 10);
            var tasks = ids.Select(async id =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await GetWorkItemHistoryAsync(id, project, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);
            return results.Where(r => r != null).Select(r => r!).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting batch activity history for work items");
            throw;
        }
    }

    public async Task<WorkItemRelationsResultDto?> GetWorkItemRelationsAsync(
        int workItemId,
        string? relationTypeFilter = null,
        bool expandTargetSummary = false,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting relations for work item {WorkItemId}", workItemId);

            var workItem = await WitClient.GetWorkItemAsync(
                project: project ?? DefaultProject,
                id: workItemId,
                expand: WorkItemExpand.Relations,
                cancellationToken: cancellationToken);

            if (workItem is null)
            {
                return null;
            }

            if (workItem.Relations is null || workItem.Relations.Count == 0)
            {
                return new WorkItemRelationsResultDto
                {
                    WorkItemId = workItemId,
                    Count = 0,
                    Relations = []
                };
            }

            var relationsList = new List<WorkItemRelationDto>();
            var targetsToFetch = new List<(WorkItemRelationDto relation, int targetId)>();

            foreach (var relation in workItem.Relations)
            {
                var relType = NormalizeRelationType(relation.Rel);

                if (!string.IsNullOrWhiteSpace(relationTypeFilter) &&
                    !string.Equals(relType, relationTypeFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var targetId = ExtractTargetWorkItemId(relation.Url);
                string? comment = null;

                if (relation.Attributes != null && relation.Attributes.TryGetValue("comment", out var commentVal))
                {
                    comment = commentVal?.ToString();
                }

                var relationDto = new WorkItemRelationDto
                {
                    RelationType = relType,
                    RawRel = relation.Rel,
                    TargetId = targetId,
                    TargetUrl = relation.Url,
                    Comment = comment
                };

                relationsList.Add(relationDto);

                if (expandTargetSummary && targetId.HasValue)
                {
                    targetsToFetch.Add((relationDto, targetId.Value));
                }
            }

            if (expandTargetSummary && targetsToFetch.Count > 0)
            {
                var targetIds = targetsToFetch.Select(t => t.targetId).Distinct().ToList();
                var summaries = await GetWorkItemSummariesAsync(targetIds, project ?? DefaultProject, cancellationToken);
                var summaryLookup = summaries.ToDictionary(s => s.Id);

                var updatedRelations = new List<WorkItemRelationDto>();
                foreach (var rel in relationsList)
                {
                    if (rel.TargetId.HasValue && summaryLookup.TryGetValue(rel.TargetId.Value, out var summary))
                    {
                        updatedRelations.Add(rel with { TargetSummary = summary });
                    }
                    else
                    {
                        updatedRelations.Add(rel);
                    }
                }
                relationsList = updatedRelations;
            }

            return new WorkItemRelationsResultDto
            {
                WorkItemId = workItemId,
                Count = relationsList.Count,
                Relations = relationsList
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting relations for work item {WorkItemId}", workItemId);
            throw;
        }
    }

    public async Task<WorkItemTreeNodeDto?> GetWorkItemTreeAsync(
        int workItemId,
        int maxDepth = 2,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        maxDepth = Math.Clamp(maxDepth, 1, 5);
        try
        {
            var rootItem = await GetWorkItemAsync(workItemId, project, includeRelations: true, cancellationToken);
            if (rootItem is null) return null;

            var visited = new HashSet<int> { workItemId };
            return await BuildTreeNodeAsync(rootItem, 1, maxDepth, project, visited, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work item tree for {WorkItemId}", workItemId);
            throw;
        }
    }

    private async Task<WorkItemTreeNodeDto> BuildTreeNodeAsync(
        WorkItemDto workItem,
        int currentDepth,
        int maxDepth,
        string? project,
        HashSet<int> visited,
        CancellationToken cancellationToken)
    {
        if (currentDepth >= maxDepth || workItem.Relations is null || workItem.Relations.Count == 0)
        {
            return new WorkItemTreeNodeDto
            {
                WorkItem = workItem,
                Children = Array.Empty<WorkItemTreeNodeDto>()
            };
        }

        var childRelations = workItem.Relations
            .Where(r => string.Equals(r.RelationType, "Child", StringComparison.OrdinalIgnoreCase) && r.TargetId.HasValue)
            .ToList();

        if (childRelations.Count == 0)
        {
            return new WorkItemTreeNodeDto
            {
                WorkItem = workItem,
                Children = Array.Empty<WorkItemTreeNodeDto>()
            };
        }

        var childrenList = new List<WorkItemTreeNodeDto>();
        var childIdsToFetch = new List<int>();

        foreach (var rel in childRelations)
        {
            var childId = rel.TargetId!.Value;
            if (!visited.Contains(childId))
            {
                visited.Add(childId);
                childIdsToFetch.Add(childId);
            }
        }

        if (childIdsToFetch.Count > 0)
        {
            var childrenDetails = await GetWorkItemsAsync(childIdsToFetch, project, includeRelations: true, cancellationToken);
            var childTasks = childrenDetails.Select(childDto =>
                BuildTreeNodeAsync(childDto, currentDepth + 1, maxDepth, project, visited, cancellationToken)
            );
            var resolvedChildren = await Task.WhenAll(childTasks);
            childrenList.AddRange(resolvedChildren);
        }

        return new WorkItemTreeNodeDto
        {
            WorkItem = workItem,
            Children = childrenList
        };
    }

    private async Task<IReadOnlyList<WorkItemSummaryDto>> GetWorkItemSummariesAsync(
        IEnumerable<int> ids,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return Array.Empty<WorkItemSummaryDto>();

        var workItems = await WitClient.GetWorkItemsAsync(
            project: project ?? DefaultProject,
            ids: idList,
            fields: SummaryFields,
            cancellationToken: cancellationToken);

        return workItems.Select(MapToSummaryDto).ToList();
    }

    #region Git Operations

    public async Task<IReadOnlyList<RepositoryDto>> GetRepositoriesAsync(string? project = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? DefaultProject;
            _logger.LogDebug("Getting repositories for project {Project}", projectName);

            var repositories = await GitClient.GetRepositoriesAsync(
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
            var projectName = project ?? DefaultProject;
            _logger.LogDebug("Getting repository {Repository} for project {Project}", repositoryNameOrId, projectName);

            var repository = await GitClient.GetRepositoryAsync(
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
            var projectName = project ?? DefaultProject;
            _logger.LogDebug("Getting branches for repository {Repository}", repositoryNameOrId);

            var branches = await GitClient.GetBranchesAsync(
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
            var projectName = project ?? DefaultProject;
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

            var items = await GitClient.GetItemsAsync(
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
            var projectName = project ?? DefaultProject;
            _logger.LogDebug("Getting file content at path {Path} in repository {Repository}", filePath, repositoryNameOrId);

            var versionDescriptor = string.IsNullOrEmpty(branchName)
                ? null
                : new GitVersionDescriptor
                {
                    VersionType = GitVersionType.Branch,
                    Version = branchName
                };

            // First get the item metadata
            var item = await GitClient.GetItemAsync(
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
            using var contentStream = await GitClient.GetItemContentAsync(
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
            var projectName = project ?? DefaultProject;
            _logger.LogDebug("Getting pull requests for repository {Repository}", repositoryNameOrId);

            var searchCriteria = new GitPullRequestSearchCriteria
            {
                Status = ParsePullRequestStatus(status),
                CreatorId = string.IsNullOrEmpty(creatorId) ? null : Guid.TryParse(creatorId, out var cid) ? cid : null,
                ReviewerId = string.IsNullOrEmpty(reviewerId) ? null : Guid.TryParse(reviewerId, out var rid) ? rid : null,
                SourceRefName = sourceRefName,
                TargetRefName = targetRefName
            };

            var pullRequests = await GitClient.GetPullRequestsAsync(
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
            var projectName = project ?? DefaultProject;
            _logger.LogDebug("Getting pull request {PullRequestId} for repository {Repository}", pullRequestId, repositoryNameOrId);

            var pullRequest = await GitClient.GetPullRequestAsync(
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
            var projectName = project ?? DefaultProject;
            _logger.LogDebug("Getting pull request {PullRequestId} at project level", pullRequestId);

            var pullRequest = await GitClient.GetPullRequestByIdAsync(
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
            var projectName = project ?? DefaultProject;
            _logger.LogDebug("Getting threads for pull request {PullRequestId}", pullRequestId);

            var threads = await GitClient.GetThreadsAsync(
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

    public async Task<PullRequestThreadDto> CreatePullRequestThreadAsync(
        string repositoryNameOrId,
        int pullRequestId,
        string content,
        string? filePath = null,
        int? lineNumber = null,
        int? endLineNumber = null,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Creating thread on pull request {PullRequestId} for repository {Repository}",
                pullRequestId,
                repositoryNameOrId);

            var thread = new GitPullRequestCommentThread
            {
                Comments =
                [
                    new Microsoft.TeamFoundation.SourceControl.WebApi.Comment
                    {
                        Content = content,
                        CommentType = CommentType.Text
                    }
                ],
                Status = CommentThreadStatus.Active
            };

            if (!string.IsNullOrWhiteSpace(filePath) && lineNumber.HasValue)
            {
                var start = new CommentPosition
                {
                    Line = lineNumber.Value,
                    Offset = 1
                };

                thread.ThreadContext = new CommentThreadContext
                {
                    FilePath = filePath,
                    RightFileStart = start,
                    RightFileEnd = new CommentPosition
                    {
                        Line = endLineNumber ?? lineNumber.Value,
                        Offset = 1
                    }
                };
            }

            var created = await GitClient.CreateThreadAsync(
                commentThread: thread,
                project: project ?? DefaultProject,
                repositoryId: repositoryNameOrId,
                pullRequestId: pullRequestId,
                userState: null,
                cancellationToken: cancellationToken);

            return MapToPullRequestThreadDto(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating thread on pull request {PullRequestId} for repository {Repository}",
                pullRequestId,
                repositoryNameOrId);
            throw;
        }
    }

    public async Task<PullRequestCommentDto> AddPullRequestThreadCommentAsync(
        string repositoryNameOrId,
        int pullRequestId,
        int threadId,
        string content,
        int? parentCommentId = null,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? DefaultProject;
            _logger.LogDebug(
                "Adding comment to thread {ThreadId} on pull request {PullRequestId}",
                threadId,
                pullRequestId);

            var comment = new Microsoft.TeamFoundation.SourceControl.WebApi.Comment
            {
                Content = content,
                ParentCommentId = parentCommentId.HasValue ? (short)parentCommentId.Value : (short)0,
                CommentType = CommentType.Text
            };

            var created = await GitClient.CreateCommentAsync(
                comment: comment,
                repositoryId: repositoryNameOrId,
                pullRequestId: pullRequestId,
                threadId: threadId,
                project: projectName,
                cancellationToken: cancellationToken);

            return new PullRequestCommentDto
            {
                Id = created.Id,
                ParentCommentId = created.ParentCommentId,
                Content = created.Content,
                Author = created.Author?.DisplayName,
                PublishedDate = created.PublishedDate,
                LastUpdatedDate = created.LastUpdatedDate,
                CommentType = created.CommentType.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error adding comment to thread {ThreadId} on pull request {PullRequestId}",
                threadId,
                pullRequestId);
            throw;
        }
    }

    public async Task<PullRequestThreadDto> UpdatePullRequestThreadStatusAsync(
        string repositoryNameOrId,
        int pullRequestId,
        int threadId,
        string status,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? DefaultProject;
            var threadStatus = ParseCommentThreadStatus(status)
                ?? throw new ArgumentException($"Unsupported pull request thread status '{status}'", nameof(status));

            _logger.LogDebug(
                "Updating thread {ThreadId} on pull request {PullRequestId} to status {Status}",
                threadId,
                pullRequestId,
                threadStatus);

            var thread = new GitPullRequestCommentThread
            {
                Status = threadStatus
            };

            var updated = await GitClient.UpdateThreadAsync(
                commentThread: thread,
                project: projectName,
                repositoryId: repositoryNameOrId,
                pullRequestId: pullRequestId,
                threadId: threadId,
                userState: null,
                cancellationToken: cancellationToken);

            return MapToPullRequestThreadDto(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error updating thread {ThreadId} on pull request {PullRequestId} to status {Status}",
                threadId,
                pullRequestId,
                status);
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
            var projectName = project ?? DefaultProject;
            _logger.LogDebug("Searching pull requests with text '{SearchText}' in repository {Repository}", searchText, repositoryNameOrId);

            // Get pull requests with status filter
            var searchCriteria = new GitPullRequestSearchCriteria
            {
                Status = ParsePullRequestStatus(status)
            };

            var pullRequests = await GitClient.GetPullRequestsAsync(
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

    public async Task<PullRequestDto> CreatePullRequestAsync(
        string repositoryNameOrId,
        string sourceRefName,
        string targetRefName,
        string title,
        string? description = null,
        bool isDraft = false,
        string? project = null,
        IEnumerable<string>? reviewerIds = null,
        IEnumerable<int>? workItemIds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? DefaultProject;
            _logger.LogDebug("Creating pull request in repository {Repository}", repositoryNameOrId);

            var gitPullRequest = new GitPullRequest
            {
                Title = title,
                Description = description,
                SourceRefName = sourceRefName,
                TargetRefName = targetRefName,
                IsDraft = isDraft
            };

            if (reviewerIds != null)
            {
                var reviewers = reviewerIds
                    .Where(id => Guid.TryParse(id, out _))
                    .Select(id => new IdentityRefWithVote { Id = id })
                    .ToList();

                if (reviewers.Count > 0)
                {
                    gitPullRequest.Reviewers = reviewers.ToArray();
                }
            }

            if (workItemIds != null)
            {
                var workItemRefs = workItemIds
                    .Select(id => new ResourceRef { Id = id.ToString(), Url = $"{OrganizationUrl}/_apis/wit/workItems/{id}" })
                    .ToList();

                if (workItemRefs.Count > 0)
                {
                    gitPullRequest.WorkItemRefs = workItemRefs.ToArray();
                }
            }

            var result = await GitClient.CreatePullRequestAsync(
                gitPullRequestToCreate: gitPullRequest,
                repositoryId: repositoryNameOrId,
                project: projectName,
                cancellationToken: cancellationToken);

            return MapToPullRequestDto(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating pull request in repository {Repository}", repositoryNameOrId);
            throw;
        }
    }

    public async Task<PullRequestDto> UpdatePullRequestAsync(
        string repositoryNameOrId,
        int pullRequestId,
        string? title = null,
        string? description = null,
        string? targetRefName = null,
        string? status = null,
        bool? isDraft = null,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var projectName = project ?? DefaultProject;
            _logger.LogDebug(
                "Updating pull request {PullRequestId} in repository {Repository}",
                pullRequestId,
                repositoryNameOrId);

            var pullRequestUpdate = new GitPullRequest();

            if (title is not null)
            {
                pullRequestUpdate.Title = title;
            }

            if (description is not null)
            {
                pullRequestUpdate.Description = description;
            }

            if (targetRefName is not null)
            {
                pullRequestUpdate.TargetRefName = targetRefName;
            }

            if (status is not null)
            {
                pullRequestUpdate.Status = ParsePullRequestUpdateStatus(status)
                    ?? throw new ArgumentException($"Unsupported pull request status '{status}'", nameof(status));
            }

            if (isDraft.HasValue)
            {
                pullRequestUpdate.IsDraft = isDraft.Value;
            }

            var result = await GitClient.UpdatePullRequestAsync(
                gitPullRequestToUpdate: pullRequestUpdate,
                project: projectName,
                repositoryId: repositoryNameOrId,
                pullRequestId: pullRequestId,
                userState: null,
                cancellationToken: cancellationToken);

            return MapToPullRequestDto(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error updating pull request {PullRequestId} in repository {Repository}",
                pullRequestId,
                repositoryNameOrId);
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

    private static PullRequestStatus? ParsePullRequestUpdateStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        return status.Trim().ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty) switch
        {
            "active" or "open" or "reopen" or "reactivate" or "reactivated" => PullRequestStatus.Active,
            "abandoned" or "abandon" => PullRequestStatus.Abandoned,
            "completed" or "complete" or "merge" or "merged" => PullRequestStatus.Completed,
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

    private static CommentThreadStatus? ParseCommentThreadStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        return status.Trim().ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty) switch
        {
            "active" or "open" or "reopen" or "reopened" => CommentThreadStatus.Active,
            "fixed" or "fix" or "resolve" or "resolved" => CommentThreadStatus.Fixed,
            "wontfix" or "wont" => CommentThreadStatus.WontFix,
            "closed" or "close" => CommentThreadStatus.Closed,
            "bydesign" => CommentThreadStatus.ByDesign,
            "pending" => CommentThreadStatus.Pending,
            _ => null
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
            EndLineNumber = thread.ThreadContext?.RightFileEnd?.Line,
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
            var projectName = project ?? DefaultProject;
            _logger.LogDebug("Getting pipelines for project {Project}", projectName);

            var definitions = await BuildClient.GetDefinitionsAsync(
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
            var projectName = project ?? DefaultProject;
            _logger.LogDebug("Getting pipeline {PipelineId}", pipelineId);

            var definition = await BuildClient.GetDefinitionAsync(
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
            var projectName = project ?? DefaultProject;
            _logger.LogDebug("Getting builds for project {Project}", projectName);

            var builds = await BuildClient.GetBuildsAsync(
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
            var projectName = project ?? DefaultProject;
            _logger.LogDebug("Getting build {BuildId}", buildId);

            var build = await BuildClient.GetBuildAsync(
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
            var projectName = project ?? DefaultProject;
            _logger.LogDebug("Getting logs for build {BuildId}", buildId);

            var logs = await BuildClient.GetBuildLogsAsync(
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
            var projectName = project ?? DefaultProject;
            _logger.LogDebug("Getting log content for build {BuildId}, log {LogId}", buildId, logId);

            var logLines = await BuildClient.GetBuildLogLinesAsync(
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
            var projectName = project ?? DefaultProject;
            _logger.LogDebug("Getting timeline for build {BuildId}", buildId);

            var timeline = await BuildClient.GetBuildTimelineAsync(
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

    #region Wiki Operations

    public async Task<IReadOnlyList<WikiDto>> GetWikisAsync(string? project = null, CancellationToken cancellationToken = default)
    {
        var projectName = project ?? DefaultProject;

        _logger.LogInformation("Getting wikis for project: {Project}", projectName ?? "(all)");

        try
        {
            var wikis = await WikiClient.GetAllWikisAsync(project: projectName, cancellationToken: cancellationToken);

            _logger.LogInformation("Found {Count} wikis", wikis.Count);

            return wikis.Select(MapToWikiDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting wikis for project: {Project}", projectName);
            throw;
        }
    }

    public async Task<WikiDto?> GetWikiAsync(string wikiIdentifier, string? project = null, CancellationToken cancellationToken = default)
    {
        var projectName = project ?? DefaultProject;

        _logger.LogInformation("Getting wiki: {WikiIdentifier} in project: {Project}", wikiIdentifier, projectName ?? "(default)");

        try
        {
            var wiki = await WikiClient.GetWikiAsync(project: projectName, wikiIdentifier: wikiIdentifier, cancellationToken: cancellationToken);
            return MapToWikiDto(wiki);
        }
        catch (Exception ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                                   ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Wiki '{WikiIdentifier}' not found", wikiIdentifier);
            return null;
        }
    }

    public async Task<WikiPageDto?> GetWikiPageAsync(
        string wikiIdentifier,
        string path,
        bool includeContent = true,
        string? version = null,
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        var projectName = project ?? DefaultProject;

        _logger.LogInformation("Getting wiki page: {Path} from wiki: {WikiIdentifier}", path, wikiIdentifier);

        try
        {
            var versionDescriptor = !string.IsNullOrWhiteSpace(version)
                ? new GitVersionDescriptor { Version = version, VersionType = GitVersionType.Branch }
                : null;

            var page = await WikiClient.GetPageAsync(
                project: projectName,
                wikiIdentifier: wikiIdentifier,
                path: path,
                recursionLevel: VersionControlRecursionType.None,
                versionDescriptor: versionDescriptor,
                includeContent: includeContent,
                cancellationToken: cancellationToken);

            return MapToWikiPageDto(page.Page, page.Page.Content);
        }
        catch (Exception ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                                   ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Wiki page '{Path}' not found in wiki '{WikiIdentifier}'", path, wikiIdentifier);
            return null;
        }
    }

    public async Task<WikiPageDto?> GetWikiPageTreeAsync(
        string wikiIdentifier,
        string path = "/",
        string recursionLevel = "OneLevel",
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        var projectName = project ?? DefaultProject;

        _logger.LogInformation("Getting wiki page tree: {Path} from wiki: {WikiIdentifier} with recursion: {Recursion}", path, wikiIdentifier, recursionLevel);

        try
        {
            var recursion = recursionLevel.Equals("Full", StringComparison.OrdinalIgnoreCase)
                ? VersionControlRecursionType.Full
                : VersionControlRecursionType.OneLevel;

            var page = await WikiClient.GetPageAsync(
                project: projectName,
                wikiIdentifier: wikiIdentifier,
                path: path,
                recursionLevel: recursion,
                includeContent: false,
                cancellationToken: cancellationToken);

            return MapToWikiPageDtoWithSubPages(page.Page);
        }
        catch (Exception ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                                   ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Wiki page '{Path}' not found in wiki '{WikiIdentifier}'", path, wikiIdentifier);
            return null;
        }
    }

    private static WikiDto MapToWikiDto(WikiV2 wiki)
    {
        return new WikiDto
        {
            Id = wiki.Id.ToString(),
            Name = wiki.Name,
            Type = wiki.Type.ToString(),
            Url = wiki.Url,
            RemoteUrl = wiki.RemoteUrl,
            ProjectId = wiki.ProjectId.ToString(),
            RepositoryId = wiki.RepositoryId.ToString(),
            MappedPath = wiki.MappedPath,
            Versions = wiki.Versions?.Select(v => v.Version).ToList()
        };
    }

    private static WikiPageDto MapToWikiPageDto(WikiPage page, string? content = null)
    {
        return new WikiPageDto
        {
            Id = page.Id,
            Path = page.Path,
            Content = content ?? page.Content,
            Order = page.Order,
            GitItemPath = page.GitItemPath,
            RemoteUrl = page.RemoteUrl,
            IsParentPage = page.IsParentPage
        };
    }

    private static WikiPageDto MapToWikiPageDtoWithSubPages(WikiPage page)
    {
        return new WikiPageDto
        {
            Id = page.Id,
            Path = page.Path,
            Order = page.Order,
            GitItemPath = page.GitItemPath,
            RemoteUrl = page.RemoteUrl,
            IsParentPage = page.IsParentPage,
            SubPages = page.SubPages?.Select(sp => new WikiPageSummaryDto
            {
                Id = sp.Id,
                Path = sp.Path,
                Order = sp.Order,
                GitItemPath = sp.GitItemPath,
                RemoteUrl = sp.RemoteUrl,
                IsParentPage = sp.IsParentPage
            }).ToList()
        };
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;

        foreach (var context in _organizationContexts.Values.Distinct())
        {
            context.Dispose();
        }

        _disposed = true;
    }

    private sealed class AzureDevOpsOrganizationContext : IDisposable
    {
        public required string Name { get; init; }

        public required string OrganizationUrl { get; init; }

        public string? DefaultProject { get; init; }

        public required VssConnection Connection { get; init; }

        public required WorkItemTrackingHttpClient WitClient { get; init; }

        public required GitHttpClient GitClient { get; init; }

        public required BuildHttpClient BuildClient { get; init; }

        public required WikiHttpClient WikiClient { get; init; }

        public void Dispose()
        {
            WitClient.Dispose();
            GitClient.Dispose();
            BuildClient.Dispose();
            WikiClient.Dispose();
            Connection.Dispose();
        }
    }
}
