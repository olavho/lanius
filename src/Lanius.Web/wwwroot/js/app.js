// Configuration
const API_URL = window.location.origin; // Use same origin as the page
const HUB_URL = `${API_URL}/hubs/repository`;

// Application State
let state = {
    repositoryId: null,
    connection: null,
    commits: [],
    branches: [],
    relationships: [], // Add relationships array
    replaySessionId: null,
    replaySpeed: 1.0,
    stats: {
        totalCommits: 0,
        totalBranches: 0,
        linesAdded: 0,
        linesRemoved: 0
    }
};

// Initialize application
document.addEventListener('DOMContentLoaded', () => {
    initializeEventHandlers();
    initializeSignalR();
});

// Event Handlers
function initializeEventHandlers() {
    // Repository
    document.getElementById('clone-btn').addEventListener('click', cloneRepository);
    document.getElementById('filter-btn').addEventListener('click', applyBranchFilter);
    
    // Replay
    document.getElementById('replay-start').addEventListener('click', startReplay);
    document.getElementById('replay-pause').addEventListener('click', pauseReplay);
    document.getElementById('replay-resume').addEventListener('click', resumeReplay);
    document.getElementById('replay-stop').addEventListener('click', stopReplay);
    
    // Speed slider
    const speedSlider = document.getElementById('speed-slider');
    speedSlider.addEventListener('input', (e) => {
        const speed = parseFloat(e.target.value);
        state.replaySpeed = speed;
        document.getElementById('speed-display').textContent = `${speed.toFixed(1)}x`;
    });
    
    // Monitoring
    document.getElementById('monitor-start').addEventListener('click', startMonitoring);
    document.getElementById('monitor-stop').addEventListener('click', stopMonitoring);
    
    // Commit detail close
    document.querySelector('.detail-close').addEventListener('click', hideCommitDetail);
}

// SignalR Setup
async function initializeSignalR() {
    state.connection = new signalR.HubConnectionBuilder()
        .withUrl(HUB_URL)
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Information)
        .build();

    // Event handlers
    state.connection.on('ReceiveNewCommits', handleNewCommits);
    state.connection.on('RepositoryUpdated', handleRepositoryUpdated);
    state.connection.on('ReplayCommit', handleReplayCommit);
    state.connection.on('ReplayCompleted', handleReplayCompleted);
    state.connection.on('ReplayError', handleReplayError);

    try {
        await state.connection.start();
        console.log('SignalR connected');
        updateStatus('monitor-status', 'Connected to server');
    } catch (err) {
        console.error('SignalR connection error:', err);
        updateStatus('monitor-status', 'Connection failed', true);
    }
}

// Repository Operations
async function cloneRepository() {
    const url = document.getElementById('repo-url').value.trim();
    if (!url) {
        updateStatus('repo-status', 'Please enter a repository URL', true);
        return;
    }

    updateStatus('repo-status', 'Cloning repository...');
    
    try {
        const response = await fetch(`${API_URL}/api/repository/clone`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ url })
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.message || 'Clone failed');
        }

        const repo = await response.json();
        
        // Clear previous repository state
        if (state.repositoryId && state.repositoryId !== repo.id) {
            clearRepositoryState();
        }
        
        state.repositoryId = repo.id;
        
        const statusMessage = repo.alreadyExisted 
            ? `Repository updated: ${repo.defaultBranch} (${repo.totalCommits} commits)`
            : `Cloned: ${repo.defaultBranch} (${repo.totalCommits} commits)`;
        updateStatus('repo-status', statusMessage);
        updateStats(repo);
        
        // Load commits and branches
        await loadRepository();
        
    } catch (err) {
        console.error('Clone error:', err);
        updateStatus('repo-status', `Error: ${err.message}`, true);
    }
}

