# Fix: Include Remote Branches in Visualization

**Date**: 2025-11-16  
**Type**: Configuration Fix

## Problem
Only showing 1 branch (`main`) even though repository has 59 branches total.

## Root Cause
- Repository has **1 local branch** (`main`)  
- Repository has **58 remote branches** (`origin/*`)
- API endpoint had `includeRemote=false` as default
- Frontend was explicitly passing `includeRemote=false`

## Solution
1. **Changed API default**: `includeRemote=true` (include remote branches by default)
2. **Removed frontend override**: Let server use its default
3. **Added branch limit**: Limit to 20 branches for performance (59 would be too many)
4. **Enhanced logging**: Show which branches are being loaded

## Changes

### `BranchesController.cs`
```csharp
// Before
[FromQuery] bool includeRemote = false

// After  
[FromQuery] bool includeRemote = true

// Also added
var limitedBranches = branches.Take(20).ToList();
```

### `app.js`
```javascript
// Before
let overviewUrl = `${API_URL}/api/repositories/${state.repositoryId}/branches/overview?includeRemote=false`;

// After (let server decide)
let overviewUrl = `${API_URL}/api/repositories/${state.repositoryId}/branches/overview`;
```

## Expected Result
After restart:
- Loads **up to 20 branches** (mix of local + remote)
- Shows **main branch timeline** (100 commits)
- Shows **branch heads** for up to 19 other branches
- Shows **merge bases** (divergence points)
- Visualizes branch structure properly

## Next Steps
1. **Stop API** (Ctrl+C)
2. **Restart API**: `dotnet run --project src/Lanius.Api`
3. **Refresh browser** (Ctrl+Shift+R)
4. **Clear branch filter** (empty)
5. **Click APPLY**
6. **Check console** - should see:
```
- Branches: 20 First 5: ['main', 'origin/agent-hosting...', ...]
- Significant commits: ~120-150
- Relationships: ~19
```

## Alternative: Use Branch Filter
If you want specific branches:
- `main, origin/feature-*` - Main + all feature branches
- `main, origin/release-*` - Main + release branches  
- `origin/experimental-*` - Just experimental branches

## Files Modified
- `src/Lanius.Api/Controllers/BranchesController.cs`
- `src/Lanius.Web/wwwroot/js/app.js`
