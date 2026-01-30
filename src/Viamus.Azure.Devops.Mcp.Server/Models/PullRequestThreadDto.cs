namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing a comment thread on a Pull Request.
/// </summary>
public sealed record PullRequestThreadDto
{
    public int Id { get; init; }
    public string? Status { get; init; }
    public string? FilePath { get; init; }
    public int? LineNumber { get; init; }
    public DateTime? PublishedDate { get; init; }
    public DateTime? LastUpdatedDate { get; init; }
    public List<PullRequestCommentDto>? Comments { get; init; }
}
