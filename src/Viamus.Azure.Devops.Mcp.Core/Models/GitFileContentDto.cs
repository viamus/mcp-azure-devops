namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing the content of an Azure DevOps Git file.
/// </summary>
public sealed record GitFileContentDto
{
    public string? Path { get; init; }
    public string? CommitId { get; init; }
    public string? Content { get; init; }
    public bool IsBinary { get; init; }
    public string? Encoding { get; init; }
    public long? Size { get; init; }
}
