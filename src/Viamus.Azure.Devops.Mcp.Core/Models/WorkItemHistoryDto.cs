namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing a single state or column transition of a work item.
/// </summary>
public sealed record WorkItemStateTransitionDto
{
    public int Revision { get; init; }
    public string State { get; init; } = string.Empty;
    public string? PreviousState { get; init; }
    public string? BoardColumn { get; init; }
    public string? PreviousBoardColumn { get; init; }
    public string MovedBy { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public double? DurationInHours { get; init; }
}

/// <summary>
/// Data transfer object containing full activity history for a work item.
/// </summary>
public sealed record WorkItemHistoryResultDto
{
    public int WorkItemId { get; init; }
    public int TotalTransitions { get; init; }
    public IReadOnlyList<WorkItemStateTransitionDto> Transitions { get; init; } = Array.Empty<WorkItemStateTransitionDto>();
}
