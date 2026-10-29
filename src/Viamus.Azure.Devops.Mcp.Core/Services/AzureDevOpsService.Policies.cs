using System.Text.Json;
using Microsoft.TeamFoundation.Policy.WebApi;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Services;

public sealed partial class AzureDevOpsService
{
    public async Task<PolicyConfigurationPageDto> GetPolicyConfigurationsAsync(
        string repositoryNameOrId,
        string refName,
        string? project = null,
        int top = 100,
        string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryNameOrId);
        ValidatePolicyRefName(refName);
        ValidatePolicyPage(top);

        try
        {
            var client = await GetGitClientAsync(cancellationToken);
            var repository = await client.GetRepositoryAsync(
                project: project ?? DefaultProject,
                repositoryId: repositoryNameOrId,
                cancellationToken: cancellationToken);
            var projectId = repository.ProjectReference?.Id ?? Guid.Empty;
            if (repository.Id == Guid.Empty || projectId == Guid.Empty)
                throw new InvalidOperationException("Azure DevOps did not return the repository and project IDs required for policy scope.");

            // The Git policy endpoint resolves repository, project and inherited branch scopes on the server.
            // The legacy PolicyHttpClient configuration list does not support hierarchical scope filtering.
            var response = await client.GetPolicyConfigurationsAsync(
                project: projectId,
                repositoryId: repository.Id,
                refName: refName,
                top: top,
                continuationToken: continuationToken,
                cancellationToken: cancellationToken);

            return new PolicyConfigurationPageDto
            {
                RepositoryId = repository.Id.ToString(),
                ProjectId = projectId.ToString(),
                RefName = refName,
                ObservedAt = DateTimeOffset.UtcNow,
                Configurations = response.PolicyConfigurations.Select(MapToPolicyConfigurationDto).ToList(),
                ContinuationToken = response.ContinuationToken
            };
        }
        catch (Exception ex)
        {
            LogOperationError(ex, "Error reading policy configurations for repository {Repository} and ref {RefName}", repositoryNameOrId, refName);
            throw;
        }
    }

    public async Task<PolicyEvaluationPageDto> GetPullRequestPolicyEvaluationsAsync(
        string repositoryNameOrId,
        int pullRequestId,
        string? project = null,
        bool includeNotApplicable = true,
        int top = 100,
        int skip = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryNameOrId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pullRequestId);
        ValidatePolicyPage(top, skip);

        try
        {
            var context = GetOrganizationContext();
            var pullRequest = await (await GetGitClientAsync(cancellationToken)).GetPullRequestAsync(
                project: project ?? context.DefaultProject,
                repositoryId: repositoryNameOrId,
                pullRequestId: pullRequestId,
                cancellationToken: cancellationToken);
            var projectId = pullRequest.Repository?.ProjectReference?.Id ?? Guid.Empty;
            var repositoryId = pullRequest.Repository?.Id ?? Guid.Empty;
            if (projectId == Guid.Empty || repositoryId == Guid.Empty)
                throw new InvalidOperationException("Azure DevOps did not return the repository and project IDs required for policy evaluations.");

            var artifactId = $"vstfs:///CodeReview/CodeReviewId/{projectId}/{pullRequestId}";
            var policyClient = await context.Connection.GetClientAsync<PolicyHttpClient>(cancellationToken);
            var records = await policyClient.GetPolicyEvaluationsAsync(
                project: projectId,
                artifactId: artifactId,
                includeNotApplicable: includeNotApplicable,
                top: top + 1,
                skip: skip,
                cancellationToken: cancellationToken);

            return new PolicyEvaluationPageDto
            {
                RepositoryId = repositoryId.ToString(),
                ProjectId = projectId.ToString(),
                PullRequestId = pullRequestId,
                TargetRefName = pullRequest.TargetRefName,
                ArtifactId = artifactId,
                ObservedAt = DateTimeOffset.UtcNow,
                IncludeNotApplicable = includeNotApplicable,
                Top = top,
                Skip = skip,
                Evaluations = records.Take(top).Select(MapToPolicyEvaluationDto).ToList(),
                NextSkip = records.Count > top ? skip + top : null
            };
        }
        catch (Exception ex)
        {
            LogOperationError(ex, "Error reading policy evaluations for pull request {PullRequestId} in repository {Repository}", pullRequestId, repositoryNameOrId);
            throw;
        }
    }

    private static void ValidatePolicyRefName(string refName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refName);
        if (!refName.StartsWith("refs/heads/", StringComparison.Ordinal) || refName.Length == "refs/heads/".Length || refName.Any(char.IsWhiteSpace))
            throw new ArgumentException("A fully-qualified branch ref such as refs/heads/main is required.", nameof(refName));
    }

    private static void ValidatePolicyPage(int top, int skip = 0)
    {
        if (top is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(top), "Page size must be between 1 and 1000.");
        if (skip < 0 || skip > int.MaxValue - top)
            throw new ArgumentOutOfRangeException(nameof(skip), "Skip must be nonnegative and leave room for the next page.");
    }

    private static PolicyConfigurationDto MapToPolicyConfigurationDto(PolicyConfiguration configuration) => new()
    {
        Id = configuration.Id,
        Revision = configuration.Revision,
        IsEnabled = configuration.IsEnabled,
        IsBlocking = configuration.IsBlocking,
        IsDeleted = configuration.IsDeleted,
        IsEnterpriseManaged = configuration.IsEnterpriseManaged,
        Type = configuration.Type is null ? null : new PolicyTypeDto
        {
            Id = configuration.Type.Id.ToString(),
            DisplayName = configuration.Type.DisplayName
        },
        Settings = PolicyJson(configuration.Settings),
        Url = configuration.Url
    };

    private static PolicyEvaluationDto MapToPolicyEvaluationDto(PolicyEvaluationRecord record) => new()
    {
        EvaluationId = record.EvaluationId.ToString(),
        ArtifactId = record.ArtifactId,
        Status = record.Status?.ToString(),
        StartedDate = record.StartedDate,
        CompletedDate = record.CompletedDate,
        Configuration = record.Configuration is null ? null : MapToPolicyConfigurationDto(record.Configuration),
        Context = PolicyJson(record.Context)
    };

    private static JsonElement? PolicyJson(Newtonsoft.Json.Linq.JObject? value)
    {
        if (value is null) return null;
        using var json = JsonDocument.Parse(value.ToString(Newtonsoft.Json.Formatting.None));
        return json.RootElement.Clone();
    }
}
