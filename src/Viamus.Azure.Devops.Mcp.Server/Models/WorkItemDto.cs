namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing an Azure DevOps Work Item.
/// </summary>
public sealed record WorkItemDto
{
    public int Id { get; init; }
    public string? Title { get; init; }
    public string? WorkItemType { get; init; }
    public string? State { get; init; }
    public string? AssignedTo { get; init; }
    public string? Description { get; init; }
    public string? AreaPath { get; init; }
    public string? IterationPath { get; init; }
    public string? Priority { get; init; }
    public string? Severity { get; init; }
    public DateTime? CreatedDate { get; init; }
    public DateTime? ChangedDate { get; init; }
    public string? CreatedBy { get; init; }
    public string? ChangedBy { get; init; }
    public string? Reason { get; init; }
    public int? ParentId { get; init; }
    public string? Url { get; init; }
    public Dictionary<string, object?>? CustomFields { get; init; }

    /// <summary>
    /// List of commit IDs linked to this work item.
    /// </summary>
    public List<WorkItemCommitLinkDto>? LinkedCommits { get; init; }

    /// <summary>
    /// List of pull request IDs linked to this work item.
    /// </summary>
    public List<WorkItemPullRequestLinkDto>? LinkedPullRequests { get; init; }
}

/// <summary>
/// Represents a commit link associated with a work item.
/// </summary>
public sealed record WorkItemCommitLinkDto
{
    public string? CommitId { get; init; }
    public string? RepositoryId { get; init; }
    public string? Url { get; init; }
}

/// <summary>
/// Represents a pull request link associated with a work item.
/// </summary>
public sealed record WorkItemPullRequestLinkDto
{
    public int PullRequestId { get; init; }
    public string? RepositoryId { get; init; }
    public string? Url { get; init; }
}
