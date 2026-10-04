namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing a file attached to a work item.
/// </summary>
public sealed record WorkItemAttachmentDto
{
    public Guid? Id { get; init; }
    public string? Name { get; init; }
    public long? Size { get; init; }
    public string? Comment { get; init; }
    public DateTime? CreatedDate { get; init; }
    public DateTime? ModifiedDate { get; init; }
    public string? Url { get; init; }
}
