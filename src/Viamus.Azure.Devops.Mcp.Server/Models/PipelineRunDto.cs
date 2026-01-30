namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing a Pipeline Run (Build).
/// </summary>
public sealed record PipelineRunDto
{
    public int RunId { get; init; }
    public string? Name { get; init; }
    public string? State { get; init; }
    public string? Result { get; init; }
    public DateTime? CreatedDate { get; init; }
    public DateTime? FinishedDate { get; init; }
    public string? SourceBranch { get; init; }
    public string? SourceVersion { get; init; }
    public string? Url { get; init; }
    public int? PipelineId { get; init; }
    public string? PipelineName { get; init; }
}
