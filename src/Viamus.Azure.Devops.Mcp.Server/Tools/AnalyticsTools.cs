using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using ModelContextProtocol.Server;
using Viamus.Azure.Devops.Mcp.Server.Configuration;
using Viamus.Azure.Devops.Mcp.Server.Models;
using Viamus.Azure.Devops.Mcp.Server.Services;

namespace Viamus.Azure.Devops.Mcp.Server.Tools;

/// <summary>
/// MCP tools for Azure DevOps Analytics operations.
/// Provides flow metrics, throughput analysis, and delivery insights.
/// </summary>
[McpServerToolType]
public sealed class AnalyticsTools
{
    private readonly IAzureDevOpsService _azureDevOpsService;
    private readonly AzureDevOpsOptions _options;
    private readonly ILogger<AnalyticsTools> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // States that indicate work has started
    private static readonly HashSet<string> InProgressStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "Active", "In Progress", "Committed", "Doing", "In Development", "Development",
        "In Review", "In Test", "Testing", "Code Review", "Resolved"
    };

    // States that indicate work is completed
    private static readonly HashSet<string> CompletedStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "Done", "Closed", "Completed", "Resolved", "Removed"
    };

    public AnalyticsTools(
        IAzureDevOpsService azureDevOpsService,
        IOptions<AzureDevOpsOptions> options,
        ILogger<AnalyticsTools> logger)
    {
        _azureDevOpsService = azureDevOpsService;
        _options = options.Value;
        _logger = logger;
    }

    [McpServerTool(Name = "get_flow_metrics")]
    [Description(@"Calculates flow metrics (Lead Time, Cycle Time, Throughput) for completed work items in a given period.

Lead Time: Time from work item creation to completion (measures total delivery time)
Cycle Time: Time from work started to completion (measures active work time)
Throughput: Number of items completed per period

Returns statistical analysis including average, median, percentiles (85th, 95th), and breakdown by type and period.
Use this to answer questions like 'How fast are we delivering?' or 'Is our delivery speed improving?'")]
    public async Task<string> GetFlowMetrics(
        [Description("The project name (required)")] string project,
        [Description("Number of days to analyze (default: 30, max: 90)")] int daysBack = 30,
        [Description("Work item types to include, comma-separated (e.g., 'Bug,User Story'). Leave empty for all types.")] string? workItemTypes = null,
        [Description("Area path filter (optional, e.g., 'Project\\Team')")] string? areaPath = null,
        [Description("Grouping period for throughput: 'day' or 'week' (default: week)")] string groupBy = "week",
        [Description("Include individual work item details in response (default: false)")] bool includeItems = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            daysBack = Math.Clamp(daysBack, 1, 90);
            var periodEnd = DateTime.UtcNow.Date;
            var periodStart = periodEnd.AddDays(-daysBack);

            _logger.LogInformation("Calculating flow metrics for project {Project} from {Start} to {End}",
                project, periodStart, periodEnd);

            // Build WIQL query for completed items
            var typeFilter = BuildTypeFilter(workItemTypes);
            var areaFilter = string.IsNullOrWhiteSpace(areaPath)
                ? string.Empty
                : $" AND [System.AreaPath] UNDER '{EscapeWiqlString(areaPath)}'";

            // Query items that were completed (state changed to Done/Closed) in the period
            // We look for items changed in the period and filter by completed state
            var wiqlQuery = $@"
                SELECT [System.Id]
                FROM WorkItems
                WHERE [System.TeamProject] = '{EscapeWiqlString(project)}'
                AND [System.State] IN ('Done', 'Closed', 'Completed', 'Resolved')
                AND [System.ChangedDate] >= '{periodStart:yyyy-MM-dd}'
                AND [System.ChangedDate] <= '{periodEnd:yyyy-MM-dd}'{typeFilter}{areaFilter}
                ORDER BY [System.ChangedDate] DESC";

            var completedItems = await _azureDevOpsService.QueryWorkItemsAsync(wiqlQuery, project, 500, cancellationToken);

            if (completedItems.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    message = "No completed work items found in the specified period",
                    periodStart,
                    periodEnd,
                    throughput = 0
                }, JsonOptions);
            }

            // Get flow data for each item (including state change history)
            var flowDataList = await GetFlowDataForItemsAsync(completedItems, project, periodStart, cancellationToken);

            // Filter to only items actually completed in the period
            var itemsInPeriod = flowDataList
                .Where(f => f.CompletedDate >= periodStart && f.CompletedDate <= periodEnd)
                .ToList();

            if (itemsInPeriod.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    message = "No work items were completed in the specified period (items may have been completed earlier)",
                    periodStart,
                    periodEnd,
                    throughput = 0
                }, JsonOptions);
            }

            // Calculate metrics
            var leadTimes = itemsInPeriod
                .Where(f => f.LeadTimeDays.HasValue)
                .Select(f => f.LeadTimeDays!.Value)
                .ToList();

            var cycleTimes = itemsInPeriod
                .Where(f => f.CycleTimeDays.HasValue)
                .Select(f => f.CycleTimeDays!.Value)
                .ToList();

            var throughputByType = itemsInPeriod
                .GroupBy(f => f.WorkItemType ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());

            var throughputByPeriod = CalculateThroughputByPeriod(itemsInPeriod, periodStart, periodEnd, groupBy);

            var result = new FlowMetricsDto
            {
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                Throughput = itemsInPeriod.Count,
                LeadTime = CalculateStatistics(leadTimes),
                CycleTime = CalculateStatistics(cycleTimes),
                ThroughputByType = throughputByType,
                ThroughputByPeriod = throughputByPeriod,
                Items = includeItems ? itemsInPeriod : []
            };

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating flow metrics");
            return JsonSerializer.Serialize(new { error = $"Error calculating flow metrics: {ex.Message}" }, JsonOptions);
        }
    }

    [McpServerTool(Name = "compare_flow_metrics")]
    [Description(@"Compares flow metrics between two periods to identify trends.
Shows whether delivery is improving or degrading by comparing Lead Time, Cycle Time, and Throughput.
Use this to answer 'Are we getting faster or slower?' or 'How does this sprint compare to last sprint?'")]
    public async Task<string> CompareFlowMetrics(
        [Description("The project name (required)")] string project,
        [Description("Days in each period to compare (default: 14)")] int periodDays = 14,
        [Description("Work item types to include, comma-separated. Leave empty for all types.")] string? workItemTypes = null,
        [Description("Area path filter (optional)")] string? areaPath = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            periodDays = Math.Clamp(periodDays, 7, 45);

            var currentEnd = DateTime.UtcNow.Date;
            var currentStart = currentEnd.AddDays(-periodDays);
            var previousEnd = currentStart.AddDays(-1);
            var previousStart = previousEnd.AddDays(-periodDays);

            // Get metrics for both periods
            var currentMetricsJson = await GetFlowMetrics(project, periodDays, workItemTypes, areaPath, "week", false, cancellationToken);
            var currentMetrics = JsonSerializer.Deserialize<FlowMetricsDto>(currentMetricsJson, JsonOptions);

            // Temporarily adjust to get previous period
            var previousMetricsJson = await GetFlowMetricsForPeriod(project, previousStart, previousEnd, workItemTypes, areaPath, cancellationToken);
            var previousMetrics = JsonSerializer.Deserialize<FlowMetricsDto>(previousMetricsJson, JsonOptions);

            var comparison = new
            {
                currentPeriod = new { start = currentStart, end = currentEnd },
                previousPeriod = new { start = previousStart, end = previousEnd },
                throughput = new
                {
                    current = currentMetrics?.Throughput ?? 0,
                    previous = previousMetrics?.Throughput ?? 0,
                    change = (currentMetrics?.Throughput ?? 0) - (previousMetrics?.Throughput ?? 0),
                    changePercent = CalculateChangePercent(previousMetrics?.Throughput ?? 0, currentMetrics?.Throughput ?? 0),
                    trend = GetTrend((previousMetrics?.Throughput ?? 0), (currentMetrics?.Throughput ?? 0), true)
                },
                leadTime = new
                {
                    currentMedian = currentMetrics?.LeadTime.Median ?? 0,
                    previousMedian = previousMetrics?.LeadTime.Median ?? 0,
                    changePercent = CalculateChangePercent(previousMetrics?.LeadTime.Median ?? 0, currentMetrics?.LeadTime.Median ?? 0),
                    trend = GetTrend(previousMetrics?.LeadTime.Median ?? 0, currentMetrics?.LeadTime.Median ?? 0, false)
                },
                cycleTime = new
                {
                    currentMedian = currentMetrics?.CycleTime.Median ?? 0,
                    previousMedian = previousMetrics?.CycleTime.Median ?? 0,
                    changePercent = CalculateChangePercent(previousMetrics?.CycleTime.Median ?? 0, currentMetrics?.CycleTime.Median ?? 0),
                    trend = GetTrend(previousMetrics?.CycleTime.Median ?? 0, currentMetrics?.CycleTime.Median ?? 0, false)
                },
                summary = GenerateComparisonSummary(currentMetrics, previousMetrics)
            };

            return JsonSerializer.Serialize(comparison, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing flow metrics");
            return JsonSerializer.Serialize(new { error = $"Error comparing flow metrics: {ex.Message}" }, JsonOptions);
        }
    }

    private async Task<string> GetFlowMetricsForPeriod(
        string project,
        DateTime periodStart,
        DateTime periodEnd,
        string? workItemTypes,
        string? areaPath,
        CancellationToken cancellationToken)
    {
        var typeFilter = BuildTypeFilter(workItemTypes);
        var areaFilter = string.IsNullOrWhiteSpace(areaPath)
            ? string.Empty
            : $" AND [System.AreaPath] UNDER '{EscapeWiqlString(areaPath)}'";

        var wiqlQuery = $@"
            SELECT [System.Id]
            FROM WorkItems
            WHERE [System.TeamProject] = '{EscapeWiqlString(project)}'
            AND [System.State] IN ('Done', 'Closed', 'Completed', 'Resolved')
            AND [System.ChangedDate] >= '{periodStart:yyyy-MM-dd}'
            AND [System.ChangedDate] <= '{periodEnd:yyyy-MM-dd}'{typeFilter}{areaFilter}
            ORDER BY [System.ChangedDate] DESC";

        var completedItems = await _azureDevOpsService.QueryWorkItemsAsync(wiqlQuery, project, 500, cancellationToken);

        if (completedItems.Count == 0)
        {
            return JsonSerializer.Serialize(new FlowMetricsDto
            {
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                Throughput = 0
            }, JsonOptions);
        }

        var flowDataList = await GetFlowDataForItemsAsync(completedItems, project, periodStart, cancellationToken);
        var itemsInPeriod = flowDataList
            .Where(f => f.CompletedDate >= periodStart && f.CompletedDate <= periodEnd)
            .ToList();

        var leadTimes = itemsInPeriod.Where(f => f.LeadTimeDays.HasValue).Select(f => f.LeadTimeDays!.Value).ToList();
        var cycleTimes = itemsInPeriod.Where(f => f.CycleTimeDays.HasValue).Select(f => f.CycleTimeDays!.Value).ToList();

        return JsonSerializer.Serialize(new FlowMetricsDto
        {
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Throughput = itemsInPeriod.Count,
            LeadTime = CalculateStatistics(leadTimes),
            CycleTime = CalculateStatistics(cycleTimes)
        }, JsonOptions);
    }

    private async Task<List<WorkItemFlowDataDto>> GetFlowDataForItemsAsync(
        IReadOnlyList<WorkItemDto> items,
        string project,
        DateTime periodStart,
        CancellationToken cancellationToken)
    {
        var flowDataList = new List<WorkItemFlowDataDto>();

        // Create connection for revision queries
        var credentials = new VssBasicCredential(string.Empty, _options.PersonalAccessToken);
        using var connection = new VssConnection(new Uri(_options.OrganizationUrl), credentials);
        using var witClient = connection.GetClient<WorkItemTrackingHttpClient>();

        foreach (var item in items)
        {
            try
            {
                // Get work item revisions to find state transitions
                var revisions = await witClient.GetRevisionsAsync(
                    project: project ?? _options.DefaultProject,
                    id: item.Id,
                    cancellationToken: cancellationToken);

                DateTime? startedDate = null;
                DateTime? completedDate = null;

                // Find when the item first entered an "in progress" state
                // and when it first entered a "completed" state
                foreach (var revision in revisions.OrderBy(r => r.Rev))
                {
                    if (revision.Fields.TryGetValue("System.State", out var stateObj) &&
                        revision.Fields.TryGetValue("System.ChangedDate", out var dateObj))
                    {
                        var state = stateObj?.ToString();
                        var changedDate = dateObj is DateTime dt ? dt : DateTime.TryParse(dateObj?.ToString(), out var parsed) ? parsed : (DateTime?)null;

                        if (state != null && changedDate.HasValue)
                        {
                            if (startedDate == null && InProgressStates.Contains(state))
                            {
                                startedDate = changedDate.Value;
                            }

                            if (CompletedStates.Contains(state))
                            {
                                completedDate = changedDate.Value;
                            }
                        }
                    }
                }

                // Calculate times
                double? leadTimeDays = null;
                double? cycleTimeDays = null;

                if (item.CreatedDate.HasValue && completedDate.HasValue)
                {
                    leadTimeDays = (completedDate.Value - item.CreatedDate.Value).TotalDays;
                }

                if (startedDate.HasValue && completedDate.HasValue)
                {
                    cycleTimeDays = (completedDate.Value - startedDate.Value).TotalDays;
                }

                flowDataList.Add(new WorkItemFlowDataDto
                {
                    Id = item.Id,
                    Title = item.Title,
                    WorkItemType = item.WorkItemType,
                    CreatedDate = item.CreatedDate,
                    StartedDate = startedDate,
                    CompletedDate = completedDate,
                    LeadTimeDays = leadTimeDays,
                    CycleTimeDays = cycleTimeDays,
                    AreaPath = item.AreaPath,
                    IterationPath = item.IterationPath
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get revisions for work item {WorkItemId}", item.Id);
                // Add item with basic data only
                flowDataList.Add(new WorkItemFlowDataDto
                {
                    Id = item.Id,
                    Title = item.Title,
                    WorkItemType = item.WorkItemType,
                    CreatedDate = item.CreatedDate,
                    AreaPath = item.AreaPath,
                    IterationPath = item.IterationPath
                });
            }
        }

        return flowDataList;
    }

    private static MetricStatistics CalculateStatistics(List<double> values)
    {
        if (values.Count == 0)
        {
            return new MetricStatistics { Count = 0 };
        }

        var sorted = values.OrderBy(v => v).ToList();
        var count = sorted.Count;
        var sum = sorted.Sum();
        var average = sum / count;

        var squaredDiffs = sorted.Select(v => Math.Pow(v - average, 2)).Sum();
        var stdDev = Math.Sqrt(squaredDiffs / count);

        return new MetricStatistics
        {
            Average = Math.Round(average, 2),
            Median = Math.Round(GetPercentile(sorted, 50), 2),
            Percentile85 = Math.Round(GetPercentile(sorted, 85), 2),
            Percentile95 = Math.Round(GetPercentile(sorted, 95), 2),
            Min = Math.Round(sorted.First(), 2),
            Max = Math.Round(sorted.Last(), 2),
            StdDev = Math.Round(stdDev, 2),
            Count = count
        };
    }

    private static double GetPercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0;
        if (sortedValues.Count == 1) return sortedValues[0];

        var index = (percentile / 100.0) * (sortedValues.Count - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);

        if (lower == upper) return sortedValues[lower];

        var fraction = index - lower;
        return sortedValues[lower] + fraction * (sortedValues[upper] - sortedValues[lower]);
    }

    private static List<ThroughputPeriod> CalculateThroughputByPeriod(
        List<WorkItemFlowDataDto> items,
        DateTime periodStart,
        DateTime periodEnd,
        string groupBy)
    {
        var periods = new List<ThroughputPeriod>();
        var isWeekly = groupBy.Equals("week", StringComparison.OrdinalIgnoreCase);
        var periodLength = isWeekly ? 7 : 1;

        var currentStart = periodStart;
        while (currentStart < periodEnd)
        {
            var currentEnd = currentStart.AddDays(periodLength);
            if (currentEnd > periodEnd) currentEnd = periodEnd;

            var count = items.Count(i =>
                i.CompletedDate >= currentStart && i.CompletedDate < currentEnd);

            periods.Add(new ThroughputPeriod
            {
                PeriodStart = currentStart,
                PeriodEnd = currentEnd,
                Count = count
            });

            currentStart = currentEnd;
        }

        return periods;
    }

    private static double CalculateChangePercent(double previous, double current)
    {
        if (previous == 0) return current > 0 ? 100 : 0;
        return Math.Round(((current - previous) / previous) * 100, 1);
    }

    private static string GetTrend(double previous, double current, bool higherIsBetter)
    {
        if (Math.Abs(current - previous) < 0.01) return "stable";

        var isHigher = current > previous;
        if (higherIsBetter)
        {
            return isHigher ? "improving" : "degrading";
        }
        else
        {
            return isHigher ? "degrading" : "improving";
        }
    }

    private static string GenerateComparisonSummary(FlowMetricsDto? current, FlowMetricsDto? previous)
    {
        if (current == null || previous == null) return "Insufficient data for comparison";

        var insights = new List<string>();

        // Throughput analysis
        if (current.Throughput > previous.Throughput)
        {
            insights.Add($"Throughput increased by {current.Throughput - previous.Throughput} items ({CalculateChangePercent(previous.Throughput, current.Throughput):+0.#}%)");
        }
        else if (current.Throughput < previous.Throughput)
        {
            insights.Add($"Throughput decreased by {previous.Throughput - current.Throughput} items ({CalculateChangePercent(previous.Throughput, current.Throughput):0.#}%)");
        }

        // Lead time analysis
        if (current.LeadTime.Median < previous.LeadTime.Median)
        {
            insights.Add($"Lead time improved (median: {previous.LeadTime.Median:0.#}d -> {current.LeadTime.Median:0.#}d)");
        }
        else if (current.LeadTime.Median > previous.LeadTime.Median)
        {
            insights.Add($"Lead time degraded (median: {previous.LeadTime.Median:0.#}d -> {current.LeadTime.Median:0.#}d)");
        }

        // Cycle time analysis
        if (current.CycleTime.Median < previous.CycleTime.Median)
        {
            insights.Add($"Cycle time improved (median: {previous.CycleTime.Median:0.#}d -> {current.CycleTime.Median:0.#}d)");
        }
        else if (current.CycleTime.Median > previous.CycleTime.Median)
        {
            insights.Add($"Cycle time degraded (median: {previous.CycleTime.Median:0.#}d -> {current.CycleTime.Median:0.#}d)");
        }

        return insights.Count > 0 ? string.Join(". ", insights) : "Metrics are stable between periods";
    }

    private static string BuildTypeFilter(string? workItemTypes)
    {
        if (string.IsNullOrWhiteSpace(workItemTypes))
        {
            return string.Empty;
        }

        var types = workItemTypes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => $"'{EscapeWiqlString(t)}'");

        return $" AND [System.WorkItemType] IN ({string.Join(", ", types)})";
    }

    private static string EscapeWiqlString(string value)
    {
        return value.Replace("'", "''");
    }

    #region WIP Analysis

    [McpServerTool(Name = "get_wip_analysis")]
    [Description(@"Analyzes Work in Progress (WIP) to identify bottlenecks and overload.

Returns:
- Total WIP count
- WIP breakdown by state (shows where work is accumulating)
- WIP by area/team (shows which teams are overloaded)
- WIP by person (shows who has too much on their plate)
- Aging items (work that's been stuck too long)

Use this to answer 'Where is work piling up?', 'Who is overloaded?', or 'What needs attention?'")]
    public async Task<string> GetWipAnalysis(
        [Description("The project name (required)")] string project,
        [Description("Work item types to include, comma-separated. Leave empty for all types.")] string? workItemTypes = null,
        [Description("Area path filter (optional)")] string? areaPath = null,
        [Description("Threshold in days to consider an item as aging (default: 14)")] int agingThresholdDays = 14,
        [Description("Maximum items per person before flagging as overloaded (default: 5)")] int overloadThreshold = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Analyzing WIP for project {Project}", project);

            // Query active/in-progress items (not completed)
            var typeFilter = BuildTypeFilter(workItemTypes);
            var areaFilter = string.IsNullOrWhiteSpace(areaPath)
                ? string.Empty
                : $" AND [System.AreaPath] UNDER '{EscapeWiqlString(areaPath)}'";

            var wiqlQuery = $@"
                SELECT [System.Id]
                FROM WorkItems
                WHERE [System.TeamProject] = '{EscapeWiqlString(project)}'
                AND [System.State] NOT IN ('Done', 'Closed', 'Completed', 'Removed', 'Cut')
                {typeFilter}{areaFilter}
                ORDER BY [System.ChangedDate] DESC";

            var wipItems = await _azureDevOpsService.QueryWorkItemsAsync(wiqlQuery, project, 500, cancellationToken);

            if (wipItems.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    message = "No work items in progress found",
                    totalWip = 0
                }, JsonOptions);
            }

            var now = DateTime.UtcNow;

            // Calculate WIP by state
            var byState = wipItems
                .GroupBy(w => w.State ?? "Unknown")
                .Select(g => new WipByStateDto
                {
                    State = g.Key,
                    Count = g.Count(),
                    Percentage = Math.Round((double)g.Count() / wipItems.Count * 100, 1),
                    AverageAgeDays = Math.Round(g.Average(w =>
                        w.ChangedDate.HasValue ? (now - w.ChangedDate.Value).TotalDays : 0), 1)
                })
                .OrderByDescending(s => s.Count)
                .ToList();

            // Calculate WIP by area
            var byArea = wipItems
                .GroupBy(w => GetLastAreaSegment(w.AreaPath))
                .Select(g =>
                {
                    var uniqueAssignees = g.Select(w => w.AssignedTo).Where(a => !string.IsNullOrEmpty(a)).Distinct().Count();
                    var itemsPerPerson = uniqueAssignees > 0 ? (double)g.Count() / uniqueAssignees : g.Count();
                    return new WipByAreaDto
                    {
                        AreaPath = g.Key,
                        Count = g.Count(),
                        UniqueAssignees = uniqueAssignees,
                        ItemsPerPerson = Math.Round(itemsPerPerson, 1),
                        IsOverloaded = itemsPerPerson > overloadThreshold
                    };
                })
                .OrderByDescending(a => a.Count)
                .ToList();

            // Calculate WIP by person
            var byPerson = wipItems
                .Where(w => !string.IsNullOrEmpty(w.AssignedTo))
                .GroupBy(w => w.AssignedTo!)
                .Select(g =>
                {
                    var stateBreakdown = g.GroupBy(w => w.State ?? "Unknown")
                        .ToDictionary(sg => sg.Key, sg => sg.Count());
                    var oldestAge = g.Max(w => w.ChangedDate.HasValue ? (now - w.ChangedDate.Value).TotalDays : 0);
                    return new WipByPersonDto
                    {
                        AssignedTo = g.Key,
                        Count = g.Count(),
                        ByState = stateBreakdown,
                        IsOverloaded = g.Count() > overloadThreshold,
                        OldestItemAgeDays = Math.Round(oldestAge, 1)
                    };
                })
                .OrderByDescending(p => p.Count)
                .ToList();

            // Calculate WIP by type
            var byType = wipItems
                .GroupBy(w => w.WorkItemType ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());

            // Find aging items
            var agingItems = wipItems
                .Where(w => w.ChangedDate.HasValue && (now - w.ChangedDate.Value).TotalDays > agingThresholdDays)
                .Select(w => new AgingWorkItemDto
                {
                    Id = w.Id,
                    Title = w.Title,
                    WorkItemType = w.WorkItemType,
                    State = w.State,
                    AssignedTo = w.AssignedTo,
                    AreaPath = w.AreaPath,
                    StateChangedDate = w.ChangedDate,
                    DaysInState = Math.Round((now - w.ChangedDate!.Value).TotalDays, 1),
                    DaysSinceCreation = w.CreatedDate.HasValue
                        ? Math.Round((now - w.CreatedDate.Value).TotalDays, 1)
                        : 0,
                    Priority = w.Priority,
                    AgingReason = $"No updates for {Math.Round((now - w.ChangedDate!.Value).TotalDays, 0)} days"
                })
                .OrderByDescending(a => a.DaysInState)
                .Take(20)
                .ToList();

            // Generate insights
            var insights = GenerateWipInsights(wipItems.Count, byState, byPerson, byArea, agingItems, overloadThreshold);

            var result = new WipAnalysisDto
            {
                AnalysisDate = now,
                TotalWip = wipItems.Count,
                ByState = byState,
                ByArea = byArea,
                ByPerson = byPerson,
                ByType = byType,
                AgingItems = agingItems,
                Insights = insights
            };

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing WIP");
            return JsonSerializer.Serialize(new { error = $"Error analyzing WIP: {ex.Message}" }, JsonOptions);
        }
    }

    [McpServerTool(Name = "get_bottlenecks")]
    [Description(@"Identifies bottlenecks in the workflow by analyzing where work is accumulating.

Analyzes:
- States with abnormally high item counts
- People with too many assignments
- Areas/teams with work piling up
- Long-running items blocking flow

Returns prioritized list of bottlenecks with recommendations.
Use this to answer 'Where is our flow blocked?' or 'What's causing delays?'")]
    public async Task<string> GetBottlenecks(
        [Description("The project name (required)")] string project,
        [Description("Work item types to include, comma-separated. Leave empty for all types.")] string? workItemTypes = null,
        [Description("Area path filter (optional)")] string? areaPath = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Identifying bottlenecks for project {Project}", project);

            // Get WIP data first
            var wipJson = await GetWipAnalysis(project, workItemTypes, areaPath, 14, 5, cancellationToken);
            var wipData = JsonSerializer.Deserialize<WipAnalysisDto>(wipJson, JsonOptions);

            if (wipData == null || wipData.TotalWip == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    message = "No work in progress found - no bottlenecks to analyze",
                    bottlenecks = Array.Empty<object>()
                }, JsonOptions);
            }

            var bottlenecks = new List<BottleneckDto>();
            var recommendations = new List<string>();

            // Analyze state bottlenecks - states with disproportionate accumulation
            var avgItemsPerState = (double)wipData.TotalWip / Math.Max(wipData.ByState.Count, 1);
            foreach (var state in wipData.ByState.Where(s => s.Count > avgItemsPerState * 1.5))
            {
                var severity = Math.Min(10, (int)(state.Count / avgItemsPerState * 3));
                bottlenecks.Add(new BottleneckDto
                {
                    Type = "State",
                    Location = state.State,
                    ItemCount = state.Count,
                    Severity = severity,
                    Description = $"{state.Count} items ({state.Percentage}%) accumulated in '{state.State}' state. Average age: {state.AverageAgeDays} days."
                });
            }

            // Analyze person bottlenecks - overloaded team members
            foreach (var person in wipData.ByPerson.Where(p => p.IsOverloaded))
            {
                var severity = Math.Min(10, person.Count);
                bottlenecks.Add(new BottleneckDto
                {
                    Type = "Person",
                    Location = person.AssignedTo,
                    ItemCount = person.Count,
                    Severity = severity,
                    Description = $"{person.AssignedTo} has {person.Count} items assigned. Oldest item is {person.OldestItemAgeDays} days old."
                });
            }

            // Analyze area bottlenecks - overloaded teams
            foreach (var area in wipData.ByArea.Where(a => a.IsOverloaded))
            {
                var severity = Math.Min(10, (int)(area.ItemsPerPerson * 2));
                bottlenecks.Add(new BottleneckDto
                {
                    Type = "Area",
                    Location = area.AreaPath,
                    ItemCount = area.Count,
                    Severity = severity,
                    Description = $"Team '{area.AreaPath}' has {area.Count} items for {area.UniqueAssignees} people ({area.ItemsPerPerson} items/person)."
                });
            }

            // Analyze aging items as bottlenecks
            if (wipData.AgingItems.Count > 5)
            {
                var avgAge = wipData.AgingItems.Average(a => a.DaysInState);
                bottlenecks.Add(new BottleneckDto
                {
                    Type = "Aging",
                    Location = "Multiple items",
                    ItemCount = wipData.AgingItems.Count,
                    Severity = Math.Min(10, wipData.AgingItems.Count / 2),
                    Description = $"{wipData.AgingItems.Count} items have been stale for an extended period. Average age: {Math.Round(avgAge, 1)} days.",
                    SampleItemIds = wipData.AgingItems.Take(5).Select(a => a.Id).ToList()
                });
            }

            // Sort by severity
            bottlenecks = bottlenecks.OrderByDescending(b => b.Severity).ToList();

            // Generate recommendations
            if (bottlenecks.Any(b => b.Type == "State"))
            {
                var stateBottleneck = bottlenecks.First(b => b.Type == "State");
                recommendations.Add($"Focus on moving items out of '{stateBottleneck.Location}' state - consider adding capacity or reviewing blockers.");
            }

            if (bottlenecks.Any(b => b.Type == "Person"))
            {
                recommendations.Add("Redistribute work among team members to balance load and reduce single points of dependency.");
            }

            if (bottlenecks.Any(b => b.Type == "Aging"))
            {
                recommendations.Add("Review aging items for blockers, unclear requirements, or dependency issues. Consider breaking down or deprioritizing.");
            }

            var result = new BottleneckAnalysisDto
            {
                AnalysisDate = DateTime.UtcNow,
                Bottlenecks = bottlenecks,
                Recommendations = recommendations
            };

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error identifying bottlenecks");
            return JsonSerializer.Serialize(new { error = $"Error identifying bottlenecks: {ex.Message}" }, JsonOptions);
        }
    }

    [McpServerTool(Name = "get_team_workload")]
    [Description(@"Analyzes workload distribution across team members.

Shows:
- Items assigned to each person
- Balance of workload across the team
- Who might need help (overloaded)
- Who has capacity (underloaded)

Use this to answer 'Who is overloaded?', 'Is work evenly distributed?', or 'Who has capacity for more work?'")]
    public async Task<string> GetTeamWorkload(
        [Description("The project name (required)")] string project,
        [Description("Area path to filter by team (recommended)")] string? areaPath = null,
        [Description("Work item types to include, comma-separated. Leave empty for all types.")] string? workItemTypes = null,
        [Description("Target items per person for comparison (default: 3)")] int targetItemsPerPerson = 3,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Analyzing team workload for project {Project}", project);

            var typeFilter = BuildTypeFilter(workItemTypes);
            var areaFilter = string.IsNullOrWhiteSpace(areaPath)
                ? string.Empty
                : $" AND [System.AreaPath] UNDER '{EscapeWiqlString(areaPath)}'";

            var wiqlQuery = $@"
                SELECT [System.Id]
                FROM WorkItems
                WHERE [System.TeamProject] = '{EscapeWiqlString(project)}'
                AND [System.State] NOT IN ('Done', 'Closed', 'Completed', 'Removed', 'Cut')
                {typeFilter}{areaFilter}
                ORDER BY [System.AssignedTo] ASC";

            var items = await _azureDevOpsService.QueryWorkItemsAsync(wiqlQuery, project, 500, cancellationToken);

            if (items.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    message = "No active work items found",
                    totalItems = 0
                }, JsonOptions);
            }

            var now = DateTime.UtcNow;

            // Group by assignee
            var workloadByPerson = items
                .GroupBy(w => w.AssignedTo ?? "[Unassigned]")
                .Select(g =>
                {
                    var stateBreakdown = g.GroupBy(w => w.State ?? "Unknown")
                        .ToDictionary(sg => sg.Key, sg => sg.Count());

                    var typeBreakdown = g.GroupBy(w => w.WorkItemType ?? "Unknown")
                        .ToDictionary(tg => tg.Key, tg => tg.Count());

                    var priorityBreakdown = g.GroupBy(w => w.Priority ?? "Unset")
                        .ToDictionary(pg => pg.Key, pg => pg.Count());

                    var avgAge = g.Average(w => w.ChangedDate.HasValue
                        ? (now - w.ChangedDate.Value).TotalDays : 0);

                    var deviation = g.Count() - targetItemsPerPerson;
                    var status = deviation > 2 ? "overloaded" : (deviation < -1 ? "has capacity" : "balanced");

                    return new
                    {
                        assignedTo = g.Key,
                        itemCount = g.Count(),
                        deviation = deviation,
                        status = status,
                        byState = stateBreakdown,
                        byType = typeBreakdown,
                        byPriority = priorityBreakdown,
                        averageItemAgeDays = Math.Round(avgAge, 1),
                        items = g.Select(w => new
                        {
                            id = w.Id,
                            title = w.Title,
                            type = w.WorkItemType,
                            state = w.State,
                            priority = w.Priority,
                            ageDays = w.ChangedDate.HasValue
                                ? Math.Round((now - w.ChangedDate.Value).TotalDays, 1) : 0
                        }).OrderBy(w => w.priority).ThenByDescending(w => w.ageDays).ToList()
                    };
                })
                .OrderByDescending(p => p.itemCount)
                .ToList();

            var unassignedCount = workloadByPerson.FirstOrDefault(p => p.assignedTo == "[Unassigned]")?.itemCount ?? 0;
            var assignedPeople = workloadByPerson.Where(p => p.assignedTo != "[Unassigned]").ToList();
            var avgItemsPerPerson = assignedPeople.Count > 0
                ? Math.Round((double)assignedPeople.Sum(p => p.itemCount) / assignedPeople.Count, 1)
                : 0;

            var summary = new
            {
                totalItems = items.Count,
                unassignedItems = unassignedCount,
                teamSize = assignedPeople.Count,
                averageItemsPerPerson = avgItemsPerPerson,
                targetItemsPerPerson = targetItemsPerPerson,
                overloadedCount = assignedPeople.Count(p => p.status == "overloaded"),
                hasCapacityCount = assignedPeople.Count(p => p.status == "has capacity"),
                balancedCount = assignedPeople.Count(p => p.status == "balanced")
            };

            var insights = new List<string>();

            if (unassignedCount > 0)
            {
                insights.Add($"{unassignedCount} items are unassigned and need attention.");
            }

            if (summary.overloadedCount > 0)
            {
                var overloaded = assignedPeople.Where(p => p.status == "overloaded").Select(p => p.assignedTo);
                insights.Add($"{summary.overloadedCount} team member(s) appear overloaded: {string.Join(", ", overloaded)}");
            }

            if (summary.hasCapacityCount > 0 && summary.overloadedCount > 0)
            {
                insights.Add("Consider redistributing work from overloaded members to those with capacity.");
            }

            return JsonSerializer.Serialize(new
            {
                analysisDate = now,
                summary,
                insights,
                workload = workloadByPerson
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing team workload");
            return JsonSerializer.Serialize(new { error = $"Error analyzing team workload: {ex.Message}" }, JsonOptions);
        }
    }

    private static string GetLastAreaSegment(string? areaPath)
    {
        if (string.IsNullOrWhiteSpace(areaPath)) return "Unknown";
        var segments = areaPath.Split('\\');
        return segments.Length > 0 ? segments[^1] : areaPath;
    }

    private static List<string> GenerateWipInsights(
        int totalWip,
        List<WipByStateDto> byState,
        List<WipByPersonDto> byPerson,
        List<WipByAreaDto> byArea,
        List<AgingWorkItemDto> agingItems,
        int overloadThreshold)
    {
        var insights = new List<string>();

        // Overall WIP insight
        insights.Add($"Total WIP: {totalWip} items across {byState.Count} states.");

        // State accumulation
        var topState = byState.FirstOrDefault();
        if (topState != null && topState.Percentage > 40)
        {
            insights.Add($"Warning: {topState.Percentage}% of WIP is in '{topState.State}' state - potential bottleneck.");
        }

        // Overloaded people
        var overloadedCount = byPerson.Count(p => p.IsOverloaded);
        if (overloadedCount > 0)
        {
            insights.Add($"{overloadedCount} team member(s) have more than {overloadThreshold} items assigned - consider rebalancing.");
        }

        // Aging items
        if (agingItems.Count > 0)
        {
            var avgAge = agingItems.Average(a => a.DaysInState);
            insights.Add($"{agingItems.Count} items are aging (avg {Math.Round(avgAge, 0)} days) - review for blockers.");
        }

        // Unbalanced teams
        var overloadedAreas = byArea.Count(a => a.IsOverloaded);
        if (overloadedAreas > 0)
        {
            insights.Add($"{overloadedAreas} team(s) appear overloaded based on items per person ratio.");
        }

        return insights;
    }

    [McpServerTool(Name = "get_aging_report")]
    [Description(@"Generates a detailed report of aging work items that need attention.

Provides:
- Items classified by urgency (Critical, High, Medium, Low)
- Breakdown by state, person, and area to identify patterns
- Specific recommendations for each aging item
- Statistics about aging patterns

Urgency is calculated based on:
- Priority (P1 items age faster than P4)
- Time since last update
- Current state (items stuck in certain states are more urgent)

Use this to answer 'What needs attention now?', 'Which items are stuck?', or 'What are we forgetting about?'")]
    public async Task<string> GetAgingReport(
        [Description("The project name (required)")] string project,
        [Description("Threshold in days to consider an item aging (default: 7)")] int agingThresholdDays = 7,
        [Description("Critical threshold multiplier (default: 3x aging threshold)")] int criticalMultiplier = 3,
        [Description("Work item types to include, comma-separated. Leave empty for all types.")] string? workItemTypes = null,
        [Description("Area path filter (optional)")] string? areaPath = null,
        [Description("Maximum number of items to return (default: 50)")] int maxItems = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating aging report for project {Project}", project);

            var typeFilter = BuildTypeFilter(workItemTypes);
            var areaFilter = string.IsNullOrWhiteSpace(areaPath)
                ? string.Empty
                : $" AND [System.AreaPath] UNDER '{EscapeWiqlString(areaPath)}'";

            // Query all active items
            var wiqlQuery = $@"
                SELECT [System.Id]
                FROM WorkItems
                WHERE [System.TeamProject] = '{EscapeWiqlString(project)}'
                AND [System.State] NOT IN ('Done', 'Closed', 'Completed', 'Removed', 'Cut')
                {typeFilter}{areaFilter}
                ORDER BY [System.ChangedDate] ASC";

            var allItems = await _azureDevOpsService.QueryWorkItemsAsync(wiqlQuery, project, 500, cancellationToken);

            if (allItems.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    message = "No active work items found",
                    totalAgingItems = 0
                }, JsonOptions);
            }

            var now = DateTime.UtcNow;
            var criticalThreshold = agingThresholdDays * criticalMultiplier;
            var highThreshold = agingThresholdDays * 2;

            // Filter to aging items and calculate urgency
            var agingItemDetails = allItems
                .Where(w => w.ChangedDate.HasValue && (now - w.ChangedDate.Value).TotalDays >= agingThresholdDays)
                .Select(w =>
                {
                    var daysSinceUpdate = (now - w.ChangedDate!.Value).TotalDays;
                    var daysSinceCreation = w.CreatedDate.HasValue ? (now - w.CreatedDate.Value).TotalDays : daysSinceUpdate;

                    // Calculate urgency score (1-10)
                    var urgencyScore = CalculateUrgencyScore(w, daysSinceUpdate, agingThresholdDays, criticalThreshold);
                    var urgency = urgencyScore >= 8 ? "Critical" :
                                  urgencyScore >= 6 ? "High" :
                                  urgencyScore >= 4 ? "Medium" : "Low";

                    var recommendation = GenerateItemRecommendation(w, daysSinceUpdate, urgency);

                    return new AgingItemDetailDto
                    {
                        Id = w.Id,
                        Title = w.Title,
                        WorkItemType = w.WorkItemType,
                        State = w.State,
                        AssignedTo = w.AssignedTo,
                        AreaPath = w.AreaPath,
                        Priority = w.Priority,
                        DaysSinceUpdate = Math.Round(daysSinceUpdate, 1),
                        DaysSinceCreation = Math.Round(daysSinceCreation, 1),
                        Urgency = urgency,
                        UrgencyScore = urgencyScore,
                        Recommendation = recommendation
                    };
                })
                .OrderByDescending(a => a.UrgencyScore)
                .ThenByDescending(a => a.DaysSinceUpdate)
                .ToList();

            if (agingItemDetails.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    message = $"No items older than {agingThresholdDays} days found - looking good!",
                    totalAgingItems = 0,
                    totalWip = allItems.Count
                }, JsonOptions);
            }

            // Group by urgency
            var byUrgency = new AgingByUrgencyDto
            {
                Critical = agingItemDetails.Where(a => a.Urgency == "Critical").Select(a => a.Id).ToList(),
                High = agingItemDetails.Where(a => a.Urgency == "High").Select(a => a.Id).ToList(),
                Medium = agingItemDetails.Where(a => a.Urgency == "Medium").Select(a => a.Id).ToList(),
                Low = agingItemDetails.Where(a => a.Urgency == "Low").Select(a => a.Id).ToList()
            };

            // Group by state
            var byState = agingItemDetails
                .GroupBy(a => a.State ?? "Unknown")
                .Select(g => new AgingByGroupDto
                {
                    Name = g.Key,
                    Count = g.Count(),
                    AverageAgeDays = Math.Round(g.Average(a => a.DaysSinceUpdate), 1),
                    MaxAgeDays = Math.Round(g.Max(a => a.DaysSinceUpdate), 1),
                    ItemIds = g.Select(a => a.Id).ToList()
                })
                .OrderByDescending(g => g.Count)
                .ToList();

            // Group by assignee
            var byAssignee = agingItemDetails
                .GroupBy(a => a.AssignedTo ?? "[Unassigned]")
                .Select(g => new AgingByGroupDto
                {
                    Name = g.Key,
                    Count = g.Count(),
                    AverageAgeDays = Math.Round(g.Average(a => a.DaysSinceUpdate), 1),
                    MaxAgeDays = Math.Round(g.Max(a => a.DaysSinceUpdate), 1),
                    ItemIds = g.Select(a => a.Id).ToList()
                })
                .OrderByDescending(g => g.Count)
                .ToList();

            // Group by area
            var byArea = agingItemDetails
                .GroupBy(a => GetLastAreaSegment(a.AreaPath))
                .Select(g => new AgingByGroupDto
                {
                    Name = g.Key,
                    Count = g.Count(),
                    AverageAgeDays = Math.Round(g.Average(a => a.DaysSinceUpdate), 1),
                    MaxAgeDays = Math.Round(g.Max(a => a.DaysSinceUpdate), 1),
                    ItemIds = g.Select(a => a.Id).ToList()
                })
                .OrderByDescending(g => g.Count)
                .ToList();

            // Summary statistics
            var summary = new AgingSummaryDto
            {
                AverageAgeDays = Math.Round(agingItemDetails.Average(a => a.DaysSinceUpdate), 1),
                MedianAgeDays = Math.Round(GetMedian(agingItemDetails.Select(a => a.DaysSinceUpdate).ToList()), 1),
                MaxAgeDays = Math.Round(agingItemDetails.Max(a => a.DaysSinceUpdate), 1),
                PercentageOfWip = Math.Round((double)agingItemDetails.Count / allItems.Count * 100, 1),
                TopAgingState = byState.FirstOrDefault()?.Name ?? "N/A",
                TopAgingAssignee = byAssignee.FirstOrDefault()?.Name ?? "N/A"
            };

            // Generate overall recommendations
            var recommendations = GenerateAgingRecommendations(agingItemDetails, byState, byAssignee, byUrgency);

            var result = new AgingReportDto
            {
                AnalysisDate = now,
                TotalAgingItems = agingItemDetails.Count,
                Summary = summary,
                ByUrgency = byUrgency,
                ByState = byState,
                ByAssignee = byAssignee,
                ByArea = byArea,
                Items = agingItemDetails.Take(maxItems).ToList(),
                Recommendations = recommendations
            };

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating aging report");
            return JsonSerializer.Serialize(new { error = $"Error generating aging report: {ex.Message}" }, JsonOptions);
        }
    }

    private static int CalculateUrgencyScore(WorkItemDto item, double daysSinceUpdate, int agingThreshold, int criticalThreshold)
    {
        var score = 0;

        // Age factor (0-4 points)
        if (daysSinceUpdate >= criticalThreshold) score += 4;
        else if (daysSinceUpdate >= agingThreshold * 2) score += 3;
        else if (daysSinceUpdate >= agingThreshold * 1.5) score += 2;
        else score += 1;

        // Priority factor (0-3 points)
        var priority = item.Priority ?? "4";
        score += priority switch
        {
            "1" => 3,
            "2" => 2,
            "3" => 1,
            _ => 0
        };

        // State factor (0-2 points) - items in certain states are more urgent
        var state = item.State?.ToLowerInvariant() ?? "";
        if (state.Contains("blocked") || state.Contains("impediment"))
            score += 2;
        else if (state.Contains("review") || state.Contains("test"))
            score += 1;

        // Unassigned penalty (0-1 point)
        if (string.IsNullOrEmpty(item.AssignedTo))
            score += 1;

        return Math.Min(10, score);
    }

    private static string GenerateItemRecommendation(WorkItemDto item, double daysSinceUpdate, string urgency)
    {
        var state = item.State?.ToLowerInvariant() ?? "";
        var hasAssignee = !string.IsNullOrEmpty(item.AssignedTo);

        if (!hasAssignee)
            return "Assign this item to a team member to ensure ownership.";

        if (state.Contains("blocked") || state.Contains("impediment"))
            return "Identify and resolve the blocker. Consider escalating if external dependency.";

        if (state.Contains("review") || state.Contains("code review"))
            return "Ensure reviewer availability. Consider pairing or distributing review load.";

        if (state.Contains("test"))
            return "Check test environment availability. Verify test cases are ready.";

        if (state.Contains("new") || state.Contains("proposed"))
            return "Item needs refinement or prioritization decision. Consider grooming session.";

        if (urgency == "Critical")
            return "Immediate attention needed. Schedule sync with assignee to unblock.";

        if (urgency == "High")
            return "Review with assignee this week. Check for hidden blockers or unclear requirements.";

        if (daysSinceUpdate > 30)
            return "Consider if this item is still relevant. May need re-prioritization or removal.";

        return "Follow up with assignee on progress and any obstacles.";
    }

    private static List<string> GenerateAgingRecommendations(
        List<AgingItemDetailDto> items,
        List<AgingByGroupDto> byState,
        List<AgingByGroupDto> byAssignee,
        AgingByUrgencyDto byUrgency)
    {
        var recommendations = new List<string>();

        if (byUrgency.Critical.Count > 0)
        {
            recommendations.Add($"URGENT: {byUrgency.Critical.Count} critical items need immediate attention. Review IDs: {string.Join(", ", byUrgency.Critical.Take(5))}");
        }

        if (byState.FirstOrDefault() is { } topState && topState.Count > items.Count * 0.3)
        {
            recommendations.Add($"'{topState.Name}' state has {topState.Count} aging items ({Math.Round(topState.Count * 100.0 / items.Count)}%). Focus on clearing this queue.");
        }

        var unassigned = byAssignee.FirstOrDefault(a => a.Name == "[Unassigned]");
        if (unassigned != null && unassigned.Count > 0)
        {
            recommendations.Add($"{unassigned.Count} aging items are unassigned. Assign owners to ensure accountability.");
        }

        var overloadedPeople = byAssignee.Where(a => a.Name != "[Unassigned]" && a.Count > 5).ToList();
        if (overloadedPeople.Count > 0)
        {
            var names = string.Join(", ", overloadedPeople.Select(p => p.Name));
            recommendations.Add($"Consider redistributing work from: {names} (each has >5 aging items).");
        }

        var veryOldItems = items.Where(i => i.DaysSinceUpdate > 30).ToList();
        if (veryOldItems.Count > 0)
        {
            recommendations.Add($"{veryOldItems.Count} items are over 30 days old. Review for relevance - consider closing or breaking down.");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Aging levels are manageable. Continue regular review cadence.");
        }

        return recommendations;
    }

    private static double GetMedian(List<double> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2
            : sorted[mid];
    }

    #endregion
}
