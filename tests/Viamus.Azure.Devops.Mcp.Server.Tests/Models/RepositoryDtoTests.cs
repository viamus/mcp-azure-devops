using System.Text.Json;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Models;

public class RepositoryDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void RepositoryDto_ShouldSerializeToJson()
    {
        var dto = new RepositoryDto
        {
            Id = "abc-123",
            Name = "my-repository",
            Url = "https://dev.azure.com/org/project/_apis/git/repositories/abc-123",
            DefaultBranch = "refs/heads/main",
            Size = 1024000,
            RemoteUrl = "https://org@dev.azure.com/org/project/_git/my-repository",
            SshUrl = "git@ssh.dev.azure.com:v3/org/project/my-repository",
            WebUrl = "https://dev.azure.com/org/project/_git/my-repository",
            ProjectId = "project-123",
            ProjectName = "MyProject",
            IsDisabled = false,
            IsFork = false
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"id\":\"abc-123\"", json);
        Assert.Contains("\"name\":\"my-repository\"", json);
        Assert.Contains("\"defaultBranch\":\"refs/heads/main\"", json);
        Assert.Contains("\"size\":1024000", json);
    }

    [Fact]
    public void RepositoryDto_ShouldDeserializeFromJson()
    {
        var json = """
        {
            "id": "xyz-456",
            "name": "test-repo",
            "defaultBranch": "refs/heads/develop",
            "size": 2048000,
            "isDisabled": true,
            "isFork": true
        }
        """;

        var dto = JsonSerializer.Deserialize<RepositoryDto>(json, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal("xyz-456", dto.Id);
        Assert.Equal("test-repo", dto.Name);
        Assert.Equal("refs/heads/develop", dto.DefaultBranch);
        Assert.Equal(2048000, dto.Size);
        Assert.True(dto.IsDisabled);
        Assert.True(dto.IsFork);
    }

    [Fact]
    public void RepositoryDto_RecordEquality_ShouldWork()
    {
        var dto1 = new RepositoryDto
        {
            Id = "abc-123",
            Name = "repo",
            DefaultBranch = "main"
        };

        var dto2 = new RepositoryDto
        {
            Id = "abc-123",
            Name = "repo",
            DefaultBranch = "main"
        };

        Assert.Equal(dto1, dto2);
    }

    [Fact]
    public void RepositoryDto_RecordInequality_ShouldWork()
    {
        var dto1 = new RepositoryDto { Id = "abc-123", Name = "repo1" };
        var dto2 = new RepositoryDto { Id = "abc-456", Name = "repo2" };

        Assert.NotEqual(dto1, dto2);
    }

    [Fact]
    public void RepositoryDto_WithRecord_ShouldSupportWith()
    {
        var original = new RepositoryDto
        {
            Id = "abc-123",
            Name = "original-repo",
            IsDisabled = false
        };

        var modified = original with { IsDisabled = true };

        Assert.Equal("abc-123", modified.Id);
        Assert.Equal("original-repo", modified.Name);
        Assert.True(modified.IsDisabled);
        Assert.NotEqual(original, modified);
    }

    [Fact]
    public void RepositoryDto_NullableProperties_ShouldAllowNull()
    {
        var dto = new RepositoryDto
        {
            Id = null,
            Name = null,
            DefaultBranch = null,
            Size = null,
            ProjectId = null,
            ProjectName = null
        };

        Assert.Null(dto.Id);
        Assert.Null(dto.Name);
        Assert.Null(dto.DefaultBranch);
        Assert.Null(dto.Size);
        Assert.Null(dto.ProjectId);
        Assert.Null(dto.ProjectName);
    }
}
