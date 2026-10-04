using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Viamus.Azure.Devops.Mcp.Server.Services;

namespace Viamus.Azure.Devops.Mcp.Server.Tools;

/// <summary>
/// MCP tools for Azure DevOps Work Item operations.
/// </summary>
[McpServerToolType]
public sealed class WorkItemTools
{
    private readonly IAzureDevOpsService _azureDevOpsService;
    private readonly IAzureDevOpsOrganizationContextAccessor _organizationContextAccessor;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public WorkItemTools(IAzureDevOpsService azureDevOpsService)
        : this(azureDevOpsService, new AzureDevOpsOrganizationContextAccessor())
    {
    }

    public WorkItemTools(
        IAzureDevOpsService azureDevOpsService,
        IAzureDevOpsOrganizationContextAccessor organizationContextAccessor)
    {
        _azureDevOpsService = azureDevOpsService;
        _organizationContextAccessor = organizationContextAccessor;
    }

    [McpServerTool(Name = "get_work_item")]
    [Description("Gets details of a specific Azure DevOps work item by its ID. Returns information such as title, state, assigned to, description, area path, iteration path, and more.")]
    public async Task<string> GetWorkItem(
        [Description("The ID of the work item to retrieve")] int workItemId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        [Description("Whether to include the work item's relationship collection (default: false)")] bool includeRelations = false,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        var workItem = await _azureDevOpsService.GetWorkItemAsync(workItemId, project, includeRelations, cancellationToken);

        if (workItem is null)
        {
            return JsonSerializer.Serialize(new { error = $"Work item {workItemId} not found" }, JsonOptions);
        }

        return JsonSerializer.Serialize(workItem, JsonOptions);
    }

    [McpServerTool(Name = "get_work_item_history")]
    [Description("Gets the activity and state/column transition history for a specific work item. Shows when the card was moved to each state/column, by whom, and the duration spent in each state.")]
    public async Task<string> GetWorkItemHistory(
        [Description("The ID of the work item to retrieve history for")] int workItemId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        if (workItemId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Work item ID must be a positive integer" }, JsonOptions);
        }

        var history = await _azureDevOpsService.GetWorkItemHistoryAsync(workItemId, project, cancellationToken);
        if (history is null)
        {
            return JsonSerializer.Serialize(new { error = $"Work item {workItemId} not found or has no activity history" }, JsonOptions);
        }

        return JsonSerializer.Serialize(history, JsonOptions);
    }

    [McpServerTool(Name = "get_work_items_history")]
    [Description("Gets the activity and state/column transition histories for a batch of work items by their IDs (comma- or semicolon-separated). Uses concurrent requests to efficiently fetch timelines for multiple cards.")]
    public async Task<string> GetWorkItemsHistory(
        [Description("Comma- or semicolon-separated list of work item IDs (e.g., '123,456,789')")] string workItemIds,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        var ids = ParseWorkItemIds(workItemIds);

        if (ids.Count == 0)
        {
            return JsonSerializer.Serialize(new { error = "No valid work item IDs provided" }, JsonOptions);
        }

        var histories = await _azureDevOpsService.GetWorkItemsHistoryAsync(ids, project, cancellationToken);
        return JsonSerializer.Serialize(new { count = histories.Count, itemHistories = histories }, JsonOptions);
    }

    [McpServerTool(Name = "get_work_items")]
    [Description("Gets details of multiple Azure DevOps work items by their IDs. Useful for batch retrieval of work items.")]
    public async Task<string> GetWorkItems(
        [Description("Comma-separated list of work item IDs to retrieve (e.g., '123,456,789')")] string workItemIds,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        [Description("Whether to include the work items' relationship collections (default: false)")] bool includeRelations = false,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        var ids = ParseWorkItemIds(workItemIds);

        if (ids.Count == 0)
        {
            return JsonSerializer.Serialize(new { error = "No valid work item IDs provided" }, JsonOptions);
        }

        var workItems = await _azureDevOpsService.GetWorkItemsAsync(ids, project, includeRelations, cancellationToken);
        return JsonSerializer.Serialize(new { count = workItems.Count, workItems }, JsonOptions);
    }

