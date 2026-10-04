namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing an Azure DevOps Git Branch.
/// </summary>
public sealed record BranchDto
{
    public string? Name { get; init; }
    public string? ObjectId { get; init; }
    public string? CreatorName { get; init; }
    public string? CreatorEmail { get; init; }
    public bool IsBaseVersion { get; init; }
}
