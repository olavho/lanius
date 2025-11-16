# SignalR Client Integration Guide

## JavaScript Client Example

### Installation
```bash
npm install @microsoft/signalr
```

### Basic Connection
```javascript
import * as signalR from '@microsoft/signalr';

// Create connection
const connection = new signalR.HubConnectionBuilder()
    .withUrl('https://localhost:5001/hubs/repository')
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Information)
    .build();

// Event handlers
connection.on('ReceiveNewCommits', (commits) => {
    console.log('New commits received:', commits);
    commits.forEach(commit => {
        displayCommit(commit);
    });
});

connection.on('RepositoryUpdated', (repository) => {
    console.log('Repository updated:', repository);
    updateRepositoryInfo(repository);
});

connection.on('Error', (error) => {
    console.error('SignalR error:', error);
});

// Start connection
async function startConnection() {
    try {
        await connection.start();
        console.log('SignalR connected');
        
        // Subscribe to repository updates
        await connection.invoke('SubscribeToRepository', 'your-repo-id');
    } catch (err) {
        console.error('Error connecting:', err);
        setTimeout(startConnection, 5000); // Retry
    }
}

// Handle disconnection
connection.onclose(async () => {
    console.log('SignalR disconnected');
    await startConnection(); // Reconnect
});

// Start the connection
startConnection();
```

### Subscribing to Repository Updates
```javascript
// Subscribe to a repository
await connection.invoke('SubscribeToRepository', repositoryId);

// Unsubscribe
await connection.invoke('UnsubscribeFromRepository', repositoryId);
```

### Handling New Commits
```javascript
connection.on('ReceiveNewCommits', (commits) => {
    commits.forEach(commit => {
        // Animate new commit appearing
        const commitElement = createCommitElement(commit);
        
        // Use D3.js to animate
        d3.select('#commit-graph')
            .append('g')
            .attr('class', 'commit-node')
            .attr('data-sha', commit.sha)
            .style('opacity', 0)
            .transition()
            .duration(500)
            .style('opacity', 1);
        
        // Color code based on diff stats
        const color = getColorFromIndicator(commit.stats.colorIndicator);
        // -1 = red (deletions), 0 = neutral, +1 = green (additions)
    });
});

function getColorFromIndicator(indicator) {
    // indicator ranges from -1 (all deletions) to +1 (all additions)
    if (indicator > 0) {
        // Green shades for additions
        const intensity = Math.floor(indicator * 255);
        return `rgb(0, ${intensity}, 0)`;
    } else if (indicator < 0) {
        // Red shades for deletions
        const intensity = Math.floor(Math.abs(indicator) * 255);
        return `rgb(${intensity}, 0, 0)`;
    } else {
        return 'rgb(128, 128, 128)'; // Neutral gray
    }
}
```

### Complete Example with D3.js Visualization
```javascript
class RepositoryVisualization {
    constructor(repositoryId) {
        this.repositoryId = repositoryId;
        this.connection = null;
        this.commits = [];
    }

    async initialize() {
        // Setup SignalR connection
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl('https://localhost:5001/hubs/repository')
            .withAutomaticReconnect()
            .build();

        // Setup event handlers
        this.connection.on('ReceiveNewCommits', (commits) => {
            this.handleNewCommits(commits);
        });

        this.connection.on('RepositoryUpdated', (repo) => {
            this.updateStats(repo);
        });

        // Connect and subscribe
        await this.connection.start();
        await this.connection.invoke('SubscribeToRepository', this.repositoryId);
        
        // Start monitoring on the server
        await fetch(`https://localhost:5001/api/monitoring/start/${this.repositoryId}`, {
            method: 'POST'
        });
    }

    handleNewCommits(commits) {
        console.log(`Received ${commits.length} new commits`);
        
        commits.forEach(commit => {
            this.commits.unshift(commit);
            this.animateNewCommit(commit);
        });
    }

    animateNewCommit(commit) {
        const svg = d3.select('#commit-graph');
        
        // Calculate position
        const x = this.getXPosition(commit.timestamp);
        const y = this.getYPosition(commit.branches[0]);
        
        // Create commit node
        const node = svg.append('circle')
            .attr('class', 'commit-node')
            .attr('cx', x)
            .attr('cy', y)
            .attr('r', this.getCommitSize(commit.stats))
            .attr('fill', this.getCommitColor(commit.stats.colorIndicator))
            .style('opacity', 0)
            .on('click', () => this.showCommitDetails(commit));

        // Animate appearance
        node.transition()
            .duration(750)
            .style('opacity', 1)
            .attr('r', this.getCommitSize(commit.stats));

        // Add tooltip
        node.append('title')
            .text(`${commit.shortMessage}\n${commit.author}\n+${commit.stats.linesAdded} -${commit.stats.linesRemoved}`);
    }

    getCommitSize(stats) {
        // Scale commit size based on total changes
        const minSize = 5;
        const maxSize = 20;
        const scale = Math.log(stats.totalChanges + 1) / Math.log(100);
        return minSize + (maxSize - minSize) * Math.min(scale, 1);
    }

    getCommitColor(colorIndicator) {
        if (colorIndicator > 0) {
            return d3.interpolateGreens(0.3 + colorIndicator * 0.7);
        } else if (colorIndicator < 0) {
            return d3.interpolateReds(0.3 + Math.abs(colorIndicator) * 0.7);
        }
        return '#808080';
    }

    updateStats(repo) {
        d3.select('#total-commits').text(repo.totalCommits);
        d3.select('#total-branches').text(repo.totalBranches);
        d3.select('#last-updated').text(new Date(repo.lastFetchedAt).toLocaleString());
    }

    async cleanup() {
        if (this.connection) {
            await this.connection.invoke('UnsubscribeFromRepository', this.repositoryId);
            await this.connection.stop();
        }
        
        // Stop monitoring on server
        await fetch(`https://localhost:5001/api/monitoring/stop/${this.repositoryId}`, {
            method: 'POST'
        });
    }
}

