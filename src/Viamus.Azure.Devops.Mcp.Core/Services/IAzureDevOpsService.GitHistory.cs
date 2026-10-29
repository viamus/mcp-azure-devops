using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Services;

public partial interface IAzureDevOpsService
{
    /// <summary>Gets one page of refs with the actual server continuation token and peeled tags.</summary>
    Task<GitRefsPageDto> GetRefsAsync(string repositoryNameOrId, string filter = "tags/", int top = 100,
        string? continuationToken = null, string? project = null, CancellationToken cancellationToken = default);

    /// <summary>Gets commit history pinned to the resolved version. Reuse the returned SHA to page safely.</summary>
    Task<GitCommitsPageDto> GetCommitsAsync(string repositoryNameOrId, string version, string versionType = "branch",
        int top = 50, int skip = 0, string? project = null, CancellationToken cancellationToken = default);

    /// <summary>Gets metadata for one exact commit SHA.</summary>
    Task<GitCommitDto> GetCommitAsync(string repositoryNameOrId, string commitId,
        string? project = null, CancellationToken cancellationToken = default);

    /// <summary>Gets a page of changes between two pinned revisions, including their resolved commit IDs.</summary>
    Task<GitCommitDiffDto> GetCommitDiffAsync(string repositoryNameOrId, string baseVersion, string targetVersion,
        string baseVersionType = "commit", string targetVersionType = "commit", bool diffCommonCommit = false,
        int top = 100, int skip = 0, string? project = null, CancellationToken cancellationToken = default);

    /// <summary>Reads file content at a branch, tag, or exact commit, pinning metadata and content to the same SHA.</summary>
    Task<GitFileContentDto?> GetFileAtVersionAsync(string repositoryNameOrId, string filePath, string version,
        string versionType = "commit", string? project = null, CancellationToken cancellationToken = default);
}
