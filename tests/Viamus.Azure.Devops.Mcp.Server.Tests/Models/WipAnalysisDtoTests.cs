using System.Text.Json;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Models;

public class WipAnalysisDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void WipAnalysisDto_ShouldSerializeToJson()
    {
        var dto = new WipAnalysisDto
        {
            AnalysisDate = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            TotalWip = 42,
            ByState =
            [
                new WipByStateDto { State = "Active", Count = 20, Percentage = 47.6, AverageAgeDays = 5.2 },
                new WipByStateDto { State = "In Progress", Count = 15, Percentage = 35.7, AverageAgeDays = 3.1 }
            ],
            ByType = new Dictionary<string, int>
            {
                { "Bug", 15 },
                { "User Story", 20 },
                { "Task", 7 }
            },
            Insights = ["Total WIP: 42 items", "Warning: high accumulation in Active state"]
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"totalWip\":42", json);
        Assert.Contains("\"byState\":", json);
        Assert.Contains("\"byType\":", json);
        Assert.Contains("\"insights\":", json);
    }

    [Fact]
    public void WipAnalysisDto_ShouldDeserializeFromJson()
    {
        var json = """
        {
            "analysisDate": "2024-01-15T10:30:00Z",
            "totalWip": 30,
            "byState": [
                { "state": "Active", "count": 15, "percentage": 50.0, "averageAgeDays": 4.5 }
            ],
            "byArea": [],
            "byPerson": [],
            "byType": { "Bug": 10, "Task": 20 },
            "agingItems": [],
            "insights": ["Test insight"]
        }
        """;

        var dto = JsonSerializer.Deserialize<WipAnalysisDto>(json, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal(30, dto.TotalWip);
        Assert.Single(dto.ByState);
        Assert.Equal("Active", dto.ByState[0].State);
        Assert.Equal(15, dto.ByState[0].Count);
    }

    [Fact]
    public void WipAnalysisDto_DefaultCollections_ShouldBeEmpty()
    {
        var dto = new WipAnalysisDto { TotalWip = 0 };

        Assert.Empty(dto.ByState);
        Assert.Empty(dto.ByArea);
        Assert.Empty(dto.ByPerson);
        Assert.Empty(dto.ByType);
        Assert.Empty(dto.AgingItems);
        Assert.Empty(dto.Insights);
    }
}

public class WipByStateDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void WipByStateDto_ShouldSerializeAllProperties()
    {
        var dto = new WipByStateDto
        {
            State = "In Progress",
            Count = 25,
            Percentage = 41.7,
            AverageAgeDays = 6.3
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"state\":\"In Progress\"", json);
        Assert.Contains("\"count\":25", json);
        Assert.Contains("\"percentage\":41.7", json);
        Assert.Contains("\"averageAgeDays\":6.3", json);
    }

    [Fact]
    public void WipByStateDto_RecordEquality_ShouldWork()
    {
        var dto1 = new WipByStateDto { State = "Active", Count = 10, Percentage = 50.0 };
        var dto2 = new WipByStateDto { State = "Active", Count = 10, Percentage = 50.0 };

        Assert.Equal(dto1, dto2);
    }
}

public class WipByAreaDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void WipByAreaDto_ShouldSerializeAllProperties()
    {
        var dto = new WipByAreaDto
        {
            AreaPath = "Project\\Team Alpha",
            Count = 18,
            UniqueAssignees = 3,
            ItemsPerPerson = 6.0,
            IsOverloaded = true
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"areaPath\":\"Project\\\\Team Alpha\"", json);
        Assert.Contains("\"count\":18", json);
        Assert.Contains("\"uniqueAssignees\":3", json);
        Assert.Contains("\"itemsPerPerson\":6", json);
        Assert.Contains("\"isOverloaded\":true", json);
    }
}

public class WipByPersonDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void WipByPersonDto_ShouldSerializeAllProperties()
    {
        var dto = new WipByPersonDto
        {
            AssignedTo = "John Doe",
            Count = 8,
            ByState = new Dictionary<string, int>
            {
                { "Active", 3 },
                { "In Progress", 5 }
            },
            IsOverloaded = true,
            OldestItemAgeDays = 15.5
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"assignedTo\":\"John Doe\"", json);
        Assert.Contains("\"count\":8", json);
        Assert.Contains("\"byState\":", json);
        Assert.Contains("\"isOverloaded\":true", json);
        Assert.Contains("\"oldestItemAgeDays\":15.5", json);
    }

    [Fact]
    public void WipByPersonDto_DefaultByState_ShouldBeEmpty()
    {
        var dto = new WipByPersonDto { AssignedTo = "Test User", Count = 1 };

        Assert.Empty(dto.ByState);
    }
}

