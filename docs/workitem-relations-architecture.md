# Architecture Decision Record (ADR) & Implementation Plan: Work Item Relationship Collection

**Feature Title:** Azure DevOps Work Item Relationship Collection Visibility  
**Target Repository:** `SouzaEduardoAC/mcp-azure-devops`  
**Phase:** 2b — Architecture & Design Specifications  
**Status:** Proposed  

---

## 1. Context & Architectural Strategy

Work items in Azure DevOps use relation collections to capture links between items. Currently, `mcp-azure-devops` strips out relation arrays during `MapToDto` except for parent ID, commit links, and pull request links.

To allow LLMs to trace defect origins, inspect parent/child hierarchies, analyze predecessor/successor blockers, and inspect test links, we are introducing structured relationship visibility into the MCP server.

---

## 2. Architectural Decisions (ADRs)

### ADR 001: Relation Type Normalization
Raw Azure DevOps relation URIs can be verbose (e.g. `System.LinkTypes.Hierarchy-Forward`, `Microsoft.VSTS.Common.TestedBy-Forward`). 

**Decision:** We will map relation strings to standardized, human- and LLM-friendly aliases while retaining `rawRel` for full transparency:

```csharp
private static readonly Dictionary<string, string> RelationTypeMappings = new(StringComparer.OrdinalIgnoreCase)
{
    ["System.LinkTypes.Hierarchy-Reverse"] = "Parent",
    ["System.LinkTypes.Hierarchy-Forward"] = "Child",
    ["System.LinkTypes.Related"] = "Related",
    ["System.LinkTypes.Dependency-Reverse"] = "Predecessor",
    ["System.LinkTypes.Dependency-Forward"] = "Successor",
    ["Microsoft.VSTS.Common.TestedBy-Forward"] = "Tested By",
    ["Microsoft.VSTS.Common.TestedBy-Reverse"] = "Tests",
    ["Hyperlink"] = "Hyperlink",
    ["AttachedFile"] = "Attachment"
};
```

---

### ADR 002: Dedicated `get_work_item_relations` MCP Tool
**Decision:** Create a dedicated tool `get_work_item_relations` tailored specifically for relationship inspection.
- **Parameters:**
  - `workItemId` (int, required)
  - `relationTypeFilter` (string, optional): Filter by `"Related"`, `"Parent"`, `"Child"`, `"Predecessor"`, `"Successor"`, `"Tests"`, `"Tested By"`.
  - `expandTargetSummary` (bool, optional, default: `false`): When `true`, fetches the target work item title, state, and type inline using batch `WitClient.GetWorkItemsAsync`.
  - `project` (string, optional)
  - `organization` (string, optional)

---

### ADR 003: Recursive Hierarchy Tree Tool `get_work_item_tree`
**Decision:** Provide `get_work_item_tree` to render recursive Parent/Child trees for Epics, Features, User Stories, and Tasks.
- **Parameters:**
  - `workItemId` (int, required)
  - `maxDepth` (int, optional, default: 2, max: 5)
  - `project` (string, optional)
  - `organization` (string, optional)
- **Cycle Safety:** Employs a `HashSet<int> visitedWorkItemIds` during traversal to prevent infinite loops if circular relationships exist.

---

### ADR 004: Opt-In Extension on `get_work_item` & `get_work_items`
**Decision:** Add `includeRelations` (bool, default: `false`) to `GetWorkItem` and `GetWorkItems` tools.
When `includeRelations: true`, `WorkItemDto` populates its `Relations` property (`List<WorkItemRelationDto>`).

---

## 3. Data Transfer Object (DTO) Contracts

### `WorkItemRelationDto.cs`
```csharp
namespace Viamus.Azure.Devops.Mcp.Server.Models;

public sealed record WorkItemRelationDto
{
    public string RelationType { get; init; } = null!;
    public string RawRel { get; init; } = null!;
    public int? TargetId { get; init; }
    public string? TargetUrl { get; init; }
    public string? Comment { get; init; }
    public WorkItemSummaryDto? TargetSummary { get; init; }
}

public sealed record WorkItemRelationsResultDto
{
    public int WorkItemId { get; init; }
    public int Count { get; init; }
    public IReadOnlyList<WorkItemRelationDto> Relations { get; init; } = [];
}

public sealed record WorkItemTreeNodeDto
{
    public WorkItemDto WorkItem { get; init; } = null!;
    public IReadOnlyList<WorkItemTreeNodeDto> Children { get; init; } = [];
}
```

---

## 4. Modified Component Map

```
┌────────────────────────────────────────────────────────┐
│                   WorkItemTools.cs                     │
│  - get_work_item_relations (NEW)                       │
│  - get_work_item_tree (NEW)                            │
│  - get_work_item (UPDATED: includeRelations)          │
│  - get_work_items (UPDATED: includeRelations)         │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│               IAzureDevOpsService.cs                   │
│  - GetWorkItemRelationsAsync (NEW)                     │
│  - GetWorkItemTreeAsync (NEW)                          │
│  - GetWorkItemAsync (UPDATED: includeRelations)        │
│  - GetWorkItemsAsync (UPDATED: includeRelations)       │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│                 AzureDevOpsService.cs                  │
│  - MapToDto (UPDATED: relation parsing logic)         │
│  - ParseRelations (NEW static helper)                  │
└────────────────────────────────────────────────────────┘
```

---

## 5. Verification & Testing Plan

### Automated Unit Tests
- `WorkItemRelationTests.cs`:
  - Tests relation type normalization for all known Azure DevOps relation URIs.
  - Tests `TargetId` parsing from URLs.
  - Tests `MapToDto` with `includeRelations = true`.
  - Tests `get_work_item_tree` cycle detection on self-referential or cyclic links.

---

## 6. Definition of Done
- [x] Architecture & ADR drafted and saved to `docs/workitem-relations-architecture.md`.
- [ ] Sign-off received for `plan` gate via `request_approval`.
- [ ] Phase 3 Compliance Evaluation complete.
- [ ] Phase 4 Implementation & Peer Review complete.
