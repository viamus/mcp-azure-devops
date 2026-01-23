using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Viamus.Azure.Devops.Mcp.Server.Services;

namespace Viamus.Azure.Devops.Mcp.Server.Tools;

/// <summary>
/// MCP tools for Azure DevOps Git Repository operations.
/// </summary>
[McpServerToolType]
public sealed class GitTools
{
    private readonly IAzureDevOpsService _azureDevOpsService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GitTools(IAzureDevOpsService azureDevOpsService)
    {
        _azureDevOpsService = azureDevOpsService;
    }

    [McpServerTool(Name = "get_repositories")]
    [Description("Gets all Git repositories in an Azure DevOps project. Returns repository details including name, default branch, URLs, and size.")]
    public async Task<string> GetRepositories(
        [Description("The project name (optional if default project is configured)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        var repositories = await _azureDevOpsService.GetRepositoriesAsync(project, cancellationToken);
        return JsonSerializer.Serialize(new { count = repositories.Count, repositories }, JsonOptions);
    }

    [McpServerTool(Name = "get_repository")]
    [Description("Gets details of a specific Git repository by name or ID. Returns repository information including URLs, default branch, and project details.")]
    public async Task<string> GetRepository(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryNameOrId))
        {
            return JsonSerializer.Serialize(new { error = "Repository name or ID is required" }, JsonOptions);
        }

        var repository = await _azureDevOpsService.GetRepositoryAsync(repositoryNameOrId, project, cancellationToken);

        if (repository is null)
        {
            return JsonSerializer.Serialize(new { error = $"Repository '{repositoryNameOrId}' not found" }, JsonOptions);
        }

        return JsonSerializer.Serialize(repository, JsonOptions);
    }

    [McpServerTool(Name = "get_branches")]
    [Description("Gets all branches in a Git repository. Returns branch names, commit IDs, and creator information.")]
    public async Task<string> GetBranches(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryNameOrId))
        {
            return JsonSerializer.Serialize(new { error = "Repository name or ID is required" }, JsonOptions);
        }

        var branches = await _azureDevOpsService.GetBranchesAsync(repositoryNameOrId, project, cancellationToken);
        return JsonSerializer.Serialize(new { repository = repositoryNameOrId, count = branches.Count, branches }, JsonOptions);
    }

    [McpServerTool(Name = "get_repository_items")]
    [Description("Gets files and folders at a specific path in a Git repository. Useful for browsing repository contents.")]
    public async Task<string> GetRepositoryItems(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The path to browse (default is root '/')")] string path = "/",
        [Description("The branch name (optional, uses default branch if not specified)")] string? branchName = null,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("Recursion level: 'None' (only specified item), 'OneLevel' (immediate children), 'Full' (all descendants). Default is 'OneLevel'.")] string recursionLevel = "OneLevel",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryNameOrId))
        {
            return JsonSerializer.Serialize(new { error = "Repository name or ID is required" }, JsonOptions);
        }

        var items = await _azureDevOpsService.GetItemsAsync(repositoryNameOrId, path, branchName, project, recursionLevel, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            repository = repositoryNameOrId,
            path,
            branch = branchName ?? "(default)",
            recursionLevel,
            count = items.Count,
            items
        }, JsonOptions);
    }

    [McpServerTool(Name = "get_file_content")]
    [Description("Gets the content of a specific file in a Git repository. Returns the file content as text. Binary files will show a placeholder message.")]
    public async Task<string> GetFileContent(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The path to the file (e.g., '/src/Program.cs')")] string filePath,
        [Description("The branch name (optional, uses default branch if not specified)")] string? branchName = null,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryNameOrId))
        {
            return JsonSerializer.Serialize(new { error = "Repository name or ID is required" }, JsonOptions);
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return JsonSerializer.Serialize(new { error = "File path is required" }, JsonOptions);
        }

        var fileContent = await _azureDevOpsService.GetFileContentAsync(repositoryNameOrId, filePath, branchName, project, cancellationToken);

        if (fileContent is null)
        {
            return JsonSerializer.Serialize(new { error = $"File '{filePath}' not found in repository '{repositoryNameOrId}'" }, JsonOptions);
        }

        return JsonSerializer.Serialize(new
        {
            repository = repositoryNameOrId,
            branch = branchName ?? "(default)",
            file = fileContent
        }, JsonOptions);
    }

    [McpServerTool(Name = "search_repository_files")]
    [Description("Searches for files in a Git repository by path pattern. Use this to find files by name or extension.")]
    public async Task<string> SearchRepositoryFiles(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The search pattern to match file paths (e.g., '.cs' for C# files, 'Controller' for files containing 'Controller')")] string searchPattern,
        [Description("The starting path to search from (default is root '/')")] string path = "/",
        [Description("The branch name (optional, uses default branch if not specified)")] string? branchName = null,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryNameOrId))
        {
            return JsonSerializer.Serialize(new { error = "Repository name or ID is required" }, JsonOptions);
        }

        if (string.IsNullOrWhiteSpace(searchPattern))
        {
            return JsonSerializer.Serialize(new { error = "Search pattern is required" }, JsonOptions);
        }

        // Get all items recursively
        var allItems = await _azureDevOpsService.GetItemsAsync(repositoryNameOrId, path, branchName, project, "Full", cancellationToken);

        // Filter by search pattern (case-insensitive)
        var matchingItems = allItems
            .Where(item => !item.IsFolder && item.Path != null &&
                           item.Path.Contains(searchPattern, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return JsonSerializer.Serialize(new
        {
            repository = repositoryNameOrId,
            branch = branchName ?? "(default)",
            searchPattern,
            searchPath = path,
            count = matchingItems.Count,
            files = matchingItems
        }, JsonOptions);
    }
}
