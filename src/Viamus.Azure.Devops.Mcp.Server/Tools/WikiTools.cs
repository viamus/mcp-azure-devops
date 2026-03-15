using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Viamus.Azure.Devops.Mcp.Server.Services;

namespace Viamus.Azure.Devops.Mcp.Server.Tools;

/// <summary>
/// MCP tools for Azure DevOps Wiki operations.
/// </summary>
[McpServerToolType]
public sealed class WikiTools
{
    private readonly IAzureDevOpsService _azureDevOpsService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public WikiTools(IAzureDevOpsService azureDevOpsService)
    {
        _azureDevOpsService = azureDevOpsService;
    }

    [McpServerTool(Name = "get_wikis")]
    [Description("Gets all wikis in an Azure DevOps project. Returns wiki details including name, type (projectWiki or codeWiki), repository, and versions.")]
    public async Task<string> GetWikis(
        [Description("The project name (optional if default project is configured)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        var wikis = await _azureDevOpsService.GetWikisAsync(project, cancellationToken);
        return JsonSerializer.Serialize(new { count = wikis.Count, wikis }, JsonOptions);
    }

    [McpServerTool(Name = "get_wiki")]
    [Description("Gets details of a specific wiki by name or ID. Returns wiki information including type, repository, mapped path, and versions.")]
    public async Task<string> GetWiki(
        [Description("The wiki name or ID")] string wikiIdentifier,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(wikiIdentifier))
        {
            return JsonSerializer.Serialize(new { error = "Wiki name or ID is required" }, JsonOptions);
        }

        var wiki = await _azureDevOpsService.GetWikiAsync(wikiIdentifier, project, cancellationToken);

        if (wiki is null)
        {
            return JsonSerializer.Serialize(new { error = $"Wiki '{wikiIdentifier}' not found" }, JsonOptions);
        }

        return JsonSerializer.Serialize(wiki, JsonOptions);
    }

    [McpServerTool(Name = "get_wiki_page")]
    [Description("Gets a wiki page by path, including its Markdown content. Use this to read the content of a specific wiki page.")]
    public async Task<string> GetWikiPage(
        [Description("The wiki name or ID")] string wikiIdentifier,
        [Description("The page path (e.g., '/Home', '/Getting-Started/Installation')")] string path,
        [Description("Whether to include the page Markdown content (default true)")] bool includeContent = true,
        [Description("Optional version/branch of the wiki")] string? version = null,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(wikiIdentifier))
        {
            return JsonSerializer.Serialize(new { error = "Wiki name or ID is required" }, JsonOptions);
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return JsonSerializer.Serialize(new { error = "Page path is required" }, JsonOptions);
        }

        var page = await _azureDevOpsService.GetWikiPageAsync(wikiIdentifier, path, includeContent, version, project, cancellationToken);

        if (page is null)
        {
            return JsonSerializer.Serialize(new { error = $"Wiki page '{path}' not found in wiki '{wikiIdentifier}'" }, JsonOptions);
        }

        return JsonSerializer.Serialize(new
        {
            wiki = wikiIdentifier,
            page
        }, JsonOptions);
    }

    [McpServerTool(Name = "get_wiki_page_tree")]
    [Description("Gets the page hierarchy (tree structure) of a wiki. Returns the sub-pages of a given page path. Useful for browsing wiki structure and discovering available pages.")]
    public async Task<string> GetWikiPageTree(
        [Description("The wiki name or ID")] string wikiIdentifier,
        [Description("The parent page path to browse (default is root '/')")] string path = "/",
        [Description("Recursion level: 'OneLevel' (immediate children) or 'Full' (all descendants). Default is 'OneLevel'.")] string recursionLevel = "OneLevel",
        [Description("The project name (optional if default project is configured)")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(wikiIdentifier))
        {
            return JsonSerializer.Serialize(new { error = "Wiki name or ID is required" }, JsonOptions);
        }

        var pageTree = await _azureDevOpsService.GetWikiPageTreeAsync(wikiIdentifier, path, recursionLevel, project, cancellationToken);

        if (pageTree is null)
        {
            return JsonSerializer.Serialize(new { error = $"Wiki page '{path}' not found in wiki '{wikiIdentifier}'" }, JsonOptions);
        }

        return JsonSerializer.Serialize(new
        {
            wiki = wikiIdentifier,
            path,
            recursionLevel,
            subPageCount = pageTree.SubPages?.Count ?? 0,
            page = pageTree
        }, JsonOptions);
    }
}
