using System.Text.Json;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Models;

public class BuildLogDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void BuildLogDto_ShouldSerializeToJson()
    {
        var dto = new BuildLogDto
        {
            Id = 1,
            Type = "Container",
            Url = "https://dev.azure.com/org/project/_apis/build/builds/123/logs/1",
            LineCount = 500,
            CreatedOn = new DateTime(2024, 1, 15, 10, 0, 0),
            LastChangedOn = new DateTime(2024, 1, 15, 10, 15, 0)
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"id\":1", json);
        Assert.Contains("\"type\":\"Container\"", json);
        Assert.Contains("\"lineCount\":500", json);
    }

    [Fact]
    public void BuildLogDto_ShouldDeserializeFromJson()
    {
        var json = """
        {
            "id": 5,
            "type": "TaskLog",
            "url": "https://example.com/logs/5",
            "lineCount": 1000
        }
        """;

        var dto = JsonSerializer.Deserialize<BuildLogDto>(json, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal(5, dto.Id);
        Assert.Equal("TaskLog", dto.Type);
        Assert.Equal("https://example.com/logs/5", dto.Url);
        Assert.Equal(1000, dto.LineCount);
    }

    [Fact]
    public void BuildLogDto_RecordEquality_ShouldWork()
    {
        var dto1 = new BuildLogDto { Id = 1, Type = "Container", LineCount = 100 };
        var dto2 = new BuildLogDto { Id = 1, Type = "Container", LineCount = 100 };

        Assert.Equal(dto1, dto2);
    }

    [Fact]
    public void BuildLogDto_RecordInequality_ShouldWork()
    {
        var dto1 = new BuildLogDto { Id = 1, LineCount = 100 };
        var dto2 = new BuildLogDto { Id = 2, LineCount = 200 };

        Assert.NotEqual(dto1, dto2);
    }

    [Fact]
    public void BuildLogDto_NullableProperties_ShouldAllowNull()
    {
        var dto = new BuildLogDto
        {
            Id = 1,
            Type = null,
            Url = null,
            CreatedOn = null,
            LastChangedOn = null
        };

        Assert.Null(dto.Type);
        Assert.Null(dto.Url);
        Assert.Null(dto.CreatedOn);
        Assert.Null(dto.LastChangedOn);
    }
}
