namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object for work item comments.
/// </summary>
public sealed record WorkItemCommentDto
{
    public int Id { get; init; }
    public int WorkItemId { get; init; }
    public string? Text { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? CreatedDate { get; init; }
}
