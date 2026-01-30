namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing a Build Timeline Record (stage, job, or task).
/// </summary>
public sealed record BuildTimelineRecordDto
{
    public string? Id { get; init; }
    public string? ParentId { get; init; }
    public string? Type { get; init; }
    public string? Name { get; init; }
    public string? State { get; init; }
    public string? Result { get; init; }
    public int Order { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? FinishTime { get; init; }
    public int? ErrorCount { get; init; }
    public int? WarningCount { get; init; }
    public string? LogUrl { get; init; }
    public int? PercentComplete { get; init; }
}
