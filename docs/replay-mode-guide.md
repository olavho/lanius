# Replay Mode Client Integration Guide

## JavaScript Client Example

### Basic Replay Control
```javascript
import * as signalR from '@microsoft/signalr';

class ReplayClient {
    constructor(apiUrl, hubUrl) {
        this.apiUrl = apiUrl;
        this.hubUrl = hubUrl;
        this.connection = null;
        this.sessionId = null;
    }

    async initialize() {
        // Setup SignalR connection
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(this.hubUrl)
            .withAutomaticReconnect()
            .build();

        // Handle replay events
        this.connection.on('ReplayCommit', (commit) => {
            this.handleReplayCommit(commit);
        });

        this.connection.on('ReplayCompleted', (data) => {
            console.log('Replay completed:', data);
            this.onReplayComplete();
        });

        this.connection.on('ReplayError', (error) => {
            console.error('Replay error:', error);
        });

        await this.connection.start();
    }

    async startReplay(repositoryId, options = {}) {
        // Start replay session via REST API
        const response = await fetch(
            `${this.apiUrl}/api/repositories/${repositoryId}/replay/start`,
            {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    speed: options.speed || 1.0,
                    startDate: options.startDate,
                    endDate: options.endDate,
                    branchFilter: options.branchFilter
                })
            }
        );

        const session = await response.json();
        this.sessionId = session.sessionId;

        console.log('Replay session started:', session);

        // Subscribe to replay stream via SignalR
        await this.connection.invoke('SubscribeToReplay', this.sessionId);

        return session;
    }

    async pause() {
        if (!this.sessionId) return;

        await fetch(
            `${this.apiUrl}/api/repositories/${this.repositoryId}/replay/${this.sessionId}/pause`,
            { method: 'POST' }
        );
    }

    async resume() {
        if (!this.sessionId) return;

        await fetch(
            `${this.apiUrl}/api/repositories/${this.repositoryId}/replay/${this.sessionId}/resume`,
            { method: 'POST' }
        );
    }

    async stop() {
        if (!this.sessionId) return;

        await fetch(
            `${this.apiUrl}/api/repositories/${this.repositoryId}/replay/${this.sessionId}/stop`,
            { method: 'POST' }
        );

        await this.connection.invoke('UnsubscribeFromReplay', this.sessionId);
        this.sessionId = null;
    }

    async setSpeed(speed) {
        if (!this.sessionId) return;

        await fetch(
            `${this.apiUrl}/api/repositories/${this.repositoryId}/replay/${this.sessionId}/speed`,
            {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(speed)
            }
        );
    }

    async getStatus() {
        if (!this.sessionId) return null;

        const response = await fetch(
            `${this.apiUrl}/api/repositories/${this.repositoryId}/replay/${this.sessionId}`
        );

        return await response.json();
    }

    handleReplayCommit(commit) {
        // Override this method to handle commits
        console.log('Received commit:', commit);
    }

    onReplayComplete() {
        // Override this method to handle completion
        console.log('Replay finished');
    }
}

// Usage
const client = new ReplayClient(
    'https://localhost:5001',
    'https://localhost:5001/hubs/repository'
);

await client.initialize();

const session = await client.startReplay('repo-id-123', {
    speed: 2.0,  // 2x speed
    startDate: '2024-01-01',
    endDate: '2024-12-31'
});

// Control playback
await client.pause();
await client.resume();
await client.setSpeed(0.5);  // Slow down to 0.5x
await client.stop();
```

### Integration with D3.js Visualization