public class AgingWorkItemDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void AgingWorkItemDto_ShouldSerializeToJson()
    {
        var dto = new AgingWorkItemDto
        {
            Id = 123,
            Title = "Old bug",
            WorkItemType = "Bug",
            State = "Active",
            AssignedTo = "Jane Doe",
            AreaPath = "Project\\Team",
            StateChangedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DaysInState = 20.5,
            DaysSinceCreation = 45.0,
            Priority = "2",
            AgingReason = "No updates for 20 days"
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"id\":123", json);
        Assert.Contains("\"title\":\"Old bug\"", json);
        Assert.Contains("\"daysInState\":20.5", json);
        Assert.Contains("\"agingReason\":\"No updates for 20 days\"", json);
    }

    [Fact]
    public void AgingWorkItemDto_NullableProperties_ShouldAllowNull()
    {
        var dto = new AgingWorkItemDto
        {
            Id = 456,
            Title = null,
            AssignedTo = null,
            StateChangedDate = null,
            DaysInState = 10.0
        };

        Assert.Null(dto.Title);
        Assert.Null(dto.AssignedTo);
        Assert.Null(dto.StateChangedDate);
        Assert.Equal(10.0, dto.DaysInState);
    }
}

public class BottleneckAnalysisDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void BottleneckAnalysisDto_ShouldSerializeToJson()
    {
        var dto = new BottleneckAnalysisDto
        {
            AnalysisDate = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            Bottlenecks =
            [
                new BottleneckDto
                {
                    Type = "State",
                    Location = "Code Review",
                    ItemCount = 15,
                    Severity = 8,
                    Description = "15 items stuck in Code Review"
                }
            ],
            Recommendations = ["Add more reviewers", "Consider pair programming"]
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"bottlenecks\":", json);
        Assert.Contains("\"recommendations\":", json);
        Assert.Contains("\"severity\":8", json);
    }

    [Fact]
    public void BottleneckAnalysisDto_DefaultCollections_ShouldBeEmpty()
    {
        var dto = new BottleneckAnalysisDto();

        Assert.Empty(dto.Bottlenecks);
        Assert.Empty(dto.Recommendations);
    }
}

public class BottleneckDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void BottleneckDto_ShouldSerializeAllProperties()
    {
        var dto = new BottleneckDto
        {
            Type = "Person",
            Location = "John Doe",
            ItemCount = 12,
            Severity = 7,
            Description = "John has too many items assigned",
            SampleItemIds = [101, 102, 103]
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"type\":\"Person\"", json);
        Assert.Contains("\"location\":\"John Doe\"", json);
        Assert.Contains("\"itemCount\":12", json);
        Assert.Contains("\"severity\":7", json);
        Assert.Contains("\"sampleItemIds\":[101,102,103]", json);
    }

    [Fact]
    public void BottleneckDto_RecordEquality_ShouldWorkForPrimitives()
    {
        var dto1 = new BottleneckDto { Type = "State", Location = "Active", ItemCount = 10, Severity = 5 };
        var dto2 = new BottleneckDto { Type = "State", Location = "Active", ItemCount = 10, Severity = 5 };

        Assert.Equal(dto1.Type, dto2.Type);
        Assert.Equal(dto1.Location, dto2.Location);
        Assert.Equal(dto1.ItemCount, dto2.ItemCount);
        Assert.Equal(dto1.Severity, dto2.Severity);
    }
}

