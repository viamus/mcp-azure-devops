namespace Viamus.Azure.Devops.Mcp.Server.Services;

/// <summary>
/// Stores the organization selected for the current MCP tool invocation.
/// </summary>
public interface IAzureDevOpsOrganizationContextAccessor
{
    /// <summary>
    /// The selected organization alias or URL for the current async flow.
    /// </summary>
    string? CurrentOrganization { get; }

    /// <summary>
    /// Selects an organization for the current async flow.
    /// </summary>
    IDisposable Use(string? organization);
}
