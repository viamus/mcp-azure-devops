namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing an Azure DevOps Git Item (file or folder).
/// </summary>
public sealed record GitItemDto
{
    public string? ObjectId { get; init; }
    public string? GitObjectType { get; init; }
    public string? CommitId { get; init; }
    public string? Path { get; init; }
    public bool IsFolder { get; init; }
    public string? Url { get; init; }
    public long? Size { get; init; }
}
