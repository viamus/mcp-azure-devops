using Microsoft.Extensions.Options;
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

    public void Dispose()
    {
        if (_disposed) return;

        _witClient.Dispose();
        _connection.Dispose();
        _disposed = true;
    }
}
