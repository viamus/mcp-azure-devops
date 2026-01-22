using System.Text.Json;
using Moq;
using Viamus.Azure.Devops.Mcp.Server.Models;
using Viamus.Azure.Devops.Mcp.Server.Services;
using Viamus.Azure.Devops.Mcp.Server.Tools;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Tools;

public class WorkItemToolsTests
{
    private readonly Mock<IAzureDevOpsService> _mockService;
    private readonly WorkItemTools _tools;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public WorkItemToolsTests()
    {
        _mockService = new Mock<IAzureDevOpsService>();
        _tools = new WorkItemTools(_mockService.Object);
    }

    #region GetWorkItem Tests

    [Fact]
    public async Task GetWorkItem_WhenWorkItemExists_ShouldReturnSerializedWorkItem()
    {
        var workItem = new WorkItemDto
        {
            Id = 123,
            Title = "Test Work Item",
            State = "Active",
            WorkItemType = "Task"
        };

        _mockService
            .Setup(s => s.GetWorkItemAsync(123, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        var result = await _tools.GetWorkItem(123);

        Assert.Contains("\"id\": 123", result);
        Assert.Contains("\"title\": \"Test Work Item\"", result);
        Assert.Contains("\"state\": \"Active\"", result);
    }

    [Fact]
    public async Task GetWorkItem_WhenWorkItemNotFound_ShouldReturnError()
    {
        _mockService
            .Setup(s => s.GetWorkItemAsync(999, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItemDto?)null);

        var result = await _tools.GetWorkItem(999);

        Assert.Contains("error", result);
        Assert.Contains("Work item 999 not found", result);
    }

    [Fact]
    public async Task GetWorkItem_WithProject_ShouldPassProjectToService()
    {
        var workItem = new WorkItemDto { Id = 123, Title = "Test" };
        _mockService
            .Setup(s => s.GetWorkItemAsync(123, "MyProject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        await _tools.GetWorkItem(123, "MyProject");

        _mockService.Verify(s => s.GetWorkItemAsync(123, "MyProject", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetWorkItems Tests

    [Fact]
    public async Task GetWorkItems_WithValidIds_ShouldReturnWorkItems()
    {
        var workItems = new List<WorkItemDto>
        {
            new() { Id = 1, Title = "Item 1" },
            new() { Id = 2, Title = "Item 2" }
        };

        _mockService
            .Setup(s => s.GetWorkItemsAsync(It.Is<IEnumerable<int>>(ids => ids.Contains(1) && ids.Contains(2)), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var result = await _tools.GetWorkItems("1,2");

        Assert.Contains("\"count\": 2", result);
        Assert.Contains("Item 1", result);
        Assert.Contains("Item 2", result);
    }

    [Fact]
    public async Task GetWorkItems_WithEmptyString_ShouldReturnError()
    {
        var result = await _tools.GetWorkItems("");

        Assert.Contains("error", result);
        Assert.Contains("No valid work item IDs provided", result);
    }

    [Fact]
    public async Task GetWorkItems_WithWhitespaceString_ShouldReturnError()
    {
        var result = await _tools.GetWorkItems("   ");

        Assert.Contains("error", result);
        Assert.Contains("No valid work item IDs provided", result);
    }

    [Fact]
    public async Task GetWorkItems_WithInvalidIds_ShouldReturnError()
    {
        var result = await _tools.GetWorkItems("abc,xyz");

        Assert.Contains("error", result);
        Assert.Contains("No valid work item IDs provided", result);
    }

    [Fact]
    public async Task GetWorkItems_WithMixedValidAndInvalidIds_ShouldProcessValidOnes()
    {
        var workItems = new List<WorkItemDto>
        {
            new() { Id = 1, Title = "Item 1" }
        };

        _mockService
            .Setup(s => s.GetWorkItemsAsync(It.Is<IEnumerable<int>>(ids => ids.Count() == 1 && ids.Contains(1)), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var result = await _tools.GetWorkItems("1,abc,xyz");

        Assert.Contains("\"count\": 1", result);
    }

    [Fact]
    public async Task GetWorkItems_WithDuplicateIds_ShouldProcessDistinct()
    {
        var workItems = new List<WorkItemDto>
        {
            new() { Id = 1, Title = "Item 1" }
        };

        _mockService
            .Setup(s => s.GetWorkItemsAsync(It.Is<IEnumerable<int>>(ids => ids.Count() == 1), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var result = await _tools.GetWorkItems("1,1,1");

        _mockService.Verify(s => s.GetWorkItemsAsync(It.Is<IEnumerable<int>>(ids => ids.Count() == 1), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetWorkItems_WithSpacesAroundIds_ShouldTrimAndProcess()
    {
        var workItems = new List<WorkItemDto>
        {
            new() { Id = 1, Title = "Item 1" },
            new() { Id = 2, Title = "Item 2" }
        };

        _mockService
            .Setup(s => s.GetWorkItemsAsync(It.IsAny<IEnumerable<int>>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var result = await _tools.GetWorkItems(" 1 , 2 ");

        Assert.Contains("\"count\": 2", result);
    }

    #endregion

    #region QueryWorkItems Tests

    [Fact]
    public async Task QueryWorkItems_ShouldReturnPaginatedResults()
    {
        var paginatedResult = new PaginatedResult<WorkItemSummaryDto>
        {
            Items = [new WorkItemSummaryDto { Id = 1, Title = "Bug 1", State = "Active" }],
            TotalCount = 1,
            Page = 1,
            PageSize = 20
        };

        _mockService
            .Setup(s => s.QueryWorkItemsSummaryAsync(It.IsAny<string>(), null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResult);

        var result = await _tools.QueryWorkItems("SELECT [System.Id] FROM WorkItems WHERE [System.State] = 'Active'");

        Assert.Contains("\"totalCount\": 1", result);
        Assert.Contains("\"page\": 1", result);
        Assert.Contains("\"pageSize\": 20", result);
        Assert.Contains("Bug 1", result);
    }

    [Fact]
    public async Task QueryWorkItems_WithPagination_ShouldPassPageParameters()
    {
        var paginatedResult = new PaginatedResult<WorkItemSummaryDto>
        {
            Items = [],
            TotalCount = 100,
            Page = 3,
            PageSize = 10
        };

        _mockService
            .Setup(s => s.QueryWorkItemsSummaryAsync(It.IsAny<string>(), null, 3, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResult);

        var result = await _tools.QueryWorkItems("SELECT * FROM WorkItems", page: 3, pageSize: 10);

        _mockService.Verify(s => s.QueryWorkItemsSummaryAsync(It.IsAny<string>(), null, 3, 10, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("\"page\": 3", result);
        Assert.Contains("\"pageSize\": 10", result);
    }

    [Fact]
    public async Task QueryWorkItems_WithProject_ShouldPassProjectToService()
    {
        var paginatedResult = new PaginatedResult<WorkItemSummaryDto>
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 20
        };

        _mockService
            .Setup(s => s.QueryWorkItemsSummaryAsync(It.IsAny<string>(), "MyProject", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResult);

        await _tools.QueryWorkItems("SELECT * FROM WorkItems", project: "MyProject");

        _mockService.Verify(s => s.QueryWorkItemsSummaryAsync(It.IsAny<string>(), "MyProject", 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryWorkItems_ShouldReturnPaginationMetadata()
    {
        var paginatedResult = new PaginatedResult<WorkItemSummaryDto>
        {
            Items = [new WorkItemSummaryDto { Id = 1, Title = "Item" }],
            TotalCount = 50,
            Page = 2,
            PageSize = 20
        };

        _mockService
            .Setup(s => s.QueryWorkItemsSummaryAsync(It.IsAny<string>(), null, 2, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResult);

        var result = await _tools.QueryWorkItems("SELECT * FROM WorkItems", page: 2);

        Assert.Contains("\"totalCount\": 50", result);
        Assert.Contains("\"totalPages\": 3", result);
        Assert.Contains("\"hasNextPage\": true", result);
        Assert.Contains("\"hasPreviousPage\": true", result);
    }

    #endregion

    #region GetWorkItemsByState Tests

    [Fact]
    public async Task GetWorkItemsByState_ShouldReturnPaginatedResults()
    {
        var paginatedResult = new PaginatedResult<WorkItemSummaryDto>
        {
            Items = [new WorkItemSummaryDto { Id = 1, Title = "Bug", State = "Active" }],
            TotalCount = 1,
            Page = 1,
            PageSize = 20
        };

        _mockService
            .Setup(s => s.QueryWorkItemsSummaryAsync(It.IsAny<string>(), "TestProject", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResult);

        var result = await _tools.GetWorkItemsByState("Active", "TestProject");

        Assert.Contains("\"state\": \"Active\"", result);
        Assert.Contains("\"totalCount\": 1", result);
    }

    [Fact]
    public async Task GetWorkItemsByState_WithWorkItemTypeFilter_ShouldIncludeInQuery()
    {
        var paginatedResult = new PaginatedResult<WorkItemSummaryDto>
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 20
        };

        _mockService
            .Setup(s => s.QueryWorkItemsSummaryAsync(It.Is<string>(q => q.Contains("[System.WorkItemType] = 'Bug'")), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResult);

        await _tools.GetWorkItemsByState("Active", "TestProject", "Bug");

        _mockService.Verify(s => s.QueryWorkItemsSummaryAsync(It.Is<string>(q => q.Contains("[System.WorkItemType] = 'Bug'")), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetWorkItemsAssignedTo Tests

    [Fact]
    public async Task GetWorkItemsAssignedTo_ShouldReturnPaginatedResults()
    {
        var paginatedResult = new PaginatedResult<WorkItemSummaryDto>
        {
            Items = [new WorkItemSummaryDto { Id = 1, Title = "Task", AssignedTo = "John Doe" }],
            TotalCount = 1,
            Page = 1,
            PageSize = 20
        };

        _mockService
            .Setup(s => s.QueryWorkItemsSummaryAsync(It.Is<string>(q => q.Contains("CONTAINS 'John Doe'")), "TestProject", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResult);

        var result = await _tools.GetWorkItemsAssignedTo("John Doe", "TestProject");

        Assert.Contains("\"assignedTo\": \"John Doe\"", result);
        Assert.Contains("\"totalCount\": 1", result);
    }

    [Fact]
    public async Task GetWorkItemsAssignedTo_WithStateFilter_ShouldIncludeInQuery()
    {
        var paginatedResult = new PaginatedResult<WorkItemSummaryDto>
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 20
        };

        _mockService
            .Setup(s => s.QueryWorkItemsSummaryAsync(It.Is<string>(q => q.Contains("[System.State] = 'Active'")), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResult);

        await _tools.GetWorkItemsAssignedTo("John", "TestProject", "Active");

        _mockService.Verify(s => s.QueryWorkItemsSummaryAsync(It.Is<string>(q => q.Contains("[System.State] = 'Active'")), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetChildWorkItems Tests

    [Fact]
    public async Task GetChildWorkItems_ShouldReturnChildItems()
    {
        var children = new List<WorkItemDto>
        {
            new() { Id = 2, Title = "Child 1", ParentId = 1 },
            new() { Id = 3, Title = "Child 2", ParentId = 1 }
        };

        _mockService
            .Setup(s => s.GetChildWorkItemsAsync(1, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(children);

        var result = await _tools.GetChildWorkItems(1);

        Assert.Contains("\"parentWorkItemId\": 1", result);
        Assert.Contains("\"count\": 2", result);
        Assert.Contains("Child 1", result);
        Assert.Contains("Child 2", result);
    }

    [Fact]
    public async Task GetChildWorkItems_WhenNoChildren_ShouldReturnEmptyList()
    {
        _mockService
            .Setup(s => s.GetChildWorkItemsAsync(1, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkItemDto>());

        var result = await _tools.GetChildWorkItems(1);

        Assert.Contains("\"count\": 0", result);
    }

    #endregion

    #region GetRecentWorkItems Tests

    [Fact]
    public async Task GetRecentWorkItems_ShouldReturnRecentItems()
    {
        var paginatedResult = new PaginatedResult<WorkItemSummaryDto>
        {
            Items = [new WorkItemSummaryDto { Id = 1, Title = "Recent Item" }],
            TotalCount = 1,
            Page = 1,
            PageSize = 20
        };

        _mockService
            .Setup(s => s.QueryWorkItemsSummaryAsync(It.Is<string>(q => q.Contains("[System.ChangedDate] >=")), "TestProject", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResult);

        var result = await _tools.GetRecentWorkItems("TestProject");

        Assert.Contains("\"daysBack\": 7", result);
        Assert.Contains("\"totalCount\": 1", result);
    }

    [Fact]
    public async Task GetRecentWorkItems_ShouldClampDaysBack()
    {
        var paginatedResult = new PaginatedResult<WorkItemSummaryDto>
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 20
        };

        _mockService
            .Setup(s => s.QueryWorkItemsSummaryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResult);

        var result = await _tools.GetRecentWorkItems("TestProject", daysBack: 100);

        Assert.Contains("\"daysBack\": 30", result);
    }

    [Fact]
    public async Task GetRecentWorkItems_WithDaysBackLessThanOne_ShouldClampToOne()
    {
        var paginatedResult = new PaginatedResult<WorkItemSummaryDto>
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 20
        };

        _mockService
            .Setup(s => s.QueryWorkItemsSummaryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResult);

        var result = await _tools.GetRecentWorkItems("TestProject", daysBack: 0);

        Assert.Contains("\"daysBack\": 1", result);
    }

    #endregion

    #region SearchWorkItems Tests

    [Fact]
    public async Task SearchWorkItems_ShouldSearchByTitle()
    {
        var paginatedResult = new PaginatedResult<WorkItemSummaryDto>
        {
            Items = [new WorkItemSummaryDto { Id = 1, Title = "Login Bug" }],
            TotalCount = 1,
            Page = 1,
            PageSize = 20
        };

        _mockService
            .Setup(s => s.QueryWorkItemsSummaryAsync(It.Is<string>(q => q.Contains("CONTAINS 'Login'")), "TestProject", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResult);

        var result = await _tools.SearchWorkItems("Login", "TestProject");

        Assert.Contains("\"searchText\": \"Login\"", result);
        Assert.Contains("\"totalCount\": 1", result);
    }

    [Fact]
    public async Task SearchWorkItems_WithWorkItemTypeFilter_ShouldIncludeInQuery()
    {
        var paginatedResult = new PaginatedResult<WorkItemSummaryDto>
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 20
        };

        _mockService
            .Setup(s => s.QueryWorkItemsSummaryAsync(It.Is<string>(q => q.Contains("[System.WorkItemType] = 'Bug'")), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResult);

        await _tools.SearchWorkItems("test", "TestProject", "Bug");

        _mockService.Verify(s => s.QueryWorkItemsSummaryAsync(It.Is<string>(q => q.Contains("[System.WorkItemType] = 'Bug'")), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region WIQL Escape Tests

    [Fact]
    public async Task GetWorkItemsByState_WithSingleQuoteInState_ShouldEscape()
    {
        var paginatedResult = new PaginatedResult<WorkItemSummaryDto>
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 20
        };

        _mockService
            .Setup(s => s.QueryWorkItemsSummaryAsync(It.Is<string>(q => q.Contains("'Won''t Fix'")), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResult);

        await _tools.GetWorkItemsByState("Won't Fix", "TestProject");

        _mockService.Verify(s => s.QueryWorkItemsSummaryAsync(It.Is<string>(q => q.Contains("'Won''t Fix'")), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchWorkItems_WithSingleQuoteInSearchText_ShouldEscape()
    {
        var paginatedResult = new PaginatedResult<WorkItemSummaryDto>
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 20
        };

        _mockService
            .Setup(s => s.QueryWorkItemsSummaryAsync(It.Is<string>(q => q.Contains("'User''s Profile'")), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResult);

        await _tools.SearchWorkItems("User's Profile", "TestProject");

        _mockService.Verify(s => s.QueryWorkItemsSummaryAsync(It.Is<string>(q => q.Contains("'User''s Profile'")), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
