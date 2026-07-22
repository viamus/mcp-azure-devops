namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing a normalized work item relationship.
/// </summary>
public sealed record WorkItemRelationDto
{
    public string RelationType { get; init; } = null!;
    public string RawRel { get; init; } = null!;
    public int? TargetId { get; init; }
    public string? TargetUrl { get; init; }
    public string? Comment { get; init; }
    public WorkItemSummaryDto? TargetSummary { get; init; }
}

/// <summary>
/// Data transfer object representing the result of querying work item relations.
/// </summary>
public sealed record WorkItemRelationsResultDto
{
    public int WorkItemId { get; init; }
    public int Count { get; init; }
    public IReadOnlyList<WorkItemRelationDto> Relations { get; init; } = [];
}
