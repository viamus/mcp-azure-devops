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
            .Setup(s => s.GetWorkItemAsync(123, null, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
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
            .Setup(s => s.GetWorkItemAsync(999, null, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
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
            .Setup(s => s.GetWorkItemAsync(123, "MyProject", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        await _tools.GetWorkItem(123, "MyProject");

        _mockService.Verify(s => s.GetWorkItemAsync(123, "MyProject", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
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
            .Setup(s => s.GetWorkItemsAsync(It.Is<IEnumerable<int>>(ids => ids.Contains(1) && ids.Contains(2)), null, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
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
            .Setup(s => s.GetWorkItemsAsync(It.Is<IEnumerable<int>>(ids => ids.Count() == 1 && ids.Contains(1)), null, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
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
            .Setup(s => s.GetWorkItemsAsync(It.Is<IEnumerable<int>>(ids => ids.Count() == 1), null, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItems);

        var result = await _tools.GetWorkItems("1,1,1");

        _mockService.Verify(s => s.GetWorkItemsAsync(It.Is<IEnumerable<int>>(ids => ids.Count() == 1), null, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
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
            .Setup(s => s.GetWorkItemsAsync(It.IsAny<IEnumerable<int>>(), null, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
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

    #region LinkWorkItems Tests

    [Fact]
    public async Task LinkWorkItems_WithPredecessors_ShouldLinkUsingDependencyReverse()
    {
        var workItem = new WorkItemDto { Id = 100, Title = "Story" };

        _mockService
            .Setup(s => s.LinkWorkItemsAsync(
                100,
                It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 10, 20 })),
                "System.LinkTypes.Dependency-Reverse",
                "Predecessor",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        var result = await _tools.LinkWorkItems(100, "10,20", "predecessor");

        Assert.Contains("\"success\": true", result);
        Assert.Contains("\"relationType\": \"System.LinkTypes.Dependency-Reverse\"", result);
        Assert.Contains("\"targetWorkItemIds\":", result);
        _mockService.Verify(s => s.LinkWorkItemsAsync(
            100,
            It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 10, 20 })),
            "System.LinkTypes.Dependency-Reverse",
            "Predecessor",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LinkWorkItems_WithParent_ShouldLinkUsingHierarchyReverse()
    {
        var workItem = new WorkItemDto { Id = 100, Title = "Story", ParentId = 50 };

        _mockService
            .Setup(s => s.LinkWorkItemsAsync(
                100,
                It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 50 })),
                "System.LinkTypes.Hierarchy-Reverse",
                "Parent",
                "MyProject",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        var result = await _tools.LinkWorkItems(100, "50", "parent", project: "MyProject");

        Assert.Contains("\"success\": true", result);
        Assert.Contains("\"relationType\": \"System.LinkTypes.Hierarchy-Reverse\"", result);
        _mockService.Verify(s => s.LinkWorkItemsAsync(
            100,
            It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 50 })),
            "System.LinkTypes.Hierarchy-Reverse",
            "Parent",
            "MyProject",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LinkWorkItems_WithCustomComment_ShouldPassCommentToService()
    {
        var workItem = new WorkItemDto { Id = 100, Title = "Story" };

        _mockService
            .Setup(s => s.LinkWorkItemsAsync(
                100,
                It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 30 })),
                "System.LinkTypes.Dependency-Forward",
                "Blocks downstream story",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        await _tools.LinkWorkItems(100, "30", "successor", comment: "Blocks downstream story");

        _mockService.Verify(s => s.LinkWorkItemsAsync(
            100,
            It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 30 })),
            "System.LinkTypes.Dependency-Forward",
            "Blocks downstream story",
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LinkWorkItems_WithInvalidSourceId_ShouldReturnError()
    {
        var result = await _tools.LinkWorkItems(0, "10", "predecessor");

        Assert.Contains("error", result);
        Assert.Contains("sourceWorkItemId", result);
    }

    [Fact]
    public async Task LinkWorkItems_WithNoValidTargets_ShouldReturnError()
    {
        var result = await _tools.LinkWorkItems(100, "abc", "predecessor");

        Assert.Contains("error", result);
        Assert.Contains("No valid target work item IDs provided", result);
    }

    [Fact]
    public async Task LinkWorkItems_WithSelfLink_ShouldReturnError()
    {
        var result = await _tools.LinkWorkItems(100, "100", "related");

        Assert.Contains("error", result);
        Assert.Contains("cannot be linked to itself", result);
    }

    [Fact]
    public async Task LinkWorkItems_WithInvalidRelationType_ShouldReturnError()
    {
        var result = await _tools.LinkWorkItems(100, "10", "duplicate");

        Assert.Contains("error", result);
        Assert.Contains("relationType", result);
    }

    [Fact]
    public async Task LinkWorkItems_WithMultipleParents_ShouldReturnError()
    {
        var result = await _tools.LinkWorkItems(100, "10;20", "parent");

        Assert.Contains("error", result);
        Assert.Contains("only have one parent", result);
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

    #region AddWorkItemComment Tests

    [Fact]
    public async Task AddWorkItemComment_WithValidComment_ShouldReturnSuccess()
    {
        var createdComment = new WorkItemCommentDto
        {
            Id = 1,
            WorkItemId = 123,
            Text = "This is a test comment",
            CreatedBy = "John Doe",
            CreatedDate = DateTime.UtcNow
        };

        _mockService
            .Setup(s => s.AddWorkItemCommentAsync(123, "This is a test comment", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdComment);

        var result = await _tools.AddWorkItemComment(123, "This is a test comment");

        Assert.Contains("\"success\": true", result);
        Assert.Contains("Comment added to work item 123", result);
        Assert.Contains("This is a test comment", result);
    }

    [Fact]
    public async Task AddWorkItemComment_WithEmptyComment_ShouldReturnError()
    {
        var result = await _tools.AddWorkItemComment(123, "");

        Assert.Contains("error", result);
        Assert.Contains("Comment text cannot be empty", result);
    }

    [Fact]
    public async Task AddWorkItemComment_WithWhitespaceComment_ShouldReturnError()
    {
        var result = await _tools.AddWorkItemComment(123, "   ");

        Assert.Contains("error", result);
        Assert.Contains("Comment text cannot be empty", result);
    }

    [Fact]
    public async Task AddWorkItemComment_WithProject_ShouldPassProjectToService()
    {
        var createdComment = new WorkItemCommentDto
        {
            Id = 1,
            WorkItemId = 123,
            Text = "Test comment",
            CreatedBy = "John Doe"
        };

        _mockService
            .Setup(s => s.AddWorkItemCommentAsync(123, "Test comment", "MyProject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdComment);

        await _tools.AddWorkItemComment(123, "Test comment", "MyProject");

        _mockService.Verify(s => s.AddWorkItemCommentAsync(123, "Test comment", "MyProject", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetWorkItemComments Tests

    [Fact]
    public async Task GetWorkItemComments_WithValidId_ShouldReturnSerializedResult()
    {
        var serviceResult = new WorkItemCommentsResultDto
        {
            Comments = new List<WorkItemCommentDto>
            {
                new() { Id = 1, WorkItemId = 123, Text = "First comment", CreatedBy = "Alice" },
                new() { Id = 2, WorkItemId = 123, Text = "Second comment", CreatedBy = "Bob" }
            },
            TotalCount = 2,
            Count = 2
        };

        _mockService
            .Setup(s => s.GetWorkItemCommentsAsync(123, null, null, null, false, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _tools.GetWorkItemComments(123);

        Assert.Contains("\"totalCount\": 2", result);
        Assert.Contains("First comment", result);
        Assert.Contains("Second comment", result);
    }

    [Fact]
    public async Task GetWorkItemComments_WithInvalidId_ShouldReturnError()
    {
        var result = await _tools.GetWorkItemComments(0);

        Assert.Contains("error", result);
        Assert.Contains("workItemId", result);
    }

    [Fact]
    public async Task GetWorkItemComments_WithNonPositiveTop_ShouldReturnError()
    {
        var result = await _tools.GetWorkItemComments(123, top: 0);

        Assert.Contains("error", result);
        Assert.Contains("top", result);
    }

    [Fact]
    public async Task GetWorkItemComments_WithInvalidOrder_ShouldReturnError()
    {
        var result = await _tools.GetWorkItemComments(123, order: "sideways");

        Assert.Contains("error", result);
        Assert.Contains("order", result);
    }

    [Fact]
    public async Task GetWorkItemComments_WithAllOptionalArgs_ShouldPassThemToService()
    {
        var serviceResult = new WorkItemCommentsResultDto
        {
            Comments = Array.Empty<WorkItemCommentDto>(),
            TotalCount = 0,
            Count = 0,
            ContinuationToken = null
        };

        _mockService
            .Setup(s => s.GetWorkItemCommentsAsync(
                123, "MyProject", 50, "token-xyz", true, "desc", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        await _tools.GetWorkItemComments(
            workItemId: 123,
            project: "MyProject",
            top: 50,
            continuationToken: "token-xyz",
            includeDeleted: true,
            order: "desc",
            includeRenderedText: true);

        _mockService.Verify(s => s.GetWorkItemCommentsAsync(
            123, "MyProject", 50, "token-xyz", true, "desc", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetWorkItemComments_WithContinuationToken_ShouldReturnTokenInResponse()
    {
        var serviceResult = new WorkItemCommentsResultDto
        {
            Comments = new List<WorkItemCommentDto> { new() { Id = 1, WorkItemId = 123 } },
            TotalCount = 250,
            Count = 1,
            ContinuationToken = "next-page-token"
        };

        _mockService
            .Setup(s => s.GetWorkItemCommentsAsync(123, null, null, null, false, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _tools.GetWorkItemComments(123);

        Assert.Contains("\"continuationToken\": \"next-page-token\"", result);
        Assert.Contains("\"totalCount\": 250", result);
    }

    #endregion

    #region CreateWorkItem Tests

    [Fact]
    public async Task CreateWorkItem_WithRequiredFieldsOnly_ShouldReturnSuccess()
    {
        var workItem = new WorkItemDto
        {
            Id = 100,
            Title = "New Task",
            WorkItemType = "Task",
            State = "New"
        };

        _mockService
            .Setup(s => s.CreateWorkItemAsync(
                "MyProject", "Task", "New Task",
                null, null, null, null, null, null, null, null, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        var result = await _tools.CreateWorkItem("MyProject", "Task", "New Task");

        Assert.Contains("\"success\": true", result);
        Assert.Contains("Work item 100 created successfully", result);
        Assert.Contains("\"id\": 100", result);
    }

    [Fact]
    public async Task CreateWorkItem_WithAllFields_ShouldPassAllFieldsToService()
    {
        var workItem = new WorkItemDto { Id = 101, Title = "Full Task" };

        _mockService
            .Setup(s => s.CreateWorkItemAsync(
                "MyProject", "User Story", "Full Task",
                "A description", "user@test.com", "MyProject\\Area",
                "MyProject\\Sprint 1", "Active", 2, 50, "tag1; tag2",
                It.Is<Dictionary<string, string>>(d => d.ContainsKey("Custom.Field")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        var result = await _tools.CreateWorkItem(
            "MyProject", "User Story", "Full Task",
            description: "A description",
            assignedTo: "user@test.com",
            areaPath: "MyProject\\Area",
            iterationPath: "MyProject\\Sprint 1",
            state: "Active",
            priority: 2,
            parentId: 50,
            tags: "tag1; tag2",
            additionalFields: "{\"Custom.Field\": \"value\"}");

        Assert.Contains("\"success\": true", result);
        _mockService.Verify(s => s.CreateWorkItemAsync(
            "MyProject", "User Story", "Full Task",
            "A description", "user@test.com", "MyProject\\Area",
            "MyProject\\Sprint 1", "Active", 2, 50, "tag1; tag2",
            It.Is<Dictionary<string, string>>(d => d["Custom.Field"] == "value"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateWorkItem_WithEmptyProject_ShouldReturnError()
    {
        var result = await _tools.CreateWorkItem("", "Task", "Title");

        Assert.Contains("error", result);
        Assert.Contains("Project name is required", result);
    }

    [Fact]
    public async Task CreateWorkItem_WithEmptyTitle_ShouldReturnError()
    {
        var result = await _tools.CreateWorkItem("MyProject", "Task", "");

        Assert.Contains("error", result);
        Assert.Contains("Title is required", result);
    }

    [Fact]
    public async Task CreateWorkItem_WithEmptyWorkItemType_ShouldReturnError()
    {
        var result = await _tools.CreateWorkItem("MyProject", "", "Title");

        Assert.Contains("error", result);
        Assert.Contains("Work item type is required", result);
    }

    [Fact]
    public async Task CreateWorkItem_WithInvalidPriority_ShouldReturnError()
    {
        var result = await _tools.CreateWorkItem("MyProject", "Task", "Title", priority: 5);

        Assert.Contains("error", result);
        Assert.Contains("Priority must be between 1 and 4", result);
    }

    [Fact]
    public async Task CreateWorkItem_WithInvalidAdditionalFieldsJson_ShouldReturnError()
    {
        var result = await _tools.CreateWorkItem("MyProject", "Task", "Title", additionalFields: "not json");

        Assert.Contains("error", result);
        Assert.Contains("Invalid JSON format for additionalFields", result);
    }

    [Fact]
    public async Task CreateWorkItem_WithValidAdditionalFieldsJson_ShouldParseAndPassToService()
    {
        var workItem = new WorkItemDto { Id = 102, Title = "Task" };

        _mockService
            .Setup(s => s.CreateWorkItemAsync(
                "MyProject", "Task", "Task",
                null, null, null, null, null, null, null, null,
                It.Is<Dictionary<string, string>>(d => d["Custom.Field1"] == "val1" && d["Custom.Field2"] == "val2"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        var result = await _tools.CreateWorkItem("MyProject", "Task", "Task",
            additionalFields: "{\"Custom.Field1\": \"val1\", \"Custom.Field2\": \"val2\"}");

        Assert.Contains("\"success\": true", result);
    }

    #endregion

    #region UpdateWorkItem Tests

    [Fact]
    public async Task UpdateWorkItem_WithTitle_ShouldReturnSuccess()
    {
        var workItem = new WorkItemDto
        {
            Id = 200,
            Title = "Updated Title",
            State = "Active"
        };

        _mockService
            .Setup(s => s.UpdateWorkItemAsync(
                200, "Updated Title", null, null, null, null, null, null, null, null, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        var result = await _tools.UpdateWorkItem(200, title: "Updated Title");

        Assert.Contains("\"success\": true", result);
        Assert.Contains("Work item 200 updated successfully", result);
    }

    [Fact]
    public async Task UpdateWorkItem_WithZeroId_ShouldReturnError()
    {
        var result = await _tools.UpdateWorkItem(0, title: "Title");

        Assert.Contains("error", result);
        Assert.Contains("Work item ID must be a positive integer", result);
    }

    [Fact]
    public async Task UpdateWorkItem_WithNegativeId_ShouldReturnError()
    {
        var result = await _tools.UpdateWorkItem(-1, title: "Title");

        Assert.Contains("error", result);
        Assert.Contains("Work item ID must be a positive integer", result);
    }

    [Fact]
    public async Task UpdateWorkItem_WithInvalidPriority_ShouldReturnError()
    {
        var result = await _tools.UpdateWorkItem(200, priority: 0);

        Assert.Contains("error", result);
        Assert.Contains("Priority must be between 1 and 4", result);
    }

    [Fact]
    public async Task UpdateWorkItem_WithInvalidAdditionalFieldsJson_ShouldReturnError()
    {
        var result = await _tools.UpdateWorkItem(200, additionalFields: "{invalid}");

        Assert.Contains("error", result);
        Assert.Contains("Invalid JSON format for additionalFields", result);
    }

    [Fact]
    public async Task UpdateWorkItem_WithProject_ShouldPassProjectToService()
    {
        var workItem = new WorkItemDto { Id = 200, Title = "Title" };

        _mockService
            .Setup(s => s.UpdateWorkItemAsync(
                200, "Title", null, null, null, null, null, null, null, null, "SpecificProject",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        await _tools.UpdateWorkItem(200, project: "SpecificProject", title: "Title");

        _mockService.Verify(s => s.UpdateWorkItemAsync(
            200, "Title", null, null, null, null, null, null, null, null, "SpecificProject",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateWorkItem_WithValidAdditionalFieldsJson_ShouldParseAndPassToService()
    {
        var workItem = new WorkItemDto { Id = 200, Title = "Title" };

        _mockService
            .Setup(s => s.UpdateWorkItemAsync(
                200, null, null, null, null, null, null, null, null,
                It.Is<Dictionary<string, string>>(d => d["Custom.Field"] == "value"),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workItem);

        var result = await _tools.UpdateWorkItem(200, additionalFields: "{\"Custom.Field\": \"value\"}");

        Assert.Contains("\"success\": true", result);
    }

    #endregion

    #region GetWorkItemHistory & GetWorkItemsHistory Tests

    [Fact]
    public async Task GetWorkItemHistory_WithValidId_ShouldReturnSerializedHistory()
    {
        var historyResult = new WorkItemHistoryResultDto
        {
            WorkItemId = 123,
            TotalTransitions = 1,
            Transitions = new List<WorkItemStateTransitionDto>
            {
                new()
                {
                    Revision = 2,
                    State = "Active",
                    PreviousState = "New",
                    MovedBy = "Alice",
                    Timestamp = DateTime.UtcNow,
                    DurationInHours = 12.5
                }
            }
        };

        _mockService
            .Setup(s => s.GetWorkItemHistoryAsync(123, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(historyResult);

        var result = await _tools.GetWorkItemHistory(123);

        Assert.Contains("\"workItemId\": 123", result);
        Assert.Contains("\"totalTransitions\": 1", result);
        Assert.Contains("\"state\": \"Active\"", result);
        Assert.Contains("\"movedBy\": \"Alice\"", result);
    }

    [Fact]
    public async Task GetWorkItemHistory_WithInvalidId_ShouldReturnError()
    {
        var result = await _tools.GetWorkItemHistory(0);

        Assert.Contains("error", result);
        Assert.Contains("Work item ID must be a positive integer", result);
    }

    [Fact]
    public async Task GetWorkItemHistory_WhenNotFound_ShouldReturnError()
    {
        _mockService
            .Setup(s => s.GetWorkItemHistoryAsync(999, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkItemHistoryResultDto?)null);

        var result = await _tools.GetWorkItemHistory(999);

        Assert.Contains("error", result);
        Assert.Contains("Work item 999 not found", result);
    }

    [Fact]
    public async Task GetWorkItemsHistory_WithBatchIds_ShouldReturnBatchResults()
    {
        var histories = new List<WorkItemHistoryResultDto>
        {
            new() { WorkItemId = 101, TotalTransitions = 1, Transitions = [] },
            new() { WorkItemId = 102, TotalTransitions = 2, Transitions = [] }
        };

        _mockService
            .Setup(s => s.GetWorkItemsHistoryAsync(It.Is<IEnumerable<int>>(ids => ids.Contains(101) && ids.Contains(102)), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(histories);

        var result = await _tools.GetWorkItemsHistory("101,102");

        Assert.Contains("\"count\": 2", result);
        Assert.Contains("\"workItemId\": 101", result);
        Assert.Contains("\"workItemId\": 102", result);
    }

    [Fact]
    public async Task GetWorkItemRelations_WithValidId_ShouldReturnRelations()
    {
        var relationsResult = new WorkItemRelationsResultDto
        {
            WorkItemId = 123,
            Count = 1,
            Relations = new List<WorkItemRelationDto>
            {
                new() { RelationType = "Related", RawRel = "System.LinkTypes.Related", TargetId = 456 }
            }
        };

        _mockService
            .Setup(s => s.GetWorkItemRelationsAsync(123, null, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(relationsResult);

        var result = await _tools.GetWorkItemRelations(123);

        Assert.Contains("\"workItemId\": 123", result);
        Assert.Contains("\"relationType\": \"Related\"", result);
        Assert.Contains("\"targetId\": 456", result);
    }

    [Fact]
    public async Task GetWorkItemRelations_WithInvalidId_ShouldReturnError()
    {
        var result = await _tools.GetWorkItemRelations(0);

        Assert.Contains("error", result);
        Assert.Contains("must be a positive integer", result);
    }

    [Fact]
    public async Task GetWorkItemTree_WithValidId_ShouldReturnTree()
    {
        var treeResult = new WorkItemTreeNodeDto
        {
            WorkItem = new WorkItemDto { Id = 1, Title = "Root Epic" },
            Children = new List<WorkItemTreeNodeDto>
            {
                new() { WorkItem = new WorkItemDto { Id = 2, Title = "Child Feature" }, Children = [] }
            }
        };

        _mockService
            .Setup(s => s.GetWorkItemTreeAsync(1, 2, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treeResult);

        var result = await _tools.GetWorkItemTree(1);

        Assert.Contains("\"title\": \"Root Epic\"", result);
        Assert.Contains("\"title\": \"Child Feature\"", result);
    }

    [Fact]
    public async Task GetWorkItemTree_WithInvalidId_ShouldReturnError()
    {
        var result = await _tools.GetWorkItemTree(-5);

        Assert.Contains("error", result);
        Assert.Contains("must be a positive integer", result);
    }

    #endregion
}