public class AgingReportDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void AgingReportDto_ShouldSerializeToJson()
    {
        var dto = new AgingReportDto
        {
            AnalysisDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            TotalAgingItems = 15,
            Summary = new AgingSummaryDto
            {
                AverageAgeDays = 12.5,
                MedianAgeDays = 10.0,
                MaxAgeDays = 45.0,
                PercentageOfWip = 25.0,
                TopAgingState = "In Review",
                TopAgingAssignee = "John Doe"
            },
            Recommendations = ["Review aging items", "Add more reviewers"]
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"totalAgingItems\":15", json);
        Assert.Contains("\"summary\":", json);
        Assert.Contains("\"recommendations\":", json);
    }

    [Fact]
    public void AgingReportDto_DefaultCollections_ShouldBeEmpty()
    {
        var dto = new AgingReportDto();

        Assert.Empty(dto.ByState);
        Assert.Empty(dto.ByAssignee);
        Assert.Empty(dto.ByArea);
        Assert.Empty(dto.Items);
        Assert.Empty(dto.Recommendations);
    }
}

public class AgingSummaryDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void AgingSummaryDto_ShouldSerializeAllProperties()
    {
        var dto = new AgingSummaryDto
        {
            AverageAgeDays = 15.5,
            MedianAgeDays = 12.0,
            MaxAgeDays = 60.0,
            PercentageOfWip = 30.5,
            TopAgingState = "Blocked",
            TopAgingAssignee = "Jane Doe"
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"averageAgeDays\":15.5", json);
        Assert.Contains("\"medianAgeDays\":12", json);
        Assert.Contains("\"maxAgeDays\":60", json);
        Assert.Contains("\"percentageOfWip\":30.5", json);
        Assert.Contains("\"topAgingState\":\"Blocked\"", json);
        Assert.Contains("\"topAgingAssignee\":\"Jane Doe\"", json);
    }
}

public class AgingByUrgencyDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void AgingByUrgencyDto_ShouldSerializeAllLevels()
    {
        var dto = new AgingByUrgencyDto
        {
            Critical = [101, 102],
            High = [103, 104, 105],
            Medium = [106],
            Low = [107, 108, 109, 110]
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"critical\":[101,102]", json);
        Assert.Contains("\"high\":[103,104,105]", json);
        Assert.Contains("\"medium\":[106]", json);
        Assert.Contains("\"low\":[107,108,109,110]", json);
    }

    [Fact]
    public void AgingByUrgencyDto_DefaultLists_ShouldBeEmpty()
    {
        var dto = new AgingByUrgencyDto();

        Assert.Empty(dto.Critical);
        Assert.Empty(dto.High);
        Assert.Empty(dto.Medium);
        Assert.Empty(dto.Low);
    }
}

public class AgingByGroupDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void AgingByGroupDto_ShouldSerializeAllProperties()
    {
        var dto = new AgingByGroupDto
        {
            Name = "In Review",
            Count = 8,
            AverageAgeDays = 14.2,
            MaxAgeDays = 35.0,
            ItemIds = [101, 102, 103]
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"name\":\"In Review\"", json);
        Assert.Contains("\"count\":8", json);
        Assert.Contains("\"averageAgeDays\":14.2", json);
        Assert.Contains("\"maxAgeDays\":35", json);
        Assert.Contains("\"itemIds\":[101,102,103]", json);
    }
}

public class AgingItemDetailDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void AgingItemDetailDto_ShouldSerializeAllProperties()
    {
        var dto = new AgingItemDetailDto
        {
            Id = 123,
            Title = "Old Bug",
            WorkItemType = "Bug",
            State = "Active",
            AssignedTo = "John Doe",
            AreaPath = "Project\\Team",
            Priority = "2",
            DaysSinceUpdate = 25.5,
            DaysSinceCreation = 45.0,
            Urgency = "High",
            UrgencyScore = 7,
            Recommendation = "Review with assignee this week"
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"id\":123", json);
        Assert.Contains("\"urgency\":\"High\"", json);
        Assert.Contains("\"urgencyScore\":7", json);
        Assert.Contains("\"recommendation\":\"Review with assignee this week\"", json);
    }

    [Fact]
    public void AgingItemDetailDto_NullableProperties_ShouldAllowNull()
    {
        var dto = new AgingItemDetailDto
        {
            Id = 456,
            Title = null,
            AssignedTo = null,
            Priority = null,
            DaysSinceUpdate = 10.0,
            Urgency = "Medium",
            UrgencyScore = 5
        };

        Assert.Null(dto.Title);
        Assert.Null(dto.AssignedTo);
        Assert.Null(dto.Priority);
        Assert.Equal("Medium", dto.Urgency);
    }
}
