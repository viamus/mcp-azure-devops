# Product Requirements Document (PRD): Work Item Relationship Collection Visibility

**Feature Title:** Azure DevOps Work Item Relationship Collection (Related, Parent, Child, Predecessor, Successor, Tests)  
**Target Repository:** `SouzaEduardoAC/mcp-azure-devops` (Forked from `viamus/mcp-azure-devops`)  
**Document Status:** Draft — Pending Phase 1 Gate Sign-off  

---

## 1. Executive Summary & Problem Statement

In Azure DevOps, work items are linked via rich relationship collections (`System.LinkTypes.Hierarchy-Reverse` [Parent], `System.LinkTypes.Hierarchy-Forward` [Child], `System.LinkTypes.Related` [Related Work], `System.LinkTypes.Dependency-Reverse` [Predecessor], `System.LinkTypes.Dependency-Forward` [Successor], `Microsoft.VSTS.Common.TestedBy-Forward`/`Reverse` [Tests/Tested By], etc.).

Currently, when LLMs query work items via `mcp-azure-devops` tools (`get_work_item`, `get_work_items`, `query_work_items`), the returned work item model omits the `relations` collection. Consequently, LLM agents have zero visibility into linked items.

### Business & Workflow Context
When a work item (e.g., User Story or Feature) encounters an issue during implementation or QA, engineering teams create a related work item (e.g., Bug or Issue) linked directly to the defective one to track origin and traceability. 
Without relationship visibility in `mcp-azure-devops`:
- Downstream LLM agents cannot trace defects back to their originating work item.
- LLMs cannot understand hierarchy trees (Parent Epics / Child Tasks) or blocking dependencies (Predecessors / Successors).
- LLMs cannot verify test coverage links (Tested By / Tests).

This PRD specifies the functional requirements to expose work item relationship collections in `mcp-azure-devops`.

---

## 2. Target Users & Use Cases

### Target Users
- **LLM Bug & Origin Traceability Agents:** AI assistants identifying defect origins, duplicate issues, and root causes across related work items.
- **Project & Portfolio Management AI:** Agents analyzing parent/child breakdown, epic completion, and dependency blockers.
- **QA Automation & Test Analysts:** LLMs verifying whether work items have associated test cases or bug reports attached.

### Key Use Cases
1. **Defect Origin Tracing:** An LLM inspects a Bug work item, finds its `Related` or `Parent` work item, and determines which original user story or task introduced the defective code.
2. **Hierarchy & Epic Breakdown:** An LLM queries a Parent Feature to list all Child User Stories and Tasks to generate progress reports.
3. **Dependency Analysis:** An LLM inspects a work item's `Predecessor` links to identify unblocked tasks or potential delivery bottlenecks.

---

## 3. Scope & Requirements (MoSCoW)

### 3.1 Must Have (V1 Scope)

#### 1. Relation Model & Type Normalization
Map raw Azure DevOps relation URIs / relation types to clean, user-friendly relationship names:

| Azure DevOps Relation Type (`rel`) | Normalized Name | Description |
| :--- | :--- | :--- |
| `System.LinkTypes.Hierarchy-Reverse` | `Parent` | Parent work item in hierarchy |
| `System.LinkTypes.Hierarchy-Forward` | `Child` | Child work item in hierarchy |
| `System.LinkTypes.Related` | `Related` | Direct bilateral relationship (e.g. bug tracking origin) |
| `System.LinkTypes.Dependency-Reverse` | `Predecessor` | Work item that must be completed prior |
| `System.LinkTypes.Dependency-Forward` | `Successor` | Work item dependent on this item |
| `Microsoft.VSTS.Common.TestedBy-Forward` | `Tested By` | Test Case testing this work item |
| `Microsoft.VSTS.Common.TestedBy-Reverse` | `Tests` | Work item tested by this test case |
| `Hyperlink` | `Hyperlink` | External web URL link |
| `AttachedFile` | `Attachment` | File attachment link |