function clearRepositoryState() {
    // Clear state
    state.commits = [];
    state.branches = [];
    state.relationships = [];
    state.replaySessionId = null;
    
    // Clear visualization
    clearVisualization();
    
    // Reset stats
    state.stats = {
        totalCommits: 0,
        totalBranches: 0,
        linesAdded: 0,
        linesRemoved: 0
    };
    
    document.getElementById('stat-commits').textContent = '0';
    document.getElementById('stat-branches').textContent = '0';
    document.getElementById('stat-additions').textContent = '+0';
    document.getElementById('stat-deletions').textContent = '-0';
    
    // Reset UI elements
    updateCanvasInfo('No repository loaded');
    setReplayButtonState(false);
    
    console.log('Repository state cleared');
}

async function loadRepository() {
    if (!state.repositoryId) return;

    try {
        updateStatus('repo-status', 'Loading branch overview...');
        
        // Get branch filter patterns - handle empty/whitespace properly
        const branchFilterInput = document.getElementById('branch-pattern').value;
        const branchFilter = branchFilterInput ? branchFilterInput.trim() : '';
        const hasFilter = branchFilter.length > 0;
        
        console.log('Branch filter input:', `"${branchFilterInput}"`);
        console.log('Has filter:', hasFilter);
        
        // Use the new overview endpoint that returns only significant commits
        // Note: includeRemote defaults to true on the server
        let overviewUrl = `${API_URL}/api/repositories/${state.repositoryId}/branches/overview`;
        if (hasFilter) {
            const patterns = branchFilter.split(',').map(p => p.trim()).filter(p => p.length > 0);
            console.log('Parsed patterns:', patterns);
            if (patterns.length > 0) {
                const queryParams = patterns.map(p => `patterns=${encodeURIComponent(p)}`).join('&');
                overviewUrl += `?${queryParams}`;
            }
        }
        
        console.log('Fetching branch overview from:', overviewUrl);
        const overviewResponse = await fetch(overviewUrl);
        if (!overviewResponse.ok) {
            const errorText = await overviewResponse.text();
            console.error('API Error Response:', errorText);
            throw new Error(`Failed to load branch overview: ${overviewResponse.statusText}`);
        }
        
        const overview = await overviewResponse.json();
        console.log('=== BRANCH OVERVIEW LOADED ===');
        console.log('Full response keys:', Object.keys(overview));
        console.log('- Branches:', overview.branches.length, 'First 5:', overview.branches.slice(0, 5).map(b => b.name));
        console.log('- Significant commits:', overview.significantCommits.length);
        console.log('- Relationships:', overview.relationships.length);

        // Extract branches and commits from overview
        state.branches = overview.branches.map(b => ({
            name: b.name,
            tipSha: b.headSha,
            timestamp: b.headTimestamp
        }));

        state.commits = overview.significantCommits.map(c => ({
            sha: c.sha,
            author: c.author,
            authorEmail: '', // Not included in overview
            timestamp: c.timestamp,
            message: c.shortMessage,
            shortMessage: c.shortMessage,
            parentShas: [], // We don't need parent relationships for overview
            isMerge: false,
            stats: c.stats,
            branches: c.branches,
            significance: c.type // Store the significance type
        }));

        // Store relationships for visualization
        state.relationships = overview.relationships || [];

        console.log(`Loaded ${state.branches.length} branches and ${state.commits.length} significant commits`);
        console.log('Relationships:', state.relationships);

        // Check if we have data to render
        if (state.commits.length === 0) {
            updateStatus('repo-status', 'No commits found', true);
            updateCanvasInfo('No commits to display');
            clearVisualization();
            return;
        }
        
        if (state.branches.length === 0) {
            updateStatus('repo-status', 'No branches found matching filter', true);
            updateCanvasInfo('No branches to display');
            clearVisualization();
            return;
        }

        // Render visualization
        console.log('Calling renderVisualization with', state.commits.length, 'commits and', state.branches.length, 'branches');
        renderVisualization();
        
        const commitTypeBreakdown = overview.significantCommits.reduce((acc, c) => {
            acc[c.type] = (acc[c.type] || 0) + 1;
            return acc;
        }, {});
        console.log('Commit types:', commitTypeBreakdown);
        
        updateCanvasInfo(`${state.commits.length} significant commits (${state.branches.length} branches)`);
        updateStatus('repo-status', `Loaded overview: ${state.commits.length} commits, ${state.branches.length} branches`);
        
    } catch (err) {
        console.error('Load error:', err);
        updateStatus('repo-status', `Error: ${err.message}`, true);
    }
}

