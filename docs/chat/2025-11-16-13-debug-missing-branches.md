# Debug: Only 2 Branches Showing

**Date**: 2025-11-16  
**Type**: Investigation

## Problem
After recent changes, only 2 branches are displaying instead of 20. Timeline is also taking too much vertical space.

## Symptoms
- Header shows "131 significant commits (20 branches)"
- Only 2 branches visible: `main` and `agent-hosting-use-configura...`
- Timeline shows months but year labels may not be visible
- Most branches seem to be filtered out during rendering

## Hypothesis
The filtering logic in `renderBranchLines()` is finding zero commits for most branches:
```javascript
const branchCommits = commitData.filter(c => 
    c.branches && c.branches.includes(branch.name)
);
```

This could happen if:
1. Branch names in `branchData` don't match branch names in `commitData.branches` arrays
2. Most commits only belong to `main` and one other branch
3. Data mismatch between API response and frontend state

## Debug Steps

### 1. Check Console Output
Look for these messages in browser console (F12):
```
=== Visualization.render START ===
Input: 131 commits, 20 branches
All branch names: [array of 20 branch names]
Rendering 20 branches
Branch main: X commits
Branch agent-hosting...: Y commits
Branch xxx: 0 commits  ? Look for many of these!
```

### 2. Check Data Consistency
Verify that:
- `state.branches` has 20 entries
- `state.commits` has 131 entries  
- Commits have `branches` arrays populated
- Branch names match between branches and commits

### 3. Check Timeline Positioning
Year labels should appear at Y = -20 (above branches).
If not visible, they might be:
- Off-screen above
- Hidden by CSS
- Rendering with wrong coordinates

## Likely Root Cause

**Theory**: The `GetBranchOverviewAsync` backend method loads commits from the **main branch timeline** (100 commits) plus **head commits** from other branches. But it doesn't load the **first commit** on each branch properly, so most branches end up with zero commits in the visualization dataset.

### Evidence
From `BranchAnalyzer.cs`:
```csharp
// Add commits from main branch timeline
var mainCommits = mainBranch.Commits.Take(100).ToList();
foreach (var commit in mainCommits)
{
    significantCommits[commit.Sha] = CreateSignificantCommit(
        commit,
        new List<string> { mainBranch.FriendlyName },  ? Only main branch!
        CommitSignificance.BranchHead);
}
```

The main timeline commits are only tagged with `main` branch, not with all branches they belong to!

## Solution

### Option 1: Include All Branches for Each Commit
When creating commits from the main timeline, check **all branches** that contain that commit:
```csharp
foreach (var commit in mainCommits)
{
    var branchesForCommit = GetBranchesForCommit(repo, commit); // Check all branches
    significantCommits[commit.Sha] = CreateSignificantCommit(
        commit,
        branchesForCommit,
        CommitSignificance.BranchHead);
}
```

### Option 2: Include More Commits Per Branch
For each branch (not just main), include several recent commits:
```csharp
foreach (var branch in branches.Where(b => b != mainBranch))
{
    // Add last 5-10 commits from this branch
    var recentCommits = branch.Commits.Take(10).ToList();
    foreach (var commit in recentCommits)
    {
        if (!significantCommits.ContainsKey(commit.Sha))
        {
            significantCommits[commit.Sha] = CreateSignificantCommit(
                commit,
                new List<string> { branch.FriendlyName },
                CommitSignificance.BranchHead);
        }
    }
}
```

## Immediate Fix

**Quick Test**: Check if the problem is in the backend or frontend.

### Backend Test
Call the API directly and inspect the response:
```
GET /api/repositories/{id}/branches/overview
```

Check the `significantCommits` array:
- How many commits have `branches` arrays with multiple branch names?
- Do most commits only list `"main"`?

### Frontend Test  
In browser console, check:
```javascript
console.log('Branches:', window.LaniusApp.state.branches.map(b => b.name));
console.log('Sample commits:', window.LaniusApp.state.commits.slice(0, 10).map(c => ({ 
    sha: c.sha.substring(0, 7), 
    branches: c.branches 
})));
```

## Timeline Fixes Applied
- **Year labels**: Moved to Y = -20 (closer to branches)
- **Month labels**: Moved to Y = -5 (very close)
- **Year lines**: Start at Y = -10 (just above branches)
- **Month lines**: Very light, opacity 0.15

## Next Steps
1. **Refresh browser** and check console
2. **Copy console output** showing branch/commit counts
3. **Check API response** to see if backend is tagging commits correctly
4. **Implement fix** based on root cause identified

## Files to Check
- `src/Lanius.Business/Services/BranchAnalyzer.cs` - GetBranchOverviewAsync method
- `src/Lanius.Web/wwwroot/js/app.js` - State mapping from API response
- `src/Lanius.Web/wwwroot/js/visualization.js` - Branch rendering logic
