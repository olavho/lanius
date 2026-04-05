// D3.js Visualization Module - Minimalist Sci-Fi Aesthetic
// Thin black/dark gray lines on light background, smooth animations

const Visualization = (() => {
    let svg, g, xScale, yScale, zoomBehavior;
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
        colors: {
            commitDefault: '#1a1a1a',
            commitAdditions: '#2d2d2d',
            commitDeletions: '#0a0a0a',
            link: '#4a4a4a',
            branchLine: '#1a1a1a',
            branchLabel: '#666666'
        }
    };

    function initialize() {
        const container = document.getElementById('commit-graph');
        const width = container.clientWidth;
        const height = container.clientHeight;

        svg = d3.select('#commit-graph')
            .attr('width', width)
            .attr('height', height);

        g = svg.append('g')
            .attr('transform', `translate(${config.margin.left}, ${config.margin.top})`);

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
                .attr('x1', x)
                .attr('y1', -10) // Start just above branches (was -config.margin.top + 30)
                .attr('x2', x)
                .attr('y2', yScale.range()[1])
                .attr('stroke', '#d0d0d0')
                .attr('stroke-width', 1)
                .attr('opacity', 0.4);
        });

        // Draw labels at top for years - more compact positioning
        const labelGroup = g.append('g').attr('class', 'timeline-label');

        years.forEach(item => {
            const x = xScale(item.date);

            // Draw year label - more compact positioning
            labelGroup.append('text')
                .attr('x', x + 5)
                .attr('y', -20) // Closer to branches (was -config.margin.top + 25)
                .attr('font-size', '11px')
                .attr('font-weight', 'bold')
                .attr('fill', config.colors.commitDefault)
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

        // Draw very light month lines (minimal visual impact)
        months.forEach(date => {
            const x = xScale(date);

            gridGroup.append('line')
                .attr('x1', x)
                .attr('y1', -5) // Start just above branches
                .attr('x2', x)
                .attr('y2', yScale.range()[1])
                .attr('stroke', '#f0f0f0')
                .attr('stroke-width', 0.5)
                .attr('opacity', 0.15);

            // Add tiny month label (optional - can remove if too cluttered)
            const month = date.toLocaleDateString('en-US', { month: 'short' });
            labelGroup.append('text')
                .attr('x', x + 2)
                .attr('y', -5) // Very close to branches (was -config.margin.top + 45)
                .attr('font-size', '8px')
                .attr('fill', '#aaa')
                .attr('opacity', 0.5)
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

            // Branch line - start at first commit, end at last commit
            branchGroup.append('line')
                .attr('class', 'branch-line')
                .attr('x1', lineStartX)
                .attr('y1', y)
                .attr('x2', lineEndX)
                .attr('y2', y)
                .attr('stroke', config.colors.branchLine)
                .attr('stroke-width', config.lineWidth)
                .attr('opacity', 0)
                .transition()
                .duration(500)
                .attr('opacity', 0.3);

            // Branch indicator box - small colored box at start of line
            const fullName = branch.name.replace(/^origin\//, '');
            const boxSize = 8;
            const boxX = lineStartX - 15; // Position box slightly before line start

            const indicatorBox = branchGroup.append('rect')
                .attr('class', 'branch-indicator')
                .attr('x', boxX)
                .attr('y', y - boxSize / 2)
                .attr('width', boxSize)
                .attr('height', boxSize)
                .attr('fill', getBranchColor(branch.name, i))
                .attr('stroke', config.colors.commitDefault)
                .attr('stroke-width', 1)
                .attr('rx', 1) // Slight rounding
                .style('cursor', 'help');

            // Add hover tooltip showing full branch name (BEFORE transition)
            indicatorBox.append('title').text(fullName);

            // Add hover highlight effect (BEFORE transition)
            indicatorBox.on('mouseenter', function () {
                d3.select(this)
                    .transition()
                    .duration(200)
                    .attr('opacity', 1)
                    .attr('stroke-width', 2);
            }).on('mouseleave', function () {
                d3.select(this)
                    .transition()
                    .duration(200)
                    .attr('opacity', 0.8)
                    .attr('stroke-width', 1);
            });

            // Apply fade-in transition AFTER appending title and events
            indicatorBox
                .attr('opacity', 0)
                .transition()
                .duration(500)
                .attr('opacity', 0.8);
        });

        console.log('Branch rendering complete');
    }

    function getBranchColor(branchName, index) {
        // Color based on branch type
        if (branchName === 'main' || branchName === 'master' || branchName === 'origin/main') {
            return '#2d2d2d'; // Dark for main
        } else if (branchName.includes('release')) {
            return '#4a90e2'; // Blue for releases
        } else if (branchName.includes('feature')) {
            return '#7ed321'; // Green for features
        } else if (branchName.includes('hotfix') || branchName.includes('fix')) {
            return '#e74c3c'; // Red for fixes
        } else if (branchName.includes('dependabot')) {
            return '#9b59b6'; // Purple for dependabot
        } else {
            // Use a color from palette based on index
            const colors = ['#34495e', '#16a085', '#f39c12', '#e67e22', '#95a5a6'];
            return colors[index % colors.length];
        }
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
                    .attr('y2', getCommitY(firstCommitOnBranch, branchYMap))
                    .attr('stroke', config.colors.link)
                    .attr('stroke-width', config.lineWidth)
                    .attr('stroke-dasharray', '3,3') // Dashed line for branch connections
                    .attr('opacity', 0)
                    .transition()
                    .duration(500)
                    .attr('opacity', 0.4);
            }
        });

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
            .attr('y2', d => getCommitY(d.target, branchYMap))
            .attr('stroke', config.colors.link)
            .attr('stroke-width', config.lineWidth)
            .attr('opacity', 0)
            .transition()
            .duration(500)
            .attr('opacity', 0.6);

        // Draw commits
        const commitNodes = g.selectAll('.commit-node')
            .data(commitData)
            .enter()
            .append('g')
            .attr('class', 'commit-node')
            .attr('transform', d => `translate(${xScale(new Date(d.timestamp))}, ${getCommitY(d, branchYMap)})`)
            .on('click', (event, d) => window.LaniusApp.showCommitDetail(d))
            .on('mouseenter', handleCommitHover)
            .on('mouseleave', handleCommitUnhover);

        commitNodes.append('circle')
            .attr('r', 0)
            .attr('fill', d => getCommitColor(d))
            .attr('stroke', config.colors.commitDefault)
            .attr('stroke-width', config.lineWidth)
            .transition()
            .duration(500)
            .attr('r', d => getCommitSize(d));

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
            .attr('class', 'commit-node fade-in')
            .attr('transform', `translate(${x}, ${y})`)
            .on('click', (event, d) => window.LaniusApp.showCommitDetail(commit))
            .on('mouseenter', handleCommitHover)
            .on('mouseleave', handleCommitUnhover);

        node.append('circle')
            .attr('r', 0)
            .attr('fill', getCommitColor(commit))
            .attr('stroke', config.colors.commitDefault)
            .attr('stroke-width', config.lineWidth)
            .style('opacity', 0)
            .transition()
            .duration(750)
            .ease(d3.easeCubicOut)
            .attr('r', getCommitSize(commit))
            .style('opacity', 1);

        // Pulse animation
        node.select('circle')
            .transition()
            .delay(750)
            .duration(1000)
            .attr('r', getCommitSize(commit) * 1.5)
            .style('opacity', 0.4)
            .transition()
            .duration(500)
            .attr('r', getCommitSize(commit))
            .style('opacity', 1);
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

    function getCommitColor(commit) {
        if (!commit.stats) return config.colors.commitDefault;

        const indicator = commit.stats.colorIndicator || 0;

        // Monochrome gradient based on indicator
        // -1 (deletions) to +1 (additions)
        if (indicator > 0) {
            // More additions: darker
            const intensity = Math.floor(indicator * 30);
            return `rgb(${45 - intensity}, ${45 - intensity}, ${45 - intensity})`;
        } else if (indicator < 0) {
            // More deletions: lighter
            const intensity = Math.floor(Math.abs(indicator) * 20);
            return `rgb(${10 + intensity}, ${10 + intensity}, ${10 + intensity})`;
        }

        return config.colors.commitDefault;
    }

    function handleCommitHover(event, d) {
        const node = d3.select(event.currentTarget);

        node.select('circle')
            .transition()
            .duration(200)
            .ease(d3.easeCubicOut)
            .attr('r', config.commitRadiusHover)
            .attr('stroke-width', 2);

        // Show tooltip
        showTooltip(event, d);
    }

    function handleCommitUnhover(event) {
        const node = d3.select(event.currentTarget);
        const commit = node.datum();

        node.select('circle')
            .transition()
            .duration(200)
            .ease(d3.easeCubicOut)
            .attr('r', getCommitSize(commit))
            .attr('stroke-width', config.lineWidth);

        hideTooltip();
    }

    function showTooltip(event, commit) {
        const tooltip = d3.select('body')
            .append('div')
            .attr('class', 'tooltip')
            .style('position', 'absolute')
            .style('background', '#fafafa')
            .style('border', '1px solid #1a1a1a')
            .style('padding', '8px')
            .style('font-family', 'var(--font-mono)')
            .style('font-size', '11px')
            .style('pointer-events', 'none')
            .style('z-index', '1000')
            .style('opacity', 0);

        tooltip.html(`
            <div><strong>${commit.shortMessage}</strong></div>
            <div>${commit.author}</div>
            <div>${new Date(commit.timestamp).toLocaleDateString()}</div>
            ${commit.stats ? `<div>+${commit.stats.linesAdded} -${commit.stats.linesRemoved}</div>` : ''}
        `);

        tooltip
            .style('left', (event.pageX + 15) + 'px')
            .style('top', (event.pageY - 15) + 'px')
            .transition()
            .duration(200)
            .style('opacity', 1);
    }

    function hideTooltip() {
        d3.selectAll('.tooltip')
            .transition()
            .duration(200)
            .style('opacity', 0)
            .remove();
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
            svg.attr('width', Math.max(layout.width, container.clientWidth))
                .attr('height', Math.max(layout.height, container.clientHeight));

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
                .attr('class', d => `edge edge-${d.type.toLowerCase()}`);

            edgeGroups.each(function (d) {
                const edge = d3.select(this);

                if (d.points && d.points.length >= 2) {
                    edge.append('line')
                        .attr('x1', d.points[0].item1)
                        .attr('y1', d.points[0].item2)
                        .attr('x2', d.points[1].item1)
                        .attr('y2', d.points[1].item2)
                        .attr('stroke', getEdgeColor(d.type))
                        .attr('stroke-width', getEdgeWidth(d.type))
                        .attr('stroke-dasharray', getEdgeDashArray(d.type))
                        .attr('opacity', 0)
                        .transition()
                        .duration(500)
                        .attr('opacity', getEdgeOpacity(d.type));
                }
            });

            // Render nodes (commits)
            const nodeGroups = g.selectAll('.commit-node')
                .data(layout.nodes)
                .enter()
                .append('g')
                .attr('class', d => `commit-node ${d.isSignificant ? 'significant' : 'normal'}`)
                .attr('transform', d => `translate(${d.x}, ${d.y})`)
                .on('click', (event, d) => showNodeDetail(d))
                .on('mouseenter', handleNodeHover)
                .on('mouseleave', handleNodeUnhover);

            nodeGroups.append('circle')
                .attr('r', 0)
                .attr('fill', d => getNodeColor(d))
                .attr('stroke', config.colors.commitDefault)
                .attr('stroke-width', d => d.isSignificant ? 1.5 : 1)
                .transition()
                .duration(500)
                .attr('r', d => d.radius);

            // Restore zoom state after rendering
            if (currentZoom && currentZoom.k !== 1) {
                g.attr('transform', currentZoom);
            }

            console.log('=== renderLayout COMPLETE ===');
        } catch (error) {
            console.error('Error rendering layout:', error);
            console.error('Stack trace:', error.stack);
        }
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

            branchGroup.append('line')
                .attr('class', 'branch-line')
                .attr('x1', lineStartX)
                .attr('y1', info.y)
                .attr('x2', lineEndX)
                .attr('y2', info.y)
                .attr('stroke', config.colors.branchLine)
                .attr('stroke-width', config.lineWidth)
                .attr('opacity', 0)
                .transition()
                .duration(500)
                .attr('opacity', 0.3);

            // Branch indicator box
            const boxSize = 8;
            const boxX = lineStartX - 15;
            const fullName = branchName.replace(/^origin\//, '');

            const indicator = branchGroup.append('rect')
                .attr('class', 'branch-indicator')
                .attr('x', boxX)
                .attr('y', info.y - boxSize / 2)
                .attr('width', boxSize)
                .attr('height', boxSize)
                .attr('fill', getBranchColor(branchName, info.index))
                .attr('stroke', config.colors.commitDefault)
                .attr('stroke-width', 1)
                .attr('rx', 1)
                .style('cursor', 'help')
                .attr('opacity', 0);

            // Add tooltip
            indicator.append('title').text(fullName);

            // Hover effects
            indicator.on('mouseenter', function () {
                d3.select(this)
                    .transition().duration(200)
                    .attr('opacity', 1)
                    .attr('stroke-width', 2);
            }).on('mouseleave', function () {
                d3.select(this)
                    .transition().duration(200)
                    .attr('opacity', 0.8)
                    .attr('stroke-width', 1);
            });

            // Fade in
            indicator.transition().duration(500).attr('opacity', 0.8);
        });
    }

    function getEdgeColor(edgeType) {
        switch (edgeType) {
            case 'Branch':
                return '#4CAF50'; // Green for branch splits
            case 'Merge':
                return '#FF9800'; // Orange for merges
            case 'Normal':
            default:
                return config.colors.link; // Gray for normal connections
        }
    }

    function getEdgeWidth(edgeType) {
        switch (edgeType) {
            case 'Branch':
            case 'Merge':
                return 2;
            case 'Normal':
            default:
                return config.lineWidth;
        }
    }

    function getEdgeDashArray(edgeType) {
        switch (edgeType) {
            case 'Branch':
                return '5,5'; // Dashed for branches
            case 'Merge':
                return '3,3'; // Dashed for merges
            case 'Normal':
            default:
                return null; // Solid for normal
        }
    }

    function getEdgeOpacity(edgeType) {
        switch (edgeType) {
            case 'Branch':
            case 'Merge':
                return 0.7;
            case 'Normal':
            default:
                return 0.4;
        }
    }

    function getNodeColor(node) {
        // Color based on branch
        if (node.branchName === 'main' || node.branchName === 'master') {
            return '#2d2d2d'; // Dark for main
        } else if (node.branchName.includes('release')) {
            return '#4a90e2'; // Blue for releases
        } else if (node.branchName.includes('feature')) {
            return '#7ed321'; // Green for features
        } else if (node.branchName.includes('hotfix') || node.branchName.includes('fix')) {
            return '#e74c3c'; // Red for fixes
        }
        return config.colors.commitDefault;
    }

    function showNodeDetail(node) {
        // Show commit detail popup
        if (window.LaniusApp && window.LaniusApp.showCommitDetail) {
            // Map node data to commit structure
            const commit = {
                sha: node.commitId,
                author: node.author || 'Unknown',
                authorEmail: '',
                timestamp: node.timestamp,
                message: node.message || 'No message',
                branches: [node.branchName],
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
            .attr('r', d.radius * 1.5)
            .attr('stroke-width', 2);

        // Show tooltip
        showNodeTooltip(event, d);
    }

    function handleNodeUnhover(event, d) {
        const node = d3.select(event.currentTarget);

        node.select('circle')
            .transition()
            .duration(200)
            .ease(d3.easeCubicOut)
            .attr('r', d.radius)
            .attr('stroke-width', d.isSignificant ? 1.5 : 1);

        hideTooltip();
    }

    function showNodeTooltip(event, node) {
        const tooltip = d3.select('body')
            .append('div')
            .attr('class', 'tooltip')
            .style('position', 'absolute')
            .style('background', '#fafafa')
            .style('border', '1px solid #1a1a1a')
            .style('padding', '8px')
            .style('font-family', 'var(--font-mono)')
            .style('font-size', '11px')
            .style('pointer-events', 'none')
            .style('z-index', '1000')
            .style('opacity', 0);

        tooltip.html(`
            <div><strong>${node.message || 'Commit'}</strong></div>
            <div>${node.author || 'Unknown author'}</div>
            <div>${new Date(node.timestamp).toLocaleDateString()}</div>
            <div>Branch: ${node.branchName}</div>
            ${node.isSignificant ? '<div style="color: #2196F3;">Significant commit</div>' : ''}
        `);

        tooltip
            .style('left', (event.pageX + 15) + 'px')
            .style('top', (event.pageY - 15) + 'px')
            .transition()
            .duration(200)
            .style('opacity', 1);
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
        zoomOut
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
