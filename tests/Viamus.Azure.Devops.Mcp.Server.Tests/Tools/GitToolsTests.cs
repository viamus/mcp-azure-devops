using System.Text.Json;
using Moq;
using Viamus.Azure.Devops.Mcp.Server.Models;
using Viamus.Azure.Devops.Mcp.Server.Services;
using Viamus.Azure.Devops.Mcp.Server.Tools;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Tools;

public class GitToolsTests
{
    private readonly Mock<IAzureDevOpsService> _mockService;
    private readonly GitTools _tools;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GitToolsTests()
    {
        _mockService = new Mock<IAzureDevOpsService>();
        _tools = new GitTools(_mockService.Object);
    }

    #region GetRepositories Tests

    [Fact]
    public async Task GetRepositories_ShouldReturnRepositoryList()
    {
        var repositories = new List<RepositoryDto>
        {
            new() { Id = "repo-1", Name = "Repository1", DefaultBranch = "refs/heads/main" },
            new() { Id = "repo-2", Name = "Repository2", DefaultBranch = "refs/heads/develop" }
        };

        _mockService
            .Setup(s => s.GetRepositoriesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repositories);

        var result = await _tools.GetRepositories();

        Assert.Contains("\"count\": 2", result);
        Assert.Contains("Repository1", result);
        Assert.Contains("Repository2", result);
    }

