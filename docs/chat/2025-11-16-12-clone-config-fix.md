# Configuration Issue Fixed - Clone Now Working!

## Problem Solved ?

**Issue**: `ArgumentException: The value cannot be an empty string. (Parameter 'path')`

**Root Cause**: The `appsettings.json` had an empty `BasePath` configuration:
```json
"RepositoryStorage": {
  "BasePath": ""  // ? Empty string
}
```

## Solution Applied

### 1. Fixed `appsettings.json`
```json
"RepositoryStorage": {
  "BasePath": "%LOCALAPPDATA%/Lanius/repositories",
  "MaxRepositories": 100,
  "AutoCleanupAge": "30.00:00:00"
}
```

### 2. Enhanced `RepositoryStorageOptions.cs`
- Added automatic default path fallback
- Environment variable expansion support
- Returns: `C:\Users\{YourUsername}\AppData\Local\Lanius\repositories`

### 3. Added Validation
- `RepositoryService` now validates BasePath on startup
- Logs the actual path being used
- Clear error messages if misconfigured

### 4. Fixed Frontend API URL
- Changed from hardcoded `https://localhost:5001`
- Now uses `window.location.origin` (works with any port)

## Repository Storage Location

Cloned repositories are stored at:
```
C:\Users\{YourUsername}\AppData\Local\Lanius\repositories\{hash}\
```

Each repository gets a unique 16-character hash based on its URL.

## Status: ? WORKING

You can now clone public repositories successfully!

---

**Date**: 2025-11-16  
**Fix Type**: Configuration + Environment variable expansion
