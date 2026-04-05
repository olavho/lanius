# Layout Architecture Refactoring Plan

**Date**: 2026-04-05  
**Status**: Planning Phase

## Overview

Refactor Lanius visualization to support multiple layout modes with clear separation of concerns between:
- **Repository Analysis** (Git data extraction)
- **Logical Layout** (algorithm-based positioning)
- **Visual Rendering** (D3.js canvas rendering)

## Requirements

### 1. Layout Modes
- **Complete Logical Layout**: Full branch lines with small dots for all commits, connections for splits/merges
- **Calendar Layouts**: Grouped by date/week/month/year with dynamic zoom transitions

### 2. Canvas Features
- Zoom and pan (horizontal & vertical)
- Zoom-aware calendar granularity switching
- Progress indicators for layout calculations

### 3. Architecture
Clear separation into three layers:
- **Analysis**: Extract Git data (existing: RepositoryService, CommitAnalyzer, BranchAnalyzer)
- **Layout**: Calculate positions and relationships (new layer)
- **Visual**: D3.js rendering with zoom/pan (existing: visualization.js)

---

## Architecture Decisions

### Decision: Pixel Coordinates vs. Logical Grid (2026-04-05)

**Context**: Layout engines currently calculate absolute pixel positions (e.g., `x: 450.5, y: 120.0`). An alternative approach would be logical grid coordinates (e.g., `row: 3, columnPercent: 0.45`) with frontend scaling to viewport.

**Decision**: Continue with pixel-based coordinates for now, defer to Phase 6.

**Rationale**:
- Current implementation works well with D3 zoom/pan
- Pixel-based approach is simpler (single coordinate system)
- Don't want to delay Phase 4 (Calendar Layout) for architectural refactoring
- Can implement both layout engines first, then refactor both together in Phase 6
- Having working examples will inform better architectural decisions

**Trade-offs**:
- Backend is viewport-aware (knows about pixel spacing)
- Less separation of concerns (mixed logical/visual)
- +Simpler for now, faster to Phase 4
- +D3 zoom handles viewport adaptation elegantly

**Future**: Phase 6 may refactor to logical coordinates if needed, but only after evaluating both layout engines in production.

---

## Proposed Architecture

### Phase 1: Business Layer Reorganization

#### Current Structure
```
Lanius.Business/
  ├── Services/
  │   ├── RepositoryService.cs      (Git operations)
  │   ├── CommitAnalyzer.cs         (Commit metadata)
  │   ├── BranchAnalyzer.cs         (Branch relationships)
  │   └── ReplayService.cs          (Rx.NET replay)
  └── Models/
      ├── Commit.cs
      ├── Branch.cs
      └── BranchOverview.cs
```

#### Proposed Structure (Domain-Driven, Co-located Models)
```
Lanius.Business/
  ├── Storage/                      ← Repository storage/management domain
  │   ├── Services/
  │   │   ├── IRepositoryStorageService.cs
  │   │   └── RepositoryStorageService.cs
  │   └── Models/
  │       └── RepositoryInfo.cs     (lightweight: id, url, path, timestamps)
  │
  ├── Analysis/                     ← Git data extraction domain
  │   ├── Services/
  │   │   ├── IRepositoryAnalyzer.cs
  │   │   ├── RepositoryAnalyzer.cs
  │   │   ├── ICommitAnalyzer.cs
  │   │   ├── CommitAnalyzer.cs
  │   │   ├── IBranchAnalyzer.cs
  │   │   └── BranchAnalyzer.cs
  │   └── Models/
  │       ├── Commit.cs
  │       ├── Branch.cs
  │       ├── BranchOverview.cs
  │       ├── BranchInfo.cs
  │       ├── DiffStats.cs
  │       ├── CommitRelation.cs
  │       ├── CommitRelationType.cs
  │       ├── CommitSignificance.cs
  │       └── SignificantInfo.cs
  │
  ├── Layout/                       ← Layout calculation domain
  │   ├── Services/
  │   │   ├── ILayoutEngine.cs
  │   │   ├── LayoutEngineBase.cs
  │   │   ├── LogicalLayoutEngine.cs
  │   │   └── CalendarLayoutEngine.cs
  │   └── Models/
  │       ├── LayoutMode.cs         (enum: Logical, Calendar)
  │       ├── LayoutResult.cs       (positions, dimensions)
  │       ├── LayoutNode.cs         (commit with x,y coords)
  │       ├── LayoutEdge.cs         (branch line segments)
  │       ├── CalendarGranularity.cs (enum: Day, Week, Month, Year)
  │       └── LayoutOptions.cs      (zoom level, mode, filters)
  │
  ├── Replay/                       ← Replay/animation domain
  │   ├── Services/
  │   │   ├── IReplayService.cs
  │   │   └── ReplayService.cs
  │   └── Models/
  │       ├── ReplayOptions.cs
  │       ├── ReplaySession.cs
  │       └── ReplayState.cs
  │
  └── Configuration/                ← Shared configuration (stays at root)
      ├── RepositoryStorageOptions.cs
      └── MonitoringOptions.cs
```