// Usage
const viz = new RepositoryVisualization('your-repo-id');
await viz.initialize();

// Cleanup when done
window.addEventListener('beforeunload', () => viz.cleanup());
```

## C# Client Example

```csharp
using Microsoft.AspNetCore.SignalR.Client;

var connection = new HubConnectionBuilder()
    .WithUrl("https://localhost:5001/hubs/repository")
    .WithAutomaticReconnect()
    .Build();

// Event handlers
connection.On<IEnumerable<CommitResponse>>("ReceiveNewCommits", commits =>
{
    foreach (var commit in commits)
    {
        Console.WriteLine($"New commit: {commit.ShortMessage} by {commit.Author}");
    }
});

connection.On<RepositoryResponse>("RepositoryUpdated", repo =>
{
    Console.WriteLine($"Repository updated: {repo.TotalCommits} total commits");
});

// Start connection
await connection.StartAsync();

// Subscribe to repository
await connection.InvokeAsync("SubscribeToRepository", "your-repo-id");

// Keep connection alive
Console.WriteLine("Press any key to exit...");
Console.ReadKey();

// Cleanup
await connection.InvokeAsync("UnsubscribeFromRepository", "your-repo-id");
await connection.StopAsync();
```

## Events Reference

### Client ? Server Methods
- `SubscribeToRepository(repositoryId)` - Subscribe to updates
- `UnsubscribeFromRepository(repositoryId)` - Unsubscribe

### Server ? Client Events
- `ReceiveNewCommits(commits[])` - New commits detected
- `RepositoryUpdated(repository)` - Repository metadata updated
- `Error(error)` - Error occurred

## Testing

### Manual Testing
1. Start the API: `dotnet run --project src/Lanius.Api`
2. Clone a repository via REST API
3. Start monitoring: `POST /api/monitoring/start/{id}`
4. Connect with SignalR client
5. Make changes to the repository locally and push
6. Observe real-time updates in client

### SignalR Connection URL
```
Development: https://localhost:5001/hubs/repository
Production:  https://your-domain.com/hubs/repository
```

## Configuration

### appsettings.json
```json
{
  "Monitoring": {
    "PollingInterval": "00:00:05",
    "Enabled": true
  }
}
```

### Adjust Polling Interval
- Default: 5 seconds
- Faster: `00:00:02` (2 seconds)
- Slower: `00:00:10` (10 seconds)

## Troubleshooting

### CORS Issues
Make sure CORS is configured to allow your frontend origin and credentials:
```csharp
policy.WithOrigins("http://localhost:3000")
      .AllowAnyHeader()
      .AllowAnyMethod()
      .AllowCredentials();
```

### Connection Drops
SignalR has automatic reconnection enabled. The client will retry with exponential backoff.

### No Updates Received
1. Check that monitoring is started: `POST /api/monitoring/start/{id}`
2. Verify repository has new commits (push changes)
3. Check server logs for polling activity
4. Ensure client is subscribed to correct repository ID
