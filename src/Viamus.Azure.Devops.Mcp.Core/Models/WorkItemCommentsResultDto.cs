namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Result wrapper for a page of work item comments.
/// </summary>
public sealed record WorkItemCommentsResultDto
{
    public required IReadOnlyList<WorkItemCommentDto> Comments { get; init; }
    public int TotalCount { get; init; }
    public int Count { get; init; }
    public string? ContinuationToken { get; init; }
    public string? NextPage { get; init; }
}
