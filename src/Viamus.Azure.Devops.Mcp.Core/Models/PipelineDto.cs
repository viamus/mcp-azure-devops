namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing an Azure DevOps Pipeline (Build Definition).
/// </summary>
public sealed record PipelineDto
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? Folder { get; init; }
    public string? Path { get; init; }
    public string? ConfigurationType { get; init; }
    public string? QueueStatus { get; init; }
    public int? Revision { get; init; }
    public string? Url { get; init; }
    public string? ProjectId { get; init; }
    public string? ProjectName { get; init; }
    public DateTime? CreatedDate { get; init; }
}
