# Critical Bugs Found - Phase 3 Zoom/Pan

**Date**: 2026-04-05  
**Status**: Critical - Must fix before Phase 4  
**Reported By**: User testing with 245 commits (11 branches) and 5331 commits (78 branches)

## Issues Summary

### 1. **Severe Performance Problem** ⚠️ CRITICAL
- **Symptom**: 15 seconds for 245 commits, 30+ seconds stuck at 10% for 5331 commits
- **Root Cause**: `GetCommitsForBranch()` enumerates entire branch commit history via LibGit2Sharp
- **Impact**: Unusable for repositories with >1000 commits

### 2. **Only One Branch Displayed** ⚠️ CRITICAL  
- **Symptom**: Layout shows commits from only one branch
- **Root Cause**: `GetBranchesForCommit()` uses `Any(c => c.Sha == commit.Sha)` which is O(branches × commits)
- **Impact**: 78 branches × 5331 commits = 415,818 operations per layout calculation

### 3. **Missing Visual Elements** ⚠️ HIGH
- **Symptom**: No branch indicators, no lines between commits
- **Root Cause**: `renderLayout()` doesn't call `renderBranchLines()` or render branch indicators
- **Impact**: Visualization incomplete, regression from Phase 2

### 4. **Overlapping Commits** ⚠️ MEDIUM
- **Symptom**: Commits bunched together in clusters
- **Root Cause**: Likely X-positioning issue in `CalculateNodePositions()`
- **Impact**: Readability poor, especially for dense time periods

---

## Detailed Analysis

### Issue 1: Performance - `GetCommitsForBranch()`

**Location**: `src/Lanius.Business/Services/CommitAnalyzer.cs:148`

```csharp
private static List<GitCommit> GetCommitsForBranch(Repository repo, string branchName)
{
    var branch = repo.Branches[branchName]
        ?? throw new InvalidOperationException($"Branch not found: {branchName}");

    return [.. branch.Commits]; // ← SLOW: Enumerates entire branch history
}
```

**Problem**:
- `branch.Commits` is a LibGit2Sharp `IQueryableCommitLog`
- Calling `[.. branch.Commits]` forces full enumeration
- For `main` branch with 5000 commits, this takes **seconds**
- Called once per branch in `LoadAllCommitsAsync()` loop

**Call Stack**:
```
LogicalLayoutEngine.LoadAllCommitsAsync()
  → CommitAnalyzer.GetCommitsAsync(branchName)
    → GetCommitsForBranch(repo, branchName)  ← SLOW
      → [.. branch.Commits] ← Enumerates 5000+ commits
```

**Performance Impact**:
- 11 branches × ~200 commits/branch = **2 seconds**
- 78 branches × ~68 commits/branch = **30+ seconds**
- Large `main` branch (5000 commits) = **10+ seconds alone**

**Fix Options**:
1. **Temporary**: Limit to recent N commits (e.g., 500)
   ```csharp
   return [.. branch.Commits.Take(500)];
   ```
2. **Better**: Use LibGit2Sharp `QueryCommits` with filters
3. **Best**: Cache commit graph, don't reload every time

---

### Issue 2: Only One Branch - `GetBranchesForCommit()`

**Location**: `src/Lanius.Business/Services/CommitAnalyzer.cs:151-159`

```csharp
private static List<string> GetBranchesForCommit(Repository repo, GitCommit commit)
{
    var branches = repo.Branches
        .Where(b => b.Commits.Any(c => c.Sha == commit.Sha)) // ← VERY SLOW
        .Select(b => b.FriendlyName)
        .ToList();

    return branches;
}
```

**Problem**:
- For EACH commit, iterates through ALL branches
- For each branch, enumerates commits to find matching SHA
- Complexity: **O(commits × branches × commits_per_branch)**
- Example: 5331 commits × 78 branches × 68 avg commits/branch = **28 million comparisons**

**Why Only One Branch Shows**:
- The operation is so slow that it likely times out or hangs
- Or the frontend gives up waiting and only renders partial data
- The layout API might be returning an error that's not handled

**Fix Options**:
1. **Temporary**: Only return first branch found
   ```csharp
   return [.. repo.Branches.First(b => b.Commits.Any(c => c.Sha == commit.Sha)).FriendlyName];
   ```
2. **Better**: Build branch→commit map once, then lookup
   ```csharp
   var branchMap = repo.Branches.ToDictionary(b => b.FriendlyName, b => new HashSet<string>(b.Commits.Select(c => c.Sha)));
   // Then lookup: branchMap.Where(kvp => kvp.Value.Contains(commit.Sha))
   ```
3. **Best**: Use `git branch --contains <sha>` command directly

---

### Issue 3: Missing Visual Elements

**Location**: `src/Lanius.Web/wwwroot/js/visualization.js:682-755`

**Current `renderLayout()` implementation**:
```javascript
function renderLayout(layout) {
    g.selectAll('*').remove();  // Clear all
    
    // Render edges
    // Render nodes
    
    // ← Missing: Branch lines, branch indicators, timeline grid
}
```

