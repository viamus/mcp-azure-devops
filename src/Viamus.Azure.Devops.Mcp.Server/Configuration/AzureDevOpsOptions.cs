namespace Viamus.Azure.Devops.Mcp.Server.Configuration;

/// <summary>
/// Configuration options for Azure DevOps connection.
/// </summary>
public sealed class AzureDevOpsOptions
{
    public const string SectionName = "AzureDevOps";

    /// <summary>
    /// The Azure DevOps organization URL (e.g., https://dev.azure.com/your-org).
    /// </summary>
    public required string OrganizationUrl { get; set; }

    /// <summary>
    /// Personal Access Token (PAT) for authentication.
    /// </summary>
    public required string PersonalAccessToken { get; set; }

    /// <summary>
    /// Default project name (optional).
    /// </summary>
    public string? DefaultProject { get; set; }
}
