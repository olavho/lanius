# MVP Implementation Summary

## Clarifications Confirmed

### Scale & Performance
- **Target**: Large repos (20K commits, 300 branches)
- **Test repo**: Microsoft Terminal (https://github.com/microsoft/terminal)
- **Strategy**: Disk storage + metadata caching

### User & Access
- **Scope**: Single-user MVP
- **Authentication**: Public repos only (no private access needed)

### Storage & Updates
- **Storage**: Disk-based, persistent
- **Updates**: Incremental (git pull), not full re-clone
- **Cleanup**: Deferred (manual for MVP)

### Real-Time Monitoring
- **Active only**: Monitor while user viewing
- **Poll interval**: 5 seconds
- **Webhooks**: Deferred post-MVP

### Visualization Priorities
1. **Branch overview** (MVP priority 1)
2. **Replay mode** (MVP priority 2)
3. **Merge visualization** (future)

### Branch Focus
- **Default**: main, project/*, release/*
- **Configurable**: Allow inclusion of feature/*, bugfix/*

### Replay Details
- **Speed**: 1 commit/sec default
- **Controls**: Play, pause, speed adjustment
- **Visual**: Commit size via color (red = deletions, green = additions)

### Commit Display
- **Statistics only**: Lines added/removed
- **No diffs**: Full content deferred
- **Color coding**: Red/green based on net change

### Deployment
- **MVP**: Self-hosted
- **Format**: Web application (ASP.NET Core + static frontend)

## Architecture Validated

```
Lanius.Api (Presentation)
  ? delegates to
Lanius.Business (Domain/Analytics)
  ? tested by
Lanius.Business.Test (Unit Tests)
```

### Technology Stack
- **Backend**: C# .NET 10, ASP.NET Core Web API
- **Git Operations**: LibGit2Sharp
- **Real-Time**: SignalR (5s polling push)
- **Replay**: Rx.NET observables
- **Frontend**: JavaScript + D3.js
- **Storage**: Disk (Git repos in `{app-data}/repos/`)
- **Testing**: MSTest

## Project State

### Current Setup
? `Lanius.Api` - .NET 10 Web API project (newly created)
? `Lanius.Business` - .NET 10 class library (empty)
? `Lanius.Business.Test` - MSTest project (empty)
? Copilot instructions updated with architecture guidelines

### Configuration Needed
- Add LibGit2Sharp to `Lanius.Business`
- Add Rx.NET (System.Reactive) to `Lanius.Business`
- Add SignalR to `Lanius.Api`
- Add project reference: `Lanius.Api` ? `Lanius.Business`
- Add project reference: `Lanius.Business.Test` ? `Lanius.Business`
- Add Moq/NSubstitute to `Lanius.Business.Test`

## Implementation Plan

### Phase 1: Foundation
1. Add NuGet packages
2. Create domain models (`Commit`, `Branch`, `DiffStats`)
3. Define service interfaces (`IRepositoryService`, `ICommitAnalyzer`)
4. Configure dependency injection in `Lanius.Api`

### Phase 2: Repository Operations
1. Implement `RepositoryService` (clone, fetch, pull)
2. Implement `CommitAnalyzer` (metadata extraction, diff stats)
3. Implement `BranchAnalyzer` (branch detection, divergence)
4. Write unit tests for all services

### Phase 3: API Layer
1. Create REST controllers:
   - `RepositoryController` (`POST /api/repo/clone`, `GET /api/repo/status`)
   - `CommitController` (`GET /api/commits`, `GET /api/commits/{sha}`)
   - `BranchController` (`GET /api/branches`, `GET /api/branches/{name}`)
2. Create DTOs for API responses

### Phase 4: Real-Time (SignalR)
1. Create `RepositoryHub` for SignalR
2. Implement polling service (5s interval)
3. Broadcast new commits to connected clients
4. Add connection management

### Phase 5: Replay Mode (Rx.NET)
1. Create `ReplayService` with observables
2. Implement playback controls (play, pause, speed)
3. Stream commits chronologically
4. Integrate with SignalR for client push

### Phase 6: Frontend (D3.js)
1. Create static HTML/JS files
2. Implement D3.js branch overview graph
3. Connect to REST API
4. Connect to SignalR hub
5. Implement replay UI controls

### Phase 7: Polish
1. Error handling and logging
2. Configuration (appsettings.json)
3. API documentation (Swagger)
4. README updates

## Next Steps

**Waiting for commit/push before starting implementation.**

Once ready:
1. Add NuGet packages to all projects
2. Create domain models in `Lanius.Business`
3. Implement `RepositoryService` with LibGit2Sharp
4. Write first unit tests
5. Create basic API controller

## Test Strategy

### Unit Tests (Priority)
- Repository cloning/fetching
- Commit metadata extraction
- Diff statistics calculation
- Branch analysis (ahead/behind, common ancestor)
- Replay observable streams

### Integration Tests (Future)
- Full repository analysis end-to-end
- SignalR hub communication
- API endpoint responses

### Manual Testing
- Clone Microsoft Terminal repo
- Analyze 20K commits, 300 branches
- Verify performance acceptable
- Test real-time updates (make local commits)
