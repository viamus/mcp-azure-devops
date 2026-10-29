using System.Text.Json;

namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>A policy configuration, preserving provider settings and branch scopes.</summary>
public sealed record PolicyConfigurationDto
{
    public int Id { get; init; }
    public int Revision { get; init; }
    public bool IsEnabled { get; init; }
    public bool IsBlocking { get; init; }
    public bool IsDeleted { get; init; }
    public bool IsEnterpriseManaged { get; init; }
    public PolicyTypeDto? Type { get; init; }
    public JsonElement? Settings { get; init; }
    public string? Url { get; init; }
}

/// <summary>Identifies the Azure DevOps policy type.</summary>
public sealed record PolicyTypeDto
{
    public string? Id { get; init; }
    public string? DisplayName { get; init; }
}

/// <summary>One server-filtered page of effective repository and branch policies.</summary>
public sealed record PolicyConfigurationPageDto
{
    public required string RepositoryId { get; init; }
    public required string ProjectId { get; init; }
    public required string RefName { get; init; }
    public DateTimeOffset ObservedAt { get; init; }
    public required IReadOnlyList<PolicyConfigurationDto> Configurations { get; init; }
    public int Count => Configurations.Count;
    public string? ContinuationToken { get; init; }
    public bool HasMore => !string.IsNullOrEmpty(ContinuationToken);
}
