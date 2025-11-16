# Update: Timeline-Based Branch Visualization

**Date**: 2025-11-16  
**Type**: Design Refinement

## Issue with Previous Approach
The previous "significant commits" approach showed only 1 commit because:
- It only returned branch HEADS and MERGE BASES between pairs
- Didn't show the TIMELINE of commits on the main branch
- Didn't visualize how branches DIVERGE from main

## New Design: Timeline with Divergence Points

### Visual Concept
```
main:    o???o???o???o???o???o???o  (HEAD)
              ?       ?
              ?       ??o???o  (release/1.0)
              ?
              ??o???o???o  (develop)
```

### What to Show
1. **Main branch timeline**: Recent 100 commits from main branch (chronological)
2. **Divergence points**: Where other branches split from main (merge bases)
3. **Branch heads**: Latest commit on each branch
4. **Relationships**: Lines showing which branches diverged from which points

## Implementation Changes

### Backend (`BranchAnalyzer.GetBranchOverviewAsync`)

**New Algorithm**:
1. Identify main branch (prefer "main", then "master", then first branch)
2. Load recent 100 commits from main branch (forms the backbone/timeline)
3. For each other branch:
   - Find its HEAD commit
   - Find MERGE BASE with main (divergence point)
   - Mark merge base as a split point
   - Create relationship: branch diverged from main at commit X

**Result**: Returns ~100 commits from main + heads of other branches + relationships

### Frontend Updates Needed

The visualization needs to:
1. Place main branch horizontally across the canvas (left to right = old to new)
2. For each divergence point, draw a branch line upward/downward
3. Position branch heads at the end of their branch lines
4. Use different colors/shapes for:
   - Regular commits on main
   - Divergence points (split points)
   - Branch heads

## Testing Instructions

1. **Stop the running API** (Ctrl+C in terminal where `dotnet run` is active)

2. **Rebuild the solution**:
```bash
dotnet build "C:\local\private\Lanius\Lanius.slnx"
```

3. **Restart the API**:
```bash
dotnet run --project src/Lanius.Api
```

4. **Refresh browser** (hard refresh: Ctrl+Shift+R)

5. **Clear branch filter** (leave it empty or just "main")

6. **Click "APPLY"**

7. **Check browser console** - should now see:
```
Loaded X branches and ~100-150 significant commits
- Branches: ['main', 'develop', 'release/1.0', ...]
- Significant commits: 120
- Relationships: 5
```

## Expected Visualization

### With Filter: "main" only
- Shows ~100 recent commits from main branch
- Horizontal timeline
- All commits in a single line

### With Filter: "main, release/*"
- Shows main branch timeline (~100 commits)
- Shows release branch heads
- Shows where release branches diverged from main
- Draws lines from divergence points to branch heads

### Without Filter (all branches)
- Shows main branch timeline
- Shows ALL local branches
- Shows all divergence points
- Creates a tree-like structure

## Current Status

### What Was Changed
? `BranchAnalyzer.GetBranchOverviewAsync` - Now loads main timeline + divergence points
? `app.js` - Enhanced logging to see what data is loaded
? `visualization.js` - Still needs update to properly render diverging branches

### What Still Needs Work

The visualization.js currently:
- Places commits based on `branches` array
- Doesn't understand the concept of "main timeline"
- Doesn't render diverging branch lines

**Next Step**: Update visualization.js to:
1. Identify main branch from data
2. Render main commits horizontally
3. Render diverging branches vertically from split points
4. Draw lines connecting divergence points to branch heads

## Data Structure Returned

```json
{
  "branches": [
    { "name": "main", "headSha": "abc123", "headTimestamp": "..." },
    { "name": "develop", "headSha": "def456", "headTimestamp": "..." }
  ],
  "significantCommits": [
    // ~100 commits from main timeline
    { "sha": "aaa", "branches": ["main"], "type": "BranchHead", ... },
    { "sha": "bbb", "branches": ["main"], "type": "MergeBase", ... }, // Divergence point
    // Branch heads
    { "sha": "def456", "branches": ["develop"], "type": "BranchHead", ... }
  ],
  "relationships": [
    { "commitSha": "bbb", "branch1": "main", "branch2": "develop", "type": "MergeBase" }
  ]
}
```

## Future Enhancements

1. **Adjustable timeline depth**: Allow user to set how many commits to show (50, 100, 500)
2. **Branch path visualization**: Show full commit path from divergence to head
3. **Interactive split points**: Click to see diff stats between divergence and head
4. **Color coding**: Different colors for different branch types
5. **Zoom controls**: Pan and zoom on large timelines

## Files Modified
- `src/Lanius.Business/Services/BranchAnalyzer.cs` - Updated GetBranchOverviewAsync
- `src/Lanius.Web/wwwroot/js/app.js` - Enhanced logging
- `docs/chat/2025-11-16-06-timeline-visualization.md` - This document

## Next Actions
1. Stop API
2. Rebuild
3. Restart API  
4. Test in browser
5. Check console logs
6. Update visualization.js if rendering is still not correct
