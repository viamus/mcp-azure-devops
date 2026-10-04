namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing an Azure DevOps Git Repository.
/// </summary>
public sealed record RepositoryDto
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Url { get; init; }
    public string? DefaultBranch { get; init; }
    public long? Size { get; init; }
    public string? RemoteUrl { get; init; }
    public string? SshUrl { get; init; }
    public string? WebUrl { get; init; }
    public string? ProjectId { get; init; }
    public string? ProjectName { get; init; }
    public bool IsDisabled { get; init; }
    public bool IsFork { get; init; }
}
