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
    replayIndex: 0,
    replayTotalCommits: 0,
    replayCommitOrder: [],  // ordered sha list matching backend stream order
    layoutMode: 'logical', // Current layout mode
    calendarGranularity: 'month', // Calendar granularity
    stats: {
        totalCommits: 0,
        totalBranches: 0,
        linesAdded: 0,
        linesRemoved: 0
    }
};

// Initialize application
document.addEventListener('DOMContentLoaded', () => {
    restorePanelStates();
    initPanelToggles();
    initializeEventHandlers();
    document.getElementById('panel-replay').style.display = state.layoutMode === 'calendar' ? 'none' : '';
    initializeSignalR();
    loadExistingRepositories();
});

function restorePanelStates() {
    document.querySelectorAll('.control-panel[id]').forEach(panel => {
        try {
            if (localStorage.getItem(`panel-collapsed:${panel.id}`) === '1') {
                panel.classList.add('control-panel--collapsed');
            }
        } catch (e) { /* localStorage unavailable */ }
    });
}

function initPanelToggles() {
    document.querySelectorAll('.panel-toggle').forEach(btn => {
        btn.addEventListener('click', () => {
            const panel = btn.closest('.control-panel');
            const isCollapsed = panel.classList.toggle('control-panel--collapsed');
            try {
                localStorage.setItem(`panel-collapsed:${panel.id}`, isCollapsed ? '1' : '0');
            } catch (e) { /* localStorage unavailable */ }
        });
    });
}

// Event Handlers
function initializeEventHandlers() {
    // Repository
    document.getElementById('clone-btn').addEventListener('click', cloneRepository);
    document.getElementById('repo-select').addEventListener('change', onRepositorySelected);
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

    // Replay scrub slider — preview on drag, seek on release
    const scrubSlider = document.getElementById('replay-scrub');
    scrubSlider.addEventListener('input', (e) => {
        const targetIndex = parseInt(e.target.value);
        const remaining = state.replayTotalCommits - targetIndex;
        document.getElementById('np-progress').textContent = `${targetIndex} / ${state.replayTotalCommits}`;
        document.getElementById('np-time-remaining').textContent = formatDuration(remaining / state.replaySpeed);
    });
    scrubSlider.addEventListener('change', (e) => {
        seekReplay(parseInt(e.target.value));
    });

    // Monitoring
    document.getElementById('monitor-start').addEventListener('click', startMonitoring);
    document.getElementById('monitor-stop').addEventListener('click', stopMonitoring);

    // Zoom controls
    document.getElementById('zoom-in').addEventListener('click', () => {
        Visualization.zoomIn();
    });
    document.getElementById('zoom-out').addEventListener('click', () => {
        Visualization.zoomOut();
    });
    document.getElementById('zoom-reset').addEventListener('click', () => {
        Visualization.resetZoom();
    });

    // Keyboard shortcuts for zoom
    document.addEventListener('keydown', (e) => {
        if ((e.ctrlKey || e.metaKey) && e.key === '0') {
            e.preventDefault();
            Visualization.resetZoom();
        } else if ((e.ctrlKey || e.metaKey) && e.key === '=') {
            e.preventDefault();
            Visualization.zoomIn();
        } else if ((e.ctrlKey || e.metaKey) && e.key === '-') {
            e.preventDefault();
            Visualization.zoomOut();
        }
    });

    // Layout mode switching
    document.getElementById('layout-mode').addEventListener('change', (e) => {
        state.layoutMode = e.target.value;
        const granularityGroup = document.getElementById('granularity-group');
        const infoText = document.getElementById('layout-mode-info');
        const replayPanel = document.getElementById('panel-replay');

        granularityGroup.classList.toggle('is-visible', state.layoutMode === 'calendar');
        replayPanel.style.display = state.layoutMode === 'calendar' ? 'none' : '';
        if (state.layoutMode === 'calendar') {
            infoText.innerHTML = '<small>Groups commits by time periods</small>';
        } else if (state.layoutMode === 'timeline') {
            infoText.innerHTML = '<small>Shows each commit at its actual date position</small>';
        } else {
            infoText.innerHTML = '<small>Shows commits on branch timelines</small>';
        }

        // Reload layout with new mode if repository is loaded
        if (state.repositoryId) {
            loadRepository();
        }
    });

    document.getElementById('calendar-granularity').addEventListener('change', (e) => {
        state.calendarGranularity = e.target.value;

        // Reload layout with new granularity if in calendar mode
        if (state.repositoryId && state.layoutMode === 'calendar') {
            loadRepository();
        }
    });

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
    state.connection.on('CommitRevealed', handleCommitRevealed);
    state.connection.on('ReplayCompleted', handleReplayCompleted);
    state.connection.on('ReplayError', handleReplayError);
    state.connection.on('LayoutProgress', handleLayoutProgress);

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

        // Refresh the repository dropdown to include the newly cloned repo
        if (!repo.alreadyExisted) {
            await loadExistingRepositories();
            // Select the newly cloned repository in the dropdown
            document.getElementById('repo-select').value = repo.id;
        }

    } catch (err) {
        console.error('Clone error:', err);
        updateStatus('repo-status', `Error: ${err.message}`, true);
    }
}

