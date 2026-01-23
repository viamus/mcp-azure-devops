CONTRIBUTING GUIDE
=================

Thank you for your interest in contributing to MCP Azure DevOps Server!

This project provides a Model Context Protocol (MCP) server that exposes tools
for interacting with Azure DevOps Work Items and Git Repositories. Contributions
of all kinds are welcome, including bug fixes, documentation improvements, new
tools, refactors, and tests.

------------------------------------------------------------
TABLE OF CONTENTS
------------------------------------------------------------

1. Code of Conduct
2. Project Goals
3. Ways to Contribute
4. Development Setup
   - Prerequisites
   - Clone & Configure
   - Run Locally
   - Run with Docker
   - Health Check
5. Branching & Workflow
6. Commit & PR Guidelines
7. Coding Standards
8. Testing
9. Adding a New MCP Tool
10. Architecture Overview
11. Security
12. Release Notes
13. Getting Help

------------------------------------------------------------
1. CODE OF CONDUCT
------------------------------------------------------------

Be respectful, constructive, and collaborative.

Harassment, discrimination, or abusive behavior will not be tolerated.
All contributors are expected to interact professionally and respectfully.

------------------------------------------------------------
2. PROJECT GOALS
------------------------------------------------------------

- Provide a reliable MCP server for Azure DevOps integration
- Offer useful, composable tools for Work Items and Git Repositories
- Keep the server safe-by-default (minimal permissions, no secret leakage)
- Maintain a clean and extensible architecture for future domains
  (Pull Requests, Pipelines, Boards, etc.)

------------------------------------------------------------
3. WAYS TO CONTRIBUTE
------------------------------------------------------------

You can contribute by:

- Reporting bugs with clear reproduction steps
- Improving documentation (README, examples, guides)
- Adding new MCP tools or extending existing ones
- Improving logging, error handling, and observability
- Writing or improving tests
- Refactoring code for clarity and maintainability

------------------------------------------------------------
4. DEVELOPMENT SETUP
------------------------------------------------------------

PREREQUISITES
-------------

- .NET 10 SDK
- Docker (optional but recommended)
- Azure DevOps Personal Access Token (PAT) with:
  - Work Items: Read & Write
  - Code: Read (for Git repository access)

CLONE & CONFIGURE
-----------------

1. Clone the repository:

   git clone <REPO_URL>
   cd <REPO_FOLDER>

2. Create a local environment file:

   cp .env.example .env

3. Edit the .env file:

   AZURE_DEVOPS_ORG_URL=https://dev.azure.com/your-organization
   AZURE_DEVOPS_PAT=your-token-here
   AZURE_DEVOPS_DEFAULT_PROJECT=your-project

IMPORTANT:
- Do NOT commit .env files or secrets.
- Never hardcode credentials.

RUN LOCALLY
-----------

   cd src/Viamus.Azure.Devops.Mcp.Server
   dotnet run

RUN WITH DOCKER
---------------

   docker compose up -d

The server will be available at:
- http://localhost:8080

HEALTH CHECK
------------

   curl http://localhost:8080/health

------------------------------------------------------------
5. BRANCHING & WORKFLOW
------------------------------------------------------------

- The main branch must remain stable.
- Create feature branches using one of the following patterns:

  feat/<short-description>
  fix/<short-description>
  docs/<short-description>
  chore/<short-description>

Workflow:
1. Create a branch from main
2. Make your changes
3. Add or update tests when applicable
4. Open a Pull Request targeting main

------------------------------------------------------------
6. COMMIT & PR GUIDELINES
------------------------------------------------------------

COMMITS
-------

Keep commits small and meaningful.
Prefer Conventional Commits style:

- feat: add get_work_items_by_area_path tool
- feat: add Git repository browsing tools
- fix: handle WIQL errors gracefully
- docs: clarify PAT permissions
- test: add unit tests for tool services
- chore: bump dependencies

PULL REQUESTS
-------------

A good Pull Request includes:

- What changed and why
- Link to related issue (if applicable)
- Notes about breaking changes (avoid if possible)
- Confirmation that no secrets were introduced
- Logs or screenshots when helpful

------------------------------------------------------------
7. CODING STANDARDS
------------------------------------------------------------

- Prefer clarity over cleverness
- Keep MCP tools focused (single responsibility)
- Avoid leaking secrets via logs or exceptions
- Validate inputs and return consistent outputs
- Errors should be actionable and safe

.NET GUIDELINES
---------------

- Use async/await consistently for IO operations
- Favor dependency injection
- Keep handlers/controllers thin
- Put business logic in services
- Keep models and DTOs explicit and simple
- Use sealed records for DTOs (immutability)

------------------------------------------------------------
8. TESTING
------------------------------------------------------------

