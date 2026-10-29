using System.Reflection;
using System.Text.Json;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Viamus.Azure.Devops.Mcp.Server.Models;
using Viamus.Azure.Devops.Mcp.Server.Services;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Services;

public class PullRequestRevisionTests
{
    [Fact]
    public void Mapping_PreservesSourceTargetAndMergeSnapshotCommitIds()
    {
        var pullRequest = new GitPullRequest
        {
            LastMergeSourceCommit = new GitCommitRef { CommitId = new string('a', 40) },
            LastMergeTargetCommit = new GitCommitRef { CommitId = new string('b', 40) },
            LastMergeCommit = new GitCommitRef { CommitId = new string('c', 40) }
        };
        var map = typeof(AzureDevOpsService).GetMethod("MapToPullRequestDto", BindingFlags.NonPublic | BindingFlags.Static)!;
        var dto = (PullRequestDto)map.Invoke(null, [pullRequest])!;
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(dto, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));

        Assert.Equal(new string('a', 40), json.RootElement.GetProperty("lastMergeSourceCommitId").GetString());
        Assert.Equal(new string('b', 40), json.RootElement.GetProperty("lastMergeTargetCommitId").GetString());
        Assert.Equal(new string('c', 40), json.RootElement.GetProperty("lastMergeCommitId").GetString());
    }
}
