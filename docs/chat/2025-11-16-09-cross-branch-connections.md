# Fix: Draw Cross-Branch Connection Lines

**Date**: 2025-11-16  
**Type**: Visualization Fix

## Problem
The visualization was showing:
- ? Main branch commits connected horizontally
- ? Other branch commits connected horizontally
- ? **No diagonal lines** from main to other branches (no visual branching)

## Root Cause
The visualization code was only drawing lines between commits **on the same branch**, but not drawing lines **from merge bases to first commits on child branches**.

The API was returning `relationships` data showing which commits are merge bases, but the frontend wasn't using it.

## Solution

### 1. Store Relationships in State (`app.js`)
```javascript
// Add to state
state.relationships = overview.relationships || [];
```

### 2. Draw Cross-Branch Connections (`visualization.js`)
```javascript
// For each relationship (merge base)
relationships.forEach(rel => {
    const mergeBaseCommit = commitMap.get(rel.commitSha); // On main
    const firstCommitOnBranch = branch2Commits[0]; // On child branch
    
    // Draw DIAGONAL LINE (dashed) from merge base to first commit
    g.append('line')
        .attr('x1', xScale(mergeBaseCommit.timestamp))
        .attr('y1', getCommitY(mergeBaseCommit, branchYMap)) // Y position on main
        .attr('x2', xScale(firstCommitOnBranch.timestamp))
        .attr('y2', getCommitY(firstCommitOnBranch, branchYMap)) // Y position on branch
        .attr('stroke-dasharray', '3,3') // Dashed line
        .attr('opacity', 0.4);
});
```

### 3. Bonus: Empty Default Filter (`index.html`)
Changed branch filter default from `value="main"` to `value=""` so all branches load by default.

## Visual Result

### Before
```
main:       ?????????????????
branch1:            ?????????    (isolated)
branch2:        ?????????????    (isolated)
```

### After
```
main:       ?????????????????
              ?     ?
               ?     ?????????  (branch1: connected)
                ?
                 ?????????????  (branch2: connected)
```

## Line Types

| Line Type | Appearance | Meaning |
|-----------|------------|---------|
| **Solid** | `?????` | Commits on same branch (chronological) |
| **Dashed** | `?????` | Branch divergence (merge base ? first commit) |

## Data Flow

### API Response
```json
{
  "relationships": [
    {
      "commitSha": "abc123",  // Merge base on main
      "branch1": "main",       // Parent branch
      "branch2": "origin/demo", // Child branch
      "type": "MergeBase"
    }
  ]
}
```

### Visualization Processing
1. Find merge base commit by SHA (on main)
2. Find first commit on child branch (sorted chronologically)
3. Draw diagonal dashed line from merge base to first commit
4. Draw solid line from first commit to branch head

## Implementation Details

### Cross-Branch Connection Algorithm
```javascript
// For each relationship
relationships.forEach(rel => {
    // 1. Get merge base commit (on parent branch - usually main)
    const mergeBase = commitMap.get(rel.commitSha);
    
    // 2. Find all commits on child branch, sort by time
    const branch2Commits = commitData
        .filter(c => c.branches.includes(rel.branch2))
        .sort((a, b) => new Date(a.timestamp) - new Date(b.timestamp));
    
    // 3. First commit = earliest timestamp on child branch
    const firstCommit = branch2Commits[0];
    
    // 4. Draw line: (mergeBase.x, mergeBase.y) ? (firstCommit.x, firstCommit.y)
    //    This creates the diagonal "branching off" visual
});
```

### Styling
- **Color**: Same as other links (`#4a4a4a`)
- **Width**: 1px (same as commit links)
- **Style**: Dashed (`stroke-dasharray: '3,3'`)
- **Opacity**: 0.4 (slightly more transparent than solid lines)

## Files Modified
- `src/Lanius.Web/wwwroot/js/app.js` - Store relationships in state
- `src/Lanius.Web/wwwroot/js/visualization.js` - Draw cross-branch connections
- `src/Lanius.Web/wwwroot/index.html` - Empty default branch filter

## Testing

### What to Look For
1. **Diagonal dashed lines** from main branch commits to other branches
2. **Solid lines** connecting commits within each branch
3. **Tree structure** clearly showing where branches diverge

### Console Logs
```
Drawing cross-branch connections for 19 relationships
Cross-branch: main ? origin/demo from abc1234 to def5678
Cross-branch: main ? origin/feature-x from 789abcd to efg0123
...
```

### Expected Visual
- Main branch: horizontal line with many commits
- Each other branch: 
  - Diagonal dashed line from main ? first commit on branch
  - Solid line from first commit ? branch head
  - Clear divergence point visible

## Browser Compatibility
- Uses SVG `stroke-dasharray` for dashed lines (supported in all modern browsers)
- Diagonal lines use standard SVG `<line>` element

## Next Steps
1. **Refresh browser** (Ctrl+Shift+R)
2. **Click APPLY** (with empty filter to load all branches)
3. **Check visualization** - should now see diagonal connecting lines!

## Future Enhancements
- **Curved paths** instead of straight diagonals (using SVG paths)
- **Color-coded branches** (different colors for feature, release, etc.)
- **Animated branching** during replay mode
- **Hover highlighting** of entire branch path
- **Merge back to main** visualization (when branches merge)
