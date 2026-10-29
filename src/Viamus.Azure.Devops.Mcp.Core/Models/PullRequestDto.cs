namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing an Azure DevOps Pull Request.
/// </summary>
public sealed record PullRequestDto
{
    public int PullRequestId { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? SourceBranch { get; init; }
    public string? TargetBranch { get; init; }
    public string? LastMergeSourceCommitId { get; init; }
    public string? LastMergeTargetCommitId { get; init; }
    public string? LastMergeCommitId { get; init; }
    public string? Status { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? CreationDate { get; init; }
    public DateTime? ClosedDate { get; init; }
    public string? MergeStatus { get; init; }
    public bool IsDraft { get; init; }
    public bool AutoCompleteEnabled { get; init; }
    public string? AutoCompleteSetBy { get; init; }
    public string? AutoCompleteSetById { get; init; }
    public string? MergeStrategy { get; init; }
    public bool? DeleteSourceBranch { get; init; }
    public string? RepositoryName { get; init; }
    public string? RepositoryId { get; init; }
    public string? ProjectName { get; init; }
    public string? Url { get; init; }
    public List<PullRequestReviewerDto>? Reviewers { get; init; }
}
