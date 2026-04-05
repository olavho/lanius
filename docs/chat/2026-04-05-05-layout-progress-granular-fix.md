# Layout Progress Reporting - Fix for Granular Updates

**Date**: 2026-04-05  
**Status**: ✅ Complete  
**Related**: Layout Progress Reporting Enhancement

## Problem

The progress reporting was getting stuck at 20% and showing misleading counts:
```
{percentage: 0, operation: 'Loading branches', processedItems: 0, totalItems: 0}
{percentage: 20, operation: 'Loading commits from 1 branches', processedItems: 0, totalItems: 0}
```

**Issues**:
1. Progress stuck at 20% during the longest operation (loading commits from all branches)
2. `processedItems` and `totalItems` hardcoded to 0
3. No feedback during the commit loading phase
4. Users couldn't tell if the operation was progressing or stalled

## Root Cause

The `LoadAllCommitsAsync` method was:
- Not accepting or reporting progress
- Processing all branches in a silent loop
- Taking the majority of time (20-50% of total operation) without updates

The progress percentages were also poorly distributed:
- 0%: Loading branches (fast)
- 20%: Loading commits (SLOW - could take 80% of actual time)
- 50%: Calculating positions (fast)
- 80%: Calculating edges (fast)
- 100%: Complete

## Solution

### 1. Updated Progress Percentages

Better distribution matching actual operation times:
- **0-10%**: Loading branches
- **10-50%**: Loading commits from each branch (granular updates)
- **50-80%**: Calculating node positions
- **80-100%**: Calculating edges

### 2. Granular Commit Loading Progress

Modified `LoadAllCommitsAsync` to:
- Accept `IProgress<LayoutProgress>` parameter
- Report progress after each branch is processed
- Show `processedBranches/totalBranches` counts
- Calculate percentage linearly across 10-50% range

```csharp
foreach (var branch in branches)
{
    var commits = await commitAnalyzer.GetCommitsAsync(...);
    result[branch.Name] = [.. commits];
    processedBranches++;

    // Report: 10% -> 50% range
    int percentage = 10 + (int)((processedBranches / (double)totalBranches) * 40);
    progress?.Report(new LayoutProgress(
        percentage,
        $"Loading commits from branch {processedBranches}/{totalBranches}",
        processedBranches,
        totalBranches));
}
```

### 3. Meaningful Counts Throughout

All progress reports now include actual counts:

| Stage | Percentage | Operation | ProcessedItems | TotalItems |
|-------|------------|-----------|----------------|------------|
| Start | 0% | "Loading branches" | 0 | 0 |
| Branches loaded | 10% | "Found X branches" | X | X |
| Loading commits | 10-50% | "Loading commits from branch N/X" | N | X |
| Commits loaded | 50% | "Loaded Y commits" | Y | Y |
| Positions calculated | 80% | "Calculating edges for Y nodes" | Y | Y |
| Complete | 100% | "Layout complete: Y commits, X branches" | Y | Y |

## User Experience Improvement

**Before**:
```
Loading branches (0%)
Loading commits from 230 branches (20%)
[STUCK HERE FOR 10+ SECONDS - no updates]
Layout complete (100%)
```

**After**:
```
Loading branches (0%)
Found 230 branches (10%)
Loading commits from branch 1/230 (10%)
Loading commits from branch 50/230 (18%)
Loading commits from branch 100/230 (27%)
Loading commits from branch 150/230 (36%)
Loading commits from branch 200/230 (44%)
Loading commits from branch 230/230 (50%)
Loaded 2500 commits (50%)
Calculating edges for 2500 nodes (80%)
Layout complete: 2500 commits, 230 branches (100%)
```

## Technical Details

### Progress Calculation Formula

```csharp
// For commit loading (10% -> 50% range):
int percentage = 10 + (int)((processedBranches / (double)totalBranches) * 40);
```

This distributes 40 percentage points across all branches, ensuring smooth progress from 10% to 50%.

### SignalR Update Frequency

For a repository with 230 branches:
- **230 progress updates** during commit loading phase
- May need throttling for very large repos (1000+ branches)
- Currently: ~2-5 updates per second (acceptable)

## Testing

- ✅ Build successful
- ✅ All 11 LogicalLayoutEngine tests passing
- ✅ Progress reporting test still valid
- ✅ Backward compatible (progress is optional)

## Benefits

1. **Real-time feedback**: Users see continuous progress
2. **Accurate counts**: ProcessedItems/TotalItems show actual work
3. **Better UX**: No more "stuck" feeling during long operations
4. **Debugging aid**: Can identify slow branches or bottlenecks
5. **Scalability**: Works for repositories of any size

## Potential Future Enhancements

1. **Throttling**: Limit updates to max 1 per 100ms for huge repos
2. **Estimated time**: Calculate ETA based on average branch processing time
3. **Parallel loading**: Load multiple branches concurrently
4. **Caching**: Cache commit data to speed up subsequent loads
5. **Incremental loading**: Load only new commits since last fetch

## Notes

- For very large repositories (1000+ branches), consider adding update throttling
- The linear distribution (10-50%) assumes branches have similar commit counts
- If branches vary wildly in size, consider weight-based progress calculation
- No breaking changes to existing tests or APIs
