using System.Reflection;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.Policy.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Viamus.Azure.Devops.Mcp.Server.Configuration;
using Viamus.Azure.Devops.Mcp.Server.Services;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Services;

public class AzureDevOpsPolicyTests
{
    private static readonly Guid RepositoryId = Guid.Parse("dc21e1f9-5154-4b2c-9ecb-611c08f280de");
    private static readonly Guid ProjectId = Guid.Parse("b67f1ca6-c786-48ce-999d-e328657350d5");
    private static readonly Guid MinimumReviewersType = Guid.Parse("fa4e907d-c16b-4a4c-9dfa-4906e5d171dd");

    [Theory]
    [InlineData("next-page", true)]
    [InlineData(null, false)]
    public async Task Configurations_UseResolvedRepositoryAndBranch_AndPreserveInheritedSettings(string? nextToken, bool expectedHasMore)
    {
        var accessor = new AzureDevOpsOrganizationContextAccessor();
        using var service = CreateService(accessor);
        using var organization = accessor.Use("secondary");
        using var cancellation = new CancellationTokenSource();
        var client = NewGitClient();
        client.Setup(value => value.GetRepositoryAsync("project", "repo", null, cancellation.Token))
            .ReturnsAsync(new GitRepository { Id = RepositoryId, ProjectReference = new TeamProjectReference { Id = ProjectId } });
        client.Setup(value => value.GetPolicyConfigurationsAsync(
                ProjectId, RepositoryId, "refs/heads/release/1", null, 25, "previous-page", null, cancellation.Token))
            .ReturnsAsync(new GitPolicyConfigurationResponse
            {
                PolicyConfigurations = [MinimumReviewerPolicy()],
                ContinuationToken = nextToken
            });
        RegisterClient(service, client.Object);
        var before = DateTimeOffset.UtcNow;

        var result = await service.GetPolicyConfigurationsAsync(
            "repo", "refs/heads/release/1", "project", 25, "previous-page", cancellation.Token);

        Assert.Equal(RepositoryId.ToString(), result.RepositoryId);
        Assert.Equal(ProjectId.ToString(), result.ProjectId);
        Assert.Equal("refs/heads/release/1", result.RefName);
        Assert.Equal(nextToken, result.ContinuationToken);
        Assert.Equal(expectedHasMore, result.HasMore);
        Assert.InRange(result.ObservedAt, before, DateTimeOffset.UtcNow);
        Assert.Equal(TimeSpan.Zero, result.ObservedAt.Offset);
        var configuration = Assert.Single(result.Configurations);
        Assert.True(configuration.IsEnabled);
        Assert.True(configuration.IsBlocking);
        Assert.Equal(MinimumReviewersType.ToString(), configuration.Type!.Id);
        var settings = configuration.Settings!.Value;
        Assert.Equal(2, settings.GetProperty("minimumApproverCount").GetInt32());
        Assert.False(settings.GetProperty("creatorVoteCounts").GetBoolean());
        Assert.True(settings.GetProperty("resetOnSourcePush").GetBoolean());
        var scope = Assert.Single(settings.GetProperty("scope").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, scope.GetProperty("repositoryId").ValueKind);
        Assert.Equal("Prefix", scope.GetProperty("matchKind").GetString());
        client.VerifyAll();
        client.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Evaluations_ResolveProjectArtifact_AndLookAheadForPagination(bool moreRecords)
    {
        var accessor = new AzureDevOpsOrganizationContextAccessor();
        using var service = CreateService(accessor);
        using var organization = accessor.Use("secondary");
        using var cancellation = new CancellationTokenSource();
        var git = NewGitClient();
        git.Setup(value => value.GetPullRequestAsync(
                "project", "repo", 123, null, null, null, null, null, null, cancellation.Token))
            .ReturnsAsync(new GitPullRequest
            {
                PullRequestId = 123,
                TargetRefName = "refs/heads/main",
                Repository = new GitRepository { Id = RepositoryId, ProjectReference = new TeamProjectReference { Id = ProjectId } }
            });
        RegisterClient(service, git.Object);
        var artifactId = $"vstfs:///CodeReview/CodeReviewId/{ProjectId}/123";
        var records = new List<PolicyEvaluationRecord>
        {
            new() { EvaluationId = Guid.NewGuid(), ArtifactId = artifactId, Status = PolicyEvaluationStatus.Approved, Configuration = MinimumReviewerPolicy(), Context = JObject.Parse("{\"value\":false}") },
            new() { EvaluationId = Guid.NewGuid(), ArtifactId = artifactId, Status = PolicyEvaluationStatus.NotApplicable }
        };
        if (moreRecords) records.Add(new PolicyEvaluationRecord { EvaluationId = Guid.NewGuid(), Status = PolicyEvaluationStatus.Running });
        var formatter = new VssJsonMediaTypeFormatter(true);
        var responseJson = JsonConvert.SerializeObject(new { count = records.Count, value = records }, formatter.SerializerSettings);
        using var handler = new RecordingPolicyHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal($"/{ProjectId}/_apis/policy/evaluations", request.RequestUri!.AbsolutePath);
            var query = QueryHelpers.ParseQuery(request.RequestUri.Query);
            Assert.Equal(artifactId, query["artifactId"].ToString());
            Assert.True(bool.Parse(query["includeNotApplicable"].ToString()));
            Assert.Equal("3", query["$top"].ToString());
            Assert.Equal("4", query["$skip"].ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });
        var policies = new PolicyHttpClient(new Uri("https://secondary.localhost:1"), handler, disposeHandler: false);
        var locations = new ApiResourceLocationCollection();
        locations.AddResourceLocation(new ApiResourceLocation
        {
            Id = Guid.Parse("c23ddff5-229c-4d04-a80b-0fdce9f360c8"),
            Area = "policy",
            ResourceName = "evaluations",
            RouteTemplate = "{project}/_apis/{area}/{resource}",
            MinVersion = new Version(1, 0),
            MaxVersion = new Version(7, 2),
            ReleasedVersion = new Version(7, 1),
            ResourceVersion = 1
        });
        policies.SetResourceLocations(locations);
        RegisterClient(service, policies);

        var result = await service.GetPullRequestPolicyEvaluationsAsync("repo", 123, "project", true, 2, 4, cancellation.Token);

        Assert.Equal(ProjectId.ToString(), result.ProjectId);
        Assert.Equal(RepositoryId.ToString(), result.RepositoryId);
        Assert.Equal("refs/heads/main", result.TargetRefName);
        Assert.Equal(artifactId, result.ArtifactId);
        Assert.Equal(2, result.Count);
        Assert.Equal(moreRecords, result.HasMore);
        Assert.Equal(moreRecords ? 6 : (int?)null, result.NextSkip);
        Assert.Equal("Approved", result.Evaluations[0].Status);
        Assert.Equal("NotApplicable", result.Evaluations[1].Status);
        Assert.False(result.Evaluations[0].Context!.Value.GetProperty("value").GetBoolean());
        Assert.Equal(2, result.Evaluations[0].Configuration!.Settings!.Value.GetProperty("minimumApproverCount").GetInt32());
        git.VerifyAll();
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Configurations_WithoutResolvedScope_DoNotQueryUnscopedPolicies()
    {
        var accessor = new AzureDevOpsOrganizationContextAccessor();
        using var service = CreateService(accessor);
        var git = NewGitClient();
        git.Setup(value => value.GetRepositoryAsync("project", "repo", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitRepository { Id = RepositoryId });
        RegisterClient(service, git.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetPolicyConfigurationsAsync("repo", "refs/heads/main", "project"));

        git.VerifyAll();
        git.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("", "refs/heads/main", 100)]
    [InlineData("repo", "main", 100)]
    [InlineData("repo", "refs/heads/", 100)]
    [InlineData("repo", "refs/heads/main branch", 100)]
    [InlineData("repo", "refs/heads/main", 0)]
    [InlineData("repo", "refs/heads/main", 1001)]
    public async Task InvalidConfigurationsRequest_IsRejectedBeforeConnecting(string repository, string branch, int top)
    {
        using var service = CreateService(new AzureDevOpsOrganizationContextAccessor());

        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.GetPolicyConfigurationsAsync(repository, branch, top: top));
    }

    [Theory]
    [InlineData("", 123, 100, 0)]
    [InlineData("repo", 0, 100, 0)]
    [InlineData("repo", 123, 0, 0)]
    [InlineData("repo", 123, 1001, 0)]
    [InlineData("repo", 123, 100, -1)]
    [InlineData("repo", 123, 100, int.MaxValue)]
    public async Task InvalidEvaluationsRequest_IsRejectedBeforeConnecting(string repository, int id, int top, int skip)
    {
        using var service = CreateService(new AzureDevOpsOrganizationContextAccessor());

        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.GetPullRequestPolicyEvaluationsAsync(repository, id, top: top, skip: skip));
    }