async function applyBranchFilter() {
    if (!state.repositoryId) return;

    const patterns = document.getElementById('branch-pattern').value
        .split(',')
        .map(p => p.trim())
        .filter(p => p);

    if (patterns.length === 0) {
        await loadRepository();
        return;
    }

    try {
        const queryParams = patterns.map(p => `patterns=${encodeURIComponent(p)}`).join('&');
        const response = await fetch(
            `${API_URL}/api/repositories/${state.repositoryId}/branches?${queryParams}`
        );
        state.branches = await response.json();

        // Reload commits for filtered branches
        await loadRepository();
        
    } catch (err) {
        console.error('Filter error:', err);
    }
}

// Replay Operations
async function startReplay() {
    if (!state.repositoryId) {
        updateStatus('replay-status', 'Clone a repository first', true);
        return;
    }

    try {
        updateStatus('replay-status', 'Starting replay...');
        
        const response = await fetch(
            `${API_URL}/api/repositories/${state.repositoryId}/replay/start`,
            {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    speed: state.replaySpeed,
                    branchFilter: document.getElementById('branch-pattern').value.split(',')[0]?.trim()
                })
            }
        );

        const session = await response.json();
        state.replaySessionId = session.sessionId;

        // Subscribe to replay stream
        await state.connection.invoke('SubscribeToReplay', session.sessionId);

        // Clear visualization for replay
        clearVisualization();
        
        updateStatus('replay-status', `Playing ${session.totalCommits} commits at ${state.replaySpeed}x`);
        setReplayButtonState(true);
        
    } catch (err) {
        console.error('Replay start error:', err);
        updateStatus('replay-status', 'Failed to start replay', true);
    }
}

async function pauseReplay() {
    if (!state.replaySessionId) return;

    try {
        await fetch(
            `${API_URL}/api/repositories/${state.repositoryId}/replay/${state.replaySessionId}/pause`,
            { method: 'POST' }
        );
        updateStatus('replay-status', 'Paused');
        document.getElementById('replay-pause').disabled = true;
        document.getElementById('replay-resume').disabled = false;
    } catch (err) {
        console.error('Pause error:', err);
    }
}

async function resumeReplay() {
    if (!state.replaySessionId) return;

    try {
        await fetch(
            `${API_URL}/api/repositories/${state.repositoryId}/replay/${state.replaySessionId}/resume`,
            { method: 'POST' }
        );
        updateStatus('replay-status', 'Playing');
        document.getElementById('replay-pause').disabled = false;
        document.getElementById('replay-resume').disabled = true;
    } catch (err) {
        console.error('Resume error:', err);
    }
}

async function stopReplay() {
    if (!state.replaySessionId) return;

    try {
        await fetch(
            `${API_URL}/api/repositories/${state.repositoryId}/replay/${state.replaySessionId}/stop`,
            { method: 'POST' }
        );
        
        await state.connection.invoke('UnsubscribeFromReplay', state.replaySessionId);
        
        state.replaySessionId = null;
        updateStatus('replay-status', 'Stopped');
        setReplayButtonState(false);
        
        // Reload full visualization
        await loadRepository();
        
    } catch (err) {
        console.error('Stop error:', err);
    }
}