// Load existing repositories into dropdown
async function loadExistingRepositories() {
    try {
        const select = document.getElementById('repo-select');

        // Show loading state
        select.innerHTML = '<option value="">Loading repositories...</option>';
        select.disabled = true;

        const response = await fetch(`${API_URL}/api/repository`);

        if (!response.ok) {
            console.warn('Failed to load existing repositories');
            select.innerHTML = '<option value="">-- Select or enter new URL --</option>';
            select.disabled = false;
            return;
        }

        const repositories = await response.json();

        // Clear and re-enable select
        select.innerHTML = '<option value="">-- Select or enter new URL --</option>';
        select.disabled = false;

        // Add repositories to dropdown (without commit count for performance)
        repositories.forEach(repo => {
            const option = document.createElement('option');
            option.value = repo.id;
            option.textContent = repo.url;
            option.dataset.url = repo.url;
            select.appendChild(option);
        });

        console.log(`Loaded ${repositories.length} existing repositories`);
    } catch (err) {
        console.error('Error loading existing repositories:', err);
        const select = document.getElementById('repo-select');
        select.innerHTML = '<option value="">-- Select or enter new URL --</option>';
        select.disabled = false;
    }
}

// Handle repository selection from dropdown
async function onRepositorySelected(event) {
    const select = event.target;
    const selectedId = select.value;

    if (!selectedId) {
        // Clear the URL input when "Select or enter new URL" is chosen
        document.getElementById('repo-url').value = '';
        return;
    }

    const selectedOption = select.options[select.selectedIndex];
    const url = selectedOption.dataset.url;

    // Update the URL input field
    document.getElementById('repo-url').value = url;

    // Clear previous repository state if different
    if (state.repositoryId && state.repositoryId !== selectedId) {
        clearRepositoryState();
    }

    // Set the repository ID and load it
    state.repositoryId = selectedId;
    updateStatus('repo-status', `Loading repository: ${url}...`);

    try {
        // Get repository info
        const response = await fetch(`${API_URL}/api/repository/${selectedId}`);

        if (!response.ok) {
            throw new Error('Repository not found');
        }

        const repo = await response.json();
        updateStats(repo);
        updateStatus('repo-status', `Loaded: ${repo.defaultBranch} (${repo.totalCommits} commits)`);

        // Load commits and branches
        await loadRepository();

    } catch (err) {
        console.error('Load error:', err);
        updateStatus('repo-status', `Error: ${err.message}`, true);
        state.repositoryId = null;
    }
}

