using System.Text.Json;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Models;

public class GitItemDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void GitItemDto_ShouldSerializeToJson()
    {
        var dto = new GitItemDto
        {
            ObjectId = "abc123def456",
            GitObjectType = "Blob",
            CommitId = "commit789",
            Path = "/src/Program.cs",
            IsFolder = false,
            Url = "https://dev.azure.com/org/project/_apis/git/repositories/repo/items",
            Size = 2048
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"objectId\":\"abc123def456\"", json);
        Assert.Contains("\"gitObjectType\":\"Blob\"", json);
        Assert.Contains("\"path\":\"/src/Program.cs\"", json);
        Assert.Contains("\"isFolder\":false", json);
        Assert.Contains("\"size\":2048", json);
    }

    [Fact]
    public void GitItemDto_ShouldDeserializeFromJson()
    {
        var json = """
        {
            "objectId": "xyz789",
            "gitObjectType": "Tree",
            "commitId": "commit123",
            "path": "/src",
            "isFolder": true,
            "size": null
        }
        """;

        var dto = JsonSerializer.Deserialize<GitItemDto>(json, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal("xyz789", dto.ObjectId);
        Assert.Equal("Tree", dto.GitObjectType);
        Assert.Equal("/src", dto.Path);
        Assert.True(dto.IsFolder);
        Assert.Null(dto.Size);
    }

    [Fact]
    public void GitItemDto_RecordEquality_ShouldWork()
    {
        var dto1 = new GitItemDto
        {
            ObjectId = "abc123",
            Path = "/src/file.cs"
        };

        var dto2 = new GitItemDto
        {
            ObjectId = "abc123",
            Path = "/src/file.cs"
        };

        Assert.Equal(dto1, dto2);
    }

    [Fact]
    public void GitItemDto_FolderItem_ShouldHaveNullSize()
    {
        var dto = new GitItemDto
        {
            ObjectId = "abc123",
            Path = "/src",
            IsFolder = true,
            Size = null
        };

        Assert.True(dto.IsFolder);
        Assert.Null(dto.Size);
    }

    [Fact]
    public void GitItemDto_FileItem_ShouldHaveSize()
    {
        var dto = new GitItemDto
        {
            ObjectId = "abc123",
            Path = "/src/file.cs",
            IsFolder = false,
            Size = 1024
        };

        Assert.False(dto.IsFolder);
        Assert.Equal(1024, dto.Size);
    }
}
