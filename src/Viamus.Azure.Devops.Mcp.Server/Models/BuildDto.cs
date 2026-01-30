namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing an Azure DevOps Build.
/// </summary>
public sealed record BuildDto
{
    public int Id { get; init; }
    public string? BuildNumber { get; init; }
    public string? Status { get; init; }
    public string? Result { get; init; }
    public string? SourceBranch { get; init; }
    public string? SourceVersion { get; init; }
    public string? RequestedBy { get; init; }
    public string? RequestedFor { get; init; }
    public DateTime? QueueTime { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? FinishTime { get; init; }
    public int? DefinitionId { get; init; }
    public string? DefinitionName { get; init; }
    public string? ProjectId { get; init; }
    public string? ProjectName { get; init; }
    public string? Url { get; init; }
    public string? LogsUrl { get; init; }
    public string? Reason { get; init; }
    public string? Priority { get; init; }
    public string? RepositoryId { get; init; }
    public string? RepositoryName { get; init; }
}
