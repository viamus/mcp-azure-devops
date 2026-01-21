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
    [Description("Queries Azure DevOps work items using WIQL (Work Item Query Language). Allows flexible searching and filtering of work items.")]
    public async Task<string> QueryWorkItems(
        [Description("The WIQL query string. Example: SELECT [System.Id], [System.Title] FROM WorkItems WHERE [System.State] = 'Active'")] string wiqlQuery,
        [Description("The project name (optional)")] string? project = null,
        [Description("Maximum number of results to return (default: 50, max: 200)")] int top = 50,
        CancellationToken cancellationToken = default)
    {
        top = Math.Clamp(top, 1, 200);

        var workItems = await _azureDevOpsService.QueryWorkItemsAsync(wiqlQuery, project, top, cancellationToken);
        return JsonSerializer.Serialize(new { count = workItems.Count, workItems }, JsonOptions);
    }

    [McpServerTool(Name = "get_work_items_by_state")]
    [Description("Gets work items filtered by state (e.g., 'Active', 'New', 'Closed', 'Resolved'). Useful for quickly finding work items in a specific state.")]
    public async Task<string> GetWorkItemsByState(
        [Description("The state to filter by (e.g., 'Active', 'New', 'Closed', 'Resolved')")] string state,
        [Description("The project name (required)")] string project,
        [Description("Optional work item type filter (e.g., 'Bug', 'Task', 'User Story')")] string? workItemType = null,
        [Description("Maximum number of results (default: 50, max: 200)")] int top = 50,
        CancellationToken cancellationToken = default)
    {
        top = Math.Clamp(top, 1, 200);

        var typeFilter = string.IsNullOrWhiteSpace(workItemType)
            ? string.Empty
            : $" AND [System.WorkItemType] = '{EscapeWiqlString(workItemType)}'";

        var wiqlQuery = $@"
            SELECT [System.Id]
            FROM WorkItems
            WHERE [System.TeamProject] = '{EscapeWiqlString(project)}'
            AND [System.State] = '{EscapeWiqlString(state)}'{typeFilter}
            ORDER BY [System.ChangedDate] DESC";

        var workItems = await _azureDevOpsService.QueryWorkItemsAsync(wiqlQuery, project, top, cancellationToken);
        return JsonSerializer.Serialize(new { count = workItems.Count, state, workItems }, JsonOptions);
    }

    [McpServerTool(Name = "get_work_items_assigned_to")]
    [Description("Gets work items assigned to a specific user. The user can be specified by display name or email.")]
    public async Task<string> GetWorkItemsAssignedTo(
        [Description("The display name or email of the user")] string assignedTo,
        [Description("The project name (required)")] string project,
        [Description("Filter by state (optional, e.g., 'Active')")] string? state = null,
        [Description("Maximum number of results (default: 50, max: 200)")] int top = 50,
        CancellationToken cancellationToken = default)
    {
        top = Math.Clamp(top, 1, 200);

        var stateFilter = string.IsNullOrWhiteSpace(state)
            ? string.Empty
            : $" AND [System.State] = '{EscapeWiqlString(state)}'";

        var wiqlQuery = $@"
            SELECT [System.Id]
            FROM WorkItems
            WHERE [System.TeamProject] = '{EscapeWiqlString(project)}'
            AND [System.AssignedTo] CONTAINS '{EscapeWiqlString(assignedTo)}'{stateFilter}
            ORDER BY [System.ChangedDate] DESC";

        var workItems = await _azureDevOpsService.QueryWorkItemsAsync(wiqlQuery, project, top, cancellationToken);
        return JsonSerializer.Serialize(new { count = workItems.Count, assignedTo, workItems }, JsonOptions);
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
    [Description("Gets recently changed work items in a project. Useful for seeing what's been updated recently.")]
    public async Task<string> GetRecentWorkItems(
        [Description("The project name (required)")] string project,
        [Description("Number of days to look back (default: 7, max: 30)")] int daysBack = 7,
        [Description("Maximum number of results (default: 50, max: 200)")] int top = 50,
        CancellationToken cancellationToken = default)
    {
        daysBack = Math.Clamp(daysBack, 1, 30);
        top = Math.Clamp(top, 1, 200);

        var sinceDate = DateTime.UtcNow.AddDays(-daysBack).ToString("yyyy-MM-dd");

        var wiqlQuery = $@"
            SELECT [System.Id]
            FROM WorkItems
            WHERE [System.TeamProject] = '{EscapeWiqlString(project)}'
            AND [System.ChangedDate] >= '{sinceDate}'
            ORDER BY [System.ChangedDate] DESC";

        var workItems = await _azureDevOpsService.QueryWorkItemsAsync(wiqlQuery, project, top, cancellationToken);
        return JsonSerializer.Serialize(new { count = workItems.Count, sinceDate, workItems }, JsonOptions);
    }

    [McpServerTool(Name = "search_work_items")]
    [Description("Searches work items by title containing the specified text.")]
    public async Task<string> SearchWorkItems(
        [Description("The search text to find in work item titles")] string searchText,
        [Description("The project name (required)")] string project,
        [Description("Optional work item type filter (e.g., 'Bug', 'Task', 'User Story')")] string? workItemType = null,
        [Description("Maximum number of results (default: 50, max: 200)")] int top = 50,
        CancellationToken cancellationToken = default)
    {
        top = Math.Clamp(top, 1, 200);

        var typeFilter = string.IsNullOrWhiteSpace(workItemType)
            ? string.Empty
            : $" AND [System.WorkItemType] = '{EscapeWiqlString(workItemType)}'";

        var wiqlQuery = $@"
            SELECT [System.Id]
            FROM WorkItems
            WHERE [System.TeamProject] = '{EscapeWiqlString(project)}'
            AND [System.Title] CONTAINS '{EscapeWiqlString(searchText)}'{typeFilter}
            ORDER BY [System.ChangedDate] DESC";

        var workItems = await _azureDevOpsService.QueryWorkItemsAsync(wiqlQuery, project, top, cancellationToken);
        return JsonSerializer.Serialize(new { count = workItems.Count, searchText, workItems }, JsonOptions);
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
