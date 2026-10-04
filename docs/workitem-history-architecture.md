# Architecture Decision Record (ADR): Work Item Activity & State Transition History

**Feature Title:** Azure DevOps Work Item Activity & State Transition History  
**Document Status:** Complete — Pending Phase 2b Gate Sign-off (`plan`)  
**Target Repository:** `SouzaEduardoAC/mcp-azure-devops`  

---

## 1. Architectural Decisions

### Decision 1: Dedicated MCP Server Tools for History Retrieval
**Status:** Approved  
**Context:** We need to provide activity history (column/state moves, author, timestamp) to LLM clients.  
**Decision:**
- Create two dedicated MCP tools in `WorkItemTools.cs`:
  1. `get_work_item_history`: Accepts `workItemId` (int), `project` (string?), `organization` (string?).
  2. `get_work_items_history`: Accepts `workItemIds` (comma/semicolon-separated string), `project` (string?), `organization` (string?).
- **Rationale:** Separating history retrieval from basic query summaries (`query_work_items`) prevents N+1 query bottlenecks and keeps standard query payloads lightweight.

---

### Decision 2: Controlled Concurrency for Batch Processing (`SemaphoreSlim`)
**Status:** Approved  
**Context:** When an LLM calls `get_work_items_history` for e.g. 15 items, making 15 sequential HTTP calls introduces ~3-5 seconds of latency. Unthrottled parallel calls can trigger HTTP 429 (Rate Limit Exceeded) from Azure DevOps.  
**Decision:**
- Use `Task.WhenAll` combined with a `SemaphoreSlim(10)` throttling gate in `AzureDevOpsService.GetWorkItemsHistoryAsync`.
- Limit maximum parallel HTTP requests to **10 concurrent connections**.

---

### Decision 3: Data Contracts & Serializer Schema
**Status:** Approved  

```csharp
namespace Viamus.Azure.Devops.Mcp.Server.Models;

public sealed record WorkItemStateTransition(
    int Revision,
    string State,
    string? PreviousState,
    string? BoardColumn,
    string? PreviousBoardColumn,
    string MovedBy,
    DateTime Timestamp,
    double? DurationInHours
);

public sealed record WorkItemHistoryResult(
    int WorkItemId,
    int TotalTransitions,
    IReadOnlyList<WorkItemStateTransition> Transitions
);
```

---

### Decision 4: Azure DevOps REST API Revisions Parsing
**Status:** Approved  
**Endpoint:** `GET /{organization}/{project}/_apis/wit/workItems/{id}/updates?api-version=7.1`  
**Parsing Logic:**
1. Parse JSON response array `value`.
2. Extract `revisedBy.displayName` or `revisedBy.uniqueName` as `MovedBy`.
3. Extract `revisedDate` as UTC `Timestamp`.
4. Inspect `fields`:
   - If `System.State` exists: read `newValue` and `oldValue`.
   - If `System.BoardColumn` (or `WEF_*_Kanban.Column`) exists: read `newValue` and `oldValue`.
5. If neither `System.State` nor `System.BoardColumn` changed in a revision, skip that revision (filter out non-state edits like title/description changes).
6. Calculate `DurationInHours` between consecutive transition timestamps.

---

## 2. File Modification Plan

This section preserves the original design plan and proposed filenames. The shared implementation now lives in Core; the current history models are in [WorkItemHistoryDto.cs](../src/Viamus.Azure.Devops.Mcp.Core/Models/WorkItemHistoryDto.cs), and tool tests are in [Tools/WorkItemToolsTests.cs](../tests/Viamus.Azure.Devops.Mcp.Server.Tests/Tools/WorkItemToolsTests.cs).

### 1. `src/Viamus.Azure.Devops.Mcp.Core/Models/WorkItemHistoryModels.cs` [NEW]
- Defines `WorkItemStateTransition` and `WorkItemHistoryResult` record classes.

### 2. `src/Viamus.Azure.Devops.Mcp.Core/Services/IAzureDevOpsService.cs` [MODIFY]
- Add `GetWorkItemHistoryAsync(int workItemId, string? project, CancellationToken cancellationToken)`
- Add `GetWorkItemsHistoryAsync(IEnumerable<int> workItemIds, string? project, CancellationToken cancellationToken)`

### 3. `src/Viamus.Azure.Devops.Mcp.Core/Services/AzureDevOpsService.cs` [MODIFY]
- Implement `GetWorkItemHistoryAsync` by issuing GET request to `_apis/wit/workItems/{id}/updates`.
- Implement `GetWorkItemsHistoryAsync` using `Task.WhenAll` and `SemaphoreSlim(10)`.

### 4. `src/Viamus.Azure.Devops.Mcp.Core/Tools/WorkItemTools.cs` [MODIFY]
- Register `[McpServerTool(Name = "get_work_item_history")]`
- Register `[McpServerTool(Name = "get_work_items_history")]`

### 5. `tests/Viamus.Azure.Devops.Mcp.Server.Tests/WorkItemToolsTests.cs` [MODIFY/NEW]
- Unit tests verifying transition parsing, empty history handling, and batch retrieval logic.

---

## 3. Verification & Compliance Plan

- **Automated Unit Tests:** `dotnet test` in project root.
- **SonarQube / Code Analysis:** Zero code smells or security vulnerabilities.
- **Backward Compatibility:** All existing tool signatures and tests remain 100% passing.