**Comparison with old `render()` method**:
```javascript
function render(commits, branches) {
    renderTimelineGrid();    // ✓ Has this
    renderBranchLines();     // ✓ Has this (with branch indicators)
    renderCommits();         // ✓ Has this
}
```

**What's missing**:
1. **Branch line rendering** (horizontal lines showing branch lifetime)
2. **Branch indicators** (colored boxes at start of each branch)
3. **Timeline grid** (year/month markers)
4. **Legend** (edge types: Normal, Branch, Merge)

**Fix**:
- Add branch line rendering to `renderLayout()`
- Extract branch info from `layout.nodes`
- Render branch indicators based on node grouping

---

### Issue 4: Overlapping Commits

**Location**: `src/Lanius.Business/Layout/Services/LogicalLayoutEngine.cs:135-175`

**Suspected issue in `CalculateNodePositions()`**:
```csharp
private static List<LayoutNode> CalculateNodePositions(...)
{
    // X position based on timestamp
    var minTime = allCommits.Min(c => c.Timestamp).ToUnixTimeSeconds();
    var maxTime = allCommits.Max(c => c.Timestamp).ToUnixTimeSeconds();
    var timeRange = maxTime - minTime;
    
    double x = options.MarginX + ((commitTime - minTime) / (double)timeRange) * (options.MaxWidth - 2 * options.MarginX);
    
    // ← If timeRange is large but commits are clustered, X positions will overlap
}
```

**Possible causes**:
1. **Time clustering**: Many commits in short time period (e.g., bulk merge)
2. **MaxWidth too small**: 2000px for thousands of commits = <1px per commit
3. **No collision detection**: Commits can occupy same X coordinate

**Fix Options**:
1. **Increase MaxWidth**: Scale based on commit count
2. **Add jitter**: Slightly offset overlapping commits vertically
3. **Aggregate clusters**: Group commits within same time window

---

## Recommended Fix Priority

### Phase 1: Performance Fixes (CRITICAL - Do Now)
**Goal**: Make application usable for >1000 commits

1. ✅ **Limit commits per branch** (quick win)
   - Add `.Take(1000)` to `GetCommitsForBranch()`
   - Document as temporary workaround
   - **Time**: 5 minutes

2. ✅ **Cache branch→commit mapping** (medium effort)
   - Build map once in `MapCommit()`
   - Store in dictionary for lookup
   - **Time**: 30 minutes

3. ⚠️ **Remove `GetBranchesForCommit()` calls** (if possible)
   - Check if branch info is actually needed for layout
   - Or use simpler "primary branch" assignment
   - **Time**: 15 minutes

**Total Phase 1 Time**: ~1 hour

---

### Phase 2: Visual Fixes (HIGH - Do Before Phase 4)
**Goal**: Restore feature parity with pre-layout-API visualization

1. ✅ **Add branch line rendering** to `renderLayout()`
   - Extract branch ranges from nodes
   - Draw horizontal lines
   - **Time**: 30 minutes

2. ✅ **Add branch indicators**
   - Colored boxes at branch start
   - Hover tooltips with branch names
   - **Time**: 20 minutes

3. ⚠️ **Add timeline grid** (optional)
   - Year/month markers
   - Can defer if time-constrained
   - **Time**: 20 minutes

**Total Phase 2 Time**: ~1 hour

---

### Phase 3: Positioning Fixes (MEDIUM - Can Defer)
**Goal**: Improve readability for dense commit clusters

1. **Increase MaxWidth dynamically**
   - Scale based on commit count
   - **Time**: 15 minutes

2. **Add collision detection** (optional)
   - Detect overlapping nodes
   - Apply vertical jitter
   - **Time**: 45 minutes

**Total Phase 3 Time**: ~1 hour

---

## Total Estimated Fix Time

- **Minimum (Critical only)**: 1 hour
- **Recommended (Critical + Visual)**: 2 hours
- **Complete (All fixes)**: 3 hours

---

## Decision

**Recommendation**: Fix Phase 1 (Performance) and Phase 2 (Visual) before proceeding to Phase 4.

**Rationale**:
- Current implementation is **unusable** for real repositories
- Missing visual elements are a **regression** from Phase 2
- Phase 4 (Calendar Layout) will compound performance issues
- 2 hours of fixes now saves days of debugging later

**Alternative**: If time-constrained, apply quick wins only:
1. Add `.Take(1000)` limit (5 minutes)
2. Remove `GetBranchesForCommit()` calls (15 minutes)
3. Add branch line rendering (30 minutes)
**Total**: 50 minutes for 80% of value

---

## Next Steps

1. **User Decision**: Proceed with fixes now or defer?
2. **If fixing now**: Implement Phase 1 + Phase 2 (2 hours)
3. **If deferring**: Document limitations, proceed to Phase 4 with `.Take(1000)` workaround
4. **After fixes**: Re-test with 245 and 5331 commit repositories
5. **Update documentation**: Phase 3 status, known limitations

---

## References

- **User Report**: "15 seconds for 245 commits, stuck at 10% for 5331 commits"
- **Implementation Plan**: `docs/plans/layout-architecture-refactoring.md`
- **Phase 3 Completion Summary**: `docs/chat/2026-04-05-07-phase-3-complete.md`
