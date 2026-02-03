using System.Text.Json;
using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Models;

public class FlowMetricsDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void FlowMetricsDto_ShouldSerializeToJson()
    {
        var dto = new FlowMetricsDto
        {
            PeriodStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEnd = new DateTime(2024, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            Throughput = 25,
            LeadTime = new MetricStatistics
            {
                Average = 5.5,
                Median = 4.0,
                Percentile85 = 8.0,
                Percentile95 = 12.0,
                Min = 1.0,
                Max = 15.0,
                StdDev = 2.5,
                Count = 25
            },
            CycleTime = new MetricStatistics
            {
                Average = 3.2,
                Median = 2.5,
                Percentile85 = 5.0,
                Percentile95 = 7.0,
                Min = 0.5,
                Max = 10.0,
                StdDev = 1.8,
                Count = 25
            }
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"throughput\":25", json);
        Assert.Contains("\"leadTime\":", json);
        Assert.Contains("\"cycleTime\":", json);
    }

    [Fact]
    public void FlowMetricsDto_ShouldDeserializeFromJson()
    {
        var json = """
        {
            "periodStart": "2024-01-01T00:00:00Z",
            "periodEnd": "2024-01-31T00:00:00Z",
            "throughput": 30,
            "leadTime": {
                "average": 5.5,
                "median": 4.0,
                "percentile85": 8.0,
                "percentile95": 12.0,
                "min": 1.0,
                "max": 15.0,
                "stdDev": 2.5,
                "count": 30
            },
            "cycleTime": {
                "average": 3.0,
                "median": 2.0,
                "percentile85": 5.0,
                "percentile95": 7.0,
                "min": 0.5,
                "max": 8.0,
                "stdDev": 1.5,
                "count": 30
            }
        }
        """;

        var dto = JsonSerializer.Deserialize<FlowMetricsDto>(json, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal(30, dto.Throughput);
        Assert.Equal(5.5, dto.LeadTime.Average);
        Assert.Equal(3.0, dto.CycleTime.Average);
    }

    [Fact]
    public void FlowMetricsDto_WithThroughputByType_ShouldSerializeCorrectly()
    {
        var dto = new FlowMetricsDto
        {
            PeriodStart = DateTime.UtcNow.AddDays(-30),
            PeriodEnd = DateTime.UtcNow,
            Throughput = 15,
            ThroughputByType = new Dictionary<string, int>
            {
                { "Bug", 5 },
                { "User Story", 7 },
                { "Task", 3 }
            }
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"throughputByType\":", json);
        Assert.Contains("\"Bug\":5", json);
        Assert.Contains("\"User Story\":7", json);
        Assert.Contains("\"Task\":3", json);
    }

    [Fact]
    public void FlowMetricsDto_WithThroughputByPeriod_ShouldSerializeCorrectly()
    {
        var dto = new FlowMetricsDto
        {
            PeriodStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEnd = new DateTime(2024, 1, 14, 0, 0, 0, DateTimeKind.Utc),
            Throughput = 10,
            ThroughputByPeriod =
            [
                new ThroughputPeriod
                {
                    PeriodStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    PeriodEnd = new DateTime(2024, 1, 7, 0, 0, 0, DateTimeKind.Utc),
                    Count = 4
                },
                new ThroughputPeriod
                {
                    PeriodStart = new DateTime(2024, 1, 8, 0, 0, 0, DateTimeKind.Utc),
                    PeriodEnd = new DateTime(2024, 1, 14, 0, 0, 0, DateTimeKind.Utc),
                    Count = 6
                }
            ]
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"throughputByPeriod\":", json);
        Assert.Contains("\"count\":4", json);
        Assert.Contains("\"count\":6", json);
    }

    [Fact]
    public void FlowMetricsDto_PrimitiveEquality_ShouldWork()
    {
        var dto1 = new FlowMetricsDto
        {
            PeriodStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEnd = new DateTime(2024, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            Throughput = 10
        };

        var dto2 = new FlowMetricsDto
        {
            PeriodStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEnd = new DateTime(2024, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            Throughput = 10
        };

        // Records with collections compare by reference, so we compare primitive properties
        Assert.Equal(dto1.PeriodStart, dto2.PeriodStart);
        Assert.Equal(dto1.PeriodEnd, dto2.PeriodEnd);
        Assert.Equal(dto1.Throughput, dto2.Throughput);
    }
}

public class MetricStatisticsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void MetricStatistics_ShouldSerializeAllProperties()
    {
        var stats = new MetricStatistics
        {
            Average = 5.5,
            Median = 4.0,
            Percentile85 = 8.0,
            Percentile95 = 12.0,
            Min = 1.0,
            Max = 15.0,
            StdDev = 2.5,
            Count = 25
        };

        var json = JsonSerializer.Serialize(stats, JsonOptions);

        Assert.Contains("\"average\":5.5", json);
        Assert.Contains("\"median\":4", json);
        Assert.Contains("\"percentile85\":8", json);
        Assert.Contains("\"percentile95\":12", json);
        Assert.Contains("\"min\":1", json);
        Assert.Contains("\"max\":15", json);
        Assert.Contains("\"stdDev\":2.5", json);
        Assert.Contains("\"count\":25", json);
    }

    [Fact]
    public void MetricStatistics_Default_ShouldHaveZeroValues()
    {
        var stats = new MetricStatistics();

        Assert.Equal(0, stats.Average);
        Assert.Equal(0, stats.Median);
        Assert.Equal(0, stats.Percentile85);
        Assert.Equal(0, stats.Percentile95);
        Assert.Equal(0, stats.Min);
        Assert.Equal(0, stats.Max);
        Assert.Equal(0, stats.StdDev);
        Assert.Equal(0, stats.Count);
    }
}

public class WorkItemFlowDataDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void WorkItemFlowDataDto_ShouldSerializeToJson()
    {
        var dto = new WorkItemFlowDataDto
        {
            Id = 123,
            Title = "Test Bug",
            WorkItemType = "Bug",
            CreatedDate = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            StartedDate = new DateTime(2024, 1, 5, 9, 0, 0, DateTimeKind.Utc),
            CompletedDate = new DateTime(2024, 1, 10, 16, 0, 0, DateTimeKind.Utc),
            LeadTimeDays = 9.25,
            CycleTimeDays = 5.29,
            AreaPath = "Project\\Team",
            IterationPath = "Project\\Sprint 1"
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"id\":123", json);
        Assert.Contains("\"title\":\"Test Bug\"", json);
        Assert.Contains("\"workItemType\":\"Bug\"", json);
        Assert.Contains("\"leadTimeDays\":9.25", json);
        Assert.Contains("\"cycleTimeDays\":5.29", json);
    }

    [Fact]
    public void WorkItemFlowDataDto_ShouldDeserializeFromJson()
    {
        var json = """
        {
            "id": 456,
            "title": "User Story",
            "workItemType": "User Story",
            "createdDate": "2024-01-01T00:00:00Z",
            "startedDate": "2024-01-03T00:00:00Z",
            "completedDate": "2024-01-08T00:00:00Z",
            "leadTimeDays": 7.0,
            "cycleTimeDays": 5.0,
            "areaPath": "Project\\Team A",
            "iterationPath": "Project\\Sprint 2"
        }
        """;

        var dto = JsonSerializer.Deserialize<WorkItemFlowDataDto>(json, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal(456, dto.Id);
        Assert.Equal("User Story", dto.Title);
        Assert.Equal(7.0, dto.LeadTimeDays);
        Assert.Equal(5.0, dto.CycleTimeDays);
    }

    [Fact]
    public void WorkItemFlowDataDto_NullableDates_ShouldAllowNull()
    {
        var dto = new WorkItemFlowDataDto
        {
            Id = 789,
            Title = "Incomplete Item",
            WorkItemType = "Task",
            CreatedDate = DateTime.UtcNow,
            StartedDate = null,
            CompletedDate = null,
            LeadTimeDays = null,
            CycleTimeDays = null
        };

        Assert.NotNull(dto.CreatedDate);
        Assert.Null(dto.StartedDate);
        Assert.Null(dto.CompletedDate);
        Assert.Null(dto.LeadTimeDays);
        Assert.Null(dto.CycleTimeDays);
    }
}

public class ThroughputPeriodTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public void ThroughputPeriod_ShouldSerializeToJson()
    {
        var dto = new ThroughputPeriod
        {
            PeriodStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEnd = new DateTime(2024, 1, 7, 0, 0, 0, DateTimeKind.Utc),
            Count = 8
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        Assert.Contains("\"count\":8", json);
        Assert.Contains("\"periodStart\":", json);
        Assert.Contains("\"periodEnd\":", json);
    }

    [Fact]
    public void ThroughputPeriod_RecordEquality_ShouldWork()
    {
        var period1 = new ThroughputPeriod
        {
            PeriodStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEnd = new DateTime(2024, 1, 7, 0, 0, 0, DateTimeKind.Utc),
            Count = 5
        };

        var period2 = new ThroughputPeriod
        {
            PeriodStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEnd = new DateTime(2024, 1, 7, 0, 0, 0, DateTimeKind.Utc),
            Count = 5
        };

        Assert.Equal(period1, period2);
    }
}
