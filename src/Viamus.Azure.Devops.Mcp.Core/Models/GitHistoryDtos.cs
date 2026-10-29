namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>A branch or tag reference, including the peeled commit for annotated tags.</summary>
public sealed record GitRefDto
{
    public string? Name { get; init; }
    public string? ObjectId { get; init; }
    public string? PeeledObjectId { get; init; }
    public string? ResolvedCommitId { get; init; }
    public string? CreatorName { get; init; }
}

/// <summary>A server-paginated refs result. Continue until ContinuationToken is null.</summary>
public sealed record GitRefsPageDto
{
    public required IReadOnlyList<GitRefDto> Refs { get; init; }
    public int Count => Refs.Count;
    public string? ContinuationToken { get; init; }
    public bool HasMore => !string.IsNullOrEmpty(ContinuationToken);
}

/// <summary>Commit metadata and parent identifiers.</summary>
public sealed record GitCommitDto
{
    public string? CommitId { get; init; }
    public string? Comment { get; init; }
    public bool CommentTruncated { get; init; }
    public string? AuthorName { get; init; }
    public string? AuthorEmail { get; init; }
    public DateTime? AuthorDate { get; init; }
    public string? CommitterName { get; init; }
    public DateTime? CommitterDate { get; init; }
    public IReadOnlyList<string> Parents { get; init; } = [];
    public string? Url { get; init; }
    public string? RemoteUrl { get; init; }
}

/// <summary>Commit history pinned to ResolvedVersion. Reuse that SHA for subsequent pages.</summary>
public sealed record GitCommitsPageDto
{
    public required string ResolvedVersion { get; init; }
    public required IReadOnlyList<GitCommitDto> Commits { get; init; }
    public int Count => Commits.Count;
    public int Skip { get; init; }
    public bool HasMore { get; init; }
    public int? NextSkip => HasMore ? Skip + Count : null;
}

/// <summary>Changed-file metadata; read old and new contents at the returned immutable commit IDs.</summary>
public sealed record GitDiffChangeDto
{
    public string? ChangeType { get; init; }
    public string? Path { get; init; }
    public string? OriginalPath { get; init; }
    public string? ObjectId { get; init; }
    public bool IsFolder { get; init; }
}

/// <summary>A page of changed-file metadata, not a unified text patch.</summary>
public sealed record GitCommitDiffDto
{
    public string? BaseCommit { get; init; }
    public string? TargetCommit { get; init; }
    public string? CommonCommit { get; init; }
    public int? AheadCount { get; init; }
    public int? BehindCount { get; init; }
    public IReadOnlyDictionary<string, int> ChangeCounts { get; init; } = new Dictionary<string, int>();
    public required IReadOnlyList<GitDiffChangeDto> Changes { get; init; }
    public int Count => Changes.Count;
    public int Skip { get; init; }
    public bool HasMore { get; init; }
    public int? NextSkip => HasMore ? Skip + Count : null;
    public bool AllChangesIncluded { get; init; }
}
