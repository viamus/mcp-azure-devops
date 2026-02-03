namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Flow metrics analysis results.
/// </summary>
public sealed record FlowMetricsDto
{
    /// <summary>
    /// Analysis period start date.
    /// </summary>
    public DateTime PeriodStart { get; init; }

    /// <summary>
    /// Analysis period end date.
    /// </summary>
    public DateTime PeriodEnd { get; init; }

    /// <summary>
    /// Total number of completed work items in the period.
    /// </summary>
    public int Throughput { get; init; }

    /// <summary>
    /// Lead time statistics in days (from creation to completion).
    /// </summary>
    public MetricStatistics LeadTime { get; init; } = new();

    /// <summary>
    /// Cycle time statistics in days (from started to completion).
    /// </summary>
    public MetricStatistics CycleTime { get; init; } = new();

    /// <summary>
    /// Throughput breakdown by period (day/week).
    /// </summary>
    public List<ThroughputPeriod> ThroughputByPeriod { get; init; } = [];

    /// <summary>
    /// Throughput breakdown by work item type.
    /// </summary>
    public Dictionary<string, int> ThroughputByType { get; init; } = new();

    /// <summary>
    /// Individual work item flow data for detailed analysis.
    /// </summary>
    public List<WorkItemFlowDataDto> Items { get; init; } = [];
}

/// <summary>
/// Statistical measures for a metric.
/// </summary>
public sealed record MetricStatistics
{
    /// <summary>
    /// Average value.
    /// </summary>
    public double Average { get; init; }

    /// <summary>
    /// Median value (50th percentile).
    /// </summary>
    public double Median { get; init; }

    /// <summary>
    /// 85th percentile value.
    /// </summary>
    public double Percentile85 { get; init; }

    /// <summary>
    /// 95th percentile value.
    /// </summary>
    public double Percentile95 { get; init; }

    /// <summary>
    /// Minimum value.
    /// </summary>
    public double Min { get; init; }

    /// <summary>
    /// Maximum value.
    /// </summary>
    public double Max { get; init; }

    /// <summary>
    /// Standard deviation.
    /// </summary>
    public double StdDev { get; init; }

    /// <summary>
    /// Number of items in the sample.
    /// </summary>
    public int Count { get; init; }
}

/// <summary>
/// Throughput data for a specific period.
/// </summary>
public sealed record ThroughputPeriod
{
    /// <summary>
    /// Period start date.
    /// </summary>
    public DateTime PeriodStart { get; init; }

    /// <summary>
    /// Period end date.
    /// </summary>
    public DateTime PeriodEnd { get; init; }

    /// <summary>
    /// Number of items completed in this period.
    /// </summary>
    public int Count { get; init; }
}

/// <summary>
/// Flow data for a single work item.
/// </summary>
public sealed record WorkItemFlowDataDto
{
    /// <summary>
    /// Work item ID.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Work item title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Work item type (Bug, Task, User Story, etc.).
    /// </summary>
    public string? WorkItemType { get; init; }

    /// <summary>
    /// Date the work item was created.
    /// </summary>
    public DateTime? CreatedDate { get; init; }

    /// <summary>
    /// Date the work item was started (entered In Progress or equivalent).
    /// </summary>
    public DateTime? StartedDate { get; init; }

    /// <summary>
    /// Date the work item was completed.
    /// </summary>
    public DateTime? CompletedDate { get; init; }

    /// <summary>
    /// Lead time in days (created to completed).
    /// </summary>
    public double? LeadTimeDays { get; init; }

    /// <summary>
    /// Cycle time in days (started to completed).
    /// </summary>
    public double? CycleTimeDays { get; init; }

    /// <summary>
    /// Area path of the work item.
    /// </summary>
    public string? AreaPath { get; init; }

    /// <summary>
    /// Iteration path of the work item.
    /// </summary>
    public string? IterationPath { get; init; }
}
