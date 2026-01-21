namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Lightweight data transfer object for work item listings.
/// Contains only essential fields to reduce payload size.
/// </summary>
public sealed record WorkItemSummaryDto
{
    public int Id { get; init; }
    public string? Title { get; init; }
    public string? WorkItemType { get; init; }
    public string? State { get; init; }
    public string? AssignedTo { get; init; }
    public string? Priority { get; init; }
    public DateTime? ChangedDate { get; init; }
    public int? ParentId { get; init; }
}