**Benefits of this structure**:
- **Domain cohesion**: Each top-level folder represents a complete domain
- **Co-location**: Models live with services that use them
- **Clear separation**: Storage vs Analysis responsibilities distinct
- **Discoverability**: Easy to find all Analysis-related code in one place
- **Reduced namespace depth**: `Lanius.Business.Analysis.Models.Commit` vs `Lanius.Business.Models.Domain.Commit`

**Responsibility Split (Storage vs Analysis)**:

| Concern | Storage Domain | Analysis Domain |
|---------|---------------|-----------------|
| **Purpose** | Manage repository files on disk | Extract Git data from repositories |
| **Operations** | Clone, fetch, delete, list | Read commits, branches, diffs |
| **Dependencies** | LibGit2Sharp (clone/fetch), File I/O | LibGit2Sharp (read), Storage (get path) |
| **Models** | `RepositoryInfo` (lightweight metadata) | `Commit`, `Branch`, `BranchOverview`, etc. |
| **Examples** | "Clone this URL", "Fetch updates", "Delete repo" | "Get all commits", "Analyze branches", "Get diff stats" |

---

## Implementation Phases

### Phase 1: Foundation - Layout Engine Abstractions
**Goal**: Create layout layer without breaking existing functionality

**Status**: ✅ **COMPLETE**

**Tasks**:
1. ✅ Create `Layout/Models/` folder with base models
2. ✅ Create `Layout/Services/ILayoutEngine.cs` interface
3. ✅ Implement `LogicalLayoutEngine.cs` with current algorithm
4. ✅ Add unit tests for layout engine (9 tests, all passing)
5. ✅ Wire up to API without breaking existing frontend

**Files Created**:
- `src/Lanius.Business/Layout/Models/LayoutMode.cs`
- `src/Lanius.Business/Layout/Models/LayoutResult.cs`
- `src/Lanius.Business/Layout/Models/LayoutNode.cs`
- `src/Lanius.Business/Layout/Models/LayoutEdge.cs`
- `src/Lanius.Business/Layout/Models/LayoutOptions.cs`
- `src/Lanius.Business/Layout/Models/CalendarGranularity.cs`
- `src/Lanius.Business/Layout/Services/ILayoutEngine.cs`
- `src/Lanius.Business/Layout/Services/LogicalLayoutEngine.cs`
- `src/Lanius.Business.Test/Layout/LogicalLayoutEngineTests.cs`
- `src/Lanius.Api/DTOs/LayoutResponse.cs`
- `src/Lanius.Api/Controllers/LayoutController.cs`

**API Endpoint**:
- `GET /api/repository/{id}/layout?mode=logical&branchFilter=main,develop`

---

### Phase 2: Logical Layout - Complete Branch Lines
**Goal**: Render all commits with proper branch line visualization

**Status**: ✅ **COMPLETE**

**Backend Tasks** (Complete):
1. ✅ Calculate positions for ALL commits
2. ✅ Calculate branch line paths (y-position per branch)
3. ✅ Identify split points (branch creation) - EdgeType.Branch
4. ✅ Identify merge points - EdgeType.Merge  
5. ✅ Generate `LayoutEdge` objects for branch line segments
6. ✅ Progress reporting functional
7. ✅ Add unit tests (11 tests total, all passing)