    [McpServerTool(Name = "query_work_items")]
    [Description("Queries Azure DevOps work items using WIQL (Work Item Query Language) with pagination. Returns a summary view (ID, Title, Type, State, Priority) to reduce payload size. Use get_work_item to get full details of a specific item.")]
    public async Task<string> QueryWorkItems(
        [Description("The WIQL query string. Example: SELECT [System.Id], [System.Title] FROM WorkItems WHERE [System.State] = 'Active'")] string wiqlQuery,
        [Description("The project name (optional)")] string? project = null,
        [Description("Page number, starting from 1 (default: 1)")] int page = 1,
        [Description("Number of items per page (default: 20, max: 20)")] int pageSize = 20,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        var result = await _azureDevOpsService.QueryWorkItemsSummaryAsync(wiqlQuery, project, page, pageSize, cancellationToken);
        return JsonSerializer.Serialize(new
        {
            totalCount = result.TotalCount,
            page = result.Page,
            pageSize = result.PageSize,
            totalPages = result.TotalPages,
            hasNextPage = result.HasNextPage,
            hasPreviousPage = result.HasPreviousPage,
            items = result.Items
        }, JsonOptions);
    }

    [McpServerTool(Name = "get_work_items_by_state")]
    [Description("Gets work items filtered by state with pagination. Returns a summary view (ID, Title, Type, State, Priority) to reduce payload size. Use get_work_item to get full details of a specific item.")]
    public async Task<string> GetWorkItemsByState(
        [Description("The state to filter by (e.g., 'Active', 'New', 'Closed', 'Resolved')")] string state,
        [Description("The project name (required)")] string project,
        [Description("Optional work item type filter (e.g., 'Bug', 'Task', 'User Story')")] string? workItemType = null,
        [Description("Page number, starting from 1 (default: 1)")] int page = 1,
        [Description("Number of items per page (default: 20, max: 20)")] int pageSize = 20,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        var typeFilter = string.IsNullOrWhiteSpace(workItemType)
            ? string.Empty
            : $" AND [System.WorkItemType] = '{EscapeWiqlString(workItemType)}'";

        var wiqlQuery = $@"
            SELECT [System.Id]
            FROM WorkItems
            WHERE [System.TeamProject] = '{EscapeWiqlString(project)}'
            AND [System.State] = '{EscapeWiqlString(state)}'{typeFilter}
            ORDER BY [System.ChangedDate] DESC";

        var result = await _azureDevOpsService.QueryWorkItemsSummaryAsync(wiqlQuery, project, page, pageSize, cancellationToken);
        return JsonSerializer.Serialize(new
        {
            state,
            workItemType,
            totalCount = result.TotalCount,
            page = result.Page,
            pageSize = result.PageSize,
            totalPages = result.TotalPages,
            hasNextPage = result.HasNextPage,
            hasPreviousPage = result.HasPreviousPage,
            items = result.Items
        }, JsonOptions);
    }

    [McpServerTool(Name = "get_work_items_assigned_to")]
    [Description("Gets work items assigned to a specific user with pagination. Returns a summary view (ID, Title, Type, State, Priority) to reduce payload size. Use get_work_item to get full details of a specific item.")]
    public async Task<string> GetWorkItemsAssignedTo(
        [Description("The display name or email of the user")] string assignedTo,
        [Description("The project name (required)")] string project,
        [Description("Filter by state (optional, e.g., 'Active')")] string? state = null,
        [Description("Page number, starting from 1 (default: 1)")] int page = 1,
        [Description("Number of items per page (default: 20, max: 20)")] int pageSize = 20,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        var stateFilter = string.IsNullOrWhiteSpace(state)
            ? string.Empty
            : $" AND [System.State] = '{EscapeWiqlString(state)}'";

        var wiqlQuery = $@"
            SELECT [System.Id]
            FROM WorkItems
            WHERE [System.TeamProject] = '{EscapeWiqlString(project)}'
            AND [System.AssignedTo] CONTAINS '{EscapeWiqlString(assignedTo)}'{stateFilter}
            ORDER BY [System.ChangedDate] DESC";

        var result = await _azureDevOpsService.QueryWorkItemsSummaryAsync(wiqlQuery, project, page, pageSize, cancellationToken);
        return JsonSerializer.Serialize(new
        {
            assignedTo,
            totalCount = result.TotalCount,
            page = result.Page,
            pageSize = result.PageSize,
            totalPages = result.TotalPages,
            hasNextPage = result.HasNextPage,
            hasPreviousPage = result.HasPreviousPage,
            items = result.Items
        }, JsonOptions);
    }

