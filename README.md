# MCP Azure DevOps Server

[![CI](https://github.com/viamus/mcp-azure-devops/actions/workflows/ci.yml/badge.svg)](https://github.com/viamus/mcp-azure-devops/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![MCP](https://img.shields.io/badge/MCP-Compatible-blue)](https://modelcontextprotocol.io/)

A [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) server for Azure DevOps integration, enabling AI assistants to interact with Azure DevOps Work Items, Git Repositories, Pull Requests, and Pipelines.

---

## Quick Start

Get up and running in 3 steps:

### 1. Clone and configure

```bash
git clone https://github.com/viamus/mcp-azure-devops.git
cd mcp-azure-devops
```

Edit `src/Viamus.Azure.Devops.Mcp.Server/appsettings.json` with your Azure DevOps credentials:

```json
{
  "AzureDevOps": {
    "OrganizationUrl": "https://dev.azure.com/your-organization",
    "PersonalAccessToken": "your-personal-access-token",
    "DefaultProject": "your-project-name"
  }
}
```

> **Need a PAT?** See [Creating a Personal Access Token](#creating-a-personal-access-token-pat) below.

### 2. Run the server

**Option A - Docker (recommended):**
```bash
docker compose up -d
# Server runs at http://localhost:8080 (use reverse proxy for HTTPS in production)
```

**Option B - .NET CLI:**
```bash
dotnet run --project src/Viamus.Azure.Devops.Mcp.Server
# Server runs at http://localhost:5000
```

### 3. Verify it's working

```bash
# Docker
curl http://localhost:8080/health

# .NET CLI
curl http://localhost:5000/health
```

You should see: `Healthy`

---

## Automated Installation (Windows)

For Windows users, a PowerShell script is available that automates the entire setup process:

```powershell
# Download and run the installer (as Administrator)
irm https://raw.githubusercontent.com/viamus/mcp-azure-devops/main/install-mcp-azure-devops.ps1 -OutFile install-mcp-azure-devops.ps1

.\install-mcp-azure-devops.ps1 `
    -OrganizationUrl "https://dev.azure.com/your-organization" `
    -PersonalAccessToken "your-pat-token" `
    -DefaultProject "your-project-name" `
    -ApiKey "your-secure-api-key"  # Optional: enables API key authentication
```

The script will automatically:
- Install .NET 10 SDK (if not present)
- Install Node.js (if not present)
- Install Claude Code CLI (if not present)
- Clone the repository
- Configure your Azure DevOps credentials
- Register the MCP server with Claude Code (HTTPS transport)

After installation, start the server:
```powershell
cd $env:USERPROFILE\mcp-azure-devops
dotnet run --project src/Viamus.Azure.Devops.Mcp.Server
```

---

## About

This project implements an MCP server that exposes tools for querying and managing Work Items and Git Repositories in Azure DevOps. It can be used with any compatible MCP client, such as Claude Desktop, Claude Code, or other assistants that support the protocol.

---

## Available Tools

### Work Item Tools

| Tool | Description |
|------|-------------|
| `get_work_item` | Gets details of a specific work item by ID |
| `get_work_items` | Gets multiple work items by IDs (batch retrieval) |
| `query_work_items` | Queries work items using WIQL (Work Item Query Language) |
| `get_work_items_by_state` | Filters work items by state (Active, New, Closed, etc.) |
| `get_work_items_assigned_to` | Gets work items assigned to a specific user |
| `get_child_work_items` | Gets child work items of a parent work item |
| `get_recent_work_items` | Gets recently changed work items |
| `search_work_items` | Searches work items by title text |
| `add_work_item_comment` | Adds a comment to a specific work item |
| `create_work_item` | Creates a new work item (Bug, Task, User Story, etc.) with support for all standard fields, parent linking, and custom fields |
| `update_work_item` | Updates an existing work item. Only specified fields are changed; omitted fields remain unchanged |

### Git Repository Tools

| Tool | Description |
|------|-------------|
| `get_repositories` | Lists all Git repositories in a project |
| `get_repository` | Gets details of a specific repository by name or ID |
| `get_branches` | Lists all branches in a repository |
| `get_repository_items` | Browses files and folders at a specific path in a repository |
| `get_file_content` | Gets the content of a specific file in a repository |
| `search_repository_files` | Searches for files by path pattern in a repository |

### Pull Request Tools

| Tool | Description |
|------|-------------|
| `get_pull_requests` | Lists pull requests with optional filters (status, creator, reviewer, branches) |
| `get_pull_request` | Gets details of a specific pull request by ID |
| `get_pull_request_threads` | Gets comment threads for a pull request |
| `search_pull_requests` | Searches pull requests by text in title or description |
| `query_pull_requests` | Advanced query with multiple combined filters |

### Pipeline/Build Tools

| Tool | Description |
|------|-------------|
| `get_pipelines` | Lists all pipelines (build definitions) in a project |
| `get_pipeline` | Gets details of a specific pipeline by ID |
| `get_pipeline_runs` | Gets recent runs (builds) for a specific pipeline |
| `get_build` | Gets details of a specific build by ID |
| `get_builds` | Lists builds with optional filters (status, result, branch, requester) |
| `get_build_logs` | Gets the list of log files for a build |
| `get_build_log_content` | Gets the content of a specific build log |
| `get_build_timeline` | Gets the timeline (stages, jobs, tasks) for a build |
| `query_builds` | Advanced query with multiple combined filters |

### Analytics Tools

Tools for analyzing delivery performance, identifying bottlenecks, and tracking work health.

| Tool | Description |
|------|-------------|
| `get_flow_metrics` | Calculates Lead Time, Cycle Time, and Throughput with statistical analysis |
| `compare_flow_metrics` | Compares metrics between two periods to identify trends |
| `get_wip_analysis` | Analyzes Work in Progress by state, area, and person |
| `get_bottlenecks` | Identifies workflow bottlenecks with severity scores and recommendations |
| `get_team_workload` | Analyzes workload distribution across team members |
| `get_aging_report` | Reports aging work items with urgency classification and recommendations |

#### Flow Metrics

- **Lead Time**: Time from work item creation to completion (measures total delivery time)
- **Cycle Time**: Time from work started to completion (measures active work time)
- **Throughput**: Number of items completed per period

#### WIP Analysis

- **By State**: Where work is accumulating
- **By Area**: Which teams/squads are overloaded
- **By Person**: Who has too many items assigned
- **Aging Items**: Work that has been stuck too long

#### Aging Report

Items are classified by urgency based on:
- Priority (P1 items age faster than P4)
- Time since last update
- Current state (blocked items are more urgent)
- Assignment status (unassigned items need attention)

---

## Prerequisites

| Requirement | Version | Notes |
|-------------|---------|-------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ | Required for local development |
| [Docker](https://www.docker.com/) | Latest | Recommended for running |
| Azure DevOps Account | - | With Personal Access Token |

### Creating a Personal Access Token (PAT)

1. Go to your Azure DevOps organization: `https://dev.azure.com/{your-org}`
2. Click on **User Settings** (gear icon) > **Personal Access Tokens**
3. Click **+ New Token**
4. Configure:
   - **Name**: `MCP Server` (or any name you prefer)
   - **Expiration**: Choose based on your needs
   - **Scopes**: Select the following permissions:

| Scope | Permission | Required for |
|-------|------------|--------------|
| Work Items | Read & Write | Work item operations |
| Code | Read | Git repositories and Pull Requests |
| Build | Read | Pipelines and Builds |

5. Click **Create** and **copy the token immediately** (you won't see it again!)

---

## Running Options

### Option 1: Docker Compose (Recommended)

Best for: Production use, quick setup without .NET installed

```bash
docker compose up -d
```

Server URL: `http://localhost:8080` (internal)

> **Important**: For production, use a reverse proxy (nginx, traefik, or a cloud load balancer) in front of the container to provide HTTPS/TLS termination.

**Useful commands:**
```bash
docker compose logs -f          # View logs
docker compose down             # Stop the server
docker compose up -d --build    # Rebuild and start
```

### Option 2: .NET CLI

Best for: Development, debugging

```bash
dotnet run --project src/Viamus.Azure.Devops.Mcp.Server
```

Server URL: `http://localhost:5000`

> **HTTPS Support**: The server can also run with HTTPS on port 5001, but this may cause certificate validation issues with some MCP clients (e.g., Claude Code). If you need HTTPS locally, configure the development certificate properly:
> ```bash
> # Trust the development certificate (first time only)
> dotnet dev-certs https --trust
>
> # Run with HTTPS
> dotnet run --project src/Viamus.Azure.Devops.Mcp.Server --urls "https://localhost:5001"
> ```
> For production environments, use a reverse proxy with properly configured TLS certificates instead.

### Option 3: Self-Contained Executable

Best for: Deployment without .NET runtime

```bash
# Windows
dotnet publish src/Viamus.Azure.Devops.Mcp.Server -c Release -r win-x64 -o ./publish/win-x64

# Linux
dotnet publish src/Viamus.Azure.Devops.Mcp.Server -c Release -r linux-x64 -o ./publish/linux-x64

# macOS (Intel)
dotnet publish src/Viamus.Azure.Devops.Mcp.Server -c Release -r osx-x64 -o ./publish/osx-x64

# macOS (Apple Silicon)
dotnet publish src/Viamus.Azure.Devops.Mcp.Server -c Release -r osx-arm64 -o ./publish/osx-arm64
```

Then run the executable directly:
```bash
# Windows
./publish/win-x64/Viamus.Azure.Devops.Mcp.Server.exe

# Linux/macOS
./publish/linux-x64/Viamus.Azure.Devops.Mcp.Server
```

> **Note**: For production, use a reverse proxy (nginx, traefik) or Application Gateway to handle TLS termination with your own certificates.

---

## Security

### API Key Authentication

The server supports optional API key authentication to protect your MCP endpoints.

#### Configuration

Add to `appsettings.json`:
```json
{
  "ServerSecurity": {
    "ApiKey": "your-secret-api-key",
    "RequireApiKey": true
  }
}
```

Or via environment variables:
```bash
# .NET CLI
ServerSecurity__ApiKey=your-secret-key ServerSecurity__RequireApiKey=true dotnet run

# Docker
docker compose up -d  # Configure in .env file
```

For Docker, add to your `.env` file:
```bash
MCP_API_KEY=your-secret-api-key
MCP_REQUIRE_API_KEY=true
```

#### How It Works

- When `RequireApiKey` is `false` (default): No authentication required
- When `RequireApiKey` is `true`: All requests (except `/health`) require a valid API key
- The `/health` endpoint is always accessible without authentication

#### Providing the API Key

Clients can provide the API key one way:

**Option 1 - X-API-Key Header (recommended):**
```bash
curl -H "X-API-Key: your-secret-key" https://localhost:5001
```


#### Generating a Secure API Key

```bash
# Using OpenSSL
openssl rand -base64 32

# Using PowerShell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

---

## Client Configuration

### Claude Code

**Without API Key authentication:**
```bash
claude mcp add azure-devops --transport http http://localhost:5000
```


**With API Key authentication:**

```bash
claude mcp add azure-devops --transport http http://localhost:5000 --header "X-API-Key: your-secret-api-key"
```

> **Note**: Use port `5000` (HTTP) if running with .NET CLI, or `8080` (HTTP) if running with Docker locally. For production deployments, configure a reverse proxy with HTTPS.

## Usage Examples

After configuring the MCP client, you can ask questions like:

### Work Items

- "List the active work items in project X"
- "What bugs are assigned to me?"
- "Show me the details of work item #1234"
- "What work items were changed in the last 7 days?"
- "Search for work items with 'login' in the title"
- "Add a comment to work item #1234 saying the bug was fixed"
- "Create a new Bug in project X titled 'Login page crashes on submit'"
- "Create a User Story assigned to John with priority 2 under parent #100"
- "Update work item #1234 to change state to 'Resolved' and assign to Jane"
- "Set the iteration path of work item #567 to 'Project\Sprint 3'"

### Git Repositories

- "List all repositories in project X"
- "Show me the branches in the 'my-app' repository"
- "What files are in the /src folder of the 'backend' repository?"
- "Get the content of the README.md file from the 'frontend' repository"
- "Search for all .cs files in the 'api' repository"
- "Show me the content of /src/Program.cs from the 'main' branch"

### Pull Requests

- "Show me all active pull requests in the 'my-repo' repository"
- "Get details of pull request #123"
- "What comments are on PR #456?"
- "Search for pull requests related to 'authentication'"
- "Show me PRs targeting the 'main' branch"
- "List PRs created by user@email.com"

### Pipelines and Builds

- "List all pipelines in the project"
- "Show me the recent builds for pipeline 'CI-Build'"
- "What's the status of build #789?"
- "Show me failed builds from the last week"
- "Get the logs for build #456"
- "Show me the timeline of build #123"
- "What builds are currently in progress?"

### Analytics and Insights

- "What's our average delivery time (lead time)?"
- "Are we delivering faster or slower than last month?"
- "Where is work piling up? Show me bottlenecks"
- "Who on the team is overloaded?"
- "What items have been stuck for too long?"
- "Show me aging work items that need attention"
- "How many items did we complete last week?"
- "What's our cycle time for bugs vs user stories?"
- "Is the 'Backend' team overloaded?"
- "Compare this sprint's throughput to the previous sprint"

---

## Troubleshooting

### Common Issues

<details>
<summary><strong>Health check returns error or connection refused</strong></summary>

1. Verify the server is running:
   ```bash
   # Docker
   docker compose ps

   # Check if port is in use
   netstat -an | grep 8080  # Docker
   netstat -an | grep 5000  # .NET CLI
   ```

2. Check logs for errors:
   ```bash
   # Docker
   docker compose logs

   # .NET CLI - errors appear in terminal
   ```
</details>

<details>
<summary><strong>Authentication failed / 401 Unauthorized (Azure DevOps)</strong></summary>

1. Verify your PAT is correct in `appsettings.json`
2. Check PAT hasn't expired in Azure DevOps
3. Ensure PAT has required scopes (Work Items, Code, Build)
4. Verify the organization URL is correct (no trailing slash)
</details>

<details>
<summary><strong>401 Unauthorized (MCP Server API Key)</strong></summary>

If API key authentication is enabled (`RequireApiKey: true`):

1. Verify the API key is correctly set in your client configuration
2. Check the `X-API-Key` header is being sent with requests
3. Ensure the API key matches exactly (case-sensitive)
4. The `/health` endpoint should still work without authentication

```bash
# Test health endpoint (should work without API key)
curl http://localhost:5000/health

# Test with API key
curl -H "X-API-Key: your-key" http://localhost:5000
```
</details>

<details>
<summary><strong>Project not found</strong></summary>

1. Verify `AZURE_DEVOPS_DEFAULT_PROJECT` matches exact project name
2. Or pass the project name explicitly in your queries
3. Check PAT has access to the project
</details>

<details>
<summary><strong>Docker: Container exits immediately</strong></summary>

1. Check if `appsettings.json` has the required Azure DevOps configuration
2. View logs: `docker compose logs`
3. Ensure port 8080 is not in use by another application
</details>

<details>
<summary><strong>.NET CLI: dotnet run fails</strong></summary>

1. Verify .NET 10 SDK is installed: `dotnet --version`
2. Restore packages: `dotnet restore`
3. Check if `appsettings.json` has the correct Azure DevOps settings
</details>

---

## Project Structure

```
mcp-azure-devops/
├── src/
│   └── Viamus.Azure.Devops.Mcp.Server/
│       ├── Configuration/      # App configuration classes
│       ├── Middleware/         # HTTP middleware (authentication, etc.)
│       ├── Models/             # DTOs and data models
│       │   ├── WorkItemDto.cs          # Work item models
│       │   ├── RepositoryDto.cs        # Git repository models
│       │   ├── PullRequestDto.cs       # Pull request models
│       │   ├── PipelineDto.cs          # Pipeline/build models
│       │   ├── FlowMetricsDto.cs       # Flow metrics models
│       │   └── WipAnalysisDto.cs       # WIP and aging models
│       ├── Services/           # Azure DevOps SDK integration
│       ├── Tools/              # MCP tool implementations
│       │   ├── WorkItemTools.cs        # Work item operations
│       │   ├── GitTools.cs             # Git repository operations
│       │   ├── PullRequestTools.cs     # Pull request operations
│       │   ├── PipelineTools.cs        # Pipeline/build operations
│       │   └── AnalyticsTools.cs       # Analytics and metrics
│       ├── Program.cs          # Entry point
│       ├── appsettings.json    # App settings
│       └── Dockerfile          # Container definition
├── tests/
│   └── Viamus.Azure.Devops.Mcp.Server.Tests/
│       ├── Configuration/      # Configuration tests
│       ├── Middleware/         # Middleware tests
│       ├── Models/             # DTO tests
│       └── Tools/              # Tool behavior tests
├── .github/                    # GitHub templates
├── docker-compose.yml          # Docker orchestration
├── CONTRIBUTING.md             # Contributor guide
├── CODE_OF_CONDUCT.md          # Community guidelines
├── SECURITY.md                 # Security policy
└── LICENSE                     # MIT License
```

## API Reference

### Work Item DTOs

#### WorkItemDto
Complete work item details including all standard fields and custom fields.

#### WorkItemSummaryDto
Lightweight representation with essential fields (Id, Title, Type, State, Priority) for list views.

#### WorkItemCommentDto
Represents a comment on a work item with author and timestamp.

### Git DTOs

#### RepositoryDto
Repository details including Id, Name, DefaultBranch, URLs, Size, and project information.

#### BranchDto
Branch information including Name, ObjectId (commit), and creator details.

#### GitItemDto
Represents a file or folder in a repository with Path, ObjectId, GitObjectType, and IsFolder flag.

#### GitFileContentDto
File content with Path, Content (text), IsBinary flag, Encoding, and Size.

### Pull Request DTOs

#### PullRequestDto
Complete pull request details including:
- PullRequestId, Title, Description
- SourceBranch, TargetBranch
- Status (Active, Abandoned, Completed)
- CreatedBy, CreationDate, ClosedDate
- Reviewers list, MergeStatus, IsDraft
- Repository and Project information

#### PullRequestReviewerDto
Reviewer information including DisplayName, Vote (-10 to 10), IsRequired, and HasDeclined status.

#### PullRequestThreadDto
Comment thread with Id, Status (Active, Fixed, etc.), FilePath, LineNumber, and Comments list.

#### PullRequestCommentDto
Individual comment with Id, Content, Author, timestamps, and CommentType.

### Pipeline/Build DTOs

#### PipelineDto
Pipeline (build definition) details including Id, Name, Folder, ConfigurationType, and QueueStatus.

#### BuildDto
Comprehensive build information including:
- Id, BuildNumber, Status, Result
- SourceBranch, SourceVersion
- RequestedBy, RequestedFor
- QueueTime, StartTime, FinishTime
- Definition details, LogsUrl, Reason, Priority

#### BuildLogDto
Build log metadata with Id, Type, Url, LineCount, and timestamps.

#### BuildTimelineRecordDto
Timeline record (stage, job, or task) with Id, ParentId, Type, Name, State, Result, timing information, and error/warning counts.

### Analytics DTOs

#### FlowMetricsDto
Flow metrics analysis results including:
- PeriodStart, PeriodEnd, Throughput (total completed items)
- LeadTime, CycleTime (MetricStatistics with average, median, percentiles)
- ThroughputByPeriod (breakdown by day/week)
- ThroughputByType (breakdown by work item type)
- Items (optional detailed list)

#### MetricStatistics
Statistical measures for a metric including Average, Median, Percentile85, Percentile95, Min, Max, StdDev, and Count.

#### WipAnalysisDto
Work in Progress analysis including:
- TotalWip, ByState, ByArea, ByPerson, ByType
- AgingItems (items stuck too long)
- Insights (auto-generated observations)

#### BottleneckAnalysisDto
Bottleneck identification results with:
- Bottlenecks (list ordered by severity)
- Recommendations (actionable suggestions)

#### BottleneckDto
Individual bottleneck with Type (State/Person/Area), Location, ItemCount, Severity (1-10), Description, and SampleItemIds.

#### AgingReportDto
Comprehensive aging analysis including:
- TotalAgingItems, Summary (statistics)
- ByUrgency (Critical/High/Medium/Low item IDs)
- ByState, ByAssignee, ByArea (grouped counts)
- Items (detailed list with recommendations)
- Recommendations (overall action items)

#### AgingItemDetailDto
Detailed aging item with Id, Title, WorkItemType, State, AssignedTo, AreaPath, Priority, DaysSinceUpdate, DaysSinceCreation, Urgency, UrgencyScore, and Recommendation.

---

## Development

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~WorkItemToolsTests"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Building

```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release
```

### Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for:
- Development setup
- Coding standards
- How to add new MCP tools
- Pull request guidelines

---

## License

This project is licensed under the [MIT License](LICENSE).

---

## Links

- [Model Context Protocol](https://modelcontextprotocol.io/)
- [Azure DevOps REST API](https://learn.microsoft.com/en-us/rest/api/azure/devops/)
- [Report an Issue](https://github.com/viamus/mcp-azure-devops/issues)
