namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing an Azure DevOps Wiki page.
/// </summary>
public sealed record WikiPageDto
{
    public int? Id { get; init; }
    public string? Path { get; init; }
    public string? Content { get; init; }
    public int? Order { get; init; }
    public string? GitItemPath { get; init; }
    public string? RemoteUrl { get; init; }
    public bool IsParentPage { get; init; }
    public List<WikiPageSummaryDto>? SubPages { get; init; }
}

/// <summary>
/// Lightweight summary of a Wiki page for listing operations.
/// </summary>
public sealed record WikiPageSummaryDto
{
    public int? Id { get; init; }
    public string? Path { get; init; }
    public int? Order { get; init; }
    public string? GitItemPath { get; init; }
    public string? RemoteUrl { get; init; }
    public bool IsParentPage { get; init; }
}
