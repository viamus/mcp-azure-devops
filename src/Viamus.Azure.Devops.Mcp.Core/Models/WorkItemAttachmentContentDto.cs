namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Data transfer object representing the downloaded content of a work item attachment.
/// </summary>
public sealed record WorkItemAttachmentContentDto
{
    public Guid Id { get; init; }
    public string? FileName { get; init; }
    public long Size { get; init; }

    /// <summary>UTF-8 text when <see cref="IsBinary"/> is false; base64-encoded bytes otherwise.</summary>
    public string? Content { get; init; }

    /// <summary>"utf-8" or "base64".</summary>
    public string? Encoding { get; init; }

    public bool IsBinary { get; init; }
}
