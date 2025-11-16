# Requirements & Technology Recommendations

## Functional Requirements

### Core Features
1. **Repository Access**
   - Accept Git repository URL (HTTPS, SSH)
   - Clone repository to local storage
   - Support incremental fetch/pull operations
   - Handle authentication (personal access tokens, SSH keys)

2. **Metadata Analysis**
   - Extract commit data: SHA, author, timestamp, message
   - Calculate diff statistics: lines added/removed per commit
   - Identify all branches (local, remote)
   - Detect merge commits and their parent relationships
   - Find first common ancestor between branches
   - Calculate branch divergence (commits ahead/behind)

3. **Visualization - Branch Overview**
   - Display main branch timeline
   - Show feature/release branches branching from main
   - Indicate merge points
   - Color-code by branch type or author

4. **Visualization - Merge Details**
   - Show merge commits into feature branches
   - Display source and target branches
   - Indicate conflict resolution commits

5. **Visualization - Replay Mode**
   - Animate commit history chronologically
   - Show commit size (lines changed) visually
   - Display branch creation/merge events
   - Playback controls (play, pause, speed adjustment)

6. **Real-Time Updates**
   - Monitor repository for new commits
   - Push updates to connected clients
   - Animate new commits appearing on graph
   - Configurable polling interval

### Non-Functional Requirements
- **Performance**: Handle repositories with 10,000+ commits
- **Responsiveness**: UI updates < 100ms
- **Scalability**: Support multiple concurrent users viewing different repos
- **Security**: Sanitize repository URLs, handle credentials securely

## Technology Stack Recommendations

### Backend (C#/.NET)

#### **LibGit2Sharp** ? PRIMARY CHOICE
- **Purpose**: Git repository interaction
- **Why**: Native C# wrapper for libgit2, full Git protocol support
- **Capabilities**:
  - Clone, fetch, pull repositories
  - Access commit history, branches, tags
  - Calculate diffs and statistics
  - Merge detection and common ancestor finding
- **Considerations**: Large repos may need disk storage; in-memory possible for small repos

#### **Rx.NET (System.Reactive)** ? EXCELLENT FIT
- **Purpose**: Replay mode implementation
- **Why**: Perfect for time-based event streams
- **Usage**:
  - Stream commits chronologically with `Observable.Interval`
  - Support pause/resume with subjects
  - Throttle/buffer for performance
  - Combine with SignalR for push to clients

#### **SignalR** ? IDEAL FOR REAL-TIME
- **Purpose**: Real-time updates to clients
- **Why**: Built into ASP.NET Core, WebSocket support
- **Usage**:
  - Push new commits to all connected clients
  - Broadcast replay events
  - Group connections by repository

#### **ASP.NET Core Web API**
- **Purpose**: RESTful API for repository operations
- **Why**: Industry standard, great tooling, SignalR integration

### Frontend (JavaScript)

#### **D3.js** ? BEST FOR CUSTOM GRAPHS
- **Purpose**: Graph visualization
- **Why**: Most flexible for custom commit graphs
- **Capabilities**:
  - Force-directed graphs for branch relationships
  - Custom SVG rendering
  - Smooth transitions and animations
  - Full control over visual design

#### **Alternatives to Consider**
- **Vis.js Network**: Simpler API, less flexibility
- **Cytoscape.js**: Good for complex graphs, steeper learning curve
- **Chart.js + Timeline plugin**: For timeline-style views

#### **SignalR JavaScript Client**
- **Purpose**: Receive real-time updates from backend
- **Why**: Official client library, easy integration

#### **Framework Choice**
- **Option 1**: Vanilla JS + D3.js (lighter, faster initial load)
- **Option 2**: React + D3.js (better state management for complex UI)
- **Recommendation**: Start with Vanilla JS, migrate to React if complexity grows

### Storage

#### **Disk Storage (Recommended)**
- Store cloned repositories in `{app-data}/repos/{repo-id}/`
- Use LibGit2Sharp's `Repository.Clone()` method
- Advantages: Handles large repos, persistent across restarts

#### **In-Memory (Optional)**
- Use `MemoryStream` for very small repos
- Requires custom libgit2 backend configuration
- Advantages: Faster, no disk I/O
- Limitations: Memory constraints, data loss on restart

### Additional Libraries

#### **Polly** (Optional)
- **Purpose**: Resilience and retry policies
- **Usage**: Retry failed git clone/fetch operations

#### **Serilog** (Recommended)
- **Purpose**: Structured logging
- **Usage**: Debug repository operations, monitor performance

## Architecture Overview