    private sealed class RecordingPolicyHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(respond(request));
        }
    }

    private static PolicyConfiguration MinimumReviewerPolicy() => new()
    {
        Id = 27,
        Revision = 3,
        IsEnabled = true,
        IsBlocking = true,
        Type = new PolicyTypeRef { Id = MinimumReviewersType, DisplayName = "Minimum number of reviewers" },
        Settings = JObject.Parse("""
            {
              "minimumApproverCount": 2,
              "creatorVoteCounts": false,
              "resetOnSourcePush": true,
              "scope": [{ "repositoryId": null, "refName": "refs/heads/release/", "matchKind": "Prefix" }]
            }
            """)
    };

    private static Mock<GitHttpClient> NewGitClient() => new(MockBehavior.Strict, new Uri("https://dev.azure.com/test"), (VssCredentials)new VssBasicCredential("", "fake"));

    private static AzureDevOpsService CreateService(AzureDevOpsOrganizationContextAccessor accessor) => new(
        Options.Create(new AzureDevOpsOptions
        {
            Organizations =
            [
                new() { Name = "primary", OrganizationUrl = "https://primary.localhost:1", PersonalAccessToken = "fake" },
                new() { Name = "secondary", OrganizationUrl = "https://secondary.localhost:1", PersonalAccessToken = "fake" }
            ]
        }), NullLogger<AzureDevOpsService>.Instance, accessor);

    private static void RegisterClient<T>(AzureDevOpsService service, T client) where T : VssHttpClientBase
    {
        // Use the SDK's own client cache so the service's actual scope and pagination calls are exercised.
        var context = typeof(AzureDevOpsService).GetMethod("GetOrganizationContext", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(service, null)!;
        var connection = (VssConnection)context.GetType().GetProperty("Connection")!.GetValue(context)!;
        var connectionType = typeof(VssConnection);
        var serviceIdentifier = connectionType.GetMethod("GetServiceIdentifier", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(connection, [typeof(T)])!;
        var keyType = connectionType.GetNestedType("ClientCacheKey", BindingFlags.NonPublic)!;
        var key = Activator.CreateInstance(keyType, [typeof(T), serviceIdentifier])!;
        var cache = (System.Collections.IDictionary)connectionType.GetField("m_cachedTypes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(connection)!;
        cache[key] = client;
    }
}
