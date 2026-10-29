using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;
using Viamus.Azure.Devops.Mcp.Server.Configuration;
using Viamus.Azure.Devops.Mcp.Server.Services;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Services;

public class GitHistoryServiceTests
{
    private const string Head = "1111111111111111111111111111111111111111";
    private const string Parent = "2222222222222222222222222222222222222222";

    [Fact]
    public async Task RefsAdapter_PreservesOpaquePaging_AndReadsPeeledTagsAndHeader()
    {
        Uri? requested = null;
        var handler = new ResponseHandler(request =>
        {
            requested = request.RequestUri;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"count\":1,\"value\":[{\"name\":\"refs/tags/v1\",\"objectId\":\"tag-object\",\"peeledObjectId\":\"" + Head + "\"}]}", Encoding.UTF8, "application/json")
            };
            response.Headers.Add("x-ms-continuationtoken", "next+/=");
            return response;
        });
        var adapterType = typeof(AzureDevOpsService).Assembly.GetType("Viamus.Azure.Devops.Mcp.Server.Services.GitHistoryHttpClient")!;
        var adapter = Activator.CreateInstance(adapterType, new Uri("https://dev.azure.com/test/"), handler, true)!;
        using var service = CreateService(adapter, adapterType);

        var page = await service.GetRefsAsync("repo name", "refs/tags/v", 10, "previous+/=", "project & name");

        Assert.Equal(Head, Assert.Single(page.Refs).ResolvedCommitId);
        Assert.Equal("tag-object", page.Refs[0].ObjectId);
        Assert.Equal("next+/=", page.ContinuationToken);
        Assert.True(page.HasMore);
        Assert.Contains("/project%20%26%20name/_apis/git/repositories/repo%20name/refs", requested!.AbsoluteUri);
        Assert.Contains("filter=tags%2Fv", requested.Query);
        Assert.Contains("%24top=10", requested.Query);
        Assert.Contains("continuationToken=previous%2B%2F%3D", requested.Query);
        Assert.Contains("peelTags=true", requested.Query);
    }

    [Fact]
    public async Task Commits_ResolvesBranchBeforePaging_AndQueriesPinnedCommitWithLookahead()
    {
        using var cancellation = new CancellationTokenSource();
        var client = NewClient();
        client.Setup(value => value.GetCommitsAsync("project", "repo",
                It.Is<GitQueryCommitsCriteria>(criteria => criteria.ItemVersion.Version == "main" && criteria.ItemVersion.VersionType == GitVersionType.Branch),
                0, 1, null, cancellation.Token))
            .ReturnsAsync([new GitCommitRef { CommitId = Head }]);
        client.Setup(value => value.GetCommitsAsync("project", "repo",
                It.Is<GitQueryCommitsCriteria>(criteria => criteria.ItemVersion.Version == Head && criteria.ItemVersion.VersionType == GitVersionType.Commit),
                3, 2, null, cancellation.Token))
            .ReturnsAsync([new GitCommitRef { CommitId = Parent, Parents = [Head] }, new GitCommitRef { CommitId = Head }]);
        using var service = CreateService(client.Object, typeof(GitHttpClient));

        var page = await service.GetCommitsAsync("repo", "refs/heads/main", top: 1, skip: 3, cancellationToken: cancellation.Token);

        Assert.Equal(Head, page.ResolvedVersion);
        Assert.Equal(Parent, Assert.Single(page.Commits).CommitId);
        Assert.True(page.HasMore);
        Assert.Equal(4, page.NextSkip);
        client.VerifyAll();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Diff_UsesPinnedDescriptors_AndDoesNotClaimPageContainsEntireDiff(bool allIncluded)
    {
        var client = NewClient();
        client.Setup(value => value.GetCommitDiffsAsync("project", "repo", false, 2, 0,
                It.Is<GitBaseVersionDescriptor>(version => version.Version == Parent && version.VersionType == GitVersionType.Commit),
                It.Is<GitTargetVersionDescriptor>(version => version.Version == Head && version.VersionType == GitVersionType.Commit), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitCommitDiffs
            {
                BaseCommit = Parent,
                TargetCommit = Head,
                AllChangesIncluded = allIncluded,
                ChangeCounts = new() { [VersionControlChangeType.Edit] = 2 },
                Changes = [new() { ChangeType = VersionControlChangeType.Edit, Item = new() { Path = "/a.yml" } }, new() { Item = new() { Path = "/b.yml" } }]
            });
        using var service = CreateService(client.Object, typeof(GitHttpClient));

        var diff = await service.GetCommitDiffAsync("repo", Parent, Head, top: 1);

        Assert.Equal("/a.yml", Assert.Single(diff.Changes).Path);
        Assert.True(diff.HasMore);
        Assert.False(diff.AllChangesIncluded);
        Assert.Equal(1, diff.NextSkip);
        client.VerifyAll();
    }

    [Fact]
    public async Task Diff_RejectsUnprovenIncompleteResponseWithoutCounts()
    {
        var client = NewClient();
        client.Setup(value => value.GetCommitDiffsAsync("project", "repo", false, 101, 0,
                It.IsAny<GitBaseVersionDescriptor>(), It.IsAny<GitTargetVersionDescriptor>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitCommitDiffs { AllChangesIncluded = false, Changes = [new() { Item = new() { Path = "/a" } }] });
        using var service = CreateService(client.Object, typeof(GitHttpClient));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetCommitDiffAsync("repo", Parent, Head));

        Assert.Contains("did not confirm", failure.Message);
    }

    [Fact]
    public async Task Diff_LastPageIsExhaustedEvenThoughPageIsNotEntireDiff()
    {
        var client = NewClient();
        client.Setup(value => value.GetCommitDiffsAsync("project", "repo", false, 2, 1,
                It.IsAny<GitBaseVersionDescriptor>(), It.IsAny<GitTargetVersionDescriptor>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitCommitDiffs
            {
                AllChangesIncluded = false,
                ChangeCounts = new() { [VersionControlChangeType.Edit] = 2 },
                Changes = [new() { Item = new() { Path = "/last" } }]
            });
        using var service = CreateService(client.Object, typeof(GitHttpClient));

        var diff = await service.GetCommitDiffAsync("repo", Parent, Head, top: 1, skip: 1);

        Assert.False(diff.HasMore);
        Assert.Null(diff.NextSkip);
        Assert.False(diff.AllChangesIncluded);
    }

    [Fact]
    public async Task FileAtVersion_UsesSameImmutableCommitForMetadataAndContent()
    {
        var client = NewClient();
        client.Setup(value => value.GetCommitsAsync("project", "repo",
                It.Is<GitQueryCommitsCriteria>(criteria => criteria.ItemVersion.Version == "v1" && criteria.ItemVersion.VersionType == GitVersionType.Tag),
                0, 1, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GitCommitRef { CommitId = Head }]);
        client.Setup(value => value.GetItemAsync("project", "repo", "/file.yml", null, null, null, null, null,
                It.Is<GitVersionDescriptor>(version => version.Version == Head && version.VersionType == GitVersionType.Commit), false, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitItem { Path = "/file.yml", CommitId = Head });
        client.Setup(value => value.GetItemContentAsync("project", "repo", "/file.yml", null, null, null, null, null,
                It.Is<GitVersionDescriptor>(version => version.Version == Head && version.VersionType == GitVersionType.Commit), null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("ref: v1\n")));
        using var service = CreateService(client.Object, typeof(GitHttpClient));

        var file = await service.GetFileAtVersionAsync("repo", "/file.yml", "refs/tags/v1", "tag");

        Assert.Equal(Head, file!.CommitId);
        Assert.Equal("ref: v1\n", file.Content);
        client.VerifyAll();
    }

    [Theory]
    [InlineData("0", "main", 1, 0)]
    [InlineData("commit", "short", 1, 0)]
    [InlineData("branch", "main", 1, int.MaxValue)]
    public async Task InvalidArguments_RejectBeforeAnyNetwork(string type, string version, int top, int skip)
    {
        using var service = CreateService();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.GetCommitsAsync("repo", version, type, top, skip));
    }

    private static Mock<GitHttpClient> NewClient() => new(MockBehavior.Strict, new Uri("https://dev.azure.com/test"), new VssCredentials());

    private static AzureDevOpsService CreateService(object? client = null, Type? clientType = null)
    {
        var service = new AzureDevOpsService(Options.Create(new AzureDevOpsOptions
        {
            OrganizationUrl = "https://dev.azure.com/test",
            PersonalAccessToken = "test-only",
            DefaultProject = "project"
        }), NullLogger<AzureDevOpsService>.Instance, new AzureDevOpsOrganizationContextAccessor());
        if (client is not null)
        {
            var context = typeof(AzureDevOpsService).GetMethod("GetOrganizationContext", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(service, null)!;
            var connection = (VssConnection)context.GetType().GetProperty("Connection")!.GetValue(context)!;
            var connectionType = typeof(VssConnection);
            var serviceIdentifier = connectionType.GetMethod("GetServiceIdentifier", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(connection, [clientType])!;
            var keyType = connectionType.GetNestedType("ClientCacheKey", BindingFlags.NonPublic)!;
            var key = Activator.CreateInstance(keyType, [clientType, serviceIdentifier])!;
            var cache = (System.Collections.IDictionary)connectionType.GetField("m_cachedTypes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(connection)!;
            cache[key] = client;
        }
        return service;
    }

    private sealed class ResponseHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var result = response(request);
            result.RequestMessage ??= request;
            return Task.FromResult(result);
        }
    }
}
