# Repository Dropdown Feature

**Date**: 2025-01-19  
**Topic**: Add dropdown to display existing repositories

## Summary

Added capability to display and select from already cloned repositories when opening the application, improving UX by eliminating the need to re-enter URLs for existing repositories.

## Changes Made

### Backend (Business Layer)

**File**: `src/Lanius.Business/Services/IRepositoryService.cs`
- Added `ListRepositoriesAsync()` method to interface

**File**: `src/Lanius.Business/Services/RepositoryService.cs`
- Implemented `ListRepositoriesAsync()` to enumerate all valid Git repositories in storage
- Returns repositories sorted by most recently used (LastFetchedAt or ClonedAt)
- Gracefully handles invalid directories

### Backend (API Layer)

**File**: `src/Lanius.Api/Controllers/RepositoryController.cs`
- Added `GET /api/repository` endpoint to list all repositories
- Returns `IEnumerable<RepositoryResponse>` with repository metadata

### Frontend

**File**: `src/Lanius.Web/wwwroot/index.html`
- Added dropdown (`<select id="repo-select">`) above URL input field
- Displays "-- Select or enter new URL --" as default option

**File**: `src/Lanius.Web/wwwroot/js/app.js`
- Added `loadExistingRepositories()` - fetches and populates dropdown on page load
- Added `onRepositorySelected()` - handles repository selection from dropdown
- Modified `cloneRepository()` - refreshes dropdown after new clone and auto-selects
- Dropdown displays: `URL (N commits)` format

### Testing

**File**: `src/Lanius.Business.Test/Services/RepositoryServiceTests.cs`
- Added `ListRepositoriesAsync_NoRepositories_ReturnsEmptyList()` test
- Added `ListRepositoriesAsync_WithRepositories_ReturnsList()` test  
- Added `ListRepositoriesAsync_WithInvalidDirectory_SkipsInvalid()` test

All 10 tests pass (1 network test skipped).

## User Experience

1. **On page load**: Dropdown automatically populated with existing repositories
2. **Select repository**: Choose from dropdown → URL field updates → repository loads
3. **Clone new repository**: Enter URL → Clone → dropdown refreshes → new repo auto-selected
4. **Manual URL entry**: Select "-- Select or enter new URL --" → enter new URL

## Technical Notes

- Repositories sorted by most recent usage for convenience
- Invalid directories in storage are gracefully skipped
- Dropdown shows commit count for quick reference
- Selection clears previous visualization state before loading new repo
