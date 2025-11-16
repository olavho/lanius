# Lanius.Web Project Separation

**Date**: 2025-11-16  
**Type**: Architecture Change

## Summary
Separated `Lanius.Web` from being a simple static file folder to a proper .NET project in the solution.

## Changes Made

### 1. Project Structure
- Created `src/Lanius.Web/Lanius.Web.csproj` as ASP.NET Core web project
- Moved static files to `wwwroot/` directory (ASP.NET Core convention):
  - `index.html`
  - `css/styles.css`
  - `js/app.js`
  - `js/visualization.js`

### 2. Solution Configuration
- Added `Lanius.Web` to `Lanius.slnx` using `dotnet sln add`
- Project properly integrated with solution build process

### 3. Integration with Lanius.Api
- Added project reference from `Lanius.Api` to `Lanius.Web`
- Updated `Program.cs` to use standard ASP.NET Core static files middleware:
  - `UseDefaultFiles()` - serves `index.html` by default
  - `UseStaticFiles()` - serves all static content
- Removed custom file provider logic (no longer needed)

### 4. Static Web Assets
- Configured `Lanius.Web.csproj` to properly expose static web assets
- Content files marked to copy to output directory
- Build process generates:
  - `Lanius.Web.staticwebassets.runtime.json`
  - Compressed versions (`.gz`) for production
  - All files properly served from wwwroot

### 5. Documentation
- Updated `.github/copilot-instructions.md`:
  - Documented `.slnx` solution file
  - Added `Lanius.Web` project description
  - Clarified frontend layer responsibilities
  - Added frontend code conventions

## Benefits
- ? Follows ASP.NET Core conventions (`wwwroot`)
- ? Proper project structure for potential future expansion
- ? Clean separation between API and frontend
- ? Static web assets properly managed by build system
- ? Compressed assets for better performance
- ? Can add frontend build tooling if needed later

## Build Verification
- Solution builds successfully
- Static web assets generated correctly
- All files accessible at runtime via `UseStaticFiles()`

## No Breaking Changes
- Frontend still served from same root path
- API endpoints unchanged
- SignalR hub endpoint unchanged
