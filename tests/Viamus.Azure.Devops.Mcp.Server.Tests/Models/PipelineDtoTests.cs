using System.Text.Json;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Models;

public class PipelineDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void PipelineDto_ShouldSerializeToJson()
    {
        var dto = new PipelineDto
        {
            Id = 123,
            Name = "CI-Build",
            Folder = "\\Builds\\Production",
            Path = "\\Builds\\Production",
            ConfigurationType = "yaml",
            QueueStatus = "Enabled",
            Revision = 5,
            Url = "https://dev.azure.com/org/project/_apis/build/definitions/123",
            ProjectId = "project-123",
            ProjectName = "MyProject",
            CreatedDate = new DateTime(2024, 1, 10, 8, 0, 0)
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"id\":123", json);
        Assert.Contains("\"name\":\"CI-Build\"", json);
        Assert.Contains("\"folder\":\"\\\\Builds\\\\Production\"", json);
        Assert.Contains("\"queueStatus\":\"Enabled\"", json);
        Assert.Contains("\"revision\":5", json);
    }

    [Fact]
    public void PipelineDto_ShouldDeserializeFromJson()
    {
        var json = """
        {
            "id": 456,
            "name": "CD-Deploy",
            "folder": "\\Deploys",
            "configurationType": "designerJson",
            "queueStatus": "Disabled",
            "revision": 10
        }
        """;

        var dto = JsonSerializer.Deserialize<PipelineDto>(json, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal(456, dto.Id);
        Assert.Equal("CD-Deploy", dto.Name);
        Assert.Equal("\\Deploys", dto.Folder);
        Assert.Equal("designerJson", dto.ConfigurationType);
        Assert.Equal("Disabled", dto.QueueStatus);
        Assert.Equal(10, dto.Revision);
    }

    [Fact]
    public void PipelineDto_RecordEquality_ShouldWork()
    {
        var dto1 = new PipelineDto
        {
            Id = 123,
            Name = "Build",
            Folder = "\\Builds"
        };

        var dto2 = new PipelineDto
        {
            Id = 123,
            Name = "Build",
            Folder = "\\Builds"
        };

        Assert.Equal(dto1, dto2);
    }

    [Fact]
    public void PipelineDto_RecordInequality_ShouldWork()
    {
        var dto1 = new PipelineDto { Id = 123, Name = "Build1" };
        var dto2 = new PipelineDto { Id = 456, Name = "Build2" };

        Assert.NotEqual(dto1, dto2);
    }

    [Fact]
    public void PipelineDto_NullableProperties_ShouldAllowNull()
    {
        var dto = new PipelineDto
        {
            Id = 1,
            Name = null,
            Folder = null,
            ConfigurationType = null,
            QueueStatus = null,
            Revision = null,
            ProjectId = null,
            ProjectName = null
        };

        Assert.Null(dto.Name);
        Assert.Null(dto.Folder);
        Assert.Null(dto.ConfigurationType);
        Assert.Null(dto.QueueStatus);
        Assert.Null(dto.Revision);
        Assert.Null(dto.ProjectId);
        Assert.Null(dto.ProjectName);
    }
}
