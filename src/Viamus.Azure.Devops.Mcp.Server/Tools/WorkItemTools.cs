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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public WorkItemTools(IAzureDevOpsService azureDevOpsService)
    {
        _azureDevOpsService = azureDevOpsService;
    }

    [McpServerTool(Name = "get_work_item")]
    [Description("Gets details of a specific Azure DevOps work item by its ID. Returns information such as title, state, assigned to, description, area path, iteration path, and more.")]
    public async Task<string> GetWorkItem(
        [Description("The ID of the work item to retrieve")] int workItemId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        var workItem = await _azureDevOpsService.GetWorkItemAsync(workItemId, project, cancellationToken);

        if (workItem is null)
        {
            return JsonSerializer.Serialize(new { error = $"Work item {workItemId} not found" }, JsonOptions);
        }

        return JsonSerializer.Serialize(workItem, JsonOptions);
    }

    [McpServerTool(Name = "get_work_items")]
    [Description("Gets details of multiple Azure DevOps work items by their IDs. Useful for batch retrieval of work items.")]
    public async Task<string> GetWorkItems(
        [Description("Comma-separated list of work item IDs to retrieve (e.g., '123,456,789')")] string workItemIds,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        var ids = ParseWorkItemIds(workItemIds);

        if (ids.Count == 0)
        {
            return JsonSerializer.Serialize(new { error = "No valid work item IDs provided" }, JsonOptions);
        }

        var workItems = await _azureDevOpsService.GetWorkItemsAsync(ids, project, cancellationToken);
        return JsonSerializer.Serialize(new { count = workItems.Count, workItems }, JsonOptions);
    }

    [McpServerTool(Name = "query_work_items")]
    [Description("Queries Azure DevOps work items using WIQL (Work Item Query Language) with pagination. Returns a summary view (ID, Title, Type, State, Priority) to reduce payload size. Use get_work_item to get full details of a specific item.")]
    public async Task<string> QueryWorkItems(
        [Description("The WIQL query string. Example: SELECT [System.Id], [System.Title] FROM WorkItems WHERE [System.State] = 'Active'")] string wiqlQuery,
        [Description("The project name (optional)")] string? project = null,
        [Description("Page number, starting from 1 (default: 1)")] int page = 1,
        [Description("Number of items per page (default: 20, max: 20)")] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
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
        CancellationToken cancellationToken = default)
    {
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
        CancellationToken cancellationToken = default)
    {
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
        CancellationToken cancellationToken = default)
    {
        var workItems = await _azureDevOpsService.GetChildWorkItemsAsync(parentWorkItemId, project, cancellationToken);
        return JsonSerializer.Serialize(new { parentWorkItemId, count = workItems.Count, children = workItems }, JsonOptions);
    }

    [McpServerTool(Name = "get_recent_work_items")]
    [Description("Gets recently changed work items with pagination. Returns a summary view (ID, Title, Type, State, Priority) to reduce payload size. Use get_work_item to get full details of a specific item.")]
    public async Task<string> GetRecentWorkItems(
        [Description("The project name (required)")] string project,
        [Description("Number of days to look back (default: 7, max: 30)")] int daysBack = 7,
        [Description("Page number, starting from 1 (default: 1)")] int page = 1,
        [Description("Number of items per page (default: 20, max: 20)")] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
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
        CancellationToken cancellationToken = default)
    {
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

    [McpServerTool(Name = "add_work_item_comment")]
    [Description("Adds a comment to a specific Azure DevOps work item. Use this to add notes, updates, or feedback to a work item.")]
    public async Task<string> AddWorkItemComment(
        [Description("The ID of the work item to comment on")] int workItemId,
        [Description("The comment text to add")] string comment,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
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

    private static List<int> ParseWorkItemIds(string workItemIds)
    {
        if (string.IsNullOrWhiteSpace(workItemIds))
        {
            return [];
        }

        return workItemIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
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
