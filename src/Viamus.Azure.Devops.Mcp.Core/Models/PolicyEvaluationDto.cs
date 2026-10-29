using System.Text.Json;

namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>The current evaluation of one policy on a pull request.</summary>
public sealed record PolicyEvaluationDto
{
    public string? EvaluationId { get; init; }
    public string? ArtifactId { get; init; }
    public string? Status { get; init; }
    public DateTime? StartedDate { get; init; }
    public DateTime? CompletedDate { get; init; }
    public PolicyConfigurationDto? Configuration { get; init; }
    public JsonElement? Context { get; init; }
}

/// <summary>One page of policy evaluations with explicit navigation metadata.</summary>
public sealed record PolicyEvaluationPageDto
{
    public required string RepositoryId { get; init; }
    public required string ProjectId { get; init; }
    public int PullRequestId { get; init; }
    public string? TargetRefName { get; init; }
    public required string ArtifactId { get; init; }
    public DateTimeOffset ObservedAt { get; init; }
    public bool IncludeNotApplicable { get; init; }
    public int Top { get; init; }
    public int Skip { get; init; }
    public required IReadOnlyList<PolicyEvaluationDto> Evaluations { get; init; }
    public int Count => Evaluations.Count;
    public int? NextSkip { get; init; }
    public bool HasMore => NextSkip.HasValue;
}
