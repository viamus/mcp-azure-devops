namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing a node in a recursive work item hierarchy tree.
/// </summary>
public sealed record WorkItemTreeNodeDto
{
    public WorkItemDto WorkItem { get; init; } = null!;
    public IReadOnlyList<WorkItemTreeNodeDto> Children { get; init; } = [];
}