```javascript
class ReplayVisualization extends ReplayClient {
    constructor(apiUrl, hubUrl, svgElement) {
        super(apiUrl, hubUrl);
        this.svg = d3.select(svgElement);
        this.commits = [];
        this.animationQueue = [];
    }

    handleReplayCommit(commit) {
        this.commits.push(commit);
        this.animateCommit(commit);
    }

    animateCommit(commit) {
        const x = this.getXPosition(commit.timestamp);
        const y = this.getYPosition(commit.branches[0]);
        
        // Create commit node
        const node = this.svg.append('g')
            .attr('class', 'commit-node')
            .attr('transform', `translate(${x}, ${y})`);

        // Add circle for commit
        const circle = node.append('circle')
            .attr('r', 0)
            .attr('fill', this.getCommitColor(commit.stats?.colorIndicator || 0))
            .style('opacity', 0);

        // Animate appearance
        circle.transition()
            .duration(500)
            .attr('r', this.getCommitSize(commit.stats))
            .style('opacity', 1);

        // Add tooltip
        node.append('title')
            .text(this.getCommitTooltip(commit));

        // Add links to parents
        commit.parentShas.forEach(parentSha => {
            this.drawLink(commit.sha, parentSha);
        });

        // Update stats
        this.updateStats();
    }

    getCommitColor(colorIndicator) {
        if (colorIndicator > 0) {
            // Green for additions
            return d3.interpolateGreens(0.3 + colorIndicator * 0.7);
        } else if (colorIndicator < 0) {
            // Red for deletions
            return d3.interpolateReds(0.3 + Math.abs(colorIndicator) * 0.7);
        }
        return '#808080'; // Gray for neutral
    }

    getCommitSize(stats) {
        if (!stats) return 5;
        
        const minSize = 5;
        const maxSize = 20;
        const scale = Math.log(stats.totalChanges + 1) / Math.log(100);
        return minSize + (maxSize - minSize) * Math.min(scale, 1);
    }

    getCommitTooltip(commit) {
        return `${commit.shortMessage}
Author: ${commit.author}
Date: ${new Date(commit.timestamp).toLocaleString()}
${commit.stats ? `+${commit.stats.linesAdded} -${commit.stats.linesRemoved}` : ''}`;
    }

    drawLink(fromSha, toSha) {
        // Find nodes and draw SVG path between them
        const fromNode = this.findCommitNode(fromSha);
        const toNode = this.findCommitNode(toSha);
        
        if (fromNode && toNode) {
            this.svg.insert('path', ':first-child')
                .attr('class', 'commit-link')
                .attr('d', this.getLinkPath(fromNode, toNode))
                .attr('stroke', '#ccc')
                .attr('stroke-width', 2)
                .attr('fill', 'none')
                .style('opacity', 0)
                .transition()
                .duration(300)
                .style('opacity', 1);
        }
    }

    updateStats() {
        const totalCommits = this.commits.length;
        const totalAdditions = this.commits.reduce((sum, c) => 
            sum + (c.stats?.linesAdded || 0), 0);
        const totalDeletions = this.commits.reduce((sum, c) => 
            sum + (c.stats?.linesRemoved || 0), 0);

        d3.select('#replay-commits').text(totalCommits);
        d3.select('#replay-additions').text(`+${totalAdditions}`);
        d3.select('#replay-deletions').text(`-${totalDeletions}`);
    }

    onReplayComplete() {
        console.log(`Replay complete: ${this.commits.length} commits visualized`);
        this.highlightFinalState();
    }

    highlightFinalState() {
        // Highlight the final commit
        this.svg.selectAll('.commit-node circle')
            .filter((d, i, nodes) => i === nodes.length - 1)
            .transition()
            .duration(1000)
            .attr('stroke', 'gold')
            .attr('stroke-width', 3);
    }
}

// Usage
const viz = new ReplayVisualization(
    'https://localhost:5001',
    'https://localhost:5001/hubs/repository',
    '#commit-graph'
);

await viz.initialize();

// Start replay with controls
const session = await viz.startReplay('repo-id', { speed: 2.0 });

// Playback controls
document.getElementById('pause-btn').onclick = () => viz.pause();
document.getElementById('resume-btn').onclick = () => viz.resume();
document.getElementById('stop-btn').onclick = () => viz.stop();

// Speed slider
document.getElementById('speed-slider').oninput = (e) => {
    viz.setSpeed(parseFloat(e.target.value));
};
```

### HTML Controls

