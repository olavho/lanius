// D3.js Visualization Module - Minimalist Sci-Fi Aesthetic
// Thin black/dark gray lines on light background, smooth animations

const Visualization = (() => {
    let svg, g, xScale, yScale, zoomBehavior;
    let axisG = null;   // fixed SVG-space group for the sticky timeline axis
    let axisScale = null; // d3 time scale for the axis (separate from xScale)
    let currentLayout = null; // last layout passed to renderLayout()
    let commitData = [];
    let branchData = [];
    let currentZoom = d3.zoomIdentity; // Preserve zoom state across re-renders

    const config = {
        margin: { top: 60, right: 40, bottom: 40, left: 100 },
        commitRadius: 4,
        commitRadiusHover: 6,
        lineWidth: 1,
        branchSpacing: 40,
        zoomExtent: [0.1, 10], // 10% to 1000% zoom
    };

    function getBranchClass(branchName) {
        const name = (branchName || '').toLowerCase().replace(/^origin\//, '');
        if (name === 'main' || name === 'master') return 'branch--main';
        if (name.includes('release')) return 'branch--release';
        if (name.includes('feature')) return 'branch--feature';
        if (name.includes('hotfix') || name.includes('fix')) return 'branch--fix';
        if (name.includes('dependabot')) return 'branch--dependabot';
        return 'branch--other';
    }

    function initialize() {
        const container = document.getElementById('commit-graph');
        const width = container.clientWidth;
        const height = container.clientHeight;

        svg = d3.select('#commit-graph')
            .attr('width', width)
            .attr('height', height);

        // Inject arrowhead marker definitions for cross-branch edges
        svg.append('defs').html(`
            <marker id="arrow-merge" markerWidth="6" markerHeight="6" refX="5" refY="3"
                    orient="auto" markerUnits="strokeWidth">
                <path d="M0,0 L6,3 L0,6 Z" fill="#FF9800"/>
            </marker>
            <marker id="arrow-branch" markerWidth="6" markerHeight="6" refX="5" refY="3"
                    orient="auto" markerUnits="strokeWidth">
                <path d="M0,0 L6,3 L0,6 Z" fill="#4CAF50"/>
            </marker>
        `);

        g = svg.append('g')
            .attr('transform', `translate(${config.margin.left}, ${config.margin.top})`);

        // Axis group sits above g in paint order; not zoomed — labels stay fixed in SVG space
        axisG = svg.append('g').attr('class', 'timeline-axis');

        // Create scales
        xScale = d3.scaleTime()
            .range([0, width - config.margin.left - config.margin.right]);

        yScale = d3.scaleLinear()
            .range([0, height - config.margin.top - config.margin.bottom]);

        // Initialize zoom behavior
        zoomBehavior = d3.zoom()
            .scaleExtent(config.zoomExtent)
            .on('zoom', (event) => {
                currentZoom = event.transform;
                g.attr('transform', event.transform);
                renderTimelineAxis();
            });

        // Apply zoom behavior to SVG
        svg.call(zoomBehavior);

        // Handle window resize
        window.addEventListener('resize', debounce(handleResize, 250));
    }

    function render(commits, branches) {
        console.log('=== Visualization.render START ===');
        console.log('Input:', commits.length, 'commits,', branches.length, 'branches');

        commitData = commits;
        branchData = branches;

        if (commits.length === 0) {
            console.warn('No commits to render');
            clearAll();
            return;
        }

        if (branches.length === 0) {
            console.warn('No branches to render');
            clearAll();
            return;
        }

        console.log('Sample commit:', commits[0]);
        console.log('Sample branch:', branches[0]);
        console.log('All branch names:', branches.map(b => b.name));

        try {
            const branchYMap = updateScales();
            console.log('Branch Y positions:', Array.from(branchYMap.entries()));
            console.log('X scale domain:', xScale.domain());
            console.log('X scale range:', xScale.range());

            renderTimelineGrid();
            renderBranchLines();
            renderCommits();
            console.log('=== Visualization.render COMPLETE ===');
        } catch (error) {
            console.error('Error rendering visualization:', error);
            console.error('Stack trace:', error.stack);
        }
    }

    function renderTimelineGrid() {
        // Clear existing grid
        g.selectAll('.timeline-grid').remove();
        g.selectAll('.timeline-label').remove();

        const [minDate, maxDate] = xScale.domain();

        // Create grid group
        const gridGroup = g.append('g').attr('class', 'timeline-grid');

        // Generate year boundaries
        const years = [];
        let currentYear = minDate.getFullYear();
        const maxYear = maxDate.getFullYear();

        while (currentYear <= maxYear) {
            const yearDate = new Date(currentYear, 0, 1); // January 1st
            if (yearDate >= minDate && yearDate <= maxDate) {
                years.push({ date: yearDate, year: currentYear });
            }
            currentYear++;
        }

        // Draw vertical grid lines for each year
        years.forEach(item => {
            const x = xScale(item.date);

            gridGroup.append('line')
                .attr('class', 'timeline-grid-line--year')
                .attr('x1', x)
                .attr('y1', -10)
                .attr('x2', x)
                .attr('y2', yScale.range()[1]);
        });

        const labelGroup = g.append('g').attr('class', 'timeline-label');

        years.forEach(item => {
            const x = xScale(item.date);

            labelGroup.append('text')
                .attr('class', 'timeline-label--year')
                .attr('x', x + 5)
                .attr('y', -20)
                .text(item.year);
        });

        // Add month markers (very light, minimal) within each year
        const months = [];
        let currentDate = new Date(minDate);
        currentDate.setDate(1); // Start of month
        currentDate.setHours(0, 0, 0, 0);

        while (currentDate <= maxDate) {
            months.push(new Date(currentDate));
            currentDate.setMonth(currentDate.getMonth() + 1);
        }

        months.forEach(date => {
            const x = xScale(date);

            gridGroup.append('line')
                .attr('class', 'timeline-grid-line--month')
                .attr('x1', x)
                .attr('y1', -5)
                .attr('x2', x)
                .attr('y2', yScale.range()[1]);

            const month = date.toLocaleDateString('en-US', { month: 'short' });
            labelGroup.append('text')
                .attr('class', 'timeline-label--month')
                .attr('x', x + 2)
                .attr('y', -5)
                .text(month);
        });
    }

    function updateScales() {
        const timestamps = commitData.map(c => new Date(c.timestamp));
        xScale.domain(d3.extent(timestamps));

        // Assign y-positions based on branches
        const branchYMap = new Map();
        branchData.forEach((branch, i) => {
            branchYMap.set(branch.name, i * config.branchSpacing);
        });

        yScale.domain([0, branchData.length * config.branchSpacing]);

        return branchYMap;
    }

    function renderBranchLines() {
        // Clear existing branch lines
        g.selectAll('.branch-group').remove();

        const branchYMap = updateScales();

        console.log('Rendering', branchData.length, 'branches');

        branchData.forEach((branch, i) => {
            const y = i * config.branchSpacing;

            const branchGroup = g.append('g')
                .attr('class', 'branch-group');

            // Get commits for this branch to determine line start and end
            const branchCommits = commitData.filter(c =>
                c.branches && c.branches.includes(branch.name)
            );

            console.log(`Branch ${branch.name}: ${branchCommits.length} commits`);

            if (branchCommits.length === 0) {
                console.warn('No commits found for branch:', branch.name);
                return; // Skip branches with no commits
            }

            // Find the earliest (leftmost) commit on this branch
            const earliestCommit = branchCommits.reduce((earliest, current) =>
                new Date(current.timestamp) < new Date(earliest.timestamp) ? current : earliest
            );
            const lineStartX = xScale(new Date(earliestCommit.timestamp)) - 20; // Start 20px before first commit

            // Find the latest (rightmost) commit on this branch
            const latestCommit = branchCommits.reduce((latest, current) =>
                new Date(current.timestamp) > new Date(latest.timestamp) ? current : latest
            );
            const lineEndX = xScale(new Date(latestCommit.timestamp)) + 50; // End 50px after last commit

            console.log(`  Line: ${lineStartX.toFixed(0)} ? ${lineEndX.toFixed(0)}, Y: ${y}`);

            const branchLine = branchGroup.append('line')
                .attr('class', 'branch-line')
                .attr('x1', lineStartX)
                .attr('y1', y)
                .attr('x2', lineEndX)
                .attr('y2', y);
            setTimeout(() => branchLine.classed('is-visible', true), 0);

            const fullName = branch.name.replace(/^origin\//, '');
            const boxSize = 8;
            const boxX = lineStartX - 15;

            const indicatorBox = branchGroup.append('rect')
                .attr('class', `branch-indicator ${getBranchClass(branch.name)}`)
                .attr('x', boxX)
                .attr('y', y - boxSize / 2)
                .attr('width', boxSize)
                .attr('height', boxSize)
                .attr('rx', 1);

            indicatorBox.append('title').text(fullName);
            setTimeout(() => indicatorBox.classed('is-visible', true), 0);
        });

        console.log('Branch rendering complete');
    }

    function renderCommits() {
        // Clear existing commits
        g.selectAll('.commit-node').remove();
        g.selectAll('.commit-link').remove();
        g.selectAll('.branch-connection').remove();
        g.selectAll('.cross-branch-connection').remove();

        const branchYMap = new Map();
        branchData.forEach((branch, i) => {
            branchYMap.set(branch.name, i * config.branchSpacing);
        });

        // Build a map of commits by SHA for quick lookup
        const commitMap = new Map();
        commitData.forEach(commit => {
            commitMap.set(commit.sha, commit);
        });

        // Draw cross-branch connections (from merge base on main to first commit on branch)
        const relationships = window.LaniusApp.state.relationships || [];
        console.log('Drawing cross-branch connections for', relationships.length, 'relationships');

        relationships.forEach(rel => {
            const mergeBaseCommit = commitMap.get(rel.commitSha);
            if (!mergeBaseCommit) return;

            // Find the first commit on branch2 (the child branch)
            const branch2Commits = commitData.filter(c =>
                c.branches && c.branches.includes(rel.branch2)
            ).sort((a, b) => new Date(a.timestamp) - new Date(b.timestamp));

            if (branch2Commits.length > 0) {
                const firstCommitOnBranch = branch2Commits[0];

                console.log(`Cross-branch: ${rel.branch1} ? ${rel.branch2}`,
                    'from', mergeBaseCommit.sha.substring(0, 7),
                    'to', firstCommitOnBranch.sha.substring(0, 7));

                // Draw diagonal line from merge base to first commit on branch
                g.append('line')
                    .attr('class', 'cross-branch-connection')
                    .attr('x1', xScale(new Date(mergeBaseCommit.timestamp)))
                    .attr('y1', getCommitY(mergeBaseCommit, branchYMap))
                    .attr('x2', xScale(new Date(firstCommitOnBranch.timestamp)))
                    .attr('y2', getCommitY(firstCommitOnBranch, branchYMap));
            }
        });
        setTimeout(() => g.selectAll('.cross-branch-connection').classed('is-visible', true), 0);

        // Draw branch connection lines (between commits on same branch)
        const branchLines = [];

        // Group commits by branch
        const commitsByBranch = new Map();
        commitData.forEach(commit => {
            if (commit.branches && commit.branches.length > 0) {
                commit.branches.forEach(branchName => {
                    if (!commitsByBranch.has(branchName)) {
                        commitsByBranch.set(branchName, []);
                    }
                    commitsByBranch.get(branchName).push(commit);
                });
            }
        });

        // For each branch, draw lines between its commits in chronological order
        commitsByBranch.forEach((commits, branchName) => {
            // Sort by timestamp (oldest first)
            const sortedCommits = commits.slice().sort((a, b) =>
                new Date(a.timestamp) - new Date(b.timestamp)
            );

            // Draw lines between consecutive commits
            for (let i = 0; i < sortedCommits.length - 1; i++) {
                const source = sortedCommits[i];
                const target = sortedCommits[i + 1];

                branchLines.push({
                    source,
                    target,
                    branch: branchName
                });
            }
        });

        // Draw branch connection lines (solid lines within same branch)
        g.selectAll('.branch-connection')
            .data(branchLines)
            .enter()
            .append('line')
            .attr('class', 'branch-connection')
            .attr('x1', d => xScale(new Date(d.source.timestamp)))
            .attr('y1', d => getCommitY(d.source, branchYMap))
            .attr('x2', d => xScale(new Date(d.target.timestamp)))
            .attr('y2', d => getCommitY(d.target, branchYMap));
        setTimeout(() => g.selectAll('.branch-connection').classed('is-visible', true), 0);

        // Draw commits
        const commitNodes = g.selectAll('.commit-node')
            .data(commitData)
            .enter()
            .append('g')
            .attr('class', d => `commit-node ${d.isSignificant ? 'is-significant' : ''} ${getBranchClass(d.branches?.[0])}`)
            .attr('transform', d => `translate(${xScale(new Date(d.timestamp))}, ${getCommitY(d, branchYMap)})`)
            .on('click', (event, d) => window.LaniusApp.showCommitDetail(d))
            .on('mouseenter', handleCommitHover)
            .on('mouseleave', handleCommitUnhover);

        commitNodes.append('circle')
            .attr('r', 0)
            .transition()
            .duration(500)
            .attr('r', d => getCommitSize(d));
        setTimeout(() => g.selectAll('.commit-node').classed('is-visible', true), 0);

        // Update stats
        updateStatsFromCommits();
    }

    function animateNewCommit(commit) {
        const branchYMap = new Map();
        branchData.forEach((branch, i) => {
            branchYMap.set(branch.name, i * config.branchSpacing);
        });

        const y = getCommitY(commit, branchYMap);
        const x = xScale(new Date(commit.timestamp));

        // Add commit node with animation
        const node = g.append('g')
            .attr('class', `commit-node is-visible ${commit.isSignificant ? 'is-significant' : ''} ${getBranchClass(commit.branches?.[0])}`)
            .attr('transform', `translate(${x}, ${y})`)
            .on('click', (event, d) => window.LaniusApp.showCommitDetail(commit))
            .on('mouseenter', handleCommitHover)
            .on('mouseleave', handleCommitUnhover);

        node.append('circle')
            .attr('r', 0)
            .transition()
            .duration(750)
            .ease(d3.easeCubicOut)
            .attr('r', getCommitSize(commit));

        // Pulse animation (radius only)
        node.select('circle')
            .transition()
            .delay(750)
            .duration(1000)
            .attr('r', getCommitSize(commit) * 1.5)
            .transition()
            .duration(500)
            .attr('r', getCommitSize(commit));
    }

    function animateReplayCommit(commit) {
        animateNewCommit(commit);

        // Update stats incrementally
        if (commit.stats) {
            const currentAdditions = parseInt(document.getElementById('stat-additions').textContent.replace('+', ''));
            const currentDeletions = parseInt(document.getElementById('stat-deletions').textContent.replace('-', ''));

            document.getElementById('stat-additions').textContent = `+${currentAdditions + commit.stats.linesAdded}`;
            document.getElementById('stat-deletions').textContent = `-${currentDeletions + commit.stats.linesRemoved}`;
        }

        const currentCommits = parseInt(document.getElementById('stat-commits').textContent);
        document.getElementById('stat-commits').textContent = currentCommits + 1;
    }

    function clearAll() {
        g.selectAll('*').remove();
        if (axisG) axisG.selectAll('*').remove();
        axisScale = null;
        currentLayout = null;
        commitData = [];
    }

    function getCommitY(commit, branchYMap) {
        // Use first branch for y-position
        if (commit.branches && commit.branches.length > 0) {
            const y = branchYMap.get(commit.branches[0]);
            if (y !== undefined) return y;
        }
        return 0;
    }

    function getCommitSize(commit) {
        if (!commit.stats) return config.commitRadius;

        // Scale based on total changes (logarithmic)
        const totalChanges = commit.stats.totalChanges || 0;
        const scale = Math.log(totalChanges + 1) / Math.log(100);
        return config.commitRadius + (scale * 3);
    }

    function handleCommitHover(event, d) {
        const node = d3.select(event.currentTarget);

        node.select('circle')
            .transition()
            .duration(200)
            .ease(d3.easeCubicOut)
            .attr('r', config.commitRadiusHover);

        showTooltip(event, d);
    }

    function handleCommitUnhover(event) {
        const node = d3.select(event.currentTarget);
        const commit = node.datum();

        node.select('circle')
            .transition()
            .duration(200)
            .ease(d3.easeCubicOut)
            .attr('r', getCommitSize(commit));

        hideTooltip();
    }

    function showTooltip(event, commit) {
        const tooltip = d3.select('body')
            .append('div')
            .attr('class', 'tooltip')
            .style('left', (event.pageX + 15) + 'px')
            .style('top', (event.pageY - 15) + 'px');

        tooltip.html(`
            <div><strong>${commit.shortMessage}</strong></div>
            <div>${commit.author}</div>
            <div>${new Date(commit.timestamp).toLocaleDateString()}</div>
            ${commit.stats ? `<div>+${commit.stats.linesAdded} -${commit.stats.linesRemoved}</div>` : ''}
        `);

        setTimeout(() => tooltip.classed('is-visible', true), 0);
    }

    function hideTooltip() {
        d3.selectAll('.tooltip').remove();
    }

    function updateStatsFromCommits() {
        const totalAdditions = commitData.reduce((sum, c) => sum + (c.stats?.linesAdded || 0), 0);
        const totalDeletions = commitData.reduce((sum, c) => sum + (c.stats?.linesRemoved || 0), 0);

        document.getElementById('stat-additions').textContent = `+${totalAdditions}`;
        document.getElementById('stat-deletions').textContent = `-${totalDeletions}`;
    }

    function handleResize() {
        const container = document.getElementById('commit-graph');
        const width = container.clientWidth;
        const height = container.clientHeight;

        svg.attr('width', width).attr('height', height);

        xScale.range([0, width - config.margin.left - config.margin.right]);
        yScale.range([0, height - config.margin.top - config.margin.bottom]);

        render(commitData, branchData);
        if (currentLayout?.mode === 'Timeline') {
            renderTimelineAxis();
        }
    }

    function debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    function initVisualization() {
        const container = d3.select('#commit-graph');

        // Calculate appropriate width based on time span
        const calculateWidth = () => {
            if (commitData.length === 0) return dimensions.width;

            const timestamps = commitData.map(c => new Date(c.timestamp));
            const [minDate, maxDate] = d3.extent(timestamps);
            const daysDiff = (maxDate - minDate) / (1000 * 60 * 60 * 24);

            // Allocate ~2px per day for readable spacing
            // Minimum of viewport width, maximum of 10x viewport width
            const calculatedWidth = Math.max(
                dimensions.width,
                Math.min(daysDiff * 2, dimensions.width * 10)
            );

            console.log(`Timeline span: ${daysDiff.toFixed(0)} days, calculated width: ${calculatedWidth.toFixed(0)}px`);
            return calculatedWidth;
        };

        const svgWidth = calculateWidth();

        svg = container
            .attr('width', '100%')
            .attr('height', dimensions.height)
            .attr('viewBox', null) // Remove viewBox to allow scrolling
            .style('display', 'block')
            .style('min-width', `${svgWidth}px`); // Set minimum width for scrolling

        g = svg.append('g')
            .attr('transform', `translate(${config.margin.left}, ${config.margin.top})`);

        // Update xScale range to use calculated width
        xScale.range([0, svgWidth - config.margin.left - config.margin.right]);
        yScale.range([0, dimensions.height - config.margin.top - config.margin.bottom]);

        console.log('Visualization initialized with width:', svgWidth);
    }

    function renderLayout(layout) {
        console.log('=== renderLayout START ===');
        console.log('Mode:', layout.mode);
        console.log('Nodes:', layout.nodes.length);
        console.log('Edges:', layout.edges.length);
        console.log('Dimensions:', layout.width, 'x', layout.height);

        currentLayout = layout;

        if (layout.nodes.length === 0) {
            console.warn('No nodes to render');
            clearAll();
            return;
        }

        try {
            // Clear existing visualization
            g.selectAll('*').remove();

            // Update canvas dimensions based on layout
            const container = document.getElementById('commit-graph');
            const svgW = Math.max(layout.width, container.clientWidth);
            const svgH = Math.max(layout.height, container.clientHeight);
            svg.attr('width', svgW).attr('height', svgH);

            // Configure axis scale from layout time range.
            // For Timeline mode, nodes are positioned in g-space with their own MarginX offset.
            // The g-group itself has a translate(config.margin.left, ...) transform.
            // Derive the pixel range from actual node positions + g-transform so axis ticks
            // land exactly on the nodes that represent those dates.
            const minTs = layout.minTimestamp ? new Date(layout.minTimestamp) : null;
            const maxTs = layout.maxTimestamp ? new Date(layout.maxTimestamp) : null;
            if (minTs && maxTs && !isNaN(minTs) && !isNaN(maxTs)) {
                if (layout.mode === 'Timeline' &&
                    layout.timelineOriginX != null && layout.timelinePixelsPerSecond != null) {
                    // g.attr('transform', currentZoom) at identity zoom = translate(0,0),
                    // so nodes render at raw node.x values in SVG space.
                    // Backend: node.x = timelineOriginX + (t - tMin).seconds * pps
                    // Axis range must use the same origin — do NOT add config.margin.left.
                    const originX = layout.timelineOriginX;
                    const spanSec = (maxTs - minTs) / 1000;
                    const totalPx = spanSec * layout.timelinePixelsPerSecond;
                    axisScale = d3.scaleTime()
                        .domain([minTs, maxTs])
                        .range([originX, originX + totalPx]);
                } else {
                    axisScale = d3.scaleTime()
                        .domain([minTs, maxTs])
                        .range([config.margin.left, svgW - config.margin.right]);
                }
            }

            // Render based on layout mode
            if (layout.mode === 'Calendar') {
                renderCalendarLayout(layout);
            } else {
                renderLogicalLayout(layout);
            }

            // Apply zoom transform; render axis for Timeline and Calendar modes
            g.attr('transform', currentZoom);
            if (layout.mode === 'Timeline' || layout.mode === 'Calendar') {
                renderTimelineAxis();
            } else if (axisG) {
                axisG.selectAll('*').remove();
            }

            console.log('=== renderLayout COMPLETE ===');
        } catch (error) {
            console.error('Error rendering layout:', error);
            console.error('Stack trace:', error.stack);
        }
    }

    function renderLogicalLayout(layout) {
        console.log('Rendering logical layout...');

        // Extract branch info and render branch lines/indicators
        const branchInfo = extractBranchInfo(layout.nodes);
        console.log('Branch info extracted:', branchInfo.size, 'branches');
        if (branchInfo.size > 0) {
            renderBranchLinesForLayout(branchInfo);
        }

        // Render edges (commit connections)
        const edgeGroups = g.selectAll('.edge')
            .data(layout.edges)
            .enter()
            .append('g')
            .attr('class', d => `edge edge-${d.type.toLowerCase()}${d.isLongSpan ? ' edge-longspan' : ''}`);

        edgeGroups.each(function (d) {
            const edge = d3.select(this);

            if (d.x1 !== undefined) {
                edge.append('line')
                    .attr('class', 'edge-line')
                    .attr('x1', d.x1)
                    .attr('y1', d.y1)
                    .attr('x2', d.x2)
                    .attr('y2', d.y2);
            }
        });
        setTimeout(() => edgeGroups.selectAll('.edge-line').classed('is-visible', true), 0);

        // Render nodes (commits) — exclude ghost/shadow-ref nodes (invisible anchors)
        const nodeGroups = g.selectAll('.commit-node')
            .data(layout.nodes.filter(n => !n.isGhost))
            .enter()
            .append('g')
            .attr('class', d => `commit-node ${d.isSignificant ? 'is-significant' : ''} ${getBranchClass(d.branchName)}`)
            .attr('data-commit-id', d => d.commitId)
            .attr('transform', d => `translate(${d.x}, ${d.y})`)
            .on('click', (event, d) => showNodeDetail(d))
            .on('mouseenter', handleNodeHover)
            .on('mouseleave', handleNodeUnhover);

        nodeGroups.append('circle')
            .attr('r', 0)
            .transition()
            .duration(500)
            .attr('r', d => d.radius);
        setTimeout(() => g.selectAll('.commit-node').classed('is-visible', true), 0);

        console.log('Logical layout rendered');
    }

    function renderCalendarLayout(layout) {
        console.log('Rendering calendar layout...');

        // Group nodes by period (they should already be positioned by backend)
        // For calendar layout, nodes represent time periods, not individual commits
        const nodeGroups = g.selectAll('.calendar-node')
            .data(layout.nodes)
            .enter()
            .append('g')
            .attr('class', 'calendar-node')
            .attr('transform', d => `translate(${d.x}, ${d.y})`)
            .on('click', (event, d) => showCalendarNodeDetail(d))
            .on('mouseenter', handleCalendarNodeHover)
            .on('mouseleave', handleCalendarNodeUnhover);

        nodeGroups.append('circle')
            .attr('r', 0)
            .transition()
            .duration(500)
            .attr('r', d => d.radius);

        nodeGroups.filter(d => d.radius > 8)
            .append('text')
            .attr('text-anchor', 'middle')
            .attr('dominant-baseline', 'middle')
            .text(d => {
                const match = d.message.match(/(\d+) commits?/);
                return match ? match[1] : '';
            });
        setTimeout(() => g.selectAll('.calendar-node').classed('is-visible', true), 0);

        console.log('Calendar layout rendered');
    }

    function renderTimelineAxis() {
        if (!axisG) return;

        // Calendar mode: ticks are anchored to node positions, not a continuous time scale
        if (currentLayout?.mode === 'Calendar') {
            renderCalendarAxis();
            return;
        }

        if (!axisScale) return;

        axisG.selectAll('*').remove();

        const svgW = parseFloat(svg.attr('width')) || 1200;
        const svgH = parseFloat(svg.attr('height')) || 600;

        const k = currentZoom.k;
        const rescaledX = currentZoom.rescaleX(axisScale);

        // Visible date range in current viewport
        const visibleMin = rescaledX.invert(0);
        const visibleMax = rescaledX.invert(svgW);

        // Tier rows stacked top-to-bottom inside config.margin.top (60px) space.
        // labelY = SVG text baseline; grid lines span full SVG height.
        // Use UTC variants so tick positions match the UTC-based timestamps from the backend.
        const tiers = [
            { name: 'year', interval: d3.utcYear, labelFn: d => d.getUTCFullYear(), labelY: 14, minK: 0 },
            { name: 'month', interval: d3.utcMonth, labelFn: d => d.toLocaleDateString('en-US', { month: 'short', timeZone: 'UTC' }), labelY: 30, minK: 0.3 },
            { name: 'week', interval: d3.utcWeek, labelFn: d => d.getUTCDate(), labelY: 44, minK: 1.0 },
            { name: 'day', interval: d3.utcDay, labelFn: d => d.getUTCDate(), labelY: 56, minK: 3.0 },
        ];

        tiers.forEach(({ name, interval, labelFn, labelY, minK }) => {
            if (k < minK) return;

            const ticks = interval.range(
                interval.floor(visibleMin),
                interval.ceil(visibleMax)
            );

            // Minimum pixel gap between labels of the same tier to prevent overlap
            const minGap = name === 'year' ? 60 : name === 'month' ? 36 : 28;
            let lastLabelX = -Infinity;

            ticks.forEach(date => {
                const x = rescaledX(date);
                if (x < -50 || x > svgW + 50) return;

                // Draw the grid line first (always)
                axisG.append('line')
                    .attr('class', `timeline-grid-line--${name}`)
                    .attr('x1', x).attr('y1', 0)
                    .attr('x2', x).attr('y2', svgH);

                // Skip label if it would overlap the previous one
                if (x - lastLabelX < minGap) return;
                lastLabelX = x;

                axisG.append('text')
                    .attr('class', `timeline-label--${name}`)
                    .attr('x', x + 3).attr('y', labelY)
                    .text(labelFn(date));
            });
        });

        // Baseline separator at the bottom of the axis band
        axisG.append('line')
            .attr('class', 'timeline-axis-baseline')
            .attr('x1', 0).attr('y1', config.margin.top - 2)
            .attr('x2', svgW).attr('y2', config.margin.top - 2);
    }

    function renderCalendarAxis() {
        if (!axisG || !currentLayout?.nodes?.length) return;
        if (!currentLayout.minTimestamp || !currentLayout.maxTimestamp) return;

        axisG.selectAll('*').remove();

        const svgW = parseFloat(svg.attr('width')) || 1200;
        const svgH = parseFloat(svg.attr('height')) || 600;

        const marginX = 50;
        const colW = currentLayout.calendarColumnWidthPx; // null for year granularity
        const nodes = currentLayout.nodes.slice().sort((a, b) => new Date(a.timestamp) - new Date(b.timestamp));
        const minTs = new Date(currentLayout.minTimestamp);
        const maxTs = new Date(currentLayout.maxTimestamp);

        // --- column-based axis (month / week / day) ---
        if (colW) {
            const gran = (currentLayout.calendarGranularity || 'Month').toLowerCase();

            // Helper: ISO week number (1-53)
            function isoWeekNumber(date) {
                const d = new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()));
                const dayNum = d.getUTCDay() || 7;
                d.setUTCDate(d.getUTCDate() + 4 - dayNum);
                const yearStart = new Date(Date.UTC(d.getUTCFullYear(), 0, 1));
                return Math.ceil((((d - yearStart) / 86400000) + 1) / 7);
            }

            if (gran === 'week') {
                // --- WEEK axis  (3 tiers: year → month → week number) ---
                // All positions: gX = marginX + (date - minTs) / (7 days) * colW
                const MS_PER_WEEK = 7 * 24 * 3600 * 1000;
                function dateToGX(date) {
                    return marginX + ((date - minTs) / MS_PER_WEEK) * colW;
                }
                function dateToSvgX(date) {
                    return currentZoom.applyX(dateToGX(date));
                }

                const colWidthSvgPx = colW * currentZoom.k;
                const firstYear = minTs.getFullYear();
                const lastYear  = maxTs.getFullYear();

                // Tier 1 — Year labels (y=14) + heavy year lines
                let lastYearLabelX = -Infinity;
                for (let y = firstYear; y <= lastYear + 1; y++) {
                    const svgX = dateToSvgX(new Date(y, 0, 1));
                    if (svgX < -50 || svgX > svgW + 50) continue;
                    axisG.append('line')
                        .attr('class', 'timeline-grid-line--year')
                        .attr('x1', svgX).attr('y1', 0)
                        .attr('x2', svgX).attr('y2', svgH);
                    if (svgX - lastYearLabelX >= 40) {
                        axisG.append('text')
                            .attr('class', 'timeline-label--year')
                            .attr('x', svgX + 3).attr('y', 14)
                            .text(y);
                        lastYearLabelX = svgX;
                    }
                }

                // Tier 2 — Month labels (y=30) + lighter month lines
                let lastMonthLabelX = -Infinity;
                for (let y = firstYear; y <= lastYear; y++) {
                    for (let m = 0; m < 12; m++) {
                        const monthStart = new Date(y, m, 1);
                        const svgX = dateToSvgX(monthStart);
                        if (svgX < -50 || svgX > svgW + 50) continue;
                        if (m > 0) { // Jan already covered by year line
                            axisG.append('line')
                                .attr('class', 'timeline-grid-line--month')
                                .attr('x1', svgX).attr('y1', 0)
                                .attr('x2', svgX).attr('y2', svgH);
                        }
                        if (svgX - lastMonthLabelX >= 24) {
                            axisG.append('text')
                                .attr('class', 'timeline-label--month')
                                .attr('x', svgX + 3).attr('y', 30)
                                .text(monthStart.toLocaleDateString('en-US', { month: 'short' }));
                            lastMonthLabelX = svgX;
                        }
                    }
                }

                // Tier 3 — Week-number labels (y=46) for ALL weeks in range
                const fmtDate = d => d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
                let lastWeekLabelX = -Infinity;
                const cursor = new Date(minTs); // minTs is always Monday (from backend)
                while (cursor <= maxTs) {
                    const svgX = dateToSvgX(cursor);
                    if (svgX >= -50 && svgX <= svgW + 50) {
                        const wn = isoWeekNumber(cursor);
                        const monday = new Date(cursor);
                        const sunday = new Date(cursor);
                        sunday.setDate(sunday.getDate() + 6);

                        const labelX = svgX + colWidthSvgPx / 2;
                        if (colWidthSvgPx >= 10 && labelX - lastWeekLabelX >= colWidthSvgPx * 0.85) {
                            const t = axisG.append('text')
                                .attr('class', 'timeline-label--week')
                                .attr('x', labelX)
                                .attr('y', 46)
                                .attr('text-anchor', 'middle')
                                .text(wn);
                            t.append('title')
                                .text(`Week ${wn}, ${fmtDate(monday)} – ${fmtDate(sunday)}`);
                            lastWeekLabelX = labelX;
                        }
                    }
                    cursor.setDate(cursor.getDate() + 7);
                }

            } else if (gran === 'day') {
                // --- DAY axis  (3 tiers: year → month → day-of-month) ---
                const MS_PER_DAY = 24 * 3600 * 1000;
                function dateToDayGX(date) {
                    return marginX + ((date - minTs) / MS_PER_DAY) * colW;
                }
                function dateToDaySvgX(date) {
                    return currentZoom.applyX(dateToDayGX(date));
                }

                const colWidthSvgPx = colW * currentZoom.k;
                const firstYear = minTs.getFullYear();
                const lastYear  = maxTs.getFullYear();

                // Tier 1 — Year labels (y=14) + heavy year lines
                let lastYearLabelX = -Infinity;
                for (let y = firstYear; y <= lastYear + 1; y++) {
                    const svgX = dateToDaySvgX(new Date(y, 0, 1));
                    if (svgX < -50 || svgX > svgW + 50) continue;
                    axisG.append('line')
                        .attr('class', 'timeline-grid-line--year')
                        .attr('x1', svgX).attr('y1', 0)
                        .attr('x2', svgX).attr('y2', svgH);
                    if (svgX - lastYearLabelX >= 40) {
                        axisG.append('text')
                            .attr('class', 'timeline-label--year')
                            .attr('x', svgX + 3).attr('y', 14)
                            .text(y);
                        lastYearLabelX = svgX;
                    }
                }

                // Tier 2 — Month labels (y=30) + lighter month lines
                let lastMonthLabelX = -Infinity;
                for (let y = firstYear; y <= lastYear; y++) {
                    for (let m = 0; m < 12; m++) {
                        const monthStart = new Date(y, m, 1);
                        const svgX = dateToDaySvgX(monthStart);
                        if (svgX < -50 || svgX > svgW + 50) continue;
                        if (m > 0) {
                            axisG.append('line')
                                .attr('class', 'timeline-grid-line--month')
                                .attr('x1', svgX).attr('y1', 0)
                                .attr('x2', svgX).attr('y2', svgH);
                        }
                        if (svgX - lastMonthLabelX >= 24) {
                            axisG.append('text')
                                .attr('class', 'timeline-label--month')
                                .attr('x', svgX + 3).attr('y', 30)
                                .text(monthStart.toLocaleDateString('en-US', { month: 'short' }));
                            lastMonthLabelX = svgX;
                        }
                    }
                }

                // Tier 3 — Day-of-month labels (y=46) for every calendar day in range
                const fmtFull = d => d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
                let lastDayLabelX = -Infinity;
                const cursor = new Date(minTs.getFullYear(), minTs.getMonth(), minTs.getDate());
                const endDate = new Date(maxTs.getFullYear(), maxTs.getMonth(), maxTs.getDate());
                while (cursor <= endDate) {
                    const svgX = dateToDaySvgX(cursor);
                    if (svgX >= -50 && svgX <= svgW + 50) {
                        const day = cursor.getDate();
                        const labelX = svgX + colWidthSvgPx / 2;
                        if (colWidthSvgPx >= 8 && labelX - lastDayLabelX >= colWidthSvgPx * 0.85) {
                            const t = axisG.append('text')
                                .attr('class', 'timeline-label--week') // reuse same style as week numbers
                                .attr('x', labelX)
                                .attr('y', 46)
                                .attr('text-anchor', 'middle')
                                .text(day);
                            t.append('title').text(fmtFull(cursor));
                            lastDayLabelX = labelX;
                        }
                    }
                    cursor.setDate(cursor.getDate() + 1);
                }

            } else {
                // --- MONTH axis (existing logic) ---
                const firstYear   = minTs.getFullYear();
                const firstMonth0 = minTs.getMonth();
                const lastYear    = maxTs.getFullYear();
                const lastMonth0  = maxTs.getMonth();

                function colToSvgX(colIdx) {
                    return currentZoom.applyX(marginX + colIdx * colW);
                }

                let lastYearLabelX  = -Infinity;
                const colWidthSvgPx = colW * currentZoom.k;

                for (let y = firstYear; y <= lastYear; y++) {
                    const mStart = (y === firstYear) ? firstMonth0 : 0;
                    const mEnd   = (y === lastYear)  ? lastMonth0  : 11;
                    for (let m = mStart; m <= mEnd; m++) {
                        const colIdx = (y - firstYear) * 12 + m - firstMonth0;
                        const svgX   = colToSvgX(colIdx);
                        if (svgX < -50 || svgX > svgW + 50) continue;

                        const isJan = m === 0;
                        axisG.append('line')
                            .attr('class', isJan ? 'timeline-grid-line--year' : 'timeline-grid-line--month')
                            .attr('x1', svgX).attr('y1', 0)
                            .attr('x2', svgX).attr('y2', svgH);

                        if (isJan && svgX - lastYearLabelX >= 40) {
                            axisG.append('text')
                                .attr('class', 'timeline-label--year')
                                .attr('x', svgX + 3).attr('y', 45)
                                .text(y);
                            lastYearLabelX = svgX;
                        }
                    }
                }

                // Right-edge closing line
                const totalCols = (lastYear - firstYear) * 12 + lastMonth0 - firstMonth0 + 1;
                const closeX = colToSvgX(totalCols);
                if (closeX >= -50 && closeX <= svgW + 50) {
                    axisG.append('line')
                        .attr('class', 'timeline-grid-line--year')
                        .attr('x1', closeX).attr('y1', 0)
                        .attr('x2', closeX).attr('y2', svgH);
                }

                // Month name labels at active nodes
                if (colWidthSvgPx >= 18) {
                    nodes.forEach(node => {
                        const svgX = currentZoom.applyX(node.x);
                        if (svgX < -50 || svgX > svgW + 50) return;
                        const label = new Date(node.timestamp).toLocaleDateString('en-US', { month: 'short' });
                        axisG.append('text')
                            .attr('class', 'timeline-label--month')
                            .attr('x', svgX)
                            .attr('y', 20)
                            .attr('text-anchor', 'middle')
                            .text(label);
                    });
                }
            }

        } else {
            // --- proportional time-based axis (year granularity) ---
            const usableWidth = currentLayout.width - 2 * marginX;
            const totalSpanMs = maxTs - minTs;

            function calendarDateToSvgX(date) {
                const gX = marginX + ((date - minTs) / totalSpanMs) * usableWidth;
                return currentZoom.applyX(gX);
            }

            const firstYear = minTs.getFullYear();
            const lastYear  = maxTs.getFullYear();
            let lastYearLabelX = -Infinity;

            for (let y = firstYear; y <= lastYear + 1; y++) {
                const svgX = calendarDateToSvgX(new Date(y, 0, 1));
                if (svgX < -50 || svgX > svgW + 50) continue;

                axisG.append('line')
                    .attr('class', 'timeline-grid-line--year')
                    .attr('x1', svgX).attr('y1', 0)
                    .attr('x2', svgX).attr('y2', svgH);

                if (svgX - lastYearLabelX >= 40) {
                    axisG.append('text')
                        .attr('class', 'timeline-label--year')
                        .attr('x', svgX + 3).attr('y', 50)
                        .text(y);
                    lastYearLabelX = svgX;
                }
            }
        }

        // Baseline separator
        axisG.append('line')
            .attr('class', 'timeline-axis-baseline')
            .attr('x1', 0).attr('y1', config.margin.top - 2)
            .attr('x2', svgW).attr('y2', config.margin.top - 2);
    }

    function showCalendarNodeDetail(node) {
        if (window.LaniusApp && window.LaniusApp.showCalendarGroupDetail) {
            window.LaniusApp.showCalendarGroupDetail(node);
        }
    }

    function handleCalendarNodeHover(event, d) {
        const node = d3.select(event.currentTarget);

        node.select('circle')
            .transition()
            .duration(200)
            .ease(d3.easeCubicOut)
            .attr('r', d.radius * 1.3);

        showCalendarNodeTooltip(event, d);
    }

    function handleCalendarNodeUnhover(event, d) {
        const node = d3.select(event.currentTarget);

        node.select('circle')
            .transition()
            .duration(200)
            .ease(d3.easeCubicOut)
            .attr('r', d.radius);

        hideTooltip();
    }

    function showCalendarNodeTooltip(event, node) {
        const tooltip = d3.select('body')
            .append('div')
            .attr('class', 'tooltip')
            .style('left', (event.pageX + 15) + 'px')
            .style('top', (event.pageY - 15) + 'px');

        tooltip.html(`
            <div><strong>${node.message || 'Calendar Group'}</strong></div>
            <div>${new Date(node.timestamp).toLocaleDateString('en-US', { month: 'long', year: 'numeric' })}</div>
        `);

        setTimeout(() => tooltip.classed('is-visible', true), 0);
    }

    function extractBranchInfo(nodes) {
        const branchInfo = new Map();

        nodes.forEach(node => {
            if (!branchInfo.has(node.branchName)) {
                branchInfo.set(node.branchName, {
                    y: node.y,
                    minX: node.x,
                    maxX: node.x,
                    index: branchInfo.size
                });
            } else {
                const info = branchInfo.get(node.branchName);
                info.minX = Math.min(info.minX, node.x);
                info.maxX = Math.max(info.maxX, node.x);
            }
        });

        return branchInfo;
    }

    function renderBranchLinesForLayout(branchInfo) {
        branchInfo.forEach((info, branchName) => {
            const branchGroup = g.append('g').attr('class', 'branch-group');

            // Branch line
            const lineStartX = info.minX - 20;
            const lineEndX = info.maxX + 50;

            const branchLine = branchGroup.append('line')
                .attr('class', 'branch-line')
                .attr('x1', lineStartX)
                .attr('y1', info.y)
                .attr('x2', lineEndX)
                .attr('y2', info.y);
            setTimeout(() => branchLine.classed('is-visible', true), 0);

            const boxSize = 8;
            const boxX = lineStartX - 15;
            const fullName = branchName.replace(/^origin\//, '');

            const indicator = branchGroup.append('rect')
                .attr('class', `branch-indicator ${getBranchClass(branchName)}`)
                .attr('x', boxX)
                .attr('y', info.y - boxSize / 2)
                .attr('width', boxSize)
                .attr('height', boxSize)
                .attr('rx', 1);

            indicator.append('title').text(fullName);
            setTimeout(() => indicator.classed('is-visible', true), 0);
        });
    }

    function showNodeDetail(node) {
        if (window.LaniusApp && window.LaniusApp.showCommitDetail) {
            const commit = {
                sha: node.commitId,
                author: node.author || 'Unknown',
                authorEmail: node.authorEmail || '',
                timestamp: node.timestamp,
                committer: node.committer || '',
                committerEmail: node.committerEmail || '',
                committerTimestamp: node.committerTimestamp || node.timestamp,
                message: node.message || '',
                branches: [node.branchName],
                parentShas: node.parentShas || [],
                stats: null
            };
            window.LaniusApp.showCommitDetail(commit);
        }
    }

    function handleNodeHover(event, d) {
        const node = d3.select(event.currentTarget);

        node.select('circle')
            .transition()
            .duration(200)
            .ease(d3.easeCubicOut)
            .attr('r', d.radius * 1.5);

        showNodeTooltip(event, d);
    }

    function handleNodeUnhover(event, d) {
        const node = d3.select(event.currentTarget);

        node.select('circle')
            .transition()
            .duration(200)
            .ease(d3.easeCubicOut)
            .attr('r', d.radius);

        hideTooltip();
    }

    function showNodeTooltip(event, node) {
        const tooltip = d3.select('body')
            .append('div')
            .attr('class', 'tooltip')
            .style('left', (event.pageX + 15) + 'px')
            .style('top', (event.pageY - 15) + 'px');

        tooltip.html(`
            <div><strong>${node.message || 'Commit'}</strong></div>
            <div>${node.author || 'Unknown author'}</div>
            <div>${new Date(node.timestamp).toLocaleDateString()}</div>
            <div>Branch: ${node.branchName}</div>
            ${node.isSignificant ? '<div class="tooltip-significant">Significant commit</div>' : ''}
        `);

        setTimeout(() => tooltip.classed('is-visible', true), 0);
    }

    // Replay mode functions
    function startReplayMode() {
        // Hide all commit nodes; they will be revealed one-by-one via revealCommit()
        g.selectAll('.commit-node').classed('replay-hidden', true);
    }

    function stopReplayMode() {
        g.selectAll('.commit-node').classed('replay-hidden', false);
    }

    function revealCommit(sha) {
        g.selectAll('.commit-node')
            .filter(function () { return d3.select(this).attr('data-commit-id') === sha; })
            .classed('replay-hidden', false);

        scrollToCommit(sha);
    }

    function revealRange(shas) {
        const shaSet = new Set(shas);
        g.selectAll('.commit-node')
            .filter(function () { return shaSet.has(d3.select(this).attr('data-commit-id')); })
            .classed('replay-hidden', false);
    }

    function scrollToCommit(sha) {
        if (!currentLayout || !svg || !zoomBehavior) return;

        const node = currentLayout.nodes.find(n => n.commitId === sha);
        if (!node) return;

        const svgEl = document.getElementById('commit-graph');
        const container = svgEl.parentElement;
        container.scrollLeft = 0;

        const viewW = container.clientWidth || 1200;
        const viewH = container.clientHeight || 600;
        const k = currentZoom.k;

        const newTx = viewW / 2 - k * node.x;
        const newTy = viewH / 2 - k * node.y;
        const newTransform = d3.zoomIdentity.translate(newTx, newTy).scale(k);

        svg.transition()
            .duration(400)
            .ease(d3.easeCubicOut)
            .call(zoomBehavior.transform, newTransform);
    }

    function jumpToCommit(sha) {
        if (!currentLayout || !svg || !zoomBehavior) return;

        const node = currentLayout.nodes.find(n => n.commitId === sha);
        if (!node) return;

        const svgEl = document.getElementById('commit-graph');
        const container = svgEl.parentElement;
        container.scrollLeft = 0;

        const viewW = container.clientWidth || 1200;
        const viewH = container.clientHeight || 600;
        const k = currentZoom.k;

        const newTx = viewW / 2 - k * node.x;
        const newTy = viewH / 2 - k * node.y;
        const newTransform = d3.zoomIdentity.translate(newTx, newTy).scale(k);

        // Apply immediately (no transition) — used after seek to override any in-flight scrolls
        svg.call(zoomBehavior.transform, newTransform);
    }

    // Zoom control functions
    function resetZoom() {
        if (svg && zoomBehavior) {
            svg.transition()
                .duration(750)
                .call(zoomBehavior.transform, d3.zoomIdentity);
        }
    }

    function zoomIn() {
        if (svg && zoomBehavior) {
            svg.transition()
                .duration(300)
                .call(zoomBehavior.scaleBy, 1.3);
        }
    }

    function zoomOut() {
        if (svg && zoomBehavior) {
            svg.transition()
                .duration(300)
                .call(zoomBehavior.scaleBy, 0.7);
        }
    }

    return {
        initialize,
        render,
        renderLayout,
        animateNewCommit,
        animateReplayCommit,
        clear: clearAll,
        resetZoom,
        zoomIn,
        zoomOut,
        startReplayMode,
        stopReplayMode,
        revealCommit,
        revealRange,
        scrollToCommit,
        jumpToCommit
    };
})();

// Initialize visualization
Visualization.initialize();

// Export to global scope
window.renderVisualization = () => {
    const layoutData = window.LaniusApp.state.layoutData;
    if (layoutData) {
        Visualization.renderLayout(layoutData);
    } else {
        // Fallback to old method if no layout data
        Visualization.render(window.LaniusApp.state.commits, window.LaniusApp.state.branches);
    }
};

window.clearVisualization = () => {
    Visualization.clear();
};

window.animateNewCommit = (commit) => {
    Visualization.animateNewCommit(commit);
};

window.animateReplayCommit = (commit) => {
    Visualization.animateReplayCommit(commit);
};

window.animateNewCommit = (commit) => {
    Visualization.animateNewCommit(commit);
};

window.animateReplayCommit = (commit) => {
    Visualization.animateReplayCommit(commit);
};
