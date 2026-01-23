# MCP Azure DevOps Server

A [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) server for Azure DevOps integration, enabling AI assistants to interact with Azure DevOps Work Items and Git Repositories.

## About

This project implements an MCP server that exposes tools for querying and managing Work Items and Git Repositories in Azure DevOps. It can be used with any compatible MCP client, such as Claude Desktop, Claude Code, or other assistants that support the protocol.

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

### Git Repository Tools

| Tool | Description |
|------|-------------|
| `get_repositories` | Lists all Git repositories in a project |
| `get_repository` | Gets details of a specific repository by name or ID |
| `get_branches` | Lists all branches in a repository |
| `get_repository_items` | Browses files and folders at a specific path in a repository |
| `get_file_content` | Gets the content of a specific file in a repository |
| `search_repository_files` | Searches for files by path pattern in a repository |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (for local execution)
- [Docker](https://www.docker.com/) (for container execution)
- An Azure DevOps account with a Personal Access Token (PAT)

### Creating a Personal Access Token (PAT)

1. Go to your Azure DevOps organization
2. Navigate to **User Settings** > **Personal Access Tokens**
3. Click **New Token**
4. Configure the required permissions:
   - **Work Items**: Read & Write
   - **Code**: Read (for Git repository access)
5. Copy the generated token

## Configuration

### Environment Variables

Copy the `.env.example` file to `.env` and configure it:

```bash
cp .env.example .env
```

Edit the `.env` file:

```env
# Required: Your Azure DevOps organization URL
AZURE_DEVOPS_ORG_URL=https://dev.azure.com/your-organization

# Required: Personal Access Token
AZURE_DEVOPS_PAT=your-token-here

# Optional: Default project name
AZURE_DEVOPS_DEFAULT_PROJECT=your-project
```

## Running the Project

### Using Docker Compose (Recommended)

```bash
docker compose up -d
```

The server will be available at `http://localhost:8080`.

### Using .NET CLI

```bash
cd src/Viamus.Azure.Devops.Mcp.Server
dotnet run
```

### Using Self-Contained Executable

You can publish the server as a self-contained executable that doesn't require .NET to be installed:

```bash
# Windows
dotnet publish src/Viamus.Azure.Devops.Mcp.Server -c Release -r win-x64 -o ./publish/win-x64

# Linux
dotnet publish src/Viamus.Azure.Devops.Mcp.Server -c Release -r linux-x64 -o ./publish/linux-x64

# macOS (Apple Silicon)
dotnet publish src/Viamus.Azure.Devops.Mcp.Server -c Release -r osx-arm64 -o ./publish/osx-arm64
```

### Verifying it's Working

Access the health check endpoint:

```bash
curl http://localhost:8080/health
```

## Configuring in Claude Desktop

Add the following configuration to your Claude Desktop config file (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "azure-devops": {
      "url": "http://localhost:8080"
    }
  }
}
```

## Configuring in Claude Code

Add to your MCP configuration file:

```json
{
  "mcpServers": {
    "azure-devops": {
      "type": "http",
      "url": "http://localhost:8080"
    }
  }
}
```

## Usage Examples

After configuring the MCP client, you can ask questions like:

### Work Items

- "List the active work items in project X"
- "What bugs are assigned to me?"
- "Show me the details of work item #1234"
- "What work items were changed in the last 7 days?"
- "Search for work items with 'login' in the title"
- "Add a comment to work item #1234 saying the bug was fixed"

### Git Repositories

- "List all repositories in project X"
- "Show me the branches in the 'my-app' repository"
- "What files are in the /src folder of the 'backend' repository?"
- "Get the content of the README.md file from the 'frontend' repository"
- "Search for all .cs files in the 'api' repository"
- "Show me the content of /src/Program.cs from the 'main' branch"

## Project Structure

```
├── src/
│   └── Viamus.Azure.Devops.Mcp.Server/
│       ├── Configuration/     # Configuration classes
│       ├── Models/            # DTOs and models
│       │   ├── WorkItemDto.cs
│       │   ├── WorkItemSummaryDto.cs
│       │   ├── WorkItemCommentDto.cs
│       │   ├── PaginatedResult.cs
│       │   ├── RepositoryDto.cs
│       │   ├── BranchDto.cs
│       │   ├── GitItemDto.cs
│       │   └── GitFileContentDto.cs
│       ├── Services/          # Azure DevOps integration services
│       │   ├── IAzureDevOpsService.cs
│       │   └── AzureDevOpsService.cs
│       ├── Tools/             # Exposed MCP tools
│       │   ├── WorkItemTools.cs
│       │   └── GitTools.cs
│       └── Program.cs         # Application entry point
├── tests/
│   └── Viamus.Azure.Devops.Mcp.Server.Tests/
│       ├── Models/            # Model unit tests
│       └── Tools/             # Tool unit tests
├── .github/
│   └── ISSUE_TEMPLATE/        # GitHub issue templates
├── docker-compose.yml         # Docker Compose configuration
├── .env.example               # Environment variables example
└── README.md
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

## License

This project is licensed under the MIT License.
