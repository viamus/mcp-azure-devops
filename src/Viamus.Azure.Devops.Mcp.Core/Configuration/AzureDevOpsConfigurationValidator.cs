namespace Viamus.Azure.Devops.Mcp.Server.Configuration;

/// <summary>Validates host configuration with messages that never echo configured values.</summary>
public static class AzureDevOpsConfigurationValidator
{
    public static IReadOnlyList<string> Validate(AzureDevOpsOptions options)
    {
        var errors = new List<string>();
        var organizations = new List<(AzureDevOpsOrganizationOptions Options, string Path)>();
        if (!string.IsNullOrWhiteSpace(options.OrganizationUrl) ||
            !string.IsNullOrWhiteSpace(options.PersonalAccessToken))
        {
            organizations.Add((new AzureDevOpsOrganizationOptions
            {
                OrganizationUrl = options.OrganizationUrl,
                PersonalAccessToken = options.PersonalAccessToken
            }, "AzureDevOps"));
        }

        if (options.Organizations is null)
        {
            errors.Add("AzureDevOps:Organizations must be an array of organization settings.");
        }
        else
        {
            for (var index = 0; index < options.Organizations.Count; index++)
            {
                var organization = options.Organizations[index];
                if (organization is null)
                {
                    errors.Add($"AzureDevOps:Organizations:{index} must contain organization settings.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(organization.Name) ||
                    !string.IsNullOrWhiteSpace(organization.OrganizationUrl) ||
                    !string.IsNullOrWhiteSpace(organization.PersonalAccessToken))
                {
                    organizations.Add((organization, $"AzureDevOps:Organizations:{index}"));
                }
            }
        }

        if (organizations.Count == 0)
        {
            errors.Add("Set AzureDevOps:OrganizationUrl and AzureDevOps:PersonalAccessToken, or configure AzureDevOps:Organizations. Environment variables use double underscores, for example AzureDevOps__PersonalAccessToken.");
        }

        foreach (var (organization, path) in organizations)
        {
            if (string.IsNullOrWhiteSpace(organization.PersonalAccessToken))
            {
                errors.Add($"{path}:PersonalAccessToken is required. Configure a valid PAT using an environment variable or private settings file.");
            }

            if (string.IsNullOrWhiteSpace(organization.OrganizationUrl))
            {
                errors.Add($"{path}:OrganizationUrl is required.");
            }
            else if (!Uri.TryCreate(organization.OrganizationUrl.Trim(), UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            {
                errors.Add($"{path}:OrganizationUrl must be an absolute HTTP(S) URL without embedded credentials, query parameters, or fragments.");
            }
        }

        return errors;
    }
}
