namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing a reviewer on a Pull Request.
/// </summary>
public sealed record PullRequestReviewerDto
{
    public string? Id { get; init; }
    public string? DisplayName { get; init; }
    public string? UniqueName { get; init; }
    public int Vote { get; init; }
    public bool IsRequired { get; init; }
    public bool HasDeclined { get; init; }
    public string? ImageUrl { get; init; }
}
