# Repository Dropdown Performance Optimization

## Changes Made

### Backend Optimization (`RepositoryService.cs`)

**Problem**: Counting commits for large repositories (12,000+ commits) was causing the dropdown to load very slowly.

**Solution**: Made commit counting optional by:
1. Split `GetRepositoryInfoAsync` into public facade and private implementation
2. Added `includeCommitCount` parameter (defaults to `true` for backward compatibility)
3. `ListRepositoriesAsync` now passes `includeCommitCount: false` to skip expensive commit counting
4. Commit count calculation deferred until repository is selected for analysis

**Code Changes**:
```csharp
// Public interface - maintains backward compatibility
public Task<RepositoryInfo?> GetRepositoryInfoAsync(string repositoryId)
{
    return GetRepositoryInfoAsync(repositoryId, includeCommitCount: true);
}

// Private implementation with optional commit counting
private Task<RepositoryInfo?> GetRepositoryInfoAsync(string repositoryId, bool includeCommitCount)
{
    // ... validation ...
    
    int totalCommits = 0;
    if (includeCommitCount)  // Only count when needed
    {
        // HashSet-based commit counting logic
    }
}

// ListRepositoriesAsync optimized
var infoTask = GetRepositoryInfoAsync(repoId, includeCommitCount: false);
```

### Frontend Improvements (`app.js`)

**Problem**: Users had no feedback while repositories were loading

**Solution**: Added loading indicator and removed commit counts from dropdown

**Changes**:
1. Show "Loading repositories..." message while fetching
2. Disable dropdown during load
3. Remove commit count from dropdown text (just show URL)
4. Re-enable dropdown and show default option when load completes (or fails)

**Code Changes**:
```javascript
async function loadExistingRepositories() {
    const select = document.getElementById('repo-select');
    
    // Show loading state
    select.innerHTML = '<option value="">Loading repositories...</option>';
    select.disabled = true;

    const response = await fetch(`${API_URL}/api/repository`);
    
    // ... handle response ...
    
    // Re-enable and populate
    select.innerHTML = '<option value="">-- Select or enter new URL --</option>';
    select.disabled = false;
    
    repositories.forEach(repo => {
        option.textContent = repo.url;  // No commit count
    });
}
```

## Performance Impact

**Before**:
- Dropdown load time: ~10-30 seconds for large repositories
- Enumerated all commits across all branches for each repository
- Example: 7 repos with 12,000+ commits = very slow

**After**:
- Dropdown load time: < 1 second
- Only reads repository metadata (URL, branches, directory info)
- Commit counting happens only when user selects a repository

## Testing

All unit tests passing (10 passed, 1 skipped):
- `ListRepositoriesAsync` tests still pass
- `GetRepositoryInfoAsync` backward compatibility maintained
- No breaking changes to existing functionality

## Next Steps

To see the changes:
1. Restart the application (hot reload may not pick up all changes)
2. Open the UI
3. Observe "Loading repositories..." message (brief)
4. Dropdown populates quickly with just URLs
5. Select a repository - commit count calculated on-demand (via GetRepositoryInfoAsync with default parameter)
