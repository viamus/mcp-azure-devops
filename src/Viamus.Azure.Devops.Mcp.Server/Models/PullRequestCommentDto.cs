namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing a comment in a Pull Request thread.
/// </summary>
public sealed record PullRequestCommentDto
{
    public int Id { get; init; }
    public int? ParentCommentId { get; init; }
    public string? Content { get; init; }
    public string? Author { get; init; }
    public DateTime? PublishedDate { get; init; }
    public DateTime? LastUpdatedDate { get; init; }
    public string? CommentType { get; init; }
}