function clearRepositoryState() {
    // Clear state
    state.commits = [];
    state.branches = [];
    state.relationships = [];
    state.replaySessionId = null;

    document.querySelector('.sidebar').classList.remove('has-repo');
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

    document.querySelector('.sidebar').classList.add('has-repo');
    try {
        updateStatus('repo-status', 'Loading repository layout...');
        showLayoutProgress('Loading commits...');

        // Subscribe to repository progress updates via SignalR
        if (state.connection && state.connection.state === signalR.HubConnectionState.Connected) {
            await state.connection.invoke('SubscribeToRepository', state.repositoryId);
        }

        // Get branch filter patterns
        const branchFilterInput = document.getElementById('branch-pattern').value;
        const branchFilter = branchFilterInput ? branchFilterInput.trim() : '';
        const hasFilter = branchFilter.length > 0;

        console.log('Branch filter input:', `"${branchFilterInput}"`);
        console.log('Has filter:', hasFilter);

        // Use the new layout endpoint with current mode
        let layoutUrl = `${API_URL}/api/repository/${state.repositoryId}/layout?mode=${state.layoutMode}`;
        if (hasFilter) {
            layoutUrl += `&branchFilter=${encodeURIComponent(branchFilter)}`;
        }
        if (state.layoutMode === 'calendar') {
            layoutUrl += `&granularity=${state.calendarGranularity}`;
        }

        console.log('Fetching layout from:', layoutUrl);
        const layoutResponse = await fetch(layoutUrl);
        if (!layoutResponse.ok) {
            const errorText = await layoutResponse.text();
            console.error('API Error Response:', errorText);
            throw new Error(`Failed to load layout: ${layoutResponse.statusText}`);
        }

        const layout = await layoutResponse.json();
        console.log('=== LAYOUT LOADED ===');
        console.log('Mode:', layout.mode);
        console.log('Nodes:', layout.nodes.length);
        console.log('Edges:', layout.edges.length);
        console.log('Dimensions:', layout.width, 'x', layout.height);
        console.log('Total commits:', layout.totalCommits);
        console.log('Total branches:', layout.totalBranches);

        // Store layout data in state
        state.layoutData = layout;
        state.branches = []; // Extract unique branches from nodes
        const branchSet = new Set();
        layout.nodes.forEach(node => {
            branchSet.add(node.branchName);
        });
        branchSet.forEach(branchName => {
            state.branches.push({ name: branchName });
        });

        // Check if we have data to render
        if (layout.nodes.length === 0) {
            updateStatus('repo-status', 'No commits found', true);
            updateCanvasInfo('No commits to display');
            clearVisualization();
            return;
        }

        // Render visualization with new layout data
        console.log('Calling renderVisualization with layout data');
        renderVisualization();
        populateReplayBranchList();

        hideLayoutProgress();
        updateCanvasInfo(`${layout.totalCommits} commits (${layout.totalBranches} branches)`);
        updateStatus('repo-status', `Loaded layout: ${layout.totalCommits} commits, ${layout.totalBranches} branches`);

    } catch (err) {
        console.error('Load error:', err);
        hideLayoutProgress();
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

    const replayBranch = document.getElementById('replay-branch').value.trim() || 'main';

    try {
        updateStatus('replay-status', 'Loading branch...');

        // Load the layout filtered to the selected branch so only those commits are shown
        await loadReplayLayout(replayBranch);

        // Build ordered commit list — matches backend sort (oldest → newest)
        state.replayCommitOrder = (state.layoutData?.nodes ?? [])
            .filter(n => !n.isGhost)
            .sort((a, b) => new Date(a.timestamp) - new Date(b.timestamp))
            .map(n => n.commitId);
        state.replayIndex = 0;
        state.replayTotalCommits = state.replayCommitOrder.length;

        const scrub = document.getElementById('replay-scrub');
        scrub.max = state.replayTotalCommits;
        scrub.value = 0;

        // Hide all commit nodes — revealed one-by-one via CommitRevealed events
        Visualization.startReplayMode();
        document.getElementById('stat-commits').textContent = '0';

        const response = await fetch(
            `${API_URL}/api/repositories/${state.repositoryId}/replay/start`,
            {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    speed: state.replaySpeed,
                    branchFilter: replayBranch,
                    startFromBranchSplit: !['main', 'master', 'origin/main', 'origin/master'].includes(replayBranch.toLowerCase())
                })
            }
        );

        const session = await response.json();
        state.replaySessionId = session.sessionId;

        // Subscribe to replay stream — triggers backend streaming
        await state.connection.invoke('SubscribeToReplay', session.sessionId);

        updateStatus('replay-status', `Playing ${session.totalCommits} commits at ${state.replaySpeed}x`);
        setReplayButtonState(true);

    } catch (err) {
        console.error('Replay start error:', err);
        updateStatus('replay-status', 'Failed to start replay', true);
    }
}

