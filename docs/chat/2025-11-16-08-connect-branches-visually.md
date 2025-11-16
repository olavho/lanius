# Feature: Connect Branches with Visual Lines

**Date**: 2025-11-16  
**Type**: Visualization Enhancement

## Problem
Branches were displayed as isolated horizontal lines with dots, but there were no visual connections showing:
- Where branches **diverge from main** (split points)
- The **path from divergence to branch head**
- Relationship between commits on the same branch

## Solution
1. **Backend**: Add first commit on each branch (after divergence point) as a significant commit
2. **Frontend**: Draw connecting lines between commits on the same branch in chronological order

## Visual Concept

### Before
```
main:           ???????????????????????????
branch1:                          ?           (isolated, no connection)
branch2:        ?                              (isolated, no connection)
```

### After
```
main:           ???????????????????????????
                         \         
                          ??????????          (branch1: connected)
                 \
                  ?                           (branch2: connected)
```

## Implementation

### Backend Changes (`BranchAnalyzer.GetBranchOverviewAsync`)

**Added logic to find first commit on each branch:**
```csharp
// Walk from branch head back to merge base
var branchCommits = branch.Commits.TakeWhile(c => c.Sha != mergeBase.Sha).ToList();
if (branchCommits.Count > 0)
{
    var firstCommitOnBranch = branchCommits.Last(); // Last in list = first chronologically
    
    // Add as significant commit
    significantCommits[firstCommitOnBranch.Sha] = CreateSignificantCommit(
        firstCommitOnBranch,
        new List<string> { branch.FriendlyName },
        CommitSignificance.MergeBase);
}
```

**Result**: Each branch now has:
1. Merge base (divergence point on main)
2. First commit on branch (right after divergence)
3. Branch head (latest commit)

### Frontend Changes (`visualization.js`)

**Added branch connection rendering:**
1. Group commits by branch
2. Sort commits chronologically within each branch
3. Draw lines between consecutive commits on the same branch

```javascript
// For each branch, draw lines between its commits in chronological order
commitsByBranch.forEach((commits, branchName) => {
    const sortedCommits = commits.slice().sort((a, b) => 
        new Date(a.timestamp) - new Date(b.timestamp)
    );
    
    // Draw lines between consecutive commits
    for (let i = 0; i < sortedCommits.length - 1; i++) {
        branchLines.push({
            source: sortedCommits[i],
            target: sortedCommits[i + 1],
            branch: branchName
        });
    }
});
```

## Expected Visualization

### For Each Branch
1. **Merge base commit** - on main timeline (where branch diverged)
2. **Connection line** - from merge base to first commit on branch
3. **First commit** - positioned on branch's horizontal line
4. **Connection line** - from first commit to branch head (if different)
5. **Branch head** - latest commit on branch

### Example: origin/demo Branch
```
main:     ?????????????????????????
               \
                \                    (diagonal line from merge base)
                 ????????????????   (origin/demo: first commit ? head)
```

## Data Flow

### API Response Structure
```json
{
  "significantCommits": [
    // Main timeline commits
    { "sha": "abc", "branches": ["main"], "type": 0 },
    { "sha": "def", "branches": ["main"], "type": 0 },
    
    // Merge base (split point)
    { "sha": "ghi", "branches": ["main"], "type": 1 },
    
    // First commit on branch
    { "sha": "jkl", "branches": ["origin/demo"], "type": 1 },
    
    // Branch head
    { "sha": "mno", "branches": ["origin/demo"], "type": 0 }
  ],
  "relationships": [
    { "commitSha": "ghi", "branch1": "main", "branch2": "origin/demo", "type": "MergeBase" }
  ]
}
```

### Visualization Processing
1. **Group** commits by branch name
2. **Sort** within each group by timestamp
3. **Draw lines** connecting commits in chronological order
4. **Result**: Visual path showing branch evolution

## Commit Types

| Type | Significance | Position | Connected To |
|------|--------------|----------|--------------|
| 0 (BranchHead) | Branch tip | Branch line | Previous commit on branch |
| 1 (MergeBase) | Divergence point | Main timeline | First commit on child branch |
| 2 (Both) | Head that's also a split | Branch line | Previous on branch + child branches |

## Testing

### What to Look For
1. **Main branch** should show many commits connected in a line
2. **Each other branch** should show:
   - At least 2 commits (first + head)
   - Lines connecting them
   - Visual divergence from main
3. **Console logs** should show:
   - More significant commits than before (includes first commits)
   - Relationships between branches

### Expected Console Output
```
- Branches: 20
- Significant commits: ~150-200 (increased from ~120)
- Relationships: ~19
Commit types: { 0: ~140, 1: ~50, 2: 10 }
```

## Files Modified
- `src/Lanius.Business/Services/BranchAnalyzer.cs` - Add first commit on each branch
- `src/Lanius.Web/wwwroot/js/visualization.js` - Draw connection lines between commits

## Next Steps
1. **Stop API** (Ctrl+C)
2. **Restart API**: `dotnet run --project src/Lanius.Api`
3. **Refresh browser** (Ctrl+Shift+R)
4. **Click APPLY** with empty filter
5. **Check visualization** - should see connecting lines

## Future Enhancements
- **Curved lines** for branch divergence (diagonal paths)
- **Color-coded lines** by branch type (feature, release, etc.)
- **Animated branching** when replay is active
- **Merge commit visualization** (when branches merge back to main)
