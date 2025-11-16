# Clarifications Required Before Implementation

## Critical (Must Answer Before Starting)

### 1. Scale & Performance
**Current State**: Unclear target repository size
**Questions**:
- What's the typical repository size you'll analyze?
  - Small: < 1,000 commits, < 10 branches
  - Medium: 1,000-10,000 commits, 10-50 branches
  - Large: > 10,000 commits, > 50 branches
- Example repository for testing?
- Performance expectations: acceptable load time for initial analysis?

**Impact**: Determines caching strategy, whether in-memory is viable, UI pagination needs
**Answer**: I'd like to use this project to analyze the project I normally work on: ~ 20000 commits, ~300 branches. 
The load time for initial analysis is not critical. Example repository could be: https://github.com/microsoft/terminal


### 2. User Scope
**Current State**: Single-user or multi-user unclear
**Questions**:
- Single developer tool (local use)?
- Team tool (shared service)?
- Public service (anonymous users)?
- Concurrent user count estimate?

**Impact**: Architecture complexity, authentication requirements, resource allocation
**Answer**: May change in future but for MVP single-user is fine.

### 3. Private Repository Access
**Current State**: Not specified
**Questions**:
- Must support private repositories in MVP?
- Acceptable authentication method:
  - Manual credential entry per repo?
  - Store encrypted credentials?
  - OAuth integration (GitHub/GitLab)?
- SSH key support needed?

**Impact**: Security architecture, credential storage, OAuth implementation effort
**Answer**: For MVP, private repository access is not required.

### 4. Repository Storage
**Current State**: Mentioned "disk or memory"
**Questions**:
- Persistent storage acceptable (repos stay on disk)?
- Auto-cleanup old repositories?
- Storage quota per user (if multi-tenant)?
- Incremental updates (git pull) or full re-clone?

**Impact**: Disk space requirements, cleanup jobs, storage infrastructure
**Answer**: Persistent storage on disk is acceptable. Auto-cleanup would be nice but not required for MVP. Incremental updates preferred.

### 5. Real-Time Scope
**Current State**: "Callback whenever new commits made"
**Questions**:
- Monitor only while user is actively viewing?
- Continuous monitoring even when no one is viewing?
- Acceptable polling interval (10s, 30s, 1min)?
- GitHub webhooks integration priority?

**Impact**: Background service design, resource usage, webhook infrastructure
**Answer**: Monitor only while user is actively viewing. Polling interval of 5s is acceptable. Webhooks integration can be deferred.

## Important (Should Clarify Early)

### 6. Visualization Priorities
**Current State**: Three viz types mentioned
**Questions**:
- Which visualization is most important for MVP?
  1. Branch overview
  2. Merge visualization
  3. Replay mode
- Should all three be available in first release?

**Impact**: Development sequencing, MVP timeline
**Answers**: Branch overview is most important, followed by replay mode.

### 7. Branch Analysis Scope
**Current State**: "All release and feature branches"
**Questions**:
- Default branch filters (e.g., main, develop, release/*, feature/*)?
- User-configurable filters?
- Maximum branches to display simultaneously?
- Merged/deleted branch handling?

**Impact**: Query complexity, UI design, performance
**Answer**: Main focus is on main, project and release/* branches. But also bugfix and feature branches should be possible to include.

### 8. Replay Mode Details
**Current State**: "Re-run the history... video visualization"
**Questions**:
- User controls: play/pause/speed/skip?
- Default playback speed (real-time, 1 commit/sec, configurable)?
- Visual representation of commit size (bubble size, color intensity)?
- Date range filtering for replay?

**Impact**: UI complexity, Rx.NET implementation details
**Answer**: User controls for play/pause and speed adjustment are needed. Default playback speed of 1 commit/sec is acceptable.

### 9. Commit Detail Level
**Current State**: "Lines added/subtracted" mentioned
**Questions**:
- Show full diff content or just statistics?
- File-level changes or just totals?
- Link to actual commit in remote (GitHub/GitLab)?
- Display commit message in full or truncated?

**Impact**: Data extraction scope, API payload size, UI design
**Answer**: Just statistics (lines added/removed) are sufficient for MVP. Should be used as basis for display; ex. a scale using red when more lines are removed than added, green when more added than removed.

### 10. Deployment Target
**Current State**: Not specified
**Questions**:
- Self-hosted (Docker, Windows Service)?
- Cloud-hosted (Azure, AWS)?
- Desktop application (Electron wrapper)?
- Single-page app (static hosting + API)?

**Impact**: Deployment strategy, infrastructure requirements, packaging
**Answer**: In the MVP phase, self-hosted solution is acceptable.

## Nice-to-Know (Can Defer)

### 11. Export & Sharing
- Save visualization as image/SVG?
- Share live view with colleagues (unique URL)?
- Embed in other tools (iframe)?
**Answer**: None needed for MVP.

### 12. Comparison Features
- Side-by-side branch comparison?
- Diff two repositories?
- Historical comparison (repo state at different times)?
**Answer**: Good ideas, but none needed for MVP.

### 13. Extensibility
- Plugin system for custom visualizations?
- Custom metrics integration?
- API for third-party tools?
**Answer**: None needed for MVP.
- 
### 14. UI/UX Preferences
- Dark/light theme?
- Mobile support needed?
- Accessibility requirements?
**Answer**: None needed for MVP.
-
## Assumptions (Review & Confirm)

Based on your description, I'm assuming:

1. ✓ **C# backend** for Git operations (LibGit2Sharp)
2. ✓ **JavaScript/D3.js frontend** for visualization
3. ✓ **Web-based** application (not desktop)
4. ✓ **SignalR** for real-time updates
5. ✓ **Rx.NET** for replay mode
6. ✓ **RESTful API** for repository operations
7. ✓ **Disk storage** for cloned repositories (based on "store it on disk")
8. ✗ **Single repository at a time** (multi-repo comparison not mentioned)

**Please confirm or correct these assumptions.**
Yes, these assumptions are correct.

## Recommended Decision Order

To maximize progress with minimal rework:

1. **Decide first**: Scale & user scope (answers 1 & 2)
2. **Then decide**: Private repo support (answer 3)
3. **Then decide**: Visualization priority (answer 6)
4. **Defer until later**: Export, comparison features (11-14)

## Sample Repository Suggestions

For testing during development:
- **Small**: Your own Lanius repo
- **Medium**: ASP.NET Core Docs (https://github.com/dotnet/AspNetCore.Docs)
- **Large**: Linux kernel (https://github.com/torvalds/linux) - stress test

Recommend starting with small/medium, ensure it works well before tackling large repos.
**Answer**: I agree, focus on small/medium first.


## Next Actions

Once clarifications are provided:
1. Update requirements document
2. Create detailed API specifications
3. Define data models
4. Begin LibGit2Sharp prototype
5. Create D3.js visualization proof-of-concept