async function loadReplayLayout(branchFilter) {
    let layoutUrl = `${API_URL}/api/repository/${state.repositoryId}/layout?mode=${state.layoutMode}`;
    layoutUrl += `&branchFilter=${encodeURIComponent(branchFilter)}`;
    if (state.layoutMode === 'calendar') {
        layoutUrl += `&granularity=${state.calendarGranularity}`;
    }

    const layoutResponse = await fetch(layoutUrl);
    if (!layoutResponse.ok) {
        throw new Error(`Failed to load replay layout: ${layoutResponse.statusText}`);
    }

    const layout = await layoutResponse.json();
    state.layoutData = layout;
    renderVisualization();
}

function populateReplayBranchList() {
    const datalist = document.getElementById('replay-branch-list');
    const input = document.getElementById('replay-branch');

    datalist.innerHTML = '';
    const names = state.branches.map(b => b.name);

    names.forEach(name => {
        const opt = document.createElement('option');
        opt.value = name;
        datalist.appendChild(opt);
    });

    // Set default to the main/master branch if the current value isn't in the list
    if (names.length > 0 && !names.includes(input.value)) {
        const mainBranch = names.find(n =>
            n === 'main' || n === 'origin/main' || n === 'master' || n === 'origin/master'
        );
        input.value = mainBranch ?? names[0];
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

        // Clear replay state and reload full visualization
        Visualization.stopReplayMode();
        document.getElementById('replay-now-playing').classList.add('hidden');
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

function handleCommitRevealed(data) {
    if (data.sessionId !== state.replaySessionId) return;
    Visualization.revealCommit(data.sha);
    state.replayIndex++;
    const el = document.getElementById('stat-commits');
    el.textContent = String(parseInt(el.textContent) + 1);

    const node = state.layoutData?.nodes?.find(n => n.commitId === data.sha);
    if (node) {
        const message = node.message || '';
        document.getElementById('np-message').textContent = message;
        document.getElementById('np-sha').textContent = data.sha.substring(0, 8);
        document.getElementById('np-branch').textContent = node.branchName || '';
        document.getElementById('np-author').textContent = node.authorEmail
            ? `${node.author} <${node.authorEmail}>`
            : node.author || 'Unknown';
        document.getElementById('np-author-date').textContent = formatIsoDate(node.timestamp);
        document.getElementById('np-committer').textContent = node.committerEmail
            ? `${node.committer} <${node.committerEmail}>`
            : node.committer || '';
        document.getElementById('np-committer-date').textContent = formatIsoDate(node.committerTimestamp);

        const parentsRow = document.getElementById('np-parents-row');
        if (node.parentShas && node.parentShas.length > 0) {
            document.getElementById('np-parents').textContent = node.parentShas.map(s => s.substring(0, 8)).join(', ');
            parentsRow.style.display = '';
        } else {
            parentsRow.style.display = 'none';
        }

        document.getElementById('replay-now-playing').classList.remove('hidden');
    }

    updateReplayProgress();
}

function handleReplayCompleted(data) {
    if (data.sessionId !== state.replaySessionId) return;
    console.log('Replay completed:', data);
    updateStatus('replay-status', 'Replay completed');
    setReplayButtonState(false);
    document.getElementById('replay-now-playing').classList.add('hidden');
}

function handleReplayError(error) {
    if (error.sessionId && error.sessionId !== state.replaySessionId) return;
    console.error('Replay error:', error);
    updateStatus('replay-status', `Error: ${error.message}`, true);
    setReplayButtonState(false);
}

function updateReplayProgress() {
    const { replayIndex, replayTotalCommits, replaySpeed } = state;
    const remaining = replayTotalCommits - replayIndex;
    document.getElementById('replay-scrub').value = replayIndex;
    document.getElementById('np-progress').textContent = `${replayIndex} / ${replayTotalCommits}`;
    document.getElementById('np-time-remaining').textContent = formatDuration(remaining / replaySpeed);
}

function formatDuration(seconds) {
    if (!isFinite(seconds) || seconds <= 0) return '';
    const m = Math.floor(seconds / 60);
    const s = Math.ceil(seconds % 60);
    return m > 0 ? `${m}:${String(s).padStart(2, '0')} remaining` : `${s}s remaining`;
}

async function seekReplay(targetIndex) {
    if (!state.repositoryId) return;

    const clampedIndex = Math.max(0, Math.min(targetIndex, state.replayTotalCommits));

    // Null out session ID IMMEDIATELY — before any await — so every in-flight
    // CommitRevealed / ReplayCompleted event from the old session is rejected
    // by the session-ID guard in handleCommitRevealed / handleReplayCompleted.
    const oldSessionId = state.replaySessionId;
    state.replaySessionId = null;

    // Stop current session if active
    if (oldSessionId) {
        try {
            await fetch(
                `${API_URL}/api/repositories/${state.repositoryId}/replay/${oldSessionId}/stop`,
                { method: 'POST' }
            );
            await state.connection.invoke('UnsubscribeFromReplay', oldSessionId);
        } catch (err) {
            console.warn('seekReplay: stop failed', err);
        }
    }

    // Re-hide all nodes, then reveal 0..clampedIndex-1 instantly (no scroll yet)
    Visualization.startReplayMode();
    if (clampedIndex > 0) {
        Visualization.revealRange(state.replayCommitOrder.slice(0, clampedIndex));
    }

    state.replayIndex = clampedIndex;
    document.getElementById('stat-commits').textContent = String(clampedIndex);
    updateReplayProgress();

    // Restart backend stream from the new position
    const replayBranch = document.getElementById('replay-branch').value.trim() || 'main';
    try {
        const response = await fetch(
            `${API_URL}/api/repositories/${state.repositoryId}/replay/start`,
            {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    speed: state.replaySpeed,
                    branchFilter: replayBranch,
                    startIndex: clampedIndex
                })
            }
        );
        const session = await response.json();
        state.replaySessionId = session.sessionId;
        await state.connection.invoke('SubscribeToReplay', session.sessionId);
        setReplayButtonState(true);
        updateStatus('replay-status', `Playing from ${clampedIndex} / ${state.replayTotalCommits}`);
    } catch (err) {
        console.error('seekReplay: restart failed', err);
        updateStatus('replay-status', 'Seek failed', true);
    }

    // Jump synchronously LAST — overrides any in-flight CommitRevealed scroll events
    if (clampedIndex > 0) {
        Visualization.jumpToCommit(state.replayCommitOrder[clampedIndex - 1]);
    }
}

