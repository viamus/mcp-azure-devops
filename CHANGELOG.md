# Changelog

All notable changes to this project are documented in this file.

## [Unreleased]

### Added

- A .NET 10 STDIO host in `src/Viamus.Azure.Devops.Mcp.Stdio`, sharing Azure DevOps tools and configuration with the HTTP host through `Viamus.Azure.Devops.Mcp.Core`.
- Centralized MCP tool errors with `isError`, actionable messages, stable error codes, HTTP status when available, and a transient-failure indicator for PAT/authentication, permission, resource, rate-limit, network, timeout, configuration, and unexpected failures.
- Local HTTP and STDIO protocol integration tests, including a simulated Azure DevOps authentication failure over HTTP, safe handling of unsupported insecure Basic authentication, and continued tool discovery after failures.

### Fixed

- Azure DevOps SDK clients are created only when needed, so invalid or expired PATs no longer block MCP initialization and tool discovery.
- Wiki reads no longer interpret authentication failures as missing resources based on exception message text.
- Existing tool validation error responses now also set the MCP failure flag.
- STDIO runtime logs go to stderr and configuration loads relative to the executable.
## [1.3.0] - 2026-07-20

### Added

- Multi-organization Azure DevOps configuration with organization-specific PATs, default projects, and an optional `organization` argument across MCP tools. The existing single-organization configuration remains supported.
- Wiki tools for listing wikis and reading wiki pages.
- Work item attachment upload, download, and metadata tools.
- Work item discussion retrieval and pull request thread comments.
- Pull request thread creation, status updates, and pull request updates.
- Work item relation linking.
- `ActivatedDate` and `ClosedDate` fields in work item responses.

### Fixed

- Work item parent IDs are now derived from the Azure DevOps `Hierarchy-Reverse` relation.

### Removed

- Flow analytics, WIP analysis, bottleneck, workload, and aging report tools introduced in v1.2.0.

### Upgrade notes

- Existing single-organization installations do not require configuration changes.
- To use more than one organization, configure `AzureDevOps:DefaultOrganization` and `AzureDevOps:Organizations`, or the equivalent environment variables documented in `.env.example`.
- Clients that used the removed analytics tools must migrate those workflows before upgrading.

[1.3.0]: https://github.com/viamus/mcp-azure-devops/compare/v1.2.0...v1.3.0
