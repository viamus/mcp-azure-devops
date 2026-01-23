using System.Text.Json;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Models;

public class GitFileContentDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void GitFileContentDto_ShouldSerializeToJson()
    {
        var dto = new GitFileContentDto
        {
            Path = "/src/Program.cs",
            CommitId = "abc123def456",
            Content = "using System;\n\nclass Program { }",
            IsBinary = false,
            Encoding = "UTF-8",
            Size = 35
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"path\":\"/src/Program.cs\"", json);
        Assert.Contains("\"commitId\":\"abc123def456\"", json);
        Assert.Contains("\"isBinary\":false", json);
        Assert.Contains("\"encoding\":\"UTF-8\"", json);
        Assert.Contains("\"size\":35", json);
    }

    [Fact]
    public void GitFileContentDto_ShouldDeserializeFromJson()
    {
        var json = """
        {
            "path": "/docs/readme.md",
            "commitId": "xyz789",
            "content": "# Readme",
            "isBinary": false,
            "encoding": "UTF-8",
            "size": 8
        }
        """;

        var dto = JsonSerializer.Deserialize<GitFileContentDto>(json, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal("/docs/readme.md", dto.Path);
        Assert.Equal("xyz789", dto.CommitId);
        Assert.Equal("# Readme", dto.Content);
        Assert.False(dto.IsBinary);
        Assert.Equal("UTF-8", dto.Encoding);
        Assert.Equal(8, dto.Size);
    }

    [Fact]
    public void GitFileContentDto_RecordEquality_ShouldWork()
    {
        var dto1 = new GitFileContentDto
        {
            Path = "/file.cs",
            Content = "content"
        };

        var dto2 = new GitFileContentDto
        {
            Path = "/file.cs",
            Content = "content"
        };

        Assert.Equal(dto1, dto2);
    }

    [Fact]
    public void GitFileContentDto_BinaryFile_ShouldHavePlaceholderContent()
    {
        var dto = new GitFileContentDto
        {
            Path = "/images/logo.png",
            CommitId = "abc123",
            Content = "[Binary file content not shown]",
            IsBinary = true,
            Size = 102400
        };

        Assert.True(dto.IsBinary);
        Assert.Equal("[Binary file content not shown]", dto.Content);
    }

    [Fact]
    public void GitFileContentDto_NullableProperties_ShouldAllowNull()
    {
        var dto = new GitFileContentDto
        {
            Path = null,
            CommitId = null,
            Content = null,
            Encoding = null,
            Size = null
        };

        Assert.Null(dto.Path);
        Assert.Null(dto.CommitId);
        Assert.Null(dto.Content);
        Assert.Null(dto.Encoding);
        Assert.Null(dto.Size);
    }
}
