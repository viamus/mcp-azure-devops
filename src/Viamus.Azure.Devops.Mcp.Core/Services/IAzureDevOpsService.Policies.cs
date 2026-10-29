using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Services;

public partial interface IAzureDevOpsService
{
    /// <summary>Reads policies applicable to the repository and branch, including inherited policies.</summary>
    Task<PolicyConfigurationPageDto> GetPolicyConfigurationsAsync(
        string repositoryNameOrId,
        string refName,
        string? project = null,
        int top = 100,
        string? continuationToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the current policy evaluations for a pull request without changing policies or votes.</summary>
    Task<PolicyEvaluationPageDto> GetPullRequestPolicyEvaluationsAsync(
        string repositoryNameOrId,
        int pullRequestId,
        string? project = null,
        bool includeNotApplicable = true,
        int top = 100,
        int skip = 0,
        CancellationToken cancellationToken = default);
}
