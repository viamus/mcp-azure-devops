namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing an Azure DevOps Wiki.
/// </summary>
public sealed record WikiDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Type { get; init; }
    public string? Url { get; init; }
    public string? RemoteUrl { get; init; }
    public string? ProjectId { get; init; }
    public string? ProjectName { get; init; }
    public string? RepositoryId { get; init; }
    public string? MappedPath { get; init; }
    public List<string>? Versions { get; init; }
}
