using System.Text.RegularExpressions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Viamus.Azure.Devops.Mcp.Core.Errors;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Services;

public sealed partial class AzureDevOpsService
{
    public async Task<GitRefsPageDto> GetRefsAsync(string repositoryNameOrId, string filter = "tags/", int top = 100,
        string? continuationToken = null, string? project = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryNameOrId);
        ValidateGitPage(top, 0);
        var context = GetOrganizationContext();
        var client = await context.Connection.GetClientAsync<GitHistoryHttpClient>(cancellationToken);
        var normalizedFilter = filter?.Trim() ?? string.Empty;
        if (normalizedFilter.StartsWith("refs/", StringComparison.Ordinal)) normalizedFilter = normalizedFilter[5..];
        var result = await client.GetRefsPageAsync(repositoryNameOrId, project ?? context.DefaultProject,
            normalizedFilter, top, continuationToken, cancellationToken);
        return new GitRefsPageDto
        {
            Refs = result.Refs.Select(reference => new GitRefDto
            {
                Name = reference.Name,
                ObjectId = reference.ObjectId,
                PeeledObjectId = reference.PeeledObjectId,
                ResolvedCommitId = string.IsNullOrEmpty(reference.PeeledObjectId) ? reference.ObjectId : reference.PeeledObjectId,
                CreatorName = reference.Creator?.DisplayName
            }).ToList(),
            ContinuationToken = result.ContinuationToken
        };
    }

    public async Task<GitCommitsPageDto> GetCommitsAsync(string repositoryNameOrId, string version, string versionType = "branch",
        int top = 50, int skip = 0, string? project = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryNameOrId);
        ValidateGitPage(top, skip);
        var descriptor = ParseGitHistoryVersion(version, versionType);
        var client = await GetGitClientAsync(cancellationToken);
        var projectName = project ?? DefaultProject;
        var resolved = await ResolveGitHistoryVersionAsync(client, repositoryNameOrId, projectName, descriptor, cancellationToken);
        var commits = await client.GetCommitsAsync(project: projectName, repositoryId: repositoryNameOrId,
            searchCriteria: new GitQueryCommitsCriteria { ItemVersion = new GitVersionDescriptor { Version = resolved, VersionType = GitVersionType.Commit } },
            top: top + 1, skip: skip, cancellationToken: cancellationToken);
        return new GitCommitsPageDto
        {
            ResolvedVersion = resolved,
            Commits = commits.Take(top).Select(MapGitHistoryCommit).ToList(),
            Skip = skip,
            HasMore = commits.Count > top
        };
    }

    public async Task<GitCommitDto> GetCommitAsync(string repositoryNameOrId, string commitId,
        string? project = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryNameOrId);
        var descriptor = ParseGitHistoryVersion(commitId, "commit");
        var commit = await (await GetGitClientAsync(cancellationToken)).GetCommitAsync(
            commitId: descriptor.Version, project: project ?? DefaultProject, repositoryId: repositoryNameOrId,
            changeCount: 0, cancellationToken: cancellationToken);
        return MapGitHistoryCommit(commit);
    }

    public async Task<GitCommitDiffDto> GetCommitDiffAsync(string repositoryNameOrId, string baseVersion, string targetVersion,
        string baseVersionType = "commit", string targetVersionType = "commit", bool diffCommonCommit = false,
        int top = 100, int skip = 0, string? project = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryNameOrId);
        ValidateGitPage(top, skip);
        var baseDescriptor = ParseGitHistoryVersion(baseVersion, baseVersionType);
        var targetDescriptor = ParseGitHistoryVersion(targetVersion, targetVersionType);
        var client = await GetGitClientAsync(cancellationToken);
        var projectName = project ?? DefaultProject;
        var resolved = await Task.WhenAll(
            ResolveGitHistoryVersionAsync(client, repositoryNameOrId, projectName, baseDescriptor, cancellationToken),
            ResolveGitHistoryVersionAsync(client, repositoryNameOrId, projectName, targetDescriptor, cancellationToken));
        var diff = await client.GetCommitDiffsAsync(project: projectName, repositoryId: repositoryNameOrId,
            diffCommonCommit: diffCommonCommit, top: top + 1, skip: skip,
            baseVersionDescriptor: new GitBaseVersionDescriptor { Version = resolved[0], VersionType = GitVersionType.Commit },
            targetVersionDescriptor: new GitTargetVersionDescriptor { Version = resolved[1], VersionType = GitVersionType.Commit },
            cancellationToken: cancellationToken);
        var changes = diff.Changes?.ToList() ?? [];
        var counts = diff.ChangeCounts?.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value) ?? [];
        var total = counts.Count > 0 ? counts.Values.Sum(value => (long)value) : (long?)null;
        var hasMore = changes.Count > top || total > (long)skip + Math.Min(top, changes.Count);
        if (!hasMore && !diff.AllChangesIncluded && total is null)
            throw new InvalidOperationException("Git diff did not confirm that all changes were returned and supplied no total for pagination.");
        if (hasMore && changes.Count == 0) throw new InvalidOperationException("Git diff returned no changes before the advertised end of the diff.");
        return new GitCommitDiffDto
        {
            BaseCommit = diff.BaseCommit ?? resolved[0],
            TargetCommit = diff.TargetCommit ?? resolved[1],
            CommonCommit = diff.CommonCommit,
            AheadCount = diff.AheadCount,
            BehindCount = diff.BehindCount,
            ChangeCounts = counts,
            Changes = changes.Take(top).Select(change => new GitDiffChangeDto
            {
                ChangeType = change.ChangeType.ToString(),
                Path = change.Item?.Path,
                OriginalPath = change.OriginalPath ?? change.SourceServerItem,
                ObjectId = change.Item?.ObjectId,
                IsFolder = change.Item?.IsFolder ?? false
            }).ToList(),
            Skip = skip,
            HasMore = hasMore,
            AllChangesIncluded = skip == 0 && !hasMore
        };
    }

    public async Task<GitFileContentDto?> GetFileAtVersionAsync(string repositoryNameOrId, string filePath, string version,
        string versionType = "commit", string? project = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryNameOrId);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var descriptor = ParseGitHistoryVersion(version, versionType);
        var client = await GetGitClientAsync(cancellationToken);
        var projectName = project ?? DefaultProject;
        try
        {
            var resolved = await ResolveGitHistoryVersionAsync(client, repositoryNameOrId, projectName, descriptor, cancellationToken);
            var pinned = new GitVersionDescriptor { Version = resolved, VersionType = GitVersionType.Commit };
            var item = await client.GetItemAsync(project: projectName, repositoryId: repositoryNameOrId,
                path: filePath, versionDescriptor: pinned, includeContent: false, cancellationToken: cancellationToken);
            if (item.IsFolder) throw new ArgumentException("The requested path is a folder, not a file.", nameof(filePath));
            using var stream = await client.GetItemContentAsync(project: projectName, repositoryId: repositoryNameOrId,
                path: filePath, versionDescriptor: pinned, cancellationToken: cancellationToken);
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync(cancellationToken);
            var binary = content.Take(8000).Any(character => character == '\0');
            return new GitFileContentDto
            {
                Path = item.Path,
                CommitId = resolved,
                Content = binary ? "[Binary file content not shown]" : content,
                IsBinary = binary,
                Encoding = reader.CurrentEncoding.WebName,
                Size = System.Text.Encoding.UTF8.GetByteCount(content)
            };
        }
        catch (Exception exception) when (AzureDevOpsErrorClassifier.IsNotFound(exception))
        {
            return null;
        }
    }

    private static GitVersionDescriptor ParseGitHistoryVersion(string version, string versionType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        var type = versionType?.Trim().ToLowerInvariant() switch
        {
            "branch" => GitVersionType.Branch,
            "tag" => GitVersionType.Tag,
            "commit" => GitVersionType.Commit,
            _ => throw new ArgumentException("Version type must be branch, tag, or commit.", nameof(versionType))
        };
        var normalized = version.Trim();
        if (type == GitVersionType.Branch && normalized.StartsWith("refs/heads/", StringComparison.Ordinal)) normalized = normalized[11..];
        if (type == GitVersionType.Tag && normalized.StartsWith("refs/tags/", StringComparison.Ordinal)) normalized = normalized[10..];
        if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException("A version is required.", nameof(version));
        if (type == GitVersionType.Commit && !Regex.IsMatch(normalized, "\\A[0-9a-fA-F]{40}\\z"))
            throw new ArgumentException("Commit versions must be full 40-character hexadecimal SHAs.", nameof(version));
        return new GitVersionDescriptor { Version = normalized, VersionType = type };
    }

    private static void ValidateGitPage(int top, int skip)
    {
        if (top is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(top), "Page size must be between 1 and 1000.");
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        if (skip > int.MaxValue - top) throw new ArgumentOutOfRangeException(nameof(skip), "Skip plus page size must fit in a 32-bit integer.");
    }

    private static async Task<string> ResolveGitHistoryVersionAsync(GitHttpClient client, string repository, string? project,
        GitVersionDescriptor descriptor, CancellationToken cancellationToken)
    {
        if (descriptor.VersionType == GitVersionType.Commit) return descriptor.Version.ToLowerInvariant();
        var commits = await client.GetCommitsAsync(project: project, repositoryId: repository,
            searchCriteria: new GitQueryCommitsCriteria { ItemVersion = descriptor },
            top: 1, skip: 0, cancellationToken: cancellationToken);
        return commits.FirstOrDefault()?.CommitId
            ?? throw new ArgumentException("The requested version did not resolve to a commit.", nameof(descriptor));
    }

    private static GitCommitDto MapGitHistoryCommit(GitCommitRef commit) => new()
    {
        CommitId = commit.CommitId,
        Comment = commit.Comment,
        CommentTruncated = commit.CommentTruncated,
        AuthorName = commit.Author?.Name,
        AuthorEmail = commit.Author?.Email,
        AuthorDate = commit.Author?.Date,
        CommitterName = commit.Committer?.Name,
        CommitterDate = commit.Committer?.Date,
        Parents = commit.Parents?.ToList() ?? [],
        Url = commit.Url,
        RemoteUrl = commit.RemoteUrl
    };
}
