using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Viamus.Azure.Devops.Mcp.Server.Configuration;
using Viamus.Azure.Devops.Mcp.Server.Models;
using Viamus.Azure.Devops.Mcp.Server.Services;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Services;

public class AzureDevOpsAutoCompleteTests
{
    private static readonly Guid AuthenticatedIdentityId = Guid.Parse("ca2af105-dea4-4cc3-89c1-f74e9ec7fefd");

    [Fact]
    public void Enable_UsesAuthenticatedIdentityWithoutCompletingOrEditingOtherPullRequestFields()
    {
        var current = new GitPullRequest
        {
            Title = "Existing title",
            Status = PullRequestStatus.Active,
            CreatedBy = new IdentityRef { Id = Guid.NewGuid().ToString() }
        };

        var update = CreateUpdate(current, true);

        Assert.Equal(AuthenticatedIdentityId.ToString(), update.AutoCompleteSetBy.Id);
        Assert.NotEqual(current.CreatedBy.Id, update.AutoCompleteSetBy.Id);
        Assert.Equal(PullRequestStatus.NotSet, update.Status);
        Assert.Null(update.Title);
        Assert.Null(update.Description);
        Assert.Null(update.IsDraft);
        Assert.Null(update.TargetRefName);
        Assert.Equal(GitPullRequestMergeStrategy.NoFastForward, update.CompletionOptions.MergeStrategy);
        Assert.False(update.CompletionOptions.BypassPolicy);
        Assert.False(update.CompletionOptions.DeleteSourceBranch);
    }

    [Fact]
    public void Enable_PreservesExistingCompletionPreferencesAndRemovesPolicyBypass()
    {
        var current = new GitPullRequest
        {
            CompletionOptions = new GitPullRequestCompletionOptions
            {
                MergeStrategy = GitPullRequestMergeStrategy.RebaseMerge,
                DeleteSourceBranch = true,
                MergeCommitMessage = "Keep this message",
                TransitionWorkItems = true,
                AutoCompleteIgnoreConfigIds = [12, 34],
                BypassPolicy = true,
                BypassReason = "Old bypass reason"
            }
        };

        var update = CreateUpdate(current, true);

        Assert.Equal(GitPullRequestMergeStrategy.RebaseMerge, update.CompletionOptions.MergeStrategy);
        Assert.True(update.CompletionOptions.DeleteSourceBranch);
        Assert.Equal("Keep this message", update.CompletionOptions.MergeCommitMessage);
        Assert.True(update.CompletionOptions.TransitionWorkItems);
        Assert.Equal(new[] { 12, 34 }, update.CompletionOptions.AutoCompleteIgnoreConfigIds);
        Assert.False(update.CompletionOptions.BypassPolicy);
        Assert.Null(update.CompletionOptions.BypassReason);
        Assert.True(current.CompletionOptions.BypassPolicy);
    }

    [Fact]
    public void Enable_ExplicitOptionsOverrideExistingValuesIncludingFalse()
    {
        var current = new GitPullRequest
        {
            CompletionOptions = new GitPullRequestCompletionOptions
            {
                MergeStrategy = GitPullRequestMergeStrategy.Rebase,
                DeleteSourceBranch = true
            }
        };

        var update = CreateUpdate(current, true, GitPullRequestMergeStrategy.Squash, false);

        Assert.Equal(GitPullRequestMergeStrategy.Squash, update.CompletionOptions.MergeStrategy);
        Assert.False(update.CompletionOptions.DeleteSourceBranch);
    }

    [Fact]
    public void Enable_PreservesLegacySquashPreferenceWhenExplicitStrategyIsAbsent()
    {
        var current = new GitPullRequest
        {
            CompletionOptions = new GitPullRequestCompletionOptions { SquashMerge = true }
        };

        var update = CreateUpdate(current, true);

        Assert.Equal(GitPullRequestMergeStrategy.Squash, update.CompletionOptions.MergeStrategy);
    }

    [Fact]
    public void Disable_ClearsIdentityWithoutUpdatingCompletionPreferences()
    {
        var current = new GitPullRequest
        {
            AutoCompleteSetBy = new IdentityRef { Id = AuthenticatedIdentityId.ToString() },
            CompletionOptions = new GitPullRequestCompletionOptions { DeleteSourceBranch = true }
        };

        var update = CreateUpdate(current, false);

        Assert.Equal(Guid.Empty.ToString(), update.AutoCompleteSetBy.Id);
        Assert.Null(update.CompletionOptions);
        Assert.Equal(PullRequestStatus.NotSet, update.Status);
    }

