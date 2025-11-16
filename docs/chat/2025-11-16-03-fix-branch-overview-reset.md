# Fix: Branch Overview Not Reset on Repository Change

**Date**: 2025-11-16  
**Type**: Bug Fix

## Problem
When cloning a new/different repository, the branch overview visualization was not being reset, causing the old repository's commits and branches to remain displayed alongside (or mixed with) the new repository's data.

## Root Cause
The frontend `cloneRepository()` function was not clearing the application state before loading the new repository. This meant:
- Old commits remained in `state.commits`
- Old branches remained in `state.branches`
- Visualization module retained old `commitData` and `branchData`
- Stats were not reset between repositories

## Solution
Added proper state management when switching repositories:

### 1. State Clearing Function (`app.js`)
Created `clearRepositoryState()` function that:
- Clears commits and branches arrays
- Resets replay session
- Clears visualization via `clearVisualization()`
- Resets stats to zero
- Updates UI elements to default state
- Logs the action for debugging

### 2. Repository Switch Detection
Modified `cloneRepository()` to:
- Detect when repository ID changes (new repository)
- Call `clearRepositoryState()` before loading new data
- Display appropriate status message:
  - "Repository updated" if `alreadyExisted` is true
  - "Cloned" if newly cloned

### 3. API Response Consistency
Updated `GetRepository` endpoint to include `AlreadyExisted` field (set to `false` for GET requests, as it's only meaningful for clone operations).

## Changes Made

### `src/Lanius.Web/wwwroot/js/app.js`

**Added `clearRepositoryState()` function:**
```javascript
function clearRepositoryState() {
    state.commits = [];
    state.branches = [];
    state.replaySessionId = null;
    clearVisualization();
    // Reset stats...
}
```

**Updated `cloneRepository()`:**
```javascript
// Clear previous repository state
if (state.repositoryId && state.repositoryId !== repo.id) {
    clearRepositoryState();
}

const statusMessage = repo.alreadyExisted 
    ? `Repository updated: ${repo.defaultBranch} (${repo.totalCommits} commits)`
    : `Cloned: ${repo.defaultBranch} (${repo.totalCommits} commits)`;
```

### `src/Lanius.Api/Controllers/RepositoryController.cs`

**Updated `GetRepository()` to include `AlreadyExisted` field:**
```csharp
return Ok(new RepositoryResponse
{
    // ...existing fields...
    AlreadyExisted = false // Always false for GET endpoint
});
```

## Testing Scenarios

### Scenario 1: Clone Repository A, then Clone Repository B
? **Expected:** Repository A's data is cleared, Repository B's data loads fresh  
? **Result:** Visualization shows only Repository B's commits and branches

### Scenario 2: Clone Repository A, then Clone Repository A again
? **Expected:** Fetch updates for Repository A, reload existing data  
? **Result:** Status shows "Repository updated", visualization refreshes

### Scenario 3: Clone Repository A, navigate away, come back
? **Expected:** State is preserved (unless page refreshes)  
? **Result:** Repository A remains loaded

## User Experience Improvements

**Before:**
- Cloning new repository showed mixed data from both repositories
- Stats accumulated across repositories
- Confusing visualization with overlapping branches

**After:**
- Clean transition between repositories
- Correct stats for current repository only
- Clear visual indication of repository switch
- Status message indicates whether repository was newly cloned or updated

## Build Status
? No compilation errors  
? JavaScript syntax valid  
? All changes applied successfully  

## Future Enhancements
- Add confirmation dialog when switching repositories if replay is active
- Persist current repository ID in localStorage
- Add repository switcher in UI to quickly return to previously cloned repos
- Consider caching repository data client-side for faster switching
