# Technical Analysis: Azure DevOps Work Item Relationship Collection

**Feature:** Work Item Relationship Collection Visibility  
**Target Repository:** `SouzaEduardoAC/mcp-azure-devops`  
**Phase:** 2a — Discovery & Forensic Analysis  

---

## 1. Codebase Forensics & Current Architecture

### Existing Code Structure
- **Service Layer (`AzureDevOpsService.cs`):**
  - Uses `Microsoft.TeamFoundation.WorkItemTracking.WebApi.WorkItemTrackingHttpClient` to communicate with Azure DevOps.
  - In `GetWorkItemAsync` and `GetWorkItemsAsync`, calls `WitClient.GetWorkItemAsync(..., expand: WorkItemExpand.All)` which already retrieves the complete `relations` array from Azure DevOps REST API.
  - `MapToDto` currently iterates over `workItem.Relations` solely to extract `ParentId` (from `System.LinkTypes.Hierarchy-Reverse`), `LinkedCommits` (from `vstfs:///Git/Commit/...`), and `LinkedPullRequests` (from `vstfs:///Git/PullRequestId/...`).
  - All other relations (such as `System.LinkTypes.Related`, `System.LinkTypes.Dependency-Forward`/`Reverse`, `Microsoft.VSTS.Common.TestedBy-Forward`/`Reverse`) are currently discarded during DTO projection.

- **DTO Models (`WorkItemDto.cs`, `WorkItemSummaryDto.cs`):**
  - `WorkItemDto` currently lacks a `Relations` property.
  - `WorkItemSummaryDto` contains basic scalar fields (`Id`, `Title`, `State`, `AssignedTo`, `ParentId`).

- **MCP Tools (`WorkItemTools.cs`):**
  - `link_work_items` tool already exists for creating links (supporting relation types: `parent`, `child`, `predecessor`, `successor`, `related`).
  - Read tools (`get_work_item`, `get_work_items`, `query_work_items`, `get_child_work_items`) do not return normalized relation collections.

---

## 2. Target API & Relation Type Normalization

### Azure DevOps Relation Types (`rel`)

| Raw Azure DevOps `rel` URI | Normalized Name | Directional Meaning |
| :--- | :--- | :--- |
| `System.LinkTypes.Hierarchy-Reverse` | `Parent` | Item's parent work item |
| `System.LinkTypes.Hierarchy-Forward` | `Child` | Item's child work item |
| `System.LinkTypes.Related` | `Related` | Bilateral link (e.g., origin defect link) |
| `System.LinkTypes.Dependency-Reverse` | `Predecessor` | Blocking item that must finish first |
| `System.LinkTypes.Dependency-Forward` | `Successor` | Blocked item depending on this |
| `Microsoft.VSTS.Common.TestedBy-Forward` | `Tested By` | Test Case testing this requirement |
| `Microsoft.VSTS.Common.TestedBy-Reverse` | `Tests` | Requirement tested by this test case |
| `Hyperlink` | `Hyperlink` | External web URL |
| `AttachedFile` | `Attachment` | File attachment |

### ID Extraction Heuristics
Target URLs for work items follow the format:
`https://dev.azure.com/{organization}/{project}/_apis/wit/workItems/{targetId}`
`TargetId` can be parsed via standard regex or string segment parsing (`/workItems/(\d+)$`).

---

## 3. Impact & Risk Analysis

1. **Zero Extra REST Calls for Single/Batch Fetch:** Because `WitClient.GetWorkItemAsync` and `GetWorkItemsAsync` use `WorkItemExpand.All`, relation parsing occurs entirely in-memory without extra network calls.
2. **Token Efficiency:** Adding `includeRelations: false` by default preserves lightweight JSON responses for existing LLM prompts while allowing targeted inspection when relationship visibility is needed.
3. **Cycle Safety in Hierarchy Trees:** Recursive tree retrieval (`get_work_item_tree`) must track visited work item IDs (`HashSet<int>`) to prevent infinite recursion if invalid or circular link graphs exist.

---

## 4. Next Steps
Upon approval of this Technical Analysis (`discovery` gate), Phase 2b Architecture & ADR will specify the exact C# DTO contracts, interface additions, and unit testing strategy.
