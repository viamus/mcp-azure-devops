namespace Viamus.Azure.Devops.Mcp.Server.Models;

/// <summary>
/// Work in Progress (WIP) analysis results.
/// </summary>
public sealed record WipAnalysisDto
{
    /// <summary>
    /// Analysis timestamp.
    /// </summary>
    public DateTime AnalysisDate { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Total number of items in progress (not completed).
    /// </summary>
    public int TotalWip { get; init; }

    /// <summary>
    /// WIP breakdown by state.
    /// </summary>
    public List<WipByStateDto> ByState { get; init; } = [];

    /// <summary>
    /// WIP breakdown by area path (team/squad).
    /// </summary>
    public List<WipByAreaDto> ByArea { get; init; } = [];

    /// <summary>
    /// WIP breakdown by assigned person.
    /// </summary>
    public List<WipByPersonDto> ByPerson { get; init; } = [];

    /// <summary>
    /// WIP breakdown by work item type.
    /// </summary>
    public Dictionary<string, int> ByType { get; init; } = new();

    /// <summary>
    /// Items that have been in progress for too long (aging items).
    /// </summary>
    public List<AgingWorkItemDto> AgingItems { get; init; } = [];

    /// <summary>
    /// Summary insights about the WIP status.
    /// </summary>
    public List<string> Insights { get; init; } = [];
}

/// <summary>
/// WIP count by state.
/// </summary>
public sealed record WipByStateDto
{
    /// <summary>
    /// State name.
    /// </summary>
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// Number of items in this state.
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// Percentage of total WIP.
    /// </summary>
    public double Percentage { get; init; }

    /// <summary>
    /// Average age in days for items in this state.
    /// </summary>
    public double AverageAgeDays { get; init; }
}

/// <summary>
/// WIP count by area path (team/squad).
/// </summary>
public sealed record WipByAreaDto
{
    /// <summary>
    /// Area path.
    /// </summary>
    public string AreaPath { get; init; } = string.Empty;

    /// <summary>
    /// Number of items in this area.
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// Number of unique assignees in this area.
    /// </summary>
    public int UniqueAssignees { get; init; }

    /// <summary>
    /// Average items per person in this area.
    /// </summary>
    public double ItemsPerPerson { get; init; }

    /// <summary>
    /// Whether this area appears overloaded (high items per person).
    /// </summary>
    public bool IsOverloaded { get; init; }
}

/// <summary>
/// WIP count by person.
/// </summary>
public sealed record WipByPersonDto
{
    /// <summary>
    /// Person's display name.
    /// </summary>
    public string AssignedTo { get; init; } = string.Empty;

    /// <summary>
    /// Number of items assigned to this person.
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// Breakdown by state for this person.
    /// </summary>
    public Dictionary<string, int> ByState { get; init; } = new();

    /// <summary>
    /// Whether this person appears overloaded.
    /// </summary>
    public bool IsOverloaded { get; init; }

    /// <summary>
    /// Oldest item age in days assigned to this person.
    /// </summary>
    public double OldestItemAgeDays { get; init; }
}

/// <summary>
/// Work item that has been in progress for too long.
/// </summary>
public sealed record AgingWorkItemDto
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
    /// Work item type.
    /// </summary>
    public string? WorkItemType { get; init; }

    /// <summary>
    /// Current state.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Person assigned to this item.
    /// </summary>
    public string? AssignedTo { get; init; }

    /// <summary>
    /// Area path.
    /// </summary>
    public string? AreaPath { get; init; }

    /// <summary>
    /// Date when the item entered current state.
    /// </summary>
    public DateTime? StateChangedDate { get; init; }

    /// <summary>
    /// Days in current state.
    /// </summary>
    public double DaysInState { get; init; }

    /// <summary>
    /// Days since creation.
    /// </summary>
    public double DaysSinceCreation { get; init; }

    /// <summary>
    /// Priority of the item.
    /// </summary>
    public string? Priority { get; init; }

    /// <summary>
    /// Reason for aging classification.
    /// </summary>
    public string AgingReason { get; init; } = string.Empty;
}

/// <summary>
/// Bottleneck analysis results.
/// </summary>
public sealed record BottleneckAnalysisDto
{
    /// <summary>
    /// Analysis timestamp.
    /// </summary>
    public DateTime AnalysisDate { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Identified bottlenecks ordered by severity.
    /// </summary>
    public List<BottleneckDto> Bottlenecks { get; init; } = [];

    /// <summary>
    /// Recommendations for addressing bottlenecks.
    /// </summary>
    public List<string> Recommendations { get; init; } = [];
}

/// <summary>
/// A single bottleneck in the flow.
/// </summary>
public sealed record BottleneckDto
{
    /// <summary>
    /// Type of bottleneck (State, Person, Area, Type).
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Name/identifier of the bottleneck location.
    /// </summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>
    /// Number of items blocked/waiting.
    /// </summary>
    public int ItemCount { get; init; }

