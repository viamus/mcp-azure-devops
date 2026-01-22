using Viamus.Azure.Devops.Mcp.Server.Models;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Models;

public class PaginatedResultTests
{
    [Theory]
    [InlineData(100, 10, 10)]  // 100 items, 10 per page = 10 pages
    [InlineData(95, 10, 10)]   // 95 items, 10 per page = 10 pages (ceiling)
    [InlineData(10, 10, 1)]    // 10 items, 10 per page = 1 page
    [InlineData(1, 10, 1)]     // 1 item, 10 per page = 1 page
    [InlineData(0, 10, 0)]     // 0 items = 0 pages
    [InlineData(25, 20, 2)]    // 25 items, 20 per page = 2 pages
    [InlineData(50, 50, 1)]    // 50 items, 50 per page = 1 page
    public void TotalPages_ShouldCalculateCorrectly(int totalCount, int pageSize, int expectedTotalPages)
    {
        var result = new PaginatedResult<string>
        {
            Items = [],
            TotalCount = totalCount,
            Page = 1,
            PageSize = pageSize
        };

        Assert.Equal(expectedTotalPages, result.TotalPages);
    }

    [Fact]
    public void TotalPages_WhenPageSizeIsZero_ShouldReturnZero()
    {
        var result = new PaginatedResult<string>
        {
            Items = [],
            TotalCount = 100,
            Page = 1,
            PageSize = 0
        };

        Assert.Equal(0, result.TotalPages);
    }

    [Theory]
    [InlineData(1, 10, 100, true)]   // Page 1 of 10, has next
    [InlineData(5, 10, 100, true)]   // Page 5 of 10, has next
    [InlineData(9, 10, 100, true)]   // Page 9 of 10, has next
    [InlineData(10, 10, 100, false)] // Page 10 of 10, no next
    [InlineData(1, 10, 10, false)]   // Page 1 of 1 (10 items, 10 per page), no next
    [InlineData(1, 20, 0, false)]    // Empty result, no next
    public void HasNextPage_ShouldReturnCorrectValue(int page, int pageSize, int totalCount, bool expectedHasNext)
    {
        var result = new PaginatedResult<string>
        {
            Items = [],
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        Assert.Equal(expectedHasNext, result.HasNextPage);
    }

    [Theory]
    [InlineData(1, false)]   // Page 1, no previous
    [InlineData(2, true)]    // Page 2, has previous
    [InlineData(5, true)]    // Page 5, has previous
    [InlineData(10, true)]   // Page 10, has previous
    public void HasPreviousPage_ShouldReturnCorrectValue(int page, bool expectedHasPrevious)
    {
        var result = new PaginatedResult<string>
        {
            Items = [],
            TotalCount = 100,
            Page = page,
            PageSize = 10
        };

        Assert.Equal(expectedHasPrevious, result.HasPreviousPage);
    }

    [Fact]
    public void Items_ShouldReturnProvidedItems()
    {
        var items = new List<string> { "item1", "item2", "item3" };

        var result = new PaginatedResult<string>
        {
            Items = items,
            TotalCount = 100,
            Page = 1,
            PageSize = 10
        };

        Assert.Equal(items, result.Items);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public void AllProperties_ShouldBeSetCorrectly()
    {
        var items = new List<int> { 1, 2, 3 };

        var result = new PaginatedResult<int>
        {
            Items = items,
            TotalCount = 25,
            Page = 2,
            PageSize = 10
        };

        Assert.Equal(items, result.Items);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
    }

    [Fact]
    public void EmptyResult_ShouldHaveCorrectProperties()
    {
        var result = new PaginatedResult<string>
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 20
        };

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        Assert.False(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public void SinglePage_ShouldHaveNoNavigationOptions()
    {
        var items = new List<string> { "a", "b", "c" };

        var result = new PaginatedResult<string>
        {
            Items = items,
            TotalCount = 3,
            Page = 1,
            PageSize = 10
        };

        Assert.Equal(1, result.TotalPages);
        Assert.False(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public void LastPage_ShouldHaveOnlyPreviousPage()
    {
        var result = new PaginatedResult<string>
        {
            Items = ["item"],
            TotalCount = 25,
            Page = 3,
            PageSize = 10
        };

        Assert.Equal(3, result.TotalPages);
        Assert.False(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
    }

    [Fact]
    public void MiddlePage_ShouldHaveBothNavigationOptions()
    {
        var result = new PaginatedResult<string>
        {
            Items = [],
            TotalCount = 100,
            Page = 5,
            PageSize = 10
        };

        Assert.Equal(10, result.TotalPages);
        Assert.True(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
    }
}
