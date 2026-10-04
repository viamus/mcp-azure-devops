namespace Viamus.Azure.Devops.Mcp.Server.Configuration;

/// <summary>
/// Configuration options for Azure DevOps connection.
/// </summary>
public sealed class AzureDevOpsOptions
{
    public const string SectionName = "AzureDevOps";

    /// <summary>
    /// The default Azure DevOps organization URL (e.g., https://dev.azure.com/your-org).
    /// Kept for backward compatibility with single-organization configuration.
    /// </summary>
    public string? OrganizationUrl { get; set; }

    /// <summary>
    /// Personal Access Token (PAT) for the default organization.
    /// Kept for backward compatibility with single-organization configuration.
    /// </summary>
    public string? PersonalAccessToken { get; set; }

    /// <summary>
    /// Default project name (optional).
    /// </summary>
    public string? DefaultProject { get; set; }

    /// <summary>
    /// Default organization name or URL when multiple organizations are configured.
    /// </summary>
    public string? DefaultOrganization { get; set; }

    /// <summary>
    /// Additional Azure DevOps organizations, each with its own PAT.
    /// </summary>
    public IList<AzureDevOpsOrganizationOptions> Organizations { get; set; } = [];

    /// <summary>
    /// Gets all configured organizations, including the backward-compatible root settings.
    /// </summary>
    public IReadOnlyList<AzureDevOpsOrganizationOptions> GetConfiguredOrganizations()
    {
        var organizations = new List<AzureDevOpsOrganizationOptions>();

        if (!string.IsNullOrWhiteSpace(OrganizationUrl) || !string.IsNullOrWhiteSpace(PersonalAccessToken))
        {
            organizations.Add(new AzureDevOpsOrganizationOptions
            {
                Name = GetOrganizationNameFromUrl(OrganizationUrl) ?? "default",
                OrganizationUrl = OrganizationUrl,
                PersonalAccessToken = PersonalAccessToken,
                DefaultProject = DefaultProject
            });
        }

        organizations.AddRange(Organizations.Where(o =>
            !string.IsNullOrWhiteSpace(o.Name) ||
            !string.IsNullOrWhiteSpace(o.OrganizationUrl) ||
            !string.IsNullOrWhiteSpace(o.PersonalAccessToken)));

        return organizations;
    }

    /// <summary>
    /// Validates the Azure DevOps configuration.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        var organizations = GetConfiguredOrganizations();

        if (organizations.Count == 0)
        {
            errors.Add("Configure AzureDevOps:OrganizationUrl and AzureDevOps:PersonalAccessToken, or at least one AzureDevOps:Organizations entry.");
            return errors;
        }

        foreach (var organization in organizations)
        {
            var displayName = string.IsNullOrWhiteSpace(organization.Name)
                ? organization.OrganizationUrl ?? "(unnamed organization)"
                : organization.Name;

            if (string.IsNullOrWhiteSpace(organization.OrganizationUrl))
            {
                errors.Add($"Azure DevOps organization '{displayName}' requires OrganizationUrl.");
            }

            if (string.IsNullOrWhiteSpace(organization.PersonalAccessToken))
            {
                errors.Add($"Azure DevOps organization '{displayName}' requires PersonalAccessToken.");
            }
        }

        return errors;
    }

    private static string? GetOrganizationNameFromUrl(string? organizationUrl)
    {
        if (string.IsNullOrWhiteSpace(organizationUrl) ||
            !Uri.TryCreate(organizationUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            return uri.Segments
                .Select(segment => segment.Trim('/'))
                .FirstOrDefault(segment => !string.IsNullOrWhiteSpace(segment));
        }

        const string visualStudioSuffix = ".visualstudio.com";
        if (uri.Host.EndsWith(visualStudioSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return uri.Host[..^visualStudioSuffix.Length];
        }

        return uri.Host;
    }
}

/// <summary>
/// Configuration options for one Azure DevOps organization.
/// </summary>
public sealed class AzureDevOpsOrganizationOptions
{
    /// <summary>
    /// Friendly organization alias used by tool calls.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The Azure DevOps organization URL (e.g., https://dev.azure.com/your-org).
    /// </summary>
    public string? OrganizationUrl { get; set; }

    /// <summary>
    /// Personal Access Token (PAT) for authentication.
    /// </summary>
    public string? PersonalAccessToken { get; set; }

    /// <summary>
    /// Default project name for this organization (optional).
    /// </summary>
    public string? DefaultProject { get; set; }
}
