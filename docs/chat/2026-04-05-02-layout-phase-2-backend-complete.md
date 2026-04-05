# Phase 2 Backend Complete: Split & Merge Detection

**Date**: 2026-04-05  
**Status**: Backend Complete, Frontend Pending

## Summary

Phase 2 backend implementation is **complete**. The `LogicalLayoutEngine` now properly detects and creates three types of edges:
- **Normal**: Commits on the same branch
- **Branch**: Split points where a commit diverges from a parent branch
- **Merge**: Merge points where commits from multiple branches converge

## Implementation

### Enhanced Edge Detection

Rewrote `CalculateEdges` to:

1. **Build commit-to-branch mapping**: Each commit assigned to its "primary branch" (first occurrence)
2. **Detect cross-branch relationships first**:
   - Iterate through all commits
   - Check each parent commit
   - If parent is on different branch → create Branch or Merge edge
   - Merge edge: commit has multiple parents
   - Branch edge: commit has single parent from different branch
3. **Create same-branch edges second**:
   - Iterate through commits in each branch
   - Only create Normal edges if both commits belong to that branch
   - Skip if cross-branch edge already created

### Key Algorithm Change

**Before**: Created only Normal edges along each branch timeline  
**After**: Prioritizes cross-branch edges (Branch/Merge), then fills in Normal edges

This ensures splits and merges are correctly identified before drawing timeline connections.

## Tests Added

Two new tests validate split/merge detection:

### 1. `CalculateLayoutAsync_BranchSplit_CreatesBranchEdge`
- **Scenario**: Feature branch splits from main
- **Setup**: 
  - main1 commit on main branch
  - feature1 commit on feature branch with parent=main1
- **Assertion**: EdgeType.Branch from main1 → feature1

### 2. `CalculateLayoutAsync_MergeBranches_CreatesMergeEdge`  
- **Scenario**: Feature branch merges back to main
- **Setup**:
  - base1 commit (shared ancestor)
  - feature1 commit on feature branch
  - merge1 commit on main with parents=[base1, feature1]
- **Assertions**:
  - EdgeType.Merge from feature1 → merge1
  - Merge commit marked as significant (radius=6)

**Total Tests**: 11 (all passing)

## Test Results

```
✅ CalculateLayoutAsync_EmptyRepository_ReturnsEmptyResult
✅ CalculateLayoutAsync_SingleBranchSingleCommit_CreatesOneNode
✅ CalculateLayoutAsync_TwoCommitsOneBranch_CreatesNodesAndEdge
✅ CalculateLayoutAsync_MergeCommit_MarkedAsSignificant
✅ CalculateLayoutAsync_MultipleBranches_AssignsDifferentYLanes
✅ CalculateLayoutAsync_WithBranchFilter_FiltersCorrectly
✅ CalculateLayoutAsync_ReportsProgress
✅ CalculateLayoutAsync_NullRepositoryId_ThrowsArgumentException
✅ CalculateLayoutAsync_NullOptions_ThrowsArgumentNullException
✅ CalculateLayoutAsync_BranchSplit_CreatesBranchEdge (NEW)
✅ CalculateLayoutAsync_MergeBranches_CreatesMergeEdge (NEW)
```

## API Status

**Endpoint**: `GET /api/repository/{id}/layout?mode=logical&branchFilter=main,develop`

**Response includes**:
```json
{
  "nodes": [
    {
      "commitId": "abc123",
      "x": 100.5,
      "y": 40.0,
      "radius": 4,
      "branchName": "main",
      "timestamp": "2026-04-05T10:00:00Z",
      "message": "Commit message",
      "isSignificant": false
    }
  ],
  "edges": [
    {
      "fromCommitId": "abc123",
      "toCommitId": "def456",
      "type": "Branch",  // or "Normal", "Merge"
      "branchName": "feature",
      "points": [[100, 40], [120, 80]]
    }
  ],
  "width": 2000,
  "height": 600,
  "totalCommits": 42,
  "totalBranches": 3
}
```

## Remaining Work (Phase 2 Frontend)

### visualization.js Changes Needed

1. **Consume layout API** instead of current branch overview
   ```javascript
   const layoutData = await fetch(`/api/repository/${repoId}/layout?mode=logical`);
   ```

2. **Render nodes** as circles
   ```javascript
   svg.selectAll('circle.commit')
      .data(layoutData.nodes)
      .enter().append('circle')
        .attr('cx', d => d.x)
        .attr('cy', d => d.y)
        .attr('r', d => d.radius)
        .attr('class', d => d.isSignificant ? 'significant' : 'normal');
   ```

3. **Render edges** as lines with type-specific styles
   ```javascript
   svg.selectAll('line.edge')
      .data(layoutData.edges)
      .enter().append('line')
        .attr('x1', d => d.points[0][0])
        .attr('y1', d => d.points[0][1])
        .attr('x2', d => d.points[1][0])
        .attr('y2', d => d.points[1][1])
        .attr('class', d => `edge-${d.type.toLowerCase()}`);
   ```

4. **Add CSS styles**
   ```css
   .edge-normal { stroke: #ccc; stroke-width: 1px; }
   .edge-branch { stroke: #4CAF50; stroke-width: 2px; stroke-dasharray: 5,5; }
   .edge-merge { stroke: #FF9800; stroke-width: 2px; }
   .commit.significant { fill: #2196F3; }
   .commit.normal { fill: #ccc; }
   ```

## Next Steps

**Option A**: Continue with frontend (Phase 2 complete)
- Update `visualization.js` to consume layout API
- Render nodes and edges with D3.js
- Test with real repositories

**Option B**: Move to Phase 3 (Zoom & Pan)
- Add D3 zoom behavior to canvas
- Enable pan/drag navigation
- Come back to complete Phase 2 frontend after zoom is in place

**Recommendation**: Complete Phase 2 frontend first to validate the backend works end-to-end before adding zoom/pan complexity.

## Dependencies Removed

- Removed unused `IRepositoryService` dependency from `LogicalLayoutEngine`
- Now only depends on `ICommitAnalyzer` and `IBranchAnalyzer`
- Aligns with planned domain separation (Layout depends on Analysis, not Storage)

## Files Modified

### Business Logic
- `src/Lanius.Business/Layout/Services/LogicalLayoutEngine.cs`
  - Enhanced `CalculateEdges` method (100+ lines)
  - Removed unused `IRepositoryService` dependency

### Tests
- `src/Lanius.Business.Test/Layout/LogicalLayoutEngineTests.cs`
  - Added `SynchronousProgress<T>` helper (fixes race condition)
  - Added 2 new tests for split/merge detection
  - Removed unused `IRepositoryService` mock

### Documentation
- `docs/plans/layout-architecture-refactoring.md`
  - Updated Phase 2 status: Backend ✅, Frontend ❌
  - Added implementation details

## Performance Considerations

Current algorithm complexity:
- **Node creation**: O(n) where n = total commits
- **Edge creation**: O(n*p) where p = average parents per commit (typically 1-2)
- **Memory**: O(n) for node/edge storage

For 10,000 commits with average 1.5 parents:
- Expected time: < 500ms
- Expected memory: ~10MB

## Success Criteria Met

- ✅ All commits positioned correctly
- ✅ Branch lanes assigned  
- ✅ Split points detected (EdgeType.Branch)
- ✅ Merge points detected (EdgeType.Merge)
- ✅ Progress reporting works
- ✅ Unit tests comprehensive (11 tests)
- ✅ API endpoint functional
- ⚠️ Frontend rendering pending

**Phase 2 Backend**: **COMPLETE** ✅