    [Fact]
    public void Disable_SdkSerializationSendsEmptyGuidAndOmitsUnrelatedFields()
    {
        var update = CreateUpdate(new GitPullRequest(), false);
        var formatter = new VssJsonMediaTypeFormatter(true);

        var json = JObject.Parse(JsonConvert.SerializeObject(update, formatter.SerializerSettings));

        Assert.Equal(Guid.Empty.ToString(), json["autoCompleteSetBy"]?["id"]?.Value<string>());
        Assert.Null(json["completionOptions"]);
        Assert.Null(json["status"]);
        Assert.Null(json["isDraft"]);
        Assert.Null(json["title"]);
    }

    [Fact]
    public void Enable_SdkCompletionOptionsRoundTripKeepsPolicyBypassAndBranchDeletionDisabled()
    {
        var current = new GitPullRequest
        {
            CompletionOptions = new GitPullRequestCompletionOptions
            {
                BypassPolicy = true,
                BypassReason = "Old bypass reason",
                DeleteSourceBranch = true
            }
        };
        var update = CreateUpdate(current, true, deleteSourceBranch: false);
        var formatter = new VssJsonMediaTypeFormatter(true);

        var json = JObject.Parse(JsonConvert.SerializeObject(update, formatter.SerializerSettings));

        var deserialized = JsonConvert.DeserializeObject<GitPullRequest>(json.ToString(), formatter.SerializerSettings)!;

        // SDK omits default false fields; the supplied completion-options object deserializes to false.
        Assert.NotNull(json["completionOptions"]);
        Assert.False(deserialized.CompletionOptions.BypassPolicy);
        Assert.False(deserialized.CompletionOptions.DeleteSourceBranch);
        Assert.Null(deserialized.CompletionOptions.BypassReason);
        Assert.Null(json["completionOptions"]!["bypassReason"]);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("00000000-0000-0000-0000-000000000000", false)]
    [InlineData("ca2af105-dea4-4cc3-89c1-f74e9ec7fefd", true)]
    public void Mapping_ReportsAutoCompleteStateFromIdentity(string? identityId, bool expectedEnabled)
    {
        var current = new GitPullRequest
        {
            AutoCompleteSetBy = identityId is null ? null : new IdentityRef { Id = identityId, DisplayName = "PAT owner" },
            CompletionOptions = new GitPullRequestCompletionOptions
            {
                MergeStrategy = GitPullRequestMergeStrategy.Squash,
                DeleteSourceBranch = true
            }
        };

        var dto = InvokePrivate<PullRequestDto>("MapToPullRequestDto", current);

        Assert.Equal(expectedEnabled, dto.AutoCompleteEnabled);
        Assert.Equal(identityId, dto.AutoCompleteSetById);
        Assert.Equal(identityId is null ? null : "PAT owner", dto.AutoCompleteSetBy);
        Assert.Equal("Squash", dto.MergeStrategy);
        Assert.True(dto.DeleteSourceBranch);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("999")]
    [InlineData("Squash,Rebase")]
    [InlineData("merge")]
    [InlineData("")]
    public async Task InvalidStrategy_IsRejectedBeforeConnecting(string strategy)
    {
        using var service = CreateService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.SetPullRequestAutoCompleteAsync(
            "repo", 123, true, mergeStrategy: strategy));

        Assert.Equal("mergeStrategy", exception.ParamName);
    }

    [Theory]
    [InlineData("Squash", null)]
    [InlineData(null, false)]
    [InlineData(null, true)]
    public async Task DisableWithCompletionOptions_IsRejectedBeforeConnecting(string? strategy, bool? deleteSourceBranch)
    {
        using var service = CreateService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.SetPullRequestAutoCompleteAsync(
            "repo", 123, false, strategy, deleteSourceBranch));

        Assert.Contains("only be provided when enabling", exception.Message);
    }

    [Theory]
    [InlineData("", 123)]
    [InlineData("repo", 0)]
    [InlineData("repo", -1)]
    public async Task InvalidPullRequestIdentifiers_AreRejectedBeforeConnecting(string repository, int pullRequestId)
    {
        using var service = CreateService();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.SetPullRequestAutoCompleteAsync(repository, pullRequestId, true));
    }

    private static AzureDevOpsService CreateService() => new(
        Options.Create(new AzureDevOpsOptions
        {
            OrganizationUrl = "http://127.0.0.1:1",
            PersonalAccessToken = "fake"
        }),
        NullLogger<AzureDevOpsService>.Instance,
        new AzureDevOpsOrganizationContextAccessor());

    private static GitPullRequest CreateUpdate(
        GitPullRequest current,
        bool enabled,
        GitPullRequestMergeStrategy? strategy = null,
        bool? deleteSourceBranch = null) =>
        InvokePrivate<GitPullRequest>("CreateAutoCompleteUpdate", current, enabled, AuthenticatedIdentityId, strategy, deleteSourceBranch);

    // Exercise the SDK request and response mapping without introducing a client factory solely for tests.
    private static T InvokePrivate<T>(string name, params object?[] arguments) =>
        (T)typeof(AzureDevOpsService).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, arguments)!;
}