// Monitoring Operations
async function startMonitoring() {
    if (!state.repositoryId) return;

    try {
        await fetch(
            `${API_URL}/api/monitoring/start/${state.repositoryId}`,
            { method: 'POST' }
        );
        
        await state.connection.invoke('SubscribeToRepository', state.repositoryId);
        
        updateStatus('monitor-status', 'Monitoring active (5s polling)');
        document.getElementById('monitor-start').disabled = true;
        document.getElementById('monitor-stop').disabled = false;
        
    } catch (err) {
        console.error('Monitor start error:', err);
    }
}

async function stopMonitoring() {
    if (!state.repositoryId) return;

    try {
        await fetch(
            `${API_URL}/api/monitoring/stop/${state.repositoryId}`,
            { method: 'POST' }
        );
        
        await state.connection.invoke('UnsubscribeFromRepository', state.repositoryId);
        
        updateStatus('monitor-status', 'Monitoring stopped');
        document.getElementById('monitor-start').disabled = false;
        document.getElementById('monitor-stop').disabled = true;
        
    } catch (err) {
        console.error('Monitor stop error:', err);
    }
}

// SignalR Event Handlers
function handleNewCommits(commits) {
    console.log('New commits received:', commits);
    commits.forEach(commit => {
        state.commits.unshift(commit);
        animateNewCommit(commit);
    });
    updateCanvasInfo(`${state.commits.length} commits (${commits.length} new)`);
}

function handleRepositoryUpdated(repo) {
    console.log('Repository updated:', repo);
    updateStats(repo);
}

function handleReplayCommit(commit) {
    console.log('Replay commit:', commit);
    animateReplayCommit(commit);
}

function handleReplayCompleted(data) {
    console.log('Replay completed:', data);
    updateStatus('replay-status', 'Replay completed');
    setReplayButtonState(false);
}

function handleReplayError(error) {
    console.error('Replay error:', error);
    updateStatus('replay-status', `Error: ${error.message}`, true);
    setReplayButtonState(false);
}

// UI Helper Functions
function updateStatus(elementId, message, isError = false) {
    const element = document.getElementById(elementId);
    element.textContent = message;
    element.style.color = isError ? '#d32f2f' : 'var(--fg-tertiary)';
}

function updateStats(repo) {
    state.stats.totalCommits = repo.totalCommits;
    state.stats.totalBranches = repo.totalBranches;
    
    document.getElementById('stat-commits').textContent = repo.totalCommits;
    document.getElementById('stat-branches').textContent = repo.totalBranches;
}

function updateCanvasInfo(text) {
    document.getElementById('canvas-info').textContent = text;
}

function setReplayButtonState(isPlaying) {
    document.getElementById('replay-start').disabled = isPlaying;
    document.getElementById('replay-pause').disabled = !isPlaying;
    document.getElementById('replay-resume').disabled = true;
    document.getElementById('replay-stop').disabled = !isPlaying;
}

function showCommitDetail(commit) {
    document.getElementById('detail-sha').textContent = commit.sha.substring(0, 8);
    document.getElementById('detail-author').textContent = `${commit.author} <${commit.authorEmail}>`;
    document.getElementById('detail-date').textContent = new Date(commit.timestamp).toLocaleString();
    document.getElementById('detail-branches').textContent = commit.branches.join(', ');
    document.getElementById('detail-message').textContent = commit.message;
    
    if (commit.stats) {
        document.getElementById('detail-additions').textContent = `+${commit.stats.linesAdded}`;
        document.getElementById('detail-deletions').textContent = `-${commit.stats.linesRemoved}`;
        document.getElementById('detail-files').textContent = `${commit.stats.filesChanged} files`;
    }
    
    document.getElementById('commit-detail').classList.remove('hidden');
}

function hideCommitDetail() {
    document.getElementById('commit-detail').classList.add('hidden');
}

// Export for visualization module
window.LaniusApp = {
    state,
    showCommitDetail,
    updateStats
};