```html
<div class="replay-controls">
    <button id="start-btn">Start Replay</button>
    <button id="pause-btn">Pause</button>
    <button id="resume-btn">Resume</button>
    <button id="stop-btn">Stop</button>
    
    <div class="speed-control">
        <label>Speed: <span id="speed-display">1.0x</span></label>
        <input type="range" id="speed-slider" min="0.1" max="5" step="0.1" value="1.0">
    </div>
    
    <div class="replay-stats">
        <div>Commits: <span id="replay-commits">0</span></div>
        <div>Additions: <span id="replay-additions">+0</span></div>
        <div>Deletions: <span id="replay-deletions">-0</span></div>
    </div>
</div>

<svg id="commit-graph" width="1200" height="600"></svg>

<script>
const viz = new ReplayVisualization(
    'https://localhost:5001',
    'https://localhost:5001/hubs/repository',
    '#commit-graph'
);

document.getElementById('start-btn').onclick = async () => {
    await viz.initialize();
    await viz.startReplay('your-repo-id', {
        speed: parseFloat(document.getElementById('speed-slider').value)
    });
};

document.getElementById('pause-btn').onclick = () => viz.pause();
document.getElementById('resume-btn').onclick = () => viz.resume();
document.getElementById('stop-btn').onclick = () => viz.stop();

document.getElementById('speed-slider').oninput = (e) => {
    const speed = parseFloat(e.target.value);
    document.getElementById('speed-display').textContent = `${speed.toFixed(1)}x`;
    if (viz.sessionId) {
        viz.setSpeed(speed);
    }
};
</script>
```

## API Reference

### Start Replay
```http
POST /api/repositories/{repositoryId}/replay/start
Content-Type: application/json

{
  "speed": 1.0,
  "startDate": "2024-01-01T00:00:00Z",
  "endDate": "2024-12-31T23:59:59Z",
  "branchFilter": "main"
}

Response: 200 OK
{
  "sessionId": "abc123...",
  "repositoryId": "repo-id",
  "state": "Playing",
  "speed": 1.0,
  "totalCommits": 150,
  "currentIndex": 0,
  "startedAt": "2024-11-16T12:00:00Z"
}
```

### Control Playback
```http
POST /api/repositories/{repositoryId}/replay/{sessionId}/pause
POST /api/repositories/{repositoryId}/replay/{sessionId}/resume
POST /api/repositories/{repositoryId}/replay/{sessionId}/stop
POST /api/repositories/{repositoryId}/replay/{sessionId}/speed
```

### Get Status
```http
GET /api/repositories/{repositoryId}/replay/{sessionId}
```

## SignalR Events

### Client ? Server
```javascript
connection.invoke('SubscribeToReplay', sessionId);
connection.invoke('UnsubscribeFromReplay', sessionId);
```

### Server ? Client
```javascript
connection.on('ReplayCommit', (commit) => { ... });
connection.on('ReplayCompleted', (data) => { ... });
connection.on('ReplayError', (error) => { ... });
```

## Speed Values

- `0.5` - Half speed (1 commit per 2 seconds)
- `1.0` - Normal speed (1 commit per second) - Default
- `2.0` - Double speed (2 commits per second)
- `5.0` - 5x speed (5 commits per second)

## Filtering

### Date Range
```javascript
await client.startReplay('repo-id', {
    startDate: '2024-01-01T00:00:00Z',
    endDate: '2024-06-30T23:59:59Z'
});
```

### Branch Filter
```javascript
await client.startReplay('repo-id', {
    branchFilter: 'main'
});
```

## Testing Replay Mode

### 1. Clone a Repository
```bash
curl -X POST https://localhost:5001/api/repository/clone \
  -H "Content-Type: application/json" \
  -d '{"url":"https://github.com/octocat/Hello-World.git"}'
```

### 2. Start Replay Session
```bash
curl -X POST https://localhost:5001/api/repositories/{id}/replay/start \
  -H "Content-Type: application/json" \
  -d '{"speed": 2.0}'
```

### 3. Connect SignalR and Subscribe
```javascript
await connection.invoke('SubscribeToReplay', sessionId);
```

### 4. Observe Commits Streaming
Commits will arrive via `ReplayCommit` event at the specified speed.

### 5. Control Playback
```javascript
await client.pause();   // Pause
await client.resume();  // Resume
await client.setSpeed(0.5);  // Slow down
await client.stop();    // Stop and cleanup
```

## Tips

- **Start slow**: Begin with speed 0.5 to see commits clearly
- **Use date filters**: Focus on specific time periods
- **Monitor progress**: Use `GetStatus()` to track replay state
- **Cleanup**: Always call `stop()` when done to free resources