```
???????????????????????????????????????????????????????????
?                    Frontend (Browser)                    ?
?  ??????????????  ????????????????  ??????????????????? ?
?  ?  D3.js     ?  ?  SignalR     ?  ?  HTTP Client    ? ?
?  ?  Graphs    ????  Client      ?  ?  (fetch/axios)  ? ?
?  ??????????????  ????????????????  ??????????????????? ?
???????????????????????????????????????????????????????????
                      ? WebSocket             ? HTTP
???????????????????????????????????????????????????????????
?              ASP.NET Core Backend                        ?
?  ???????????????????  ??????????????????????????????   ?
?  ?  SignalR Hub    ?  ?  Web API Controllers       ?   ?
?  ?  - Broadcasts   ?  ?  - /api/repo/clone         ?   ?
?  ?  - Replay       ?  ?  - /api/commits            ?   ?
?  ???????????????????  ??????????????????????????????   ?
?           ?                    ?                         ?
?  ???????????????????????????????????????????????????   ?
?  ?         Repository Service                       ?   ?
?  ?  - LibGit2Sharp operations                       ?   ?
?  ?  - Metadata extraction                           ?   ?
?  ????????????????????????????????????????????????????   ?
?           ?                                              ?
?  ????????????????????????????????????????????????????   ?
?  ?         Rx.NET Replay Service                     ?   ?
?  ?  - Observable commit streams                      ?   ?
?  ?  - Playback control                               ?   ?
?  ????????????????????????????????????????????????????   ?
????????????????????????????????????????????????????????????
                           ?
                  ???????????????????
                  ?  Git Repos      ?
                  ?  (Disk Storage) ?
                  ???????????????????
```

## Key Design Decisions Needed

### 1. Repository Storage Strategy
**Question**: Disk storage with caching, or in-memory only for small repos?
**Recommendation**: Disk storage with metadata caching in Redis/memory
**Rationale**: Flexibility to handle any repo size, better for multi-user scenarios

### 2. Real-Time Monitoring Approach
**Question**: Poll repositories or use Git hooks?
**Options**:
- **Polling**: Background service checks for updates every N seconds
- **Git Hooks**: Repository pushes trigger webhook to your API
- **Hybrid**: Polling for public repos, webhooks for owned repos

**Recommendation**: Start with polling, add webhook support later
**Rationale**: Simpler, works with any repo (including GitHub without configuration)

### 3. Visualization Data Format
**Question**: Send full commit graph or pre-calculated positions?
**Options**:
- **Raw data**: Send commits/branches, let D3.js calculate layout
- **Pre-calculated**: Backend calculates node positions, send coordinates
  
**Recommendation**: Raw data to frontend, D3.js handles layout
**Rationale**: More flexible, easier to adjust visualizations client-side

### 4. Branch Analysis Scope
**Question**: Analyze all branches or user-selected subset?
**Recommendation**: Default to main + recent branches, allow user filtering
**Rationale**: Performance (large repos have hundreds of branches)

### 5. Authentication Strategy
**Question**: How to handle private repositories?
**Options**:
- Store user credentials (encrypted)
- Use personal access tokens (PAT)
- OAuth with Git providers (GitHub, GitLab, Bitbucket)

**Recommendation**: PAT input per repository + optional OAuth
**Rationale**: Balance security and ease of use

## Clarifications Needed

### High Priority
1. **Target repository size**: Typical number of commits/branches?
2. **Multi-tenant**: Will multiple users analyze different repos simultaneously?
3. **Authentication**: Support for private repositories in MVP?
4. **Deployment**: Self-hosted or cloud service?
5. **Replay speed**: Should users control playback speed (1x, 2x, 10x)?

### Medium Priority
6. **Branch filtering**: Default branches to show (e.g., only main + release/*, feature/*)?
7. **Commit detail level**: Show full diff or just statistics?
8. **Export**: Save visualizations as images/videos?
9. **Comparison**: Compare two branches side-by-side?

### Low Priority (Future)
10. **Multiple repositories**: Compare commits across different repos?
11. **Code analysis**: Integrate code quality metrics per commit?
12. **Notifications**: Alert on specific commit patterns?

## Next Steps

1. **Decide on storage strategy** (disk recommended)
2. **Define API contracts** for core endpoints
3. **Create domain models** (Commit, Branch, DiffStats, etc.)
4. **Prototype LibGit2Sharp integration** (clone + metadata extraction)
5. **Build simple D3.js proof-of-concept** (static commit graph)
6. **Integrate SignalR** for real-time updates
7. **Implement Rx.NET replay** pipeline
