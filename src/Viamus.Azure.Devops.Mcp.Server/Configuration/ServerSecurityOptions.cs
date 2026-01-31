namespace Viamus.Azure.Devops.Mcp.Server.Configuration;

/// <summary>
/// Configuration options for server security.
/// </summary>
public sealed class ServerSecurityOptions
{
    public const string SectionName = "ServerSecurity";

    /// <summary>
    /// The API key for authentication.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Whether to require API key authentication (default: false).
    /// </summary>
    public bool RequireApiKey { get; set; } = false;
}