    [McpServerTool(Name = "get_child_work_items")]
    [Description("Gets all child work items of a parent work item. Useful for viewing tasks under a user story or bugs under a feature.")]
    public async Task<string> GetChildWorkItems(
        [Description("The ID of the parent work item")] int parentWorkItemId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        var workItems = await _azureDevOpsService.GetChildWorkItemsAsync(parentWorkItemId, project, cancellationToken);
        return JsonSerializer.Serialize(new { parentWorkItemId, count = workItems.Count, children = workItems }, JsonOptions);
    }

    [McpServerTool(Name = "link_work_items")]
    [Description("Links an existing Azure DevOps work item to one or more other work items. relationType accepts parent, child, predecessor, successor, related, or the matching System.LinkTypes.* reference name.")]
    public async Task<string> LinkWorkItems(
        [Description("The work item ID to update with the new link. For a story, this is the story ID.")] int sourceWorkItemId,
        [Description("Comma- or semicolon-separated target work item IDs to link to (e.g., '123,456' or '123;456')")] string targetWorkItemIds,
        [Description("Relation from the source work item's perspective: parent, child, predecessor, successor, related, or a System.LinkTypes.* reference name")] string relationType,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("Optional comment to store on the relation")] string? comment = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        if (sourceWorkItemId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "sourceWorkItemId must be a positive integer" }, JsonOptions);
        }

        var targetIds = ParseWorkItemIds(targetWorkItemIds);
        if (targetIds.Count == 0)
        {
            return JsonSerializer.Serialize(new { error = "No valid target work item IDs provided" }, JsonOptions);
        }

        if (targetIds.Contains(sourceWorkItemId))
        {
            return JsonSerializer.Serialize(new { error = "A work item cannot be linked to itself" }, JsonOptions);
        }

        var normalizedRelationType = NormalizeWorkItemRelationType(relationType);
        if (normalizedRelationType is null)
        {
            return JsonSerializer.Serialize(new
            {
                error = "relationType must be one of: parent, child, predecessor, successor, related"
            }, JsonOptions);
        }

        if (normalizedRelationType == "System.LinkTypes.Hierarchy-Reverse" && targetIds.Count > 1)
        {
            return JsonSerializer.Serialize(new { error = "A work item can only have one parent" }, JsonOptions);
        }

        var relationComment = string.IsNullOrWhiteSpace(comment)
            ? GetDefaultRelationComment(normalizedRelationType)
            : comment.Trim();

        var workItem = await _azureDevOpsService.LinkWorkItemsAsync(
            sourceWorkItemId,
            targetIds,
            normalizedRelationType,
            relationComment,
            project,
            cancellationToken);

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Work item {sourceWorkItemId} linked to {targetIds.Count} work item(s)",
            sourceWorkItemId,
            targetWorkItemIds = targetIds,
            relationType = normalizedRelationType,
            workItem
        }, JsonOptions);
    }

    [McpServerTool(Name = "get_work_item_relations")]
    [Description("Gets the normalized list of relationships (Parent, Child, Related, Predecessor, Successor, Tests, Tested By, Hyperlink, Attachment) associated with a work item.")]
    public async Task<string> GetWorkItemRelations(
        [Description("The ID of the work item to retrieve relations for")] int workItemId,
        [Description("Optional relation type to filter by (e.g. 'Related', 'Parent', 'Child', 'Predecessor', 'Successor')")] string? relationTypeFilter = null,
        [Description("Whether to retrieve full summaries (ID, Title, State, Type) for the target work items inline (default: false)")] bool expandTargetSummary = false,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        if (workItemId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Work item ID must be a positive integer" }, JsonOptions);
        }

        var result = await _azureDevOpsService.GetWorkItemRelationsAsync(
            workItemId, relationTypeFilter, expandTargetSummary, project, cancellationToken);

        if (result is null)
        {
            return JsonSerializer.Serialize(new { error = $"Work item {workItemId} not found" }, JsonOptions);
        }

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool(Name = "get_work_item_tree")]
    [Description("Recursively gets parent and child work items starting from a root work item, up to a specified depth limit. Includes cycle detection to prevent infinite recursion on loop links.")]
    public async Task<string> GetWorkItemTree(
        [Description("The ID of the root work item to build the tree from")] int workItemId,
        [Description("Maximum depth to recurse (1-5, default: 2)")] int maxDepth = 2,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        if (workItemId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Work item ID must be a positive integer" }, JsonOptions);
        }

        var result = await _azureDevOpsService.GetWorkItemTreeAsync(workItemId, maxDepth, project, cancellationToken);
        if (result is null)
        {
            return JsonSerializer.Serialize(new { error = $"Work item {workItemId} not found" }, JsonOptions);
        }

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool(Name = "get_recent_work_items")]
    [Description("Gets recently changed work items with pagination. Returns a summary view (ID, Title, Type, State, Priority) to reduce payload size. Use get_work_item to get full details of a specific item.")]
    public async Task<string> GetRecentWorkItems(
        [Description("The project name (required)")] string project,
        [Description("Number of days to look back (default: 7, max: 30)")] int daysBack = 7,
        [Description("Page number, starting from 1 (default: 1)")] int page = 1,
        [Description("Number of items per page (default: 20, max: 20)")] int pageSize = 20,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        daysBack = Math.Clamp(daysBack, 1, 30);

        var sinceDate = DateTime.UtcNow.AddDays(-daysBack).ToString("yyyy-MM-dd");

        var wiqlQuery = $@"
            SELECT [System.Id]
            FROM WorkItems
            WHERE [System.TeamProject] = '{EscapeWiqlString(project)}'
            AND [System.ChangedDate] >= '{sinceDate}'
            ORDER BY [System.ChangedDate] DESC";

        var result = await _azureDevOpsService.QueryWorkItemsSummaryAsync(wiqlQuery, project, page, pageSize, cancellationToken);
        return JsonSerializer.Serialize(new
        {
            sinceDate,
            daysBack,
            totalCount = result.TotalCount,
            page = result.Page,
            pageSize = result.PageSize,
            totalPages = result.TotalPages,
            hasNextPage = result.HasNextPage,
            hasPreviousPage = result.HasPreviousPage,
            items = result.Items
        }, JsonOptions);
    }

    [McpServerTool(Name = "search_work_items")]
    [Description("Searches work items by title with pagination. Returns a summary view (ID, Title, Type, State, Priority) to reduce payload size. Use get_work_item to get full details of a specific item.")]
    public async Task<string> SearchWorkItems(
        [Description("The search text to find in work item titles")] string searchText,
        [Description("The project name (required)")] string project,
        [Description("Optional work item type filter (e.g., 'Bug', 'Task', 'User Story')")] string? workItemType = null,
        [Description("Page number, starting from 1 (default: 1)")] int page = 1,
        [Description("Number of items per page (default: 20, max: 20)")] int pageSize = 20,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        var typeFilter = string.IsNullOrWhiteSpace(workItemType)
            ? string.Empty
            : $" AND [System.WorkItemType] = '{EscapeWiqlString(workItemType)}'";

        var wiqlQuery = $@"
            SELECT [System.Id]
            FROM WorkItems
            WHERE [System.TeamProject] = '{EscapeWiqlString(project)}'
            AND [System.Title] CONTAINS '{EscapeWiqlString(searchText)}'{typeFilter}
            ORDER BY [System.ChangedDate] DESC";

        var result = await _azureDevOpsService.QueryWorkItemsSummaryAsync(wiqlQuery, project, page, pageSize, cancellationToken);
        return JsonSerializer.Serialize(new
        {
            searchText,
            workItemType,
            totalCount = result.TotalCount,
            page = result.Page,
            pageSize = result.PageSize,
            totalPages = result.TotalPages,
            hasNextPage = result.HasNextPage,
            hasPreviousPage = result.HasPreviousPage,
            items = result.Items
        }, JsonOptions);
    }

    [McpServerTool(Name = "get_work_item_attachments")]
    [Description("Lists files attached to a work item. Returns each attachment's name, size (bytes), upload/modified dates, optional comment, and download URL. Authenticate with the same PAT to fetch the binary content from the URL.")]
    public async Task<string> GetWorkItemAttachments(
        [Description("The ID of the work item")] int workItemId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        if (workItemId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Work item ID must be a positive integer" }, JsonOptions);
        }

        var attachments = await _azureDevOpsService.GetWorkItemAttachmentsAsync(workItemId, project, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            workItemId,
            count = attachments.Count,
            attachments
        }, JsonOptions);
    }

    [McpServerTool(Name = "get_work_item_attachment_content")]
    [Description("Downloads the content of a work item attachment by its GUID (use get_work_item_attachments to discover GUIDs) and returns it inline. Text files come back as UTF-8; binary files as base64-encoded bytes. Refuses files larger than maxBytes (default 10 MB) â€” for those, use the URL from get_work_item_attachments and download out-of-band.")]
    public async Task<string> GetWorkItemAttachmentContent(
        [Description("The attachment GUID (e.g., '2ee06d3b-4ea4-4390-80de-474a4e1e4355')")] string attachmentId,
        [Description("Optional original filename (echoed back in the response)")] string? fileName = null,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("Maximum bytes to download (default 10485760 = 10 MB). Larger attachments are rejected.")] int maxBytes = 10 * 1024 * 1024,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        if (!Guid.TryParse(attachmentId, out var guid))
        {
            return JsonSerializer.Serialize(new { error = "attachmentId must be a valid GUID" }, JsonOptions);
        }

        if (maxBytes <= 0)
        {
            return JsonSerializer.Serialize(new { error = "maxBytes must be positive" }, JsonOptions);
        }

        var content = await _azureDevOpsService.GetWorkItemAttachmentContentAsync(
            guid, fileName, project, maxBytes, cancellationToken);

        if (content is null)
        {
            return JsonSerializer.Serialize(new { error = $"Attachment {attachmentId} not found" }, JsonOptions);
        }

        return JsonSerializer.Serialize(content, JsonOptions);
    }

    [McpServerTool(Name = "add_work_item_comment")]
    [Description("Adds a comment to a specific Azure DevOps work item. Use this to add notes, updates, or feedback to a work item.")]
    public async Task<string> AddWorkItemComment(
        [Description("The ID of the work item to comment on")] int workItemId,
        [Description("The comment text to add")] string comment,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        if (string.IsNullOrWhiteSpace(comment))
        {
            return JsonSerializer.Serialize(new { error = "Comment text cannot be empty" }, JsonOptions);
        }

        var createdComment = await _azureDevOpsService.AddWorkItemCommentAsync(workItemId, comment, project, cancellationToken);
        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Comment added to work item {workItemId}",
            comment = createdComment
        }, JsonOptions);
    }

    [McpServerTool(Name = "get_work_item_comments")]
    [Description("Gets the comments (discussion history) of a specific Azure DevOps work item. Returns each comment's author, text, creation/modification timestamps, and supports pagination via continuationToken.")]
    public async Task<string> GetWorkItemComments(
        [Description("The ID of the work item whose comments will be retrieved")] int workItemId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("Maximum number of comments to return per page (default: 200)")] int? top = null,
        [Description("Continuation token from a previous response to fetch the next page")] string? continuationToken = null,
        [Description("Whether to include deleted comments (default: false)")] bool includeDeleted = false,
        [Description("Sort order: 'asc' (oldest first) or 'desc' (newest first). Defaults to server order.")] string? order = null,
        [Description("If true, includes the rendered HTML of each comment in addition to its Markdown text (default: false)")] bool includeRenderedText = false,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        if (workItemId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "workItemId must be a positive integer" }, JsonOptions);
        }

        if (top.HasValue && top.Value <= 0)
        {
            return JsonSerializer.Serialize(new { error = "top must be a positive integer" }, JsonOptions);
        }

        if (!string.IsNullOrWhiteSpace(order))
        {
            var normalized = order.Trim().ToLowerInvariant();
            if (normalized is not ("asc" or "desc" or "ascending" or "descending" or "oldest" or "newest"))
            {
                return JsonSerializer.Serialize(new { error = "order must be 'asc' or 'desc'" }, JsonOptions);
            }
        }

        var result = await _azureDevOpsService.GetWorkItemCommentsAsync(
            workItemId,
            project,
            top,
            continuationToken,
            includeDeleted,
            order,
            includeRenderedText,
            cancellationToken);

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool(Name = "create_work_item")]
    [Description("Creates a new work item in Azure DevOps. Supports setting all standard fields plus custom fields via additionalFields.")]
    public async Task<string> CreateWorkItem(
        [Description("The project name where the work item will be created")] string project,
        [Description("The type of work item to create (e.g., 'Bug', 'Task', 'User Story', 'Feature', 'Epic')")] string workItemType,
        [Description("The title of the work item")] string title,
        [Description("The description of the work item (supports HTML)")] string? description = null,
        [Description("The display name or email of the user to assign the work item to")] string? assignedTo = null,
        [Description("The area path for the work item")] string? areaPath = null,
        [Description("The iteration path for the work item")] string? iterationPath = null,
        [Description("The initial state of the work item (e.g., 'New', 'Active')")] string? state = null,
        [Description("The priority of the work item (1-4, where 1 is highest)")] int? priority = null,
        [Description("The ID of the parent work item to link to")] int? parentId = null,
        [Description("Semicolon-separated tags (e.g., 'tag1; tag2; tag3')")] string? tags = null,
        [Description("JSON string of additional fields as key-value pairs (e.g., '{\"Custom.Field\": \"value\"}')")] string? additionalFields = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        if (string.IsNullOrWhiteSpace(project))
        {
            return JsonSerializer.Serialize(new { error = "Project name is required" }, JsonOptions);
        }

        if (string.IsNullOrWhiteSpace(workItemType))
        {
            return JsonSerializer.Serialize(new { error = "Work item type is required" }, JsonOptions);
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return JsonSerializer.Serialize(new { error = "Title is required" }, JsonOptions);
        }

        if (priority.HasValue && (priority.Value < 1 || priority.Value > 4))
        {
            return JsonSerializer.Serialize(new { error = "Priority must be between 1 and 4" }, JsonOptions);
        }

        Dictionary<string, string>? parsedAdditionalFields = null;
        if (!string.IsNullOrWhiteSpace(additionalFields))
        {
            parsedAdditionalFields = ParseAdditionalFields(additionalFields);
            if (parsedAdditionalFields == null)
            {
                return JsonSerializer.Serialize(new { error = "Invalid JSON format for additionalFields" }, JsonOptions);
            }
        }

        var workItem = await _azureDevOpsService.CreateWorkItemAsync(
            project, workItemType, title, description, assignedTo,
            areaPath, iterationPath, state, priority, parentId, tags,
            parsedAdditionalFields, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Work item {workItem.Id} created successfully",
            workItem
        }, JsonOptions);
    }

    [McpServerTool(Name = "update_work_item")]
    [Description("Updates an existing Azure DevOps work item. Only specified fields will be updated; omitted fields remain unchanged.")]
    public async Task<string> UpdateWorkItem(
        [Description("The ID of the work item to update")] int workItemId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("New title for the work item")] string? title = null,
        [Description("New description for the work item (supports HTML)")] string? description = null,
        [Description("New assignee display name or email")] string? assignedTo = null,
        [Description("New state for the work item (e.g., 'Active', 'Closed', 'Resolved')")] string? state = null,
        [Description("New area path")] string? areaPath = null,
        [Description("New iteration path")] string? iterationPath = null,
        [Description("New priority (1-4, where 1 is highest)")] int? priority = null,
        [Description("New semicolon-separated tags (e.g., 'tag1; tag2; tag3')")] string? tags = null,
        [Description("JSON string of additional fields as key-value pairs (e.g., '{\"Custom.Field\": \"value\"}')")] string? additionalFields = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        if (workItemId <= 0)
        {
            return JsonSerializer.Serialize(new { error = "Work item ID must be a positive integer" }, JsonOptions);
        }

        if (priority.HasValue && (priority.Value < 1 || priority.Value > 4))
        {
            return JsonSerializer.Serialize(new { error = "Priority must be between 1 and 4" }, JsonOptions);
        }

        Dictionary<string, string>? parsedAdditionalFields = null;
        if (!string.IsNullOrWhiteSpace(additionalFields))
        {
            parsedAdditionalFields = ParseAdditionalFields(additionalFields);
            if (parsedAdditionalFields == null)
            {
                return JsonSerializer.Serialize(new { error = "Invalid JSON format for additionalFields" }, JsonOptions);
            }
        }

        var workItem = await _azureDevOpsService.UpdateWorkItemAsync(
            workItemId, title, description, assignedTo, state,
            areaPath, iterationPath, priority, tags,
            parsedAdditionalFields, project, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Work item {workItem.Id} updated successfully",
            workItem
        }, JsonOptions);
    }

    private static Dictionary<string, string>? ParseAdditionalFields(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? NormalizeWorkItemRelationType(string? relationType)
    {
        if (string.IsNullOrWhiteSpace(relationType))
        {
            return null;
        }

        var normalized = relationType
            .Trim()
            .Replace('_', '-')
            .Replace(' ', '-')
            .ToLowerInvariant();

        return normalized switch
        {
            "parent" or "hierarchy-reverse" or "system.linktypes.hierarchy-reverse" => "System.LinkTypes.Hierarchy-Reverse",
            "child" or "hierarchy-forward" or "system.linktypes.hierarchy-forward" => "System.LinkTypes.Hierarchy-Forward",
            "predecessor" or "blocked-by" or "depends-on" or "dependency-reverse" or "system.linktypes.dependency-reverse" => "System.LinkTypes.Dependency-Reverse",
            "successor" or "blocks" or "dependency-forward" or "system.linktypes.dependency-forward" => "System.LinkTypes.Dependency-Forward",
            "related" or "system.linktypes.related" => "System.LinkTypes.Related",
            _ => null
        };
    }

    private static string GetDefaultRelationComment(string relationType) =>
        relationType switch
        {
            "System.LinkTypes.Hierarchy-Reverse" => "Parent",
            "System.LinkTypes.Hierarchy-Forward" => "Child",
            "System.LinkTypes.Dependency-Reverse" => "Predecessor",
            "System.LinkTypes.Dependency-Forward" => "Successor",
            "System.LinkTypes.Related" => "Related",
            _ => relationType
        };

    private static List<int> ParseWorkItemIds(string workItemIds)
    {
        if (string.IsNullOrWhiteSpace(workItemIds))
        {
            return [];
        }

        return workItemIds
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => int.TryParse(id, out var parsed) ? parsed : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
    }

    private static string EscapeWiqlString(string value)
    {
        return value.Replace("'", "''");
    }
}
