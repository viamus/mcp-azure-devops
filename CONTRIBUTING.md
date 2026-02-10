# Contributing Guide

Thank you for your interest in contributing to MCP Azure DevOps Server!

This project provides a Model Context Protocol (MCP) server that exposes tools for interacting with Azure DevOps Work Items, Git Repositories, Pull Requests, and Pipelines. Contributions of all kinds are welcome, including bug fixes, documentation improvements, new tools, refactors, and tests.

---

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Project Goals](#project-goals)
- [Ways to Contribute](#ways-to-contribute)
- [Development Setup](#development-setup)
- [Branching & Workflow](#branching--workflow)
- [Commit & PR Guidelines](#commit--pr-guidelines)
- [Coding Standards](#coding-standards)
- [Testing](#testing)
- [Adding a New MCP Tool](#adding-a-new-mcp-tool)
- [Architecture Overview](#architecture-overview)
- [Security](#security)
- [Getting Help](#getting-help)

---

## Code of Conduct

Be respectful, constructive, and collaborative.

Harassment, discrimination, or abusive behavior will not be tolerated. All contributors are expected to interact professionally and respectfully. See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) for details.

---

## Project Goals

- Provide a reliable MCP server for Azure DevOps integration
- Offer useful, composable tools for Work Items, Git Repositories, Pull Requests, and Pipelines
- Keep the server safe-by-default (minimal permissions, no secret leakage)
- Maintain a clean and extensible architecture for future domains (Boards, Wikis, etc.)

---

## Ways to Contribute

You can contribute by:

- **Reporting bugs** with clear reproduction steps
- **Improving documentation** (README, examples, guides)
- **Adding new MCP tools** or extending existing ones
- **Improving logging**, error handling, and observability
- **Writing or improving tests**
- **Refactoring code** for clarity and maintainability

---

## Development Setup

### Prerequisites

| Requirement | Version | Purpose |
|-------------|---------|---------|
| .NET SDK | 10.0+ | Build and run |
| Docker | Latest | Container deployment (optional) |
| Azure DevOps PAT | - | API authentication |

**PAT Required Scopes:**
- Work Items: Read & Write
- Code: Read
- Build: Read

### Clone & Configure

```bash
# 1. Clone the repository
git clone https://github.com/viamus/mcp-azure-devops.git
cd mcp-azure-devops

# 2. Create environment file
cp .env.example .env

# 3. Edit .env with your credentials
```

> **Warning**: Never commit `.env` files or hardcode credentials!

### Run Locally

```bash
# Using .NET CLI
dotnet run --project src/Viamus.Azure.Devops.Mcp.Server

# Using Docker
docker compose up -d
```

### Verify Setup

```bash
# .NET CLI (port 5000)
curl http://localhost:5000/health

# Docker (port 8080)
curl http://localhost:8080/health
```

---

## Branching & Workflow

The `main` branch must remain stable. Create feature branches using these patterns:

| Prefix | Purpose | Example |
|--------|---------|---------|
| `feat/` | New features | `feat/add-wiki-tools` |
| `fix/` | Bug fixes | `fix/wiql-query-error` |
| `docs/` | Documentation | `docs/improve-readme` |
| `chore/` | Maintenance | `chore/update-deps` |
| `test/` | Test additions | `test/pipeline-tools` |

### Workflow

1. Create a branch from `main`
2. Make your changes
3. Add or update tests
4. Run tests locally: `dotnet test`
5. Open a Pull Request targeting `main`

---

## Commit & PR Guidelines

### Commits

Use [Conventional Commits](https://www.conventionalcommits.org/) style:

```
feat: add get_work_items_by_area_path tool
fix: handle WIQL errors gracefully
docs: clarify PAT permissions
test: add unit tests for pipeline tools
chore: bump dependencies
```

### Pull Requests

A good PR includes:

- **What** changed and **why**
- Link to related issue (if applicable)
- Notes about breaking changes (avoid if possible)
- Confirmation that no secrets were introduced
- Logs or screenshots when helpful

---

## Coding Standards

### General Principles

- Prefer clarity over cleverness
- Keep MCP tools focused (single responsibility)
- Avoid leaking secrets via logs or exceptions
- Validate inputs and return consistent outputs
- Errors should be actionable and safe

### .NET Guidelines

- Use `async/await` consistently for I/O operations
- Favor dependency injection
- Keep handlers/controllers thin
- Put business logic in services
- Keep models and DTOs explicit and simple
- Use `sealed record` for DTOs (immutability)

---

## Testing

When behavior changes, tests should be added or updated.

### Running Tests

```bash
# All tests
dotnet test

# Specific test class
dotnet test --filter "FullyQualifiedName~WorkItemToolsTests"
dotnet test --filter "FullyQualifiedName~GitToolsTests"
dotnet test --filter "FullyQualifiedName~PullRequestToolsTests"
dotnet test --filter "FullyQualifiedName~PipelineToolsTests"

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test Structure

```
tests/Viamus.Azure.Devops.Mcp.Server.Tests/
├── Models/     # DTO serialization and equality tests
└── Tools/      # Tool behavior tests with mocked services
```

### Testing Layers

- **Unit tests**: Services and mapping logic
- **Contract tests**: MCP tool outputs
- **Integration tests**: HTTP endpoints (optional but encouraged)

---

## Adding a New MCP Tool

### Checklist

Before creating a new tool, ensure it has:

- [ ] Clear and descriptive name (snake_case)
- [ ] Single responsibility
- [ ] Stable inputs and outputs
- [ ] Parameter validation
- [ ] Safe and consistent error handling
- [ ] Unit tests
- [ ] Documentation in README.md

### Steps

1. **Add tool implementation** in `src/.../Tools/`

```csharp
[McpServerToolType]
public sealed class MyTools
{
    private readonly IAzureDevOpsService _service;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MyTools(IAzureDevOpsService service) => _service = service;

    [McpServerTool(Name = "my_tool")]
    [Description("Description of what this tool does")]
    public async Task<string> MyTool(
        [Description("Parameter description")] string param,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.MyMethodAsync(param, cancellationToken);
        return JsonSerializer.Serialize(result, JsonOptions);
    }
}
```

2. **Add service method** in `src/.../Services/`
   - Add signature to `IAzureDevOpsService.cs`
   - Implement in `AzureDevOpsService.cs`

3. **Add DTOs if needed** in `src/.../Models/`
   - Use `sealed record` for immutability
   - Include XML documentation

4. **Add tests** in `tests/.../Tools/`

5. **Update README.md** with the new tool

> Tools are auto-registered via `.WithToolsFromAssembly()`

---

## Architecture Overview

### Project Structure

```
src/Viamus.Azure.Devops.Mcp.Server/
├── Configuration/
│   └── AzureDevOpsOptions.cs        # Configuration binding
├── Models/
│   ├── WorkItemDto.cs               # Work item details
│   ├── WorkItemSummaryDto.cs        # Work item list view
│   ├── WorkItemCommentDto.cs        # Work item comment
│   ├── RepositoryDto.cs             # Git repository
│   ├── BranchDto.cs                 # Git branch
│   ├── GitItemDto.cs                # Git file/folder
│   ├── GitFileContentDto.cs         # File content
│   ├── PullRequestDto.cs            # Pull request details
│   ├── PullRequestReviewerDto.cs    # PR reviewer
│   ├── PullRequestThreadDto.cs      # PR comment thread
│   ├── PullRequestCommentDto.cs     # PR comment
│   ├── PipelineDto.cs               # Pipeline definition
│   ├── BuildDto.cs                  # Build details
│   ├── BuildLogDto.cs               # Build log metadata
│   ├── BuildTimelineRecordDto.cs    # Build timeline
│   ├── PipelineRunDto.cs            # Pipeline run
│   └── PaginatedResult.cs           # Generic pagination
├── Services/
│   ├── IAzureDevOpsService.cs       # Service interface
│   └── AzureDevOpsService.cs        # Implementation
├── Tools/
│   ├── WorkItemTools.cs             # Work Item tools (11)
│   ├── GitTools.cs                  # Git Repository tools (6)
│   ├── PullRequestTools.cs          # Pull Request tools (5)
│   └── PipelineTools.cs             # Pipeline/Build tools (9)
└── Program.cs                       # Entry point & DI
```

### Key Patterns

| Pattern | Description |
|---------|-------------|
| Dependency Injection | Services registered as singletons |
| Interface-based design | Enables testing with mocks |
| DTOs as sealed records | Immutability and value equality |
| JSON serialization | CamelCase, indented output |
| Error handling | JSON error responses, no exceptions to client |

### Azure DevOps SDK Clients

| Client | Used For |
|--------|----------|
| `WorkItemTrackingHttpClient` | Work Items, WIQL queries, comments |
| `GitHttpClient` | Repositories, branches, items, file content, PRs |
| `BuildHttpClient` | Pipelines, builds, logs, timelines |

---

## Security

- **Never commit secrets** (PATs, tokens, credentials)
- **Avoid logging sensitive data**
- **Validate all external inputs**
- **Sanitize user inputs** in WIQL queries (escape single quotes)

If you discover a security vulnerability:
- **Do NOT open a public issue**
- Contact the maintainers privately (see [SECURITY.md](SECURITY.md))

---

## Getting Help

If you need help:

1. Check the [README](README.md) first
2. Search existing [issues](https://github.com/viamus/mcp-azure-devops/issues)
3. Open a new issue with:
   - Expected behavior
   - Actual behavior
   - Reproduction steps
   - Logs (with secrets removed)
   - Environment details (OS, .NET version, Docker version)

---

**Thank you for contributing to MCP Azure DevOps Server!**

Your help makes this project better for everyone.