**Frontend Tasks** (Complete):
1. ✅ Update `app.js` to consume layout API (`GET /api/repository/{id}/layout`)
2. ✅ Add `renderLayout()` method to `visualization.js`
3. ✅ Render `LayoutNode` objects as circles (4px normal, 6px significant)
4. ✅ Render `LayoutEdge` objects as lines (different colors/styles for Normal/Branch/Merge)
5. ✅ Add legend for edge types (Normal=gray, Branch=green dashed, Merge=orange dashed)
6. ✅ Add hover tooltips for nodes
7. ✅ Add click handling for commit details

**Implementation Details**:

**Backend** (LogicalLayoutEngine):
- Assigns each commit to its "primary branch" (first occurrence)
- Creates EdgeType.Branch for splits (parent on different branch, single parent)
- Creates EdgeType.Merge for merges (multiple parents from different branches)
- Creates EdgeType.Normal for same-branch commit sequences
- Avoids duplicate edges

**Frontend** (visualization.js):
- New `renderLayout(layout)` method consumes layout API response
- Renders edges with type-specific styling:
  - Normal: gray, solid, width 1px, opacity 0.4
  - Branch: green (#4CAF50), dashed (5,5), width 2px, opacity 0.7
  - Merge: orange (#FF9800), dashed (3,3), width 2px, opacity 0.7
- Renders nodes with branch-specific colors:
  - main/master: dark gray (#2d2d2d)
  - release branches: blue (#4a90e2)
  - feature branches: green (#7ed321)
  - hotfix branches: red (#e74c3c)
- Hover effects: enlarge node by 1.5x with tooltip
- Legend showing all edge types

**Files Modified**:
- `src/Lanius.Web/wwwroot/js/app.js` - Updated `loadRepository()` to use layout API
- `src/Lanius.Web/wwwroot/js/visualization.js` - Added `renderLayout()` method
- `src/Lanius.Web/wwwroot/index.html` - Added edge type legend

**Duration**: 3 days (2 backend, 1 frontend)  
**Risk**: ✅ Mitigated - Complete end-to-end working

---

### Phase 3: Canvas Zoom and Pan
**Goal**: Enable zoom/pan navigation for large graphs

**Status**: ✅ **COMPLETE**

**Tasks**:
1. ✅ Add D3 zoom behavior to SVG canvas
2. ✅ Bind zoom to `g` transform (existing group)
3. ✅ Add zoom controls (buttons and mouse wheel)
4. ✅ Add pan controls (click + drag)
5. ✅ Preserve zoom/pan state during re-renders
6. ✅ Add keyboard shortcuts (Ctrl+0/=/-)

**Implementation Details**:
- **Zoom behavior**: D3 zoom with scale extent [0.1, 10] (10% to 1000%)
- **Mouse wheel**: Zoom in/out at cursor position
- **Click + drag**: Pan the graph
- **Buttons**: Zoom In, Zoom Out, Reset Zoom
- **Keyboard shortcuts**: 
  - Ctrl+0: Reset zoom
  - Ctrl+=: Zoom in
  - Ctrl+-: Zoom out
- **State preservation**: `currentZoom` saved and restored across re-renders

**Files Modified**:
- `src/Lanius.Web/wwwroot/js/visualization.js` - Added zoom behavior, control functions
- `src/Lanius.Web/wwwroot/index.html` - Added zoom control buttons UI
- `src/Lanius.Web/wwwroot/js/app.js` - Added event handlers for buttons and keyboard

**Duration**: 1 hour (actual)  
**Risk**: Low (D3 built-in feature)  
**Implementation Date**: 2026-04-05

**Implementation**:
```javascript
// In visualization.js
const zoom = d3.zoom()
    .scaleExtent([0.1, 10])  // 10% to 1000% zoom
    .on('zoom', (event) => {
        g.attr('transform', event.transform);
    });

svg.call(zoom);

// Add reset button
function resetZoom() {
    svg.transition().duration(750).call(
        zoom.transform,
        d3.zoomIdentity
    );
}
```

**Duration**: 2 days  
**Risk**: Low (D3 built-in feature)

---

### Phase 4: Calendar Layout Engine
**Goal**: Group commits by calendar periods with zoom-aware granularity

**Status**: 🔄 **IN PROGRESS** - Backend Complete, API/Frontend Pending

**Tasks**:
1. ✅ Create `CalendarLayoutEngine.cs`
2. ✅ Implement grouping by day/week/month/year (Month-only MVP for Phase 4a)
3. ✅ Calculate aggregate stats per group (commit count, period labels)
4. ✅ Position groups as nodes (single horizontal row MVP)
5. ✅ Add comprehensive unit tests (18 tests, all passing)
6. ⏳ Wire up to API endpoint (LayoutController)
7. ⏳ Frontend calendar rendering (visualization.js)
8. ⏳ Mode switcher UI (Logical/Calendar toggle)
9. ❌ Zoom threshold detection for granularity switching (deferred to Phase 4b)

**Algorithm**:
```
CalendarLayoutEngine(granularity):
1. Group commits by time period:
   - Day: commits on same date
   - Week: commits in same ISO week
   - Month: commits in same month
   - Year: commits in same year

2. For each group:
   - X position = start of period
   - Width = period duration
   - Height = commit count (or lines changed)
   - Color intensity = activity level

3. Return LayoutResult with:
   - Nodes: One per group (not per commit)
   - Metadata: commit count, date range, stats
```

**Zoom Granularity Switching**:
```javascript
// In visualization.js
const ZOOM_THRESHOLDS = {
    year: { min: 0.1, max: 0.5 },
    month: { min: 0.5, max: 2.0 },
    week: { min: 2.0, max: 5.0 },
    day: { min: 5.0, max: 10.0 }
};

function onZoomChange(zoomLevel) {
    const newGranularity = determineGranularity(zoomLevel);
    if (newGranularity !== currentGranularity) {
        currentGranularity = newGranularity;
        reloadLayout(newGranularity);
    }
}
```

**Frontend Changes**:
- Render calendar groups as rectangles
- Show aggregate stats on hover
- Smooth transition between granularity levels

**Duration**: 5-6 days  
**Risk**: High (complex grouping logic, zoom coordination)

---

### Phase 5: Progress Indicators
**Goal**: Show progress during layout calculations

**Status**: ✅ **INTERFACE COMPLETE** (Implementation in Phase 1)

**Tasks**:
1. ✅ Add `IProgress<LayoutProgress>` to layout engine methods
2. ✅ Report progress percentage and current operation (in LogicalLayoutEngine)
3. ❌ Add SignalR hub method for layout progress updates
4. ❌ Display progress bar in UI
5. ❌ Show operation details ("Analyzing 12,000 commits...")

**Backend**:
```csharp
public interface ILayoutEngine
{
    Task<LayoutResult> CalculateLayoutAsync(
        RepositoryInfo repo,
        LayoutOptions options,
        IProgress<LayoutProgress>? progress = null,
        CancellationToken cancellationToken = default
    );
}

public record LayoutProgress(
    int Percentage,
    string Operation,
    int ProcessedItems,
    int TotalItems
);
```

**SignalR**:
```csharp
// In RepositoryHub.cs
public async Task ReportLayoutProgress(string sessionId, LayoutProgress progress)
{
    await Clients.All.SendAsync("LayoutProgress", sessionId, progress);
}
```

**Frontend**:
```javascript
// In app.js
connection.on('LayoutProgress', (sessionId, progress) => {
    updateProgressBar(progress.percentage);
    updateProgressText(`${progress.operation} (${progress.processedItems}/${progress.totalItems})`);
});
```

**Duration**: 2-3 days  
**Risk**: Low (straightforward progress reporting)

---

### Phase 6: Reorganize Business Layer
**Goal**: Refactor to domain-driven structure with co-located models

**Status**: ⚠️ **PARTIALLY COMPLETE** (Layout domain created, others pending)

**Migration Strategy**:
1. ✅ Create new folder structure (Layout/ created, others pending)
2. ❌ Move files gradually (one domain at a time)
3. ❌ Update namespaces to match new structure
4. ❌ Update all `using` statements across solution
5. ❌ Run tests after each domain migration
6. ❌ Update API/DTOs that reference moved models

**Migration Order**:
1. **Layout** (new code, easier to organize from start)
2. **Storage** (extract from RepositoryService)
3. **Replay** (smallest domain, least dependencies)
4. **Analysis** (depends on Storage, do after Storage is stable)

**Namespace Changes**:
```csharp
// OLD
using Lanius.Business.Models;
using Lanius.Business.Services;

// NEW
using Lanius.Business.Storage.Models;
using Lanius.Business.Storage.Services;
using Lanius.Business.Analysis.Models;
using Lanius.Business.Analysis.Services;
using Lanius.Business.Layout.Models;
using Lanius.Business.Layout.Services;
using Lanius.Business.Replay.Models;
using Lanius.Business.Replay.Services;
```

**Risk**: Medium (breaking change across entire solution)  
**Recommendation**: 
- Create new structure FIRST (Phase 1)
- Migrate incrementally during implementation
- Keep old namespaces as aliases temporarily if needed

---

## Implementation Order (Recommended)

### Sprint 1: Foundation (5 days)
1. **Phase 1**: Layout Engine Abstractions (2-3 days)
2. **Phase 3**: Zoom and Pan (2 days)

**Milestone**: Can zoom/pan existing visualization with layout engine abstraction in place

### Sprint 2: Complete Logical Layout (4 days)
3. **Phase 2**: Logical Layout with all commits (3-4 days)

**Milestone**: See all commits with branch lines, splits, and merges

### Sprint 3: Progress & Calendar (8 days)
4. **Phase 5**: Progress Indicators (2-3 days)
5. **Phase 4**: Calendar Layout Engine (5-6 days)

**Milestone**: Switch between logical and calendar views with progress feedback

### Sprint 4: Polish (Optional)
6. **Phase 6**: Reorganize folders (2 days)

**Milestone**: Clean architecture aligned with documentation

---

## API Design

### New Endpoints

#### GET /api/repository/{id}/layout
Query parameters:
- `mode`: "logical" | "calendar"
- `granularity`: "day" | "week" | "month" | "year" (calendar only)
- `zoom`: float (0.1 to 10.0)
- `branchFilter`: comma-separated branch names

Response:
```json
{
  "mode": "logical",
  "nodes": [
    {
      "commitId": "abc123",
      "x": 100.5,
      "y": 40.0,
      "radius": 4,
      "branchName": "main",
      "timestamp": "2026-04-05T10:00:00Z"
    }
  ],
  "edges": [
    {
      "fromCommitId": "abc123",
      "toCommitId": "def456",
      "type": "branch" | "merge",
      "points": [[100, 40], [120, 80]]
    }
  ],
  "dimensions": {
    "width": 2000,
    "height": 600
  }
}
```

---

## Testing Strategy

### Unit Tests
- `LogicalLayoutEngine`: Position calculation, split/merge detection
- `CalendarLayoutEngine`: Grouping logic, granularity switching
- `LayoutCalculator`: Coordinate transformations

### Integration Tests
- Full layout generation for repositories with 100, 1000, 10000 commits
- Performance benchmarks for layout calculation time

### Manual Tests
- Load large repos (qsharp-runtime: 2500 commits, terminal: 12000 commits)
- Verify zoom/pan smoothness
- Test granularity switching at threshold levels
- Verify progress indicators show for slow operations

---

## Performance Considerations

### Layout Calculation
- **Target**: < 1 second for 1000 commits
- **Strategy**: 
  - Calculate positions incrementally
  - Cache layout results (by mode + options hash)
  - Use parallel processing for independent branches

### Rendering
- **Target**: 60 FPS zoom/pan
- **Strategy**:
  - Use D3 virtual scrolling for large commit sets
  - Render only visible nodes (viewport culling)
  - Simplify rendering at low zoom levels (aggregate commits)

### Memory
- **Target**: < 100MB for 10,000 commits
- **Strategy**:
  - Stream layout results instead of loading all in memory
  - Use SignalR streaming for large layouts

---

## Open Questions

1. **Calendar Layout Visual Design**: 
   - Rectangles/bars or vertical timelines?
   - Color coding by activity level or branch?

2. **Zoom Threshold Configuration**:
   - Hardcoded thresholds or user-configurable?
   - Smooth transition or instant switch?

3. **Large Repository Handling**:
   - Limit to recent N commits (configurable)?
   - Server-side pagination of layout results?

4. **Branch Line Routing**:
   - Straight lines or curved (Bezier)?
   - Avoid overlaps or allow?

---

## Success Metrics

- ✅ All existing tests pass
- ✅ New layout modes render correctly
- ✅ Zoom/pan is smooth (60 FPS)
- ✅ Progress indicators show for operations > 1 second
- ✅ Layout calculation < 3 seconds for 10,000 commits
- ✅ Memory usage < 100MB for typical repos

---

## Detailed Folder Reorganization Plan

### Current → Target Mapping

#### Storage Domain (NEW split from RepositoryService)
| Current Location | New Location |
|-----------------|--------------|
| `Services/RepositoryService.cs` (clone, fetch, delete, list methods) | `Storage/Services/RepositoryStorageService.cs` |
| `Services/IRepositoryService.cs` (management methods) | `Storage/Services/IRepositoryStorageService.cs` |
| `Models/RepositoryInfo.cs` | `Storage/Models/RepositoryInfo.cs` |

#### Analysis Domain
| Current Location | New Location |
|-----------------|--------------|
| `Services/RepositoryService.cs` (GetRepositoryInfoAsync logic) | `Analysis/Services/RepositoryAnalyzer.cs` |
| `Services/IRepositoryService.cs` (analysis methods) | `Analysis/Services/IRepositoryAnalyzer.cs` |
| `Services/ICommitAnalyzer.cs` | `Analysis/Services/ICommitAnalyzer.cs` |
| `Services/CommitAnalyzer.cs` | `Analysis/Services/CommitAnalyzer.cs` |
| `Services/IBranchAnalyzer.cs` | `Analysis/Services/IBranchAnalyzer.cs` |
| `Services/BranchAnalyzer.cs` | `Analysis/Services/BranchAnalyzer.cs` |
| `Models/Commit.cs` | `Analysis/Models/Commit.cs` |
| `Models/Branch.cs` | `Analysis/Models/Branch.cs` |
| `Models/BranchOverview.cs` | `Analysis/Models/BranchOverview.cs` |
| `Models/BranchInfo.cs` | `Analysis/Models/BranchInfo.cs` |
| `Models/DiffStats.cs` | `Analysis/Models/DiffStats.cs` |
| `Models/CommitRelation.cs` | `Analysis/Models/CommitRelation.cs` |
| `Models/CommitRelationType.cs` | `Analysis/Models/CommitRelationType.cs` |
| `Models/CommitSignificance.cs` | `Analysis/Models/CommitSignificance.cs` |
| `Models/SignificantInfo.cs` | `Analysis/Models/SignificantInfo.cs` |

#### Layout Domain (NEW)
| File | Location |
|------|----------|
| `LayoutMode.cs` | `Layout/Models/LayoutMode.cs` |
| `LayoutResult.cs` | `Layout/Models/LayoutResult.cs` |
| `LayoutNode.cs` | `Layout/Models/LayoutNode.cs` |
| `LayoutEdge.cs` | `Layout/Models/LayoutEdge.cs` |
| `LayoutOptions.cs` | `Layout/Models/LayoutOptions.cs` |
| `CalendarGranularity.cs` | `Layout/Models/CalendarGranularity.cs` |
| `ILayoutEngine.cs` | `Layout/Services/ILayoutEngine.cs` |
| `LayoutEngineBase.cs` | `Layout/Services/LayoutEngineBase.cs` |
| `LogicalLayoutEngine.cs` | `Layout/Services/LogicalLayoutEngine.cs` |
| `CalendarLayoutEngine.cs` | `Layout/Services/CalendarLayoutEngine.cs` |

#### Replay Domain
| Current Location | New Location |
|-----------------|--------------|
| `Services/IReplayService.cs` | `Replay/Services/IReplayService.cs` |
| `Services/ReplayService.cs` | `Replay/Services/ReplayService.cs` |
| `Models/ReplayOptions.cs` | `Replay/Models/ReplayOptions.cs` |
| `Models/ReplaySession.cs` | `Replay/Models/ReplaySession.cs` |
| `Models/ReplayState.cs` | `Replay/Models/ReplayState.cs` |

#### Configuration (stays at root)
| Current Location | New Location |
|-----------------|--------------|
| `Configuration/RepositoryStorageOptions.cs` | `Configuration/RepositoryStorageOptions.cs` *(no change)* |
| `Configuration/MonitoringOptions.cs` | `Configuration/MonitoringOptions.cs` *(no change)* |

### Migration PowerShell Script

```powershell
# Create new folder structure
$basePath = "src/Lanius.Business"

# Create domain folders
New-Item -ItemType Directory -Path "$basePath/Storage/Services" -Force
New-Item -ItemType Directory -Path "$basePath/Storage/Models" -Force
New-Item -ItemType Directory -Path "$basePath/Analysis/Services" -Force
New-Item -ItemType Directory -Path "$basePath/Analysis/Models" -Force
New-Item -ItemType Directory -Path "$basePath/Layout/Services" -Force
New-Item -ItemType Directory -Path "$basePath/Layout/Models" -Force
New-Item -ItemType Directory -Path "$basePath/Replay/Services" -Force
New-Item -ItemType Directory -Path "$basePath/Replay/Models" -Force

# Split RepositoryService into Storage and Analysis
# NOTE: Manual split required - see "Service Split Guide" below

# Move Analysis files
Move-Item "$basePath/Services/ICommitAnalyzer.cs" "$basePath/Analysis/Services/"
Move-Item "$basePath/Services/CommitAnalyzer.cs" "$basePath/Analysis/Services/"
Move-Item "$basePath/Services/IBranchAnalyzer.cs" "$basePath/Analysis/Services/"
Move-Item "$basePath/Services/BranchAnalyzer.cs" "$basePath/Analysis/Services/"

Move-Item "$basePath/Models/Commit.cs" "$basePath/Analysis/Models/"
Move-Item "$basePath/Models/Branch.cs" "$basePath/Analysis/Models/"
Move-Item "$basePath/Models/BranchOverview.cs" "$basePath/Analysis/Models/"
Move-Item "$basePath/Models/BranchInfo.cs" "$basePath/Analysis/Models/"
Move-Item "$basePath/Models/DiffStats.cs" "$basePath/Analysis/Models/"
Move-Item "$basePath/Models/CommitRelation.cs" "$basePath/Analysis/Models/"
Move-Item "$basePath/Models/CommitRelationType.cs" "$basePath/Analysis/Models/"
Move-Item "$basePath/Models/CommitSignificance.cs" "$basePath/Analysis/Models/"
Move-Item "$basePath/Models/SignificantInfo.cs" "$basePath/Analysis/Models/"

# Move RepositoryInfo to Storage
Move-Item "$basePath/Models/RepositoryInfo.cs" "$basePath/Storage/Models/"

# Move Replay files
Move-Item "$basePath/Services/IReplayService.cs" "$basePath/Replay/Services/"
Move-Item "$basePath/Services/ReplayService.cs" "$basePath/Replay/Services/"
Move-Item "$basePath/Models/ReplayOptions.cs" "$basePath/Replay/Models/"
Move-Item "$basePath/Models/ReplaySession.cs" "$basePath/Replay/Models/"
Move-Item "$basePath/Models/ReplayState.cs" "$basePath/Replay/Models/"

# Remove empty folders (after manual service split)
Remove-Item "$basePath/Services" -Recurse -Force
Remove-Item "$basePath/Models" -Recurse -Force

Write-Host "Folder structure reorganized successfully!"
Write-Host "Next: Split RepositoryService (see Service Split Guide)"
```

### Service Split Guide

**Current `RepositoryService.cs` needs to be split into TWO services:**

#### IRepositoryStorageService (Storage domain)
```csharp
namespace Lanius.Business.Storage.Services;

public interface IRepositoryStorageService
{
    // Repository lifecycle management
    Task<RepositoryInfo> CloneRepositoryAsync(string url, CancellationToken cancellationToken = default);
    Task<bool> FetchUpdatesAsync(string repositoryId, CancellationToken cancellationToken = default);
    Task DeleteRepositoryAsync(string repositoryId);

    // Repository discovery
    Task<IEnumerable<RepositoryInfo>> ListRepositoriesAsync();
    bool RepositoryExists(string repositoryId);

    // Path management
    string GetRepositoryPath(string repositoryId);
    string GenerateRepositoryId(string url);
}
```

#### IRepositoryAnalyzer (Analysis domain)
```csharp
namespace Lanius.Business.Analysis.Services;

public interface IRepositoryAnalyzer
{
    // Repository metadata extraction
    Task<RepositoryMetadata?> GetRepositoryMetadataAsync(
        string repositoryId, 
        bool includeCommitCount = true);

    // Dependencies
    // - Uses IRepositoryStorageService to get repository path
    // - Uses LibGit2Sharp to read Git data
}
```

**Migration Steps**:
1. Create `IRepositoryStorageService.cs` with clone/fetch/delete/list methods
2. Create `RepositoryStorageService.cs` with implementation (copy from current)
3. Create `IRepositoryAnalyzer.cs` with metadata extraction
4. Create `RepositoryAnalyzer.cs` (move `GetRepositoryInfoAsync` logic here)
5. Inject `IRepositoryStorageService` into `RepositoryAnalyzer`
6. Update API controllers to use both services
7. Delete old `IRepositoryService.cs` and `RepositoryService.cs`

### Namespace Update Script

```powershell
# Update namespaces in moved files
function Update-Namespace {
    param($FilePath, $OldNamespace, $NewNamespace)

    $content = Get-Content $FilePath -Raw
    $content = $content -replace "namespace $OldNamespace", "namespace $NewNamespace"
    $content = $content -replace "using $OldNamespace", "using $NewNamespace"
    Set-Content $FilePath $content -NoNewline
}

# Update Analysis files
Get-ChildItem "$basePath/Analysis" -Recurse -Filter *.cs | ForEach-Object {
    if ($_.Directory.Name -eq "Services") {
        Update-Namespace $_.FullName "Lanius.Business.Services" "Lanius.Business.Analysis.Services"
    } else {
        Update-Namespace $_.FullName "Lanius.Business.Models" "Lanius.Business.Analysis.Models"
    }
}

# Update Replay files
Get-ChildItem "$basePath/Replay" -Recurse -Filter *.cs | ForEach-Object {
    if ($_.Directory.Name -eq "Services") {
        Update-Namespace $_.FullName "Lanius.Business.Services" "Lanius.Business.Replay.Services"
    } else {
        Update-Namespace $_.FullName "Lanius.Business.Models" "Lanius.Business.Replay.Models"
    }
}

Write-Host "Namespaces updated in moved files!"
Write-Host "Next: Fix using statements in Lanius.Api and tests"
```

### Test Project Updates

Tests should mirror the production structure:
```
Lanius.Business.Test/
  ├── Storage/
  │   └── RepositoryStorageServiceTests.cs
  ├── Analysis/
  │   ├── RepositoryAnalyzerTests.cs
  │   ├── CommitAnalyzerTests.cs
  │   └── BranchAnalyzerTests.cs
  ├── Layout/
  │   ├── LogicalLayoutEngineTests.cs
  │   └── CalendarLayoutEngineTests.cs
  └── Replay/
      └── ReplayServiceTests.cs
```

---

## Implementation Order (REVISED)

### Sprint 0: Prep Work (1 day)
**NEW**: Create Layout domain structure FIRST
- Create `Layout/Models/` and `Layout/Services/` folders
- Prevents need to move Layout files later

### Sprint 1: Foundation (5 days)
1. **Phase 1**: Layout Engine Abstractions in new structure (2-3 days)
2. **Phase 3**: Zoom and Pan (2 days)

**Milestone**: Layout domain created with proper structure from start

### Sprint 2: Complete Logical Layout (4 days)
3. **Phase 2**: Logical Layout with all commits (3-4 days)

**Milestone**: All Layout code in domain-driven structure

### Sprint 3: Progress & Calendar (8 days)
4. **Phase 5**: Progress Indicators (2-3 days)
5. **Phase 4**: Calendar Layout Engine (5-6 days)

**Milestone**: Complete Layout domain functional

### Sprint 4: Migration (4-5 days)
6. **Phase 6a**: Split RepositoryService into Storage domain (2 days)
7. **Phase 6b**: Migrate Replay domain (1 day)
8. **Phase 6c**: Migrate Analysis domain (1-2 days)

**Milestone**: All of Lanius.Business in domain-driven structure

---

## Next Steps

1. **Review this plan** with team/stakeholders
2. **Create GitHub issues** for each phase
3. **Start Phase 1** (Layout Engine Abstractions)
4. **Set up performance benchmarks** early
5. **Document API contracts** before implementation
