# Phase 4 Complete: Real-Time Updates (SignalR)

## Completed Tasks

### ? SignalR Hub Created (`Lanius.Api/Hubs/`)
- **RepositoryHub** - SignalR hub for real-time communication
  - `SubscribeToRepository(repositoryId)` - Client subscribes to updates
  - `UnsubscribeFromRepository(repositoryId)` - Client unsubscribes
  - Connection/disconnection logging
  - Group-based broadcasting per repository

### ? Background Monitoring Service
- **RepositoryMonitoringService** - Hosted service for polling
  - 5-second polling interval (configurable)
  - Monitors multiple repositories simultaneously
  - Fetches updates from remote
  - Detects new commits since last check
  - Broadcasts to subscribed clients via SignalR
  - Thread-safe repository tracking
  - Scoped service creation for business logic

### ? Monitoring Controller
- **MonitoringController** (`/api/monitoring`)
  - `POST /start/{repositoryId}` - Start monitoring
  - `POST /stop/{repositoryId}` - Stop monitoring
  - Integrated with background service

### ? SignalR Integration
- Hub registered at `/hubs/repository`
- CORS configured with credentials support
- Automatic reconnection enabled
- Group-based room management

### ? Documentation
- **signalr-client-guide.md** - Complete client integration guide
  - JavaScript/TypeScript examples
  - C# client example
  - D3.js visualization integration
  - Event reference
  - Testing procedures
  - Troubleshooting tips

## Architecture

```
Client (Browser/App)
    ?
SignalR WebSocket Connection
    ?
RepositoryHub (/hubs/repository)
    ? Groups: "repo:{id}"
    ?
RepositoryMonitoringService (Background)
    ? Every 5 seconds
IRepositoryService.FetchUpdatesAsync()
    ? If new commits
ICommitAnalyzer.GetCommitsAsync()
    ?
Broadcast to Group ? Clients receive updates
```

## Real-Time Flow

### 1. Client Connects
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl('https://localhost:5001/hubs/repository')
    .build();

await connection.start();
```

### 2. Client Subscribes
```javascript
await connection.invoke('SubscribeToRepository', 'repo-id-123');
```

### 3. Server Starts Monitoring
```http
POST /api/monitoring/start/repo-id-123
```

### 4. Background Service Polls
- Every 5 seconds
- Calls `FetchUpdatesAsync()` for each monitored repository
- If updates found, gets new commits
- Compares with last known commit SHA
- Broadcasts new commits to group

### 5. Client Receives Update
```javascript
connection.on('ReceiveNewCommits', (commits) => {
    // Handle new commits
    commits.forEach(commit => {
        animateCommit(commit);
    });
});
```

## Features

### Group-Based Broadcasting
- Each repository has its own SignalR group: `repo:{repositoryId}`
- Only subscribed clients receive updates
- Efficient for multiple repositories

### Commit Tracking
- Stores last known commit SHA per repository
- Detects new commits by comparing SHAs
- Only broadcasts truly new commits

### Resource Management
- Scoped service creation for business logic
- Thread-safe repository tracking
- Proper cleanup when monitoring stops

### Error Handling
- Connection errors logged
- Polling errors don't crash service
- Automatic reconnection on client

### Configurable Polling
```json
{
  "Monitoring": {
    "PollingInterval": "00:00:05",
    "Enabled": true
  }
}
```

## API Endpoints Added

```
Monitoring:
POST /api/monitoring/start/{repositoryId}
POST /api/monitoring/stop/{repositoryId}

SignalR Hub:
WS   /hubs/repository
```

## SignalR Events

### Client ? Server (Invoke)
```javascript
connection.invoke('SubscribeToRepository', repositoryId)
connection.invoke('UnsubscribeFromRepository', repositoryId)
```

### Server ? Client (On)
```javascript
connection.on('ReceiveNewCommits', (commits) => { ... })
connection.on('RepositoryUpdated', (repository) => { ... })
connection.on('Error', (error) => { ... })
```

## Testing the Real-Time Features

### Step 1: Start the API
```bash
cd src/Lanius.Api
dotnet run
```

### Step 2: Clone a Repository
```bash
curl -X POST https://localhost:5001/api/repository/clone \
  -H "Content-Type: application/json" \
  -d '{"url":"https://github.com/octocat/Hello-World.git"}'
  
# Response: { "id": "abc123...", ... }
```

### Step 3: Start Monitoring
```bash
curl -X POST https://localhost:5001/api/monitoring/start/abc123
```

### Step 4: Connect SignalR Client
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl('https://localhost:5001/hubs/repository')
    .build();

connection.on('ReceiveNewCommits', commits => {
    console.log('NEW COMMITS:', commits);
});

await connection.start();
await connection.invoke('SubscribeToRepository', 'abc123');
```

### Step 5: Trigger Update
Make a change to the repository and push:
```bash
cd /path/to/cloned/repo
echo "test" >> README.md
git add .
git commit -m "Test commit"
git push
```

### Step 6: Observe Real-Time Update
Within 5 seconds, the client should receive the new commit via SignalR!

## Integration with D3.js

### Animated Commit Visualization
```javascript
connection.on('ReceiveNewCommits', (commits) => {
    commits.forEach(commit => {
        // Create D3 node
        const node = svg.append('circle')
            .attr('r', getSize(commit.stats.totalChanges))
            .attr('fill', getColor(commit.stats.colorIndicator))
            .style('opacity', 0);
        
        // Animate appearance
        node.transition()
            .duration(750)
            .style('opacity', 1);
    });
});

function getColor(indicator) {
    // indicator: -1 (red/deletions) to +1 (green/additions)
    if (indicator > 0) {
        return d3.interpolateGreens(0.3 + indicator * 0.7);
    } else {
        return d3.interpolateReds(0.3 + Math.abs(indicator) * 0.7);
    }
}
```

## Performance Considerations

### Polling Interval
- **5 seconds** (default) - Good balance for MVP
- Faster (2s) - More responsive but higher server load
- Slower (10s) - Lower load but less responsive

### Monitored Repositories
- Background service handles multiple repositories
- Each repository checked independently
- Failed checks don't affect others

### SignalR Scalability
- Current: Single server (MVP)
- Future: Redis backplane for multiple servers
- Sticky sessions not required (stateless)

## Build Status
? **All code compiles successfully**
? **SignalR ready for testing**

## What's Ready

? **Phase 1**: Domain models, service interfaces  
? **Phase 2**: LibGit2Sharp services, unit tests  
? **Phase 3**: REST API controllers, Swagger  
? **Phase 4**: SignalR hub, real-time monitoring  

## Next Steps - Phase 5: Replay Mode (Rx.NET)

1. **Create ReplayService** with Rx.NET observables
2. **Implement playback controls** (play, pause, speed)
3. **Stream commits chronologically** with timing
4. **Integrate with SignalR** for client push
5. **Add replay endpoints** to API

Ready for Phase 5! ??