function handleLayoutProgress(progress) {
    console.log('Layout progress:', progress);

    const { percentage, operation, processedItems, totalItems } = progress;

    const label = totalItems > 0
        ? `${operation} — ${processedItems}/${totalItems}`
        : operation;

    updateLayoutProgress(percentage, label);
    updateStatus('repo-status', `${label} (${percentage}%)`);

    if (percentage >= 100) {
        setTimeout(hideLayoutProgress, 400);
    }
}

// Layout Progress Bar
function showLayoutProgress(initialLabel = '') {
    document.getElementById('layout-progress-fill').style.width = '0%';
    document.getElementById('layout-progress-label').textContent = initialLabel;
    document.getElementById('layout-progress').classList.remove('hidden');
}

function updateLayoutProgress(percentage, label) {
    document.getElementById('layout-progress-fill').style.width = `${percentage}%`;
    document.getElementById('layout-progress-label').textContent = label;
}

function hideLayoutProgress() {
    document.getElementById('layout-progress').classList.add('hidden');
}

// UI Helper Functions
function updateStatus(elementId, message, isError = false) {
    const element = document.getElementById(elementId);
    element.textContent = message;
    element.classList.remove('status-line--info', 'status-line--success', 'status-line--error');

    if (!message || !message.trim()) {
        element.classList.add('status-line--info');
        return;
    }

    if (isError) {
        element.classList.add('status-line--error');
        return;
    }

    const successKeywords = ['loaded', 'playing', 'started', 'applied', 'connected', 'complete'];
    const isSuccess = successKeywords.some(keyword => message.toLowerCase().includes(keyword));
    element.classList.add(isSuccess ? 'status-line--success' : 'status-line--info');
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

function formatIsoDate(ts) {
    if (!ts) return '';
    const d = new Date(ts);
    if (isNaN(d)) return '';
    const pad = n => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
}

function showCommitDetail(commit) {
    // Reset to normal (individual commit) view
    document.getElementById('detail-sha-label').textContent = 'SHA';
    document.getElementById('detail-author-row').style.display = '';
    document.getElementById('detail-author-date-label').textContent = 'Author date';
    document.getElementById('detail-committer-row').style.display = '';
    document.getElementById('detail-committer-date-row').style.display = '';
    document.getElementById('detail-commit-count-row').style.display = 'none';
    document.getElementById('detail-parents-row').style.display = '';

    document.getElementById('detail-sha').textContent = commit.sha.substring(0, 8);
    document.getElementById('detail-author').textContent = commit.authorEmail
        ? `${commit.author} <${commit.authorEmail}>`
        : commit.author || 'Unknown';
    document.getElementById('detail-author-date').textContent = formatIsoDate(commit.timestamp);
    document.getElementById('detail-committer').textContent = commit.committerEmail
        ? `${commit.committer} <${commit.committerEmail}>`
        : commit.committer || '';
    document.getElementById('detail-committer-date').textContent = formatIsoDate(commit.committerTimestamp);
    document.getElementById('detail-branches').textContent = (commit.branches || []).join(', ');

    const parentsRow = document.getElementById('detail-parents-row');
    if (commit.parentShas && commit.parentShas.length > 0) {
        document.getElementById('detail-parents').textContent = commit.parentShas.map(s => s.substring(0, 8)).join(', ');
        parentsRow.style.display = '';
    } else {
        parentsRow.style.display = 'none';
    }

    const message = commit.message || '';
    document.getElementById('detail-message').textContent = message;

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

function showCalendarGroupDetail(node) {
    const gran = (state.layoutData?.calendarGranularity || 'month').toLowerCase();

    document.getElementById('detail-sha-label').textContent = 'Group';
    document.getElementById('detail-author-row').style.display = 'none';
    document.getElementById('detail-committer-row').style.display = 'none';
    document.getElementById('detail-committer-date-row').style.display = 'none';
    document.getElementById('detail-parents-row').style.display = 'none';
    document.getElementById('detail-commit-count-row').style.display = '';

    // Group ID (period key)
    document.getElementById('detail-sha').textContent = node.commitId || '';

    // Date label and value
    const dateLabel = gran === 'day' ? 'Date' :
                      gran === 'week' ? 'Week start' :
                      gran === 'year' ? 'Year' : 'Month';
    document.getElementById('detail-author-date-label').textContent = dateLabel;
    const ts = new Date(node.timestamp);
    let dateValue;
    if (gran === 'day') {
        dateValue = ts.toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' });
    } else if (gran === 'week') {
        dateValue = ts.toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' });
    } else if (gran === 'year') {
        dateValue = String(ts.getFullYear());
    } else {
        dateValue = ts.toLocaleDateString('en-US', { year: 'numeric', month: 'long' });
    }
    document.getElementById('detail-author-date').textContent = dateValue;

    document.getElementById('detail-branches').textContent = 'all';
    document.getElementById('detail-commit-count').textContent = node.commitCount || (node.groupCommitLines || []).length || '';

    // Commit listing in textarea
    const lines = node.groupCommitLines || [];
    document.getElementById('detail-message').textContent = lines.join('\n');

    // Hide diff stats
    document.getElementById('detail-additions').textContent = '';
    document.getElementById('detail-deletions').textContent = '';
    document.getElementById('detail-files').textContent = '';

    document.getElementById('commit-detail').classList.remove('hidden');
}

// Export for visualization module
window.LaniusApp = {
    state,
    showCommitDetail,
    showCalendarGroupDetail,
    updateStats
};
