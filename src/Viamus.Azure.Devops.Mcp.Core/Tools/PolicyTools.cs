using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Viamus.Azure.Devops.Mcp.Server.Services;

namespace Viamus.Azure.Devops.Mcp.Server.Tools;

/// <summary>Read-only MCP tools for effective approval policies and PR policy evaluations.</summary>
[McpServerToolType]
public sealed class PolicyTools
{
    private readonly IAzureDevOpsService _service;
    private readonly IAzureDevOpsOrganizationContextAccessor _organizationContextAccessor;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PolicyTools(IAzureDevOpsService service)
        : this(service, new AzureDevOpsOrganizationContextAccessor())
    {
    }

    public PolicyTools(IAzureDevOpsService service, IAzureDevOpsOrganizationContextAccessor organizationContextAccessor)
    {
        _service = service;
        _organizationContextAccessor = organizationContextAccessor;
    }

    [McpServerTool(Name = "get_policy_configurations", ReadOnly = true)]
    [Description("Reads effective policy configurations for a repository and target branch, including inherited project and branch scopes resolved by Azure DevOps. Preserves full settings, minimum reviewer rules, scope, enabled/blocking flags, and observation time. Follow continuationToken until hasMore is false before claiming policy discovery is complete. Does not change policies or approvals.")]
    public async Task<string> GetPolicyConfigurations(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The fully-qualified target branch ref (e.g., refs/heads/develop)")] string refName,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("Maximum number of configurations per page, between 1 and 1000 (default: 100)")] int top = 100,
        [Description("The exact continuationToken from the preceding page; omit on the first page")] string? continuationToken = null,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        if (string.IsNullOrWhiteSpace(repositoryNameOrId)) return Error("Repository name or ID is required");
        if (string.IsNullOrWhiteSpace(refName) || !refName.StartsWith("refs/heads/", StringComparison.Ordinal) || refName.Length == "refs/heads/".Length || refName.Any(char.IsWhiteSpace))
            return Error("A fully-qualified branch ref such as refs/heads/main is required");
        if (top is < 1 or > 1000) return Error("Page size must be between 1 and 1000");

        var result = await _service.GetPolicyConfigurationsAsync(repositoryNameOrId, refName, project, top, continuationToken, cancellationToken);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool(Name = "get_pull_request_policy_evaluations", ReadOnly = true)]
    [Description("Reads current policy evaluation statuses for a pull request, including each policy's full configuration and settings. Resolves the PR's project and repository before querying policy evaluations. Follow nextSkip until hasMore is false. Review votes alone do not prove that a minimum-reviewer policy is enabled or required. Does not requeue evaluations or change policies, approvals, or PR state.")]
    public async Task<string> GetPullRequestPolicyEvaluations(
        [Description("The repository name or ID")] string repositoryNameOrId,
        [Description("The pull request ID")] int pullRequestId,
        [Description("The project name (optional if default project is configured)")] string? project = null,
        [Description("Include evaluations whose status is NotApplicable (default: true)")] bool includeNotApplicable = true,
        [Description("Maximum number of evaluations per page, between 1 and 1000 (default: 100)")] int top = 100,
        [Description("Number of evaluations to skip; use nextSkip from the preceding page (default: 0)")] int skip = 0,
        [Description("The Azure DevOps organization alias or URL (optional if default organization is configured)")] string? organization = null,
        CancellationToken cancellationToken = default)
    {
        using var organizationScope = _organizationContextAccessor.Use(organization);
        if (string.IsNullOrWhiteSpace(repositoryNameOrId)) return Error("Repository name or ID is required");
        if (pullRequestId <= 0) return Error("Pull request ID must be a positive integer");
        if (top is < 1 or > 1000) return Error("Page size must be between 1 and 1000");
        if (skip < 0 || skip > int.MaxValue - top) return Error("Skip must be nonnegative and leave room for the next page");

        var result = await _service.GetPullRequestPolicyEvaluationsAsync(repositoryNameOrId, pullRequestId, project, includeNotApplicable, top, skip, cancellationToken);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    private static string Error(string message) => JsonSerializer.Serialize(new { error = message }, JsonOptions);
}
