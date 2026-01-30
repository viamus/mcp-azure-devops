namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing a Build Log entry.
/// </summary>
public sealed record BuildLogDto
{
    public int Id { get; init; }
    public string? Type { get; init; }
    public string? Url { get; init; }
    public int LineCount { get; init; }
    public DateTime? CreatedOn { get; init; }
    public DateTime? LastChangedOn { get; init; }
}
