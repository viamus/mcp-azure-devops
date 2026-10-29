using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using Viamus.Azure.Devops.Mcp.Server.Services;

namespace Viamus.Azure.Devops.Mcp.Server.Tools;

/// <summary>Read-only Git revision discovery, history, comparison, and pinned file content.</summary>
[McpServerToolType]
public sealed class GitHistoryTools(IAzureDevOpsService service, IAzureDevOpsOrganizationContextAccessor organizationContextAccessor)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    [McpServerTool(Name = "get_refs", ReadOnly = true)]
    [Description("Gets one server-paginated page of Git refs, including tags and their peeled commit IDs. Defaults to tags/. Follow continuationToken until null to enumerate every tag; never assume a partial page is the full catalog.")]
    public async Task<string> GetRefs(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("Ref prefix: tags/ (default), heads/, refs/tags/, refs/heads/, or empty for all refs")] string filter = "tags/",
        [Description("Page size, between 1 and 1000 (default: 100)")] int top = 100,
        [Description("Opaque continuationToken returned by the previous page; omit for the first page")] string? continuationToken = null,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = organizationContextAccessor.Use(organization);
        var error = ValidateGitHistoryRequest(repositoryNameOrId, top, 0);
        if (error is not null) return Error(error);
        return JsonSerializer.Serialize(await service.GetRefsAsync(repositoryNameOrId, filter, top, continuationToken, project, cancellationToken), JsonOptions);
    }

    [McpServerTool(Name = "get_commits", ReadOnly = true)]
    [Description("Gets a page of commit history at an explicit branch, tag, or full commit SHA. The response includes resolvedVersion; reuse it with versionType=commit for every subsequent page so a moving branch cannot change the history being reviewed.")]
    public async Task<string> GetCommits(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The branch/tag name or full 40-character commit SHA")] string version,
        [Description("How version is interpreted: branch (default), tag, or commit")] string versionType = "branch",
        [Description("Page size, between 1 and 1000 (default: 50)")] int top = 50,
        [Description("Number of commits to skip; use nextSkip with the returned resolvedVersion")] int skip = 0,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = organizationContextAccessor.Use(organization);
        var error = ValidateGitHistoryRequest(repositoryNameOrId, top, skip) ?? ValidateHistoryVersion(version, versionType);
        if (error is not null) return Error(error);
        return JsonSerializer.Serialize(await service.GetCommitsAsync(repositoryNameOrId, version, versionType, top, skip, project, cancellationToken), JsonOptions);
    }

    [McpServerTool(Name = "get_commit", ReadOnly = true)]
    [Description("Gets metadata for one exact Git commit, including its message, author, dates, and parent commit IDs. Use get_commit_diff to enumerate changed files.")]
    public async Task<string> GetCommit(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The full 40-character hexadecimal commit SHA")] string commitId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = organizationContextAccessor.Use(organization);
        var error = ValidateGitHistoryRequest(repositoryNameOrId, 1, 0) ?? ValidateHistoryVersion(commitId, "commit");
        if (error is not null) return Error(error);
        return JsonSerializer.Serialize(await service.GetCommitAsync(repositoryNameOrId, commitId, project, cancellationToken), JsonOptions);
    }

    [McpServerTool(Name = "get_commit_diff", ReadOnly = true)]
    [Description("Gets a page of changed-file metadata between explicit branch/tag/commit revisions. Returns pinned baseCommit and targetCommit. Follow nextSkip with those SHAs until hasMore=false. This is not a unified text patch: read each path with get_file_at_version at the returned commits to review old and new text; for merge-base comparison use commonCommit for old text.")]
    public async Task<string> GetCommitDiff(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The base branch/tag name or full commit SHA")] string baseVersion,
        [Description("The target branch/tag name or full commit SHA")] string targetVersion,
        [Description("Base revision type: branch, tag, or commit (default: commit)")] string baseVersionType = "commit",
        [Description("Target revision type: branch, tag, or commit (default: commit)")] string targetVersionType = "commit",
        [Description("Compare merge-base to target when true; compare base to target when false (default)")] bool diffCommonCommit = false,
        [Description("Page size, between 1 and 1000 (default: 100)")] int top = 100,
        [Description("Number of changes to skip; use nextSkip with the returned pinned commits")] int skip = 0,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = organizationContextAccessor.Use(organization);
        var error = ValidateGitHistoryRequest(repositoryNameOrId, top, skip)
            ?? ValidateHistoryVersion(baseVersion, baseVersionType) ?? ValidateHistoryVersion(targetVersion, targetVersionType);
        if (error is not null) return Error(error);
        return JsonSerializer.Serialize(await service.GetCommitDiffAsync(repositoryNameOrId, baseVersion, targetVersion,
            baseVersionType, targetVersionType, diffCommonCommit, top, skip, project, cancellationToken), JsonOptions);
    }

    [McpServerTool(Name = "get_file_at_version", ReadOnly = true)]
    [Description("Reads complete text file content at an explicit branch, tag, or full commit SHA. Metadata and content are pinned to one resolved commit. Use commit revisions from get_commit_diff for stable old/new comparisons. Binary content is identified and not returned as text.")]
    public async Task<string> GetFileAtVersion(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The file path, e.g. /.ambevtech/pipelines/pipeline.yml")] string filePath,
        [Description("The branch/tag name or full 40-character commit SHA")] string version,
        [Description("Revision type: branch, tag, or commit (default: commit)")] string versionType = "commit",
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = organizationContextAccessor.Use(organization);
        var error = ValidateGitHistoryRequest(repositoryNameOrId, 1, 0) ?? ValidateHistoryVersion(version, versionType);
        if (error is not null) return Error(error);
        if (string.IsNullOrWhiteSpace(filePath)) return Error("File path is required");
        var file = await service.GetFileAtVersionAsync(repositoryNameOrId, filePath, version, versionType, project, cancellationToken);
        return file is null ? Error("The requested file or revision was not found") : JsonSerializer.Serialize(new { repository = repositoryNameOrId, file }, JsonOptions);
    }

    private static string? ValidateGitHistoryRequest(string repository, int top, int skip)
    {
        if (string.IsNullOrWhiteSpace(repository)) return "Repository name or ID is required";
        if (top is < 1 or > 1000) return "Page size must be between 1 and 1000";
        if (skip < 0) return "Skip must be non-negative";
        if (skip > int.MaxValue - top) return "Skip plus page size must fit in a 32-bit integer";
        return null;
    }

    private static string? ValidateHistoryVersion(string version, string versionType)
    {
        if (string.IsNullOrWhiteSpace(version)) return "Version is required";
        var type = versionType?.Trim().ToLowerInvariant();
        if (type is not ("branch" or "tag" or "commit")) return "Version type must be branch, tag, or commit";
        if (type == "commit" && !Regex.IsMatch(version.Trim(), "\\A[0-9a-fA-F]{40}\\z")) return "Commit versions must be full 40-character hexadecimal SHAs";
        return null;
    }

    private static string Error(string message) => JsonSerializer.Serialize(new { error = message }, JsonOptions);
}