When behavior changes, tests should be added or updated.

RUNNING TESTS
-------------

   dotnet test

Or run specific tests:

   dotnet test --filter "FullyQualifiedName~WorkItemToolsTests"
   dotnet test --filter "FullyQualifiedName~GitToolsTests"

Recommended testing layers:

- Unit tests for services and mapping logic
- Contract tests for MCP tool outputs
- Integration tests for HTTP endpoints (optional but encouraged)

Tests are located under:
   tests/Viamus.Azure.Devops.Mcp.Server.Tests/
       Models/           # DTO serialization and equality tests
       Tools/            # Tool behavior tests with mocked services

------------------------------------------------------------
9. ADDING A NEW MCP TOOL
------------------------------------------------------------

When adding a new tool, ensure:

1. Clear and descriptive name
2. Single responsibility
3. Stable inputs and outputs
4. Parameter validation
5. Safe and consistent error handling

Suggested steps:

1. Add tool implementation under:
   src/Viamus.Azure.Devops.Mcp.Server/Tools/

   - Use [McpServerToolType] attribute on the class
   - Use [McpServerTool(Name = "tool_name")] on methods
   - Use [Description] attributes for documentation
   - Inject IAzureDevOpsService via constructor

2. Add or extend Azure DevOps logic under:
   src/Viamus.Azure.Devops.Mcp.Server/Services/

   - Add method signature to IAzureDevOpsService interface
   - Implement in AzureDevOpsService class
   - Use appropriate Azure DevOps SDK clients

3. Add models/DTOs if needed under:
   src/Viamus.Azure.Devops.Mcp.Server/Models/

   - Use sealed record for immutability
   - Include XML documentation
   - Keep fields nullable where appropriate

4. Tools are auto-registered via .WithToolsFromAssembly()

5. Update documentation (README.md) and add tests

EXAMPLE TOOL STRUCTURE
----------------------

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

------------------------------------------------------------
10. ARCHITECTURE OVERVIEW
------------------------------------------------------------

PROJECT STRUCTURE
-----------------

src/Viamus.Azure.Devops.Mcp.Server/
├── Configuration/
│   └── AzureDevOpsOptions.cs      # Configuration binding
├── Models/
│   ├── WorkItemDto.cs             # Work item full details
│   ├── WorkItemSummaryDto.cs      # Work item lightweight view
│   ├── WorkItemCommentDto.cs      # Work item comment
│   ├── PaginatedResult.cs         # Generic pagination wrapper
│   ├── RepositoryDto.cs           # Git repository details
│   ├── BranchDto.cs               # Git branch details
│   ├── GitItemDto.cs              # Git file/folder item
│   └── GitFileContentDto.cs       # Git file content
├── Services/
│   ├── IAzureDevOpsService.cs     # Service interface
│   └── AzureDevOpsService.cs      # Implementation
├── Tools/
│   ├── WorkItemTools.cs           # Work Item MCP tools
│   └── GitTools.cs                # Git Repository MCP tools
└── Program.cs                     # Entry point and DI setup

KEY PATTERNS
------------

- Dependency Injection: Services registered as singletons
- Interface-based design: Enables testing with mocks
- DTOs as sealed records: Immutability and value equality
- Consistent JSON serialization: CamelCase, indented output
- Error handling: JSON error responses, no exceptions to client

AZURE DEVOPS SDK CLIENTS
------------------------

- WorkItemTrackingHttpClient: Work Items, WIQL queries, comments
- GitHttpClient: Repositories, branches, items, file content

------------------------------------------------------------
11. SECURITY
------------------------------------------------------------

- Never commit secrets (PATs, tokens, credentials)
- Avoid logging sensitive data
- Validate all external inputs
- Sanitize user inputs in WIQL queries (escape single quotes)

If you discover a security vulnerability:
- Do NOT open a public issue
- Contact the maintainers privately (see SECURITY.md)

------------------------------------------------------------
12. RELEASE NOTES
------------------------------------------------------------

If your contribution changes behavior or adds a new tool, include a short
release note suggestion in your PR description, for example:

- Added: Git repository browsing tools (get_repositories, get_branches, etc.)
- Added: support for filtering work items by area path
- Fixed: WIQL query errors now return consistent tool responses
- Changed: tool output now includes changedDate metadata

------------------------------------------------------------
13. GETTING HELP
------------------------------------------------------------

If you need help:

- Check the README and documentation first
- Open a GitHub issue including:
  - Expected behavior
  - Actual behavior
  - Reproduction steps
  - Logs (with secrets removed)
  - Environment details (OS, .NET version, Docker version)

------------------------------------------------------------

Thank you for contributing to MCP Azure DevOps Server!
Your help makes this project better for everyone.
