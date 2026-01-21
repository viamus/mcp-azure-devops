# MCP Azure DevOps Server

A [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) server for Azure DevOps integration, enabling AI assistants to interact with Azure DevOps Work Items.

## About

This project implements an MCP server that exposes tools for querying and searching Work Items in Azure DevOps. It can be used with any compatible MCP client, such as Claude Desktop, Claude Code, or other assistants that support the protocol.

## Available Tools

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

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (for local execution)
- [Docker](https://www.docker.com/) (for container execution)
- An Azure DevOps account with a Personal Access Token (PAT)

### Creating a Personal Access Token (PAT)

1. Go to your Azure DevOps organization
2. Navigate to **User Settings** > **Personal Access Tokens**
3. Click **New Token**
4. Configure the required permissions:
   - **Work Items**: Read (minimum)
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

- "List the active work items in project X"
- "What bugs are assigned to me?"
- "Show me the details of work item #1234"
- "What work items were changed in the last 7 days?"
- "Search for work items with 'login' in the title"

## Project Structure

```
├── src/
│   └── Viamus.Azure.Devops.Mcp.Server/
│       ├── Configuration/     # Configuration classes
│       ├── Models/            # DTOs and models
│       ├── Services/          # Azure DevOps integration services
│       ├── Tools/             # Exposed MCP tools
│       └── Program.cs         # Application entry point
├── docker-compose.yml         # Docker Compose configuration
├── .env.example               # Environment variables example
└── README.md
```

## License

This project is licensed under the MIT License.