    /// <summary>
    /// Severity score (1-10).
    /// </summary>
    public int Severity { get; init; }

    /// <summary>
    /// Description of the bottleneck.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Sample work item IDs affected.
    /// </summary>
    public List<int> SampleItemIds { get; init; } = [];
}

/// <summary>
/// Aging report analysis results.
/// </summary>
public sealed record AgingReportDto
{
    /// <summary>
    /// Analysis timestamp.
    /// </summary>
    public DateTime AnalysisDate { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Total number of aging items.
    /// </summary>
    public int TotalAgingItems { get; init; }

    /// <summary>
    /// Summary statistics about aging.
    /// </summary>
    public AgingSummaryDto Summary { get; init; } = new();

    /// <summary>
    /// Items grouped by urgency level.
    /// </summary>
    public AgingByUrgencyDto ByUrgency { get; init; } = new();

    /// <summary>
    /// Aging items grouped by state.
    /// </summary>
    public List<AgingByGroupDto> ByState { get; init; } = [];

    /// <summary>
    /// Aging items grouped by assignee.
    /// </summary>
    public List<AgingByGroupDto> ByAssignee { get; init; } = [];

    /// <summary>
    /// Aging items grouped by area.
    /// </summary>
    public List<AgingByGroupDto> ByArea { get; init; } = [];

    /// <summary>
    /// Detailed list of aging items with recommendations.
    /// </summary>
    public List<AgingItemDetailDto> Items { get; init; } = [];

    /// <summary>
    /// Overall recommendations for addressing aging work.
    /// </summary>
    public List<string> Recommendations { get; init; } = [];
}

/// <summary>
/// Summary statistics for aging analysis.
/// </summary>
public sealed record AgingSummaryDto
{
    /// <summary>
    /// Average age of all aging items in days.
    /// </summary>
    public double AverageAgeDays { get; init; }

    /// <summary>
    /// Median age in days.
    /// </summary>
    public double MedianAgeDays { get; init; }

    /// <summary>
    /// Maximum age in days.
    /// </summary>
    public double MaxAgeDays { get; init; }

    /// <summary>
    /// Percentage of total WIP that is aging.
    /// </summary>
    public double PercentageOfWip { get; init; }

    /// <summary>
    /// States with most aging items.
    /// </summary>
    public string TopAgingState { get; init; } = string.Empty;

    /// <summary>
    /// Person with most aging items.
    /// </summary>
    public string TopAgingAssignee { get; init; } = string.Empty;
}

/// <summary>
/// Aging items grouped by urgency level.
/// </summary>
public sealed record AgingByUrgencyDto
{
    /// <summary>
    /// Critical items (highest priority, very old).
    /// </summary>
    public List<int> Critical { get; init; } = [];

    /// <summary>
    /// High urgency items.
    /// </summary>
    public List<int> High { get; init; } = [];

    /// <summary>
    /// Medium urgency items.
    /// </summary>
    public List<int> Medium { get; init; } = [];

    /// <summary>
    /// Low urgency items.
    /// </summary>
    public List<int> Low { get; init; } = [];
}

/// <summary>
/// Aging statistics for a group (state, person, area).
/// </summary>
public sealed record AgingByGroupDto
{
    /// <summary>
    /// Group name (state name, person name, or area path).
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Number of aging items in this group.
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// Average age in days for this group.
    /// </summary>
    public double AverageAgeDays { get; init; }

    /// <summary>
    /// Oldest item age in this group.
    /// </summary>
    public double MaxAgeDays { get; init; }

    /// <summary>
    /// List of work item IDs in this group.
    /// </summary>
    public List<int> ItemIds { get; init; } = [];
}

/// <summary>
/// Detailed aging item with recommendation.
/// </summary>
public sealed record AgingItemDetailDto
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
    /// Work item type.
    /// </summary>
    public string? WorkItemType { get; init; }

    /// <summary>
    /// Current state.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Assigned person.
    /// </summary>
    public string? AssignedTo { get; init; }

    /// <summary>
    /// Area path.
    /// </summary>
    public string? AreaPath { get; init; }

    /// <summary>
    /// Priority (1=highest).
    /// </summary>
    public string? Priority { get; init; }

    /// <summary>
    /// Days since last update.
    /// </summary>
    public double DaysSinceUpdate { get; init; }

    /// <summary>
    /// Days since creation.
    /// </summary>
    public double DaysSinceCreation { get; init; }

    /// <summary>
    /// Urgency classification (Critical, High, Medium, Low).
    /// </summary>
    public string Urgency { get; init; } = string.Empty;

    /// <summary>
    /// Urgency score (1-10, higher is more urgent).
    /// </summary>
    public int UrgencyScore { get; init; }

    /// <summary>
    /// Specific recommendation for this item.
    /// </summary>
    public string Recommendation { get; init; } = string.Empty;
}
