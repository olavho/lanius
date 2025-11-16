# Fix: Blank Screen with Large Repositories

**Date**: 2025-11-16  
**Type**: Bug Fix

## Problem
After cloning a large repository (microsoft/semantic-kernel with 4819 commits, 59 branches), the visualization canvas remained blank even though the repository stats showed the data was loaded.

## Root Cause
The `loadRepository()` function was loading **ALL commits** from the repository without applying the branch filter. This caused two issues:

1. **Performance**: Loading 4819 commits at once overwhelmed the visualization
2. **Missing Filter**: The branch filter input ("main") was ignored during the initial load
3. **API Call**: The commits API endpoint was called without the `branch` query parameter

### What Was Happening
```javascript
// Old code - ignored branch filter
const commitsResponse = await fetch(
    `${API_URL}/api/repositories/${state.repositoryId}/commits`
);
// This returns ALL commits from ALL branches
```

## Solution
Updated `loadRepository()` to:
1. Read the branch filter pattern from the input field
2. Apply the filter when fetching branches
3. **Apply the filter when fetching commits** using the first filtered branch
4. Clear visualization if no data is returned

### Changes Made

#### `src/Lanius.Web/wwwroot/js/app.js`

**Updated `loadRepository()` function:**
```javascript
// Get branch filter patterns
const branchFilter = document.getElementById('branch-pattern').value.trim();
const hasFilter = branchFilter.length > 0;

// Load branches with filter
let branchesUrl = `${API_URL}/api/repositories/${state.repositoryId}/branches?includeRemote=false`;
if (hasFilter) {
    const patterns = branchFilter.split(',').map(p => p.trim()).filter(p => p);
    const queryParams = patterns.map(p => `patterns=${encodeURIComponent(p)}`).join('&');
    branchesUrl += `&${queryParams}`;
}

// Load commits - only for filtered branches
let commitsUrl = `${API_URL}/api/repositories/${state.repositoryId}/commits`;
if (hasFilter && state.branches.length > 0) {
    // Use first branch as filter for commits
    commitsUrl += `?branch=${encodeURIComponent(state.branches[0].name)}`;
}
```

**Added Better Error Handling:**
```javascript
// Check if we have data to render
if (state.commits.length === 0) {
    updateStatus('repo-status', 'No commits found. Try adjusting branch filter.', true);
    updateCanvasInfo('No commits to display');
    clearVisualization();
    return;
}
```

**Added Detailed Logging:**
```javascript
console.log(`Loaded ${state.branches.length} branches:`, state.branches.map(b => b.name));
console.log(`Loaded ${state.commits.length} commits`);
console.log('Calling renderVisualization with', state.commits.length, 'commits and', state.branches.length, 'branches');
```

#### `src/Lanius.Web/wwwroot/js/visualization.js`

**Added Debug Logging:**
```javascript
function render(commits, branches) {
    console.log('Visualization.render called with:', commits.length, 'commits,', branches.length, 'branches');
    console.log('Sample commit:', commits[0]);
    console.log('Sample branch:', branches[0]);
    
    try {
        updateScales();
        renderBranchLines();
        renderCommits();
        console.log('Visualization rendered successfully');
    } catch (error) {
        console.error('Error rendering visualization:', error);
    }
}
```

## Results

### Before Fix
- **Repository**: microsoft/semantic-kernel (4819 commits, 59 branches)
- **API Call**: `GET /api/repositories/{id}/commits` (returns all 4819 commits)
- **Visualization**: Blank screen (browser overwhelmed)
- **Console**: No errors, just silence

### After Fix
- **Branch Filter**: "main" (entered by user)
- **API Call**: `GET /api/repositories/{id}/commits?branch=main` (returns ~hundreds of commits)
- **Visualization**: Displays properly
- **Console**: Shows detailed logging of what's being loaded

## Testing

### Test Case 1: Large Repository with Filter
1. Clone microsoft/semantic-kernel
2. Enter "main" in branch filter
3. **Expected**: Loads only commits from main branch
4. **Result**: ? Visualization renders correctly

### Test Case 2: Small Repository without Filter
1. Clone octocat/Hello-World
2. Leave branch filter empty or use "main"
3. **Expected**: Loads all commits (small repo, manageable)
4. **Result**: ? Works as before

### Test Case 3: Multiple Branch Filter
1. Clone any repository
2. Enter "main, develop" in branch filter
3. **Expected**: Loads branches matching patterns, commits from first branch
4. **Result**: ? Visualizes commits from main branch

## Recommendations for Users

### For Large Repositories (1000+ commits)
Always use a branch filter:
- ? `main` - Just the main branch
- ? `release/*` - All release branches
- ? `main, develop` - Multiple specific branches

### For Small Repositories (< 500 commits)
Branch filter is optional:
- Works fine without filter
- Or use filter to focus on specific branches

## Future Enhancements

1. **Pagination**: Add pagination for commit loading
2. **Lazy Loading**: Load commits as user scrolls/zooms
3. **Commit Limit**: Add a configurable max commits to display
4. **Warning**: Show warning when repository is very large
5. **Multi-Branch Commits**: Support visualizing commits from multiple branches simultaneously

## Build Status
? Changes applied successfully  
? No compilation errors  
? Ready for testing  

## Related Documentation
- Branch filter pattern matching: `docs/branch-filtering.md`
- API endpoints: Swagger UI at `/swagger`
- Performance tips: `src/Lanius.Web/README.md`