    [Fact]
    public async Task GetRepositories_WithProject_ShouldPassProjectToService()
    {
        var repositories = new List<RepositoryDto>
        {
            new() { Id = "repo-1", Name = "Repository1" }
        };

        _mockService
            .Setup(s => s.GetRepositoriesAsync("MyProject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(repositories);

        await _tools.GetRepositories("MyProject");

        _mockService.Verify(s => s.GetRepositoriesAsync("MyProject", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRepositories_WhenEmpty_ShouldReturnEmptyList()
    {
        _mockService
            .Setup(s => s.GetRepositoriesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RepositoryDto>());

        var result = await _tools.GetRepositories();

        Assert.Contains("\"count\": 0", result);
    }

    #endregion

    #region GetRepository Tests

    [Fact]
    public async Task GetRepository_WhenRepositoryExists_ShouldReturnRepository()
    {
        var repository = new RepositoryDto
        {
            Id = "repo-123",
            Name = "my-repo",
            DefaultBranch = "refs/heads/main",
            Size = 1024000
        };

        _mockService
            .Setup(s => s.GetRepositoryAsync("my-repo", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repository);

        var result = await _tools.GetRepository("my-repo");

        Assert.Contains("\"id\": \"repo-123\"", result);
        Assert.Contains("\"name\": \"my-repo\"", result);
        Assert.Contains("\"defaultBranch\": \"refs/heads/main\"", result);
    }

    [Fact]
    public async Task GetRepository_WhenRepositoryNotFound_ShouldReturnError()
    {
        _mockService
            .Setup(s => s.GetRepositoryAsync("nonexistent", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepositoryDto?)null);

        var result = await _tools.GetRepository("nonexistent");

        Assert.Contains("error", result);
        Assert.Contains("not found", result);
    }

    [Fact]
    public async Task GetRepository_WithEmptyName_ShouldReturnError()
    {
        var result = await _tools.GetRepository("");

        Assert.Contains("error", result);
        Assert.Contains("Repository name or ID is required", result);
    }

    [Fact]
    public async Task GetRepository_WithProject_ShouldPassProjectToService()
    {
        var repository = new RepositoryDto { Id = "repo-1", Name = "repo" };

        _mockService
            .Setup(s => s.GetRepositoryAsync("repo", "MyProject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(repository);

        await _tools.GetRepository("repo", "MyProject");

        _mockService.Verify(s => s.GetRepositoryAsync("repo", "MyProject", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetBranches Tests

    [Fact]
    public async Task GetBranches_ShouldReturnBranchList()
    {
        var branches = new List<BranchDto>
        {
            new() { Name = "refs/heads/main", ObjectId = "abc123", IsBaseVersion = true },
            new() { Name = "refs/heads/develop", ObjectId = "def456", IsBaseVersion = false }
        };

        _mockService
            .Setup(s => s.GetBranchesAsync("my-repo", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branches);

        var result = await _tools.GetBranches("my-repo");

        Assert.Contains("\"count\": 2", result);
        Assert.Contains("refs/heads/main", result);
        Assert.Contains("refs/heads/develop", result);
    }

    [Fact]
    public async Task GetBranches_WithEmptyRepoName_ShouldReturnError()
    {
        var result = await _tools.GetBranches("");

        Assert.Contains("error", result);
        Assert.Contains("Repository name or ID is required", result);
    }

    [Fact]
    public async Task GetBranches_WithProject_ShouldPassProjectToService()
    {
        var branches = new List<BranchDto> { new() { Name = "main" } };

        _mockService
            .Setup(s => s.GetBranchesAsync("repo", "MyProject", It.IsAny<CancellationToken>()))
            .ReturnsAsync(branches);

        await _tools.GetBranches("repo", "MyProject");

        _mockService.Verify(s => s.GetBranchesAsync("repo", "MyProject", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetRepositoryItems Tests

    [Fact]
    public async Task GetRepositoryItems_ShouldReturnItemList()
    {
        var items = new List<GitItemDto>
        {
            new() { Path = "/src", IsFolder = true, GitObjectType = "Tree" },
            new() { Path = "/README.md", IsFolder = false, GitObjectType = "Blob", Size = 1024 }
        };

        _mockService
            .Setup(s => s.GetItemsAsync("my-repo", "/", null, null, "OneLevel", It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var result = await _tools.GetRepositoryItems("my-repo");

        Assert.Contains("\"count\": 2", result);
        Assert.Contains("/src", result);
        Assert.Contains("/README.md", result);
    }

    [Fact]
    public async Task GetRepositoryItems_WithPath_ShouldPassPathToService()
    {
        var items = new List<GitItemDto> { new() { Path = "/src/file.cs" } };

        _mockService
            .Setup(s => s.GetItemsAsync("repo", "/src", null, null, "OneLevel", It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        await _tools.GetRepositoryItems("repo", "/src");

        _mockService.Verify(s => s.GetItemsAsync("repo", "/src", null, null, "OneLevel", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRepositoryItems_WithBranch_ShouldPassBranchToService()
    {
        var items = new List<GitItemDto> { new() { Path = "/file.cs" } };

        _mockService
            .Setup(s => s.GetItemsAsync("repo", "/", "develop", null, "OneLevel", It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        await _tools.GetRepositoryItems("repo", branchName: "develop");

        _mockService.Verify(s => s.GetItemsAsync("repo", "/", "develop", null, "OneLevel", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRepositoryItems_WithRecursionLevel_ShouldPassRecursionToService()
    {
        var items = new List<GitItemDto> { new() { Path = "/src/deep/file.cs" } };

        _mockService
            .Setup(s => s.GetItemsAsync("repo", "/", null, null, "Full", It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        await _tools.GetRepositoryItems("repo", recursionLevel: "Full");

        _mockService.Verify(s => s.GetItemsAsync("repo", "/", null, null, "Full", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRepositoryItems_WithEmptyRepoName_ShouldReturnError()
    {
        var result = await _tools.GetRepositoryItems("");

        Assert.Contains("error", result);
        Assert.Contains("Repository name or ID is required", result);
    }

    #endregion

    #region GetFileContent Tests

    [Fact]
    public async Task GetFileContent_ShouldReturnFileContent()
    {
        var fileContent = new GitFileContentDto
        {
            Path = "/src/Program.cs",
            CommitId = "abc123",
            Content = "using System;\n\nclass Program { }",
            IsBinary = false,
            Encoding = "UTF-8",
            Size = 35
        };

        _mockService
            .Setup(s => s.GetFileContentAsync("repo", "/src/Program.cs", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileContent);

        var result = await _tools.GetFileContent("repo", "/src/Program.cs");

        Assert.Contains("Program.cs", result);
        Assert.Contains("using System", result);
        Assert.Contains("\"isBinary\": false", result);
    }

    [Fact]
    public async Task GetFileContent_WhenFileNotFound_ShouldReturnError()
    {
        _mockService
            .Setup(s => s.GetFileContentAsync("repo", "/nonexistent.cs", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GitFileContentDto?)null);

        var result = await _tools.GetFileContent("repo", "/nonexistent.cs");

        Assert.Contains("error", result);
        Assert.Contains("not found", result);
    }

    [Fact]
    public async Task GetFileContent_WithEmptyRepoName_ShouldReturnError()
    {
        var result = await _tools.GetFileContent("", "/file.cs");

        Assert.Contains("error", result);
        Assert.Contains("Repository name or ID is required", result);
    }

    [Fact]
    public async Task GetFileContent_WithEmptyFilePath_ShouldReturnError()
    {
        var result = await _tools.GetFileContent("repo", "");

        Assert.Contains("error", result);
        Assert.Contains("File path is required", result);
    }

    [Fact]
    public async Task GetFileContent_WithBranch_ShouldPassBranchToService()
    {
        var fileContent = new GitFileContentDto { Path = "/file.cs", Content = "content" };

        _mockService
            .Setup(s => s.GetFileContentAsync("repo", "/file.cs", "feature", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileContent);

        await _tools.GetFileContent("repo", "/file.cs", "feature");

        _mockService.Verify(s => s.GetFileContentAsync("repo", "/file.cs", "feature", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetFileContent_BinaryFile_ShouldShowBinaryIndicator()
    {
        var fileContent = new GitFileContentDto
        {
            Path = "/images/logo.png",
            Content = "[Binary file content not shown]",
            IsBinary = true
        };

        _mockService
            .Setup(s => s.GetFileContentAsync("repo", "/images/logo.png", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileContent);

        var result = await _tools.GetFileContent("repo", "/images/logo.png");

        Assert.Contains("\"isBinary\": true", result);
        Assert.Contains("[Binary file content not shown]", result);
    }

    #endregion

    #region SearchRepositoryFiles Tests

    [Fact]
    public async Task SearchRepositoryFiles_ShouldReturnMatchingFiles()
    {
        var items = new List<GitItemDto>
        {
            new() { Path = "/src/Program.cs", IsFolder = false },
            new() { Path = "/src/Services/Service.cs", IsFolder = false },
            new() { Path = "/tests/Test.cs", IsFolder = false },
            new() { Path = "/src", IsFolder = true }
        };

        _mockService
            .Setup(s => s.GetItemsAsync("repo", "/", null, null, "Full", It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var result = await _tools.SearchRepositoryFiles("repo", ".cs");

        Assert.Contains("\"count\": 3", result);
        Assert.Contains("Program.cs", result);
        Assert.Contains("Service.cs", result);
        Assert.Contains("Test.cs", result);
    }

    [Fact]
    public async Task SearchRepositoryFiles_ShouldExcludeFolders()
    {
        var items = new List<GitItemDto>
        {
            new() { Path = "/src", IsFolder = true },
            new() { Path = "/src/file.cs", IsFolder = false }
        };

        _mockService
            .Setup(s => s.GetItemsAsync("repo", "/", null, null, "Full", It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var result = await _tools.SearchRepositoryFiles("repo", "src");

        // Should only include the file, not the folder
        Assert.Contains("\"count\": 1", result);
    }

    [Fact]
    public async Task SearchRepositoryFiles_WithEmptyRepoName_ShouldReturnError()
    {
        var result = await _tools.SearchRepositoryFiles("", ".cs");

        Assert.Contains("error", result);
        Assert.Contains("Repository name or ID is required", result);
    }

    [Fact]
    public async Task SearchRepositoryFiles_WithEmptyPattern_ShouldReturnError()
    {
        var result = await _tools.SearchRepositoryFiles("repo", "");

        Assert.Contains("error", result);
        Assert.Contains("Search pattern is required", result);
    }

    [Fact]
    public async Task SearchRepositoryFiles_CaseInsensitive_ShouldMatchFiles()
    {
        var items = new List<GitItemDto>
        {
            new() { Path = "/README.md", IsFolder = false },
            new() { Path = "/docs/readme.txt", IsFolder = false }
        };

        _mockService
            .Setup(s => s.GetItemsAsync("repo", "/", null, null, "Full", It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var result = await _tools.SearchRepositoryFiles("repo", "readme");

        Assert.Contains("\"count\": 2", result);
    }

    [Fact]
    public async Task SearchRepositoryFiles_WithBranch_ShouldPassBranchToService()
    {
        var items = new List<GitItemDto>();

        _mockService
            .Setup(s => s.GetItemsAsync("repo", "/src", "develop", null, "Full", It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        await _tools.SearchRepositoryFiles("repo", ".cs", "/src", "develop");

        _mockService.Verify(s => s.GetItemsAsync("repo", "/src", "develop", null, "Full", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
