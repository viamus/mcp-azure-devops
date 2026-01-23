using System.Text.Json;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Models;

public class BranchDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void BranchDto_ShouldSerializeToJson()
    {
        var dto = new BranchDto
        {
            Name = "refs/heads/main",
            ObjectId = "abc123def456",
            CreatorName = "John Doe",
            CreatorEmail = "john.doe@example.com",
            IsBaseVersion = true
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"name\":\"refs/heads/main\"", json);
        Assert.Contains("\"objectId\":\"abc123def456\"", json);
        Assert.Contains("\"creatorName\":\"John Doe\"", json);
        Assert.Contains("\"isBaseVersion\":true", json);
    }

    [Fact]
    public void BranchDto_ShouldDeserializeFromJson()
    {
        var json = """
        {
            "name": "refs/heads/feature/new-feature",
            "objectId": "xyz789abc123",
            "creatorName": "Jane Smith",
            "creatorEmail": "jane.smith@example.com",
            "isBaseVersion": false
        }
        """;

        var dto = JsonSerializer.Deserialize<BranchDto>(json, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal("refs/heads/feature/new-feature", dto.Name);
        Assert.Equal("xyz789abc123", dto.ObjectId);
        Assert.Equal("Jane Smith", dto.CreatorName);
        Assert.Equal("jane.smith@example.com", dto.CreatorEmail);
        Assert.False(dto.IsBaseVersion);
    }

    [Fact]
    public void BranchDto_RecordEquality_ShouldWork()
    {
        var dto1 = new BranchDto
        {
            Name = "main",
            ObjectId = "abc123"
        };

        var dto2 = new BranchDto
        {
            Name = "main",
            ObjectId = "abc123"
        };

        Assert.Equal(dto1, dto2);
    }

    [Fact]
    public void BranchDto_RecordInequality_ShouldWork()
    {
        var dto1 = new BranchDto { Name = "main", ObjectId = "abc123" };
        var dto2 = new BranchDto { Name = "develop", ObjectId = "xyz456" };

        Assert.NotEqual(dto1, dto2);
    }

    [Fact]
    public void BranchDto_NullableProperties_ShouldAllowNull()
    {
        var dto = new BranchDto
        {
            Name = null,
            ObjectId = null,
            CreatorName = null,
            CreatorEmail = null
        };

        Assert.Null(dto.Name);
        Assert.Null(dto.ObjectId);
        Assert.Null(dto.CreatorName);
        Assert.Null(dto.CreatorEmail);
    }
}
