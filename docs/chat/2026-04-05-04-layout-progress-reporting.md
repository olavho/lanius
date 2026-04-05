# Layout Progress Reporting Enhancement

**Date**: 2026-04-05  
**Status**: ✅ Complete  
**Related**: Phase 2 Layout Implementation

## Overview

Added real-time progress reporting for layout calculation operations via SignalR. This provides users with detailed feedback during the potentially long-running layout calculation process.

## Problem

The layout loading screen displayed only a static message: "Loading repository layout..." with no indication of progress. For large repositories with thousands of commits and hundreds of branches, this could take several seconds, leaving users uncertain about whether the operation was progressing or stalled.

## Solution

Implemented real-time progress reporting using existing SignalR infrastructure:

### Backend Changes

**LayoutController.cs**:
- Injected `IHubContext<RepositoryHub>` dependency
- Created `Progress<LayoutProgress>` reporter that broadcasts updates via SignalR
- Connected to `LayoutProgress` SignalR event for repository group

**LogicalLayoutEngine.cs** (existing):
- Already reports progress at key milestones:
  - 0%: "Loading branches"
  - 20%: "Loading commits from X branches"
  - 50%: "Calculating positions for X commits"
  - 80%: "Calculating branch line connections"
  - 100%: "Layout complete"

### Frontend Changes

**app.js**:
- Added `LayoutProgress` event listener in SignalR connection setup
- Implemented `handleLayoutProgress(progress)` handler that:
  - Formats progress messages based on operation and counts
  - Shows either "Operation: X/Y (Z%)" or "Operation (Z%)" format
  - Updates repo-status element in real-time
- Modified `loadRepository()` to subscribe to repository group before fetching layout

## Progress Message Format

The progress data includes:
```javascript
{
  percentage: 0-100,
  operation: "description",
  processedItems: number,
  totalItems: number
}
```

Display format:
- With items: `"Loading commits from 230 branches: 100/230 (20%)"`
- Without items: `"Loading branches (0%)"`

## Technical Details

### SignalR Group Subscription

The layout progress uses the existing repository group pattern:
- Group name: `repo:{repositoryId}`
- Client subscribes via `SubscribeToRepository(repositoryId)`
- Progress broadcasts to all connected clients watching that repository

### Progress Reporter

Uses `System.IProgress<T>` with inline callback:
```csharp
var progress = new Progress<LayoutProgress>(p =>
{
    hubContext.Clients.Group($"repo:{repositoryId}")
        .SendAsync("LayoutProgress", new
        {
            percentage = p.Percentage,
            operation = p.Operation,
            processedItems = p.ProcessedItems,
            totalItems = p.TotalItems
        }, cancellationToken);
});
```

### Connection State Check

Frontend verifies SignalR connection before subscribing:
```javascript
if (state.connection && state.connection.state === signalR.HubConnectionState.Connected) {
    await state.connection.invoke('SubscribeToRepository', state.repositoryId);
}
```

## User Experience

**Before**:
```
Loading repository layout...
```

**After** (example for 2500 commits, 230 branches):
```
Loading branches (0%)
Loading commits from 230 branches (20%)
Calculating positions for 2500 commits (50%)
Calculating branch line connections (80%)
Layout complete (100%)
Loaded layout: 2500 commits, 230 branches
```

## Benefits

1. **Transparency**: Users see exactly what the system is doing
2. **Progress indication**: Percentage and item counts show progress
3. **Reassurance**: Users know the operation hasn't stalled
4. **Diagnostics**: Operation names help identify bottlenecks
5. **Minimal overhead**: Uses existing SignalR connection
6. **Scalability**: Works with repositories of any size

## Testing

- ✅ Build successful
- ✅ All 11 LogicalLayoutEngine tests passing
- ✅ Backward compatible (progress parameter is optional)
- ✅ Graceful degradation (works without SignalR connection)

## Future Enhancements

Potential improvements:
1. **Progress bar**: Visual progress indicator instead of text
2. **Estimated time**: Calculate and show estimated completion time
3. **Cancellation**: Allow users to cancel long-running operations
4. **Detailed stats**: Show per-branch processing stats
5. **Throttling**: Limit progress update frequency for very large repositories

## Notes

- Progress reporting has minimal performance impact (fire-and-forget SignalR messages)
- The `IProgress<T>` interface is already tested in `CalculateLayoutAsync_ReportsProgress` test
- No breaking changes to existing APIs
- Works seamlessly with existing monitoring and replay features