#### 2. `WorkItemRelation` Data Model
```json
{
  "relationType": "Related",
  "rawRel": "System.LinkTypes.Related",
  "targetId": 1234,
  "targetUrl": "https://dev.azure.com/org/proj/_apis/wit/workItems/1234",
  "comment": "Origin bug reported during Sprint 5 testing"
}
```

#### 3. Azure DevOps REST Integration
- Use `$expand=relations` parameter on Azure DevOps WIT REST API endpoints (`GET _apis/wit/workitems/{id}?$expand=relations` and `GET _apis/wit/workitems?ids={ids}&$expand=relations`).
- Parse the `relations` JSON array returned by Azure DevOps.

#### 4. Dedicated MCP Tool: `get_work_item_relations`
- **Name:** `get_work_item_relations`
- **Parameters:**
  - `workItemId` (integer, required): ID of the work item.
  - `relationTypeFilter` (string, optional): Filter by normalized relation type (e.g. `"Related"`, `"Parent"`, `"Child"`, `"Predecessor"`, `"Successor"`).
  - `project` (string, optional): Azure DevOps project.
  - `organization` (string, optional): Azure DevOps organization.
- **Returns:** List of normalized `WorkItemRelation` objects, with target work item summary metadata (ID, Title, WorkItemType, State) expanded where applicable.

#### 5. Opt-in `includeRelations` Parameter on Existing Query Tools
- Add optional `includeRelations` (boolean, default: `false`) parameter to:
  - `get_work_item`
  - `get_work_items`
  - `query_work_items`
- When `includeRelations: true`, populate a `relations` list on each returned `WorkItemDto`.

---

### 3.2 Should Have (V1 Scope)
- **`get_work_item_tree` Tool:**
  - Recursively fetches `Parent` and `Child` relationships up to a configurable `depth` (default: 2, max: 5) to return a full tree structure for Epics/Features/Stories/Tasks.

---

### 3.3 Could Have (V1.1 Scope)
- Summary counts of relations per type (e.g., `relatedCount`, `childCount`, `bugCount`) embedded directly in basic `WorkItemDto`.

---

### 3.4 Won't Have (Out of V1 Scope)
- Relation creation/deletion (writing link updates to Azure DevOps). V1 focuses exclusively on visibility and read queries for LLMs.

---

## 4. Technical Constraints & Performance Considerations

1. **REST API Batching:**
   - Azure DevOps API supports `$expand=relations` on batch work item calls (`/workitems?ids=1,2,3&$expand=relations`).
   - Using `$expand=relations` on single or bulk GETs requires zero extra HTTP calls per work item.
2. **Backward Compatibility:**
   - `includeRelations` defaults to `false` on existing query endpoints to ensure existing JSON contracts and prompt token consumption remain optimized.
3. **Target ID Extraction:**
   - Azure DevOps relation objects contain `url` like `https://dev.azure.com/org/project/_apis/wit/workItems/42`. `targetId` must be reliably extracted via regex/URL parsing (`/workItems/(\d+)$`).

---

## 5. RICE Prioritization

| Feature Component | Reach | Impact | Confidence | Effort | RICE Score |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`WorkItemRelation` model & `$expand=relations` REST integration** | High (100%) | Massive (3.0) | High (100%) | Low (2) | **150.0** |
| **`get_work_item_relations` tool** | High (100%) | High (2.0) | High (100%) | Low (1) | **200.0** |
| **`includeRelations` flag on `get_work_item(s)` / `query_work_items`** | High (80%) | High (2.0) | High (95%) | Low (1) | **152.0** |
| **`get_work_item_tree` tool** | Medium (50%) | Medium (1.0) | High (90%) | Medium (2) | **22.5** |

---

## 6. Definition of Done (Phase 1 Exit Criteria)

- [x] PRD written to `docs/workitem-relations-prd.md`.
- [ ] PRD submitted for human review (`prd` gate).
- [ ] Approval received via `/squad:approve prd`.
- [ ] Phase 2 Architecture & ADR design initiated.
