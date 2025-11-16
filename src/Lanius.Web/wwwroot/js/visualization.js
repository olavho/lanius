// D3.js Visualization Module - Minimalist Sci-Fi Aesthetic
// Thin black/dark gray lines on light background, smooth animations

const Visualization = (() => {
    let svg, g, xScale, yScale;
    let commitData = [];
    let branchData = [];
    
    const config = {
        margin: { top: 60, right: 40, bottom: 40, left: 100 },
        commitRadius: 4,
        commitRadiusHover: 6,
        lineWidth: 1,
        branchSpacing: 40,
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

        // Handle window resize
        window.addEventListener('resize', debounce(handleResize, 250));
    }

    function render(commits, branches) {
        commitData = commits;
        branchData = branches;

        if (commits.length === 0) {
            clearAll();
            return;
        }

        updateScales();
        renderBranchLines();
        renderCommits();
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
        
        branchData.forEach((branch, i) => {
            const y = i * config.branchSpacing;
            
            const branchGroup = g.append('g')
                .attr('class', 'branch-group');

            // Branch line
            branchGroup.append('line')
                .attr('class', 'branch-line')
                .attr('x1', -config.margin.left + 20)
                .attr('y1', y)
                .attr('x2', xScale.range()[1])
                .attr('y2', y)
                .attr('stroke', config.colors.branchLine)
                .attr('stroke-width', config.lineWidth)
                .attr('opacity', 0)
                .transition()
                .duration(500)
                .attr('opacity', 0.3);

            // Branch label
            branchGroup.append('text')
                .attr('class', 'branch-label')
                .attr('x', -config.margin.left + 25)
                .attr('y', y)
                .attr('dy', '0.35em')
                .text(branch.name)
                .attr('fill', config.colors.branchLabel)
                .attr('opacity', 0)
                .transition()
                .duration(500)
                .attr('opacity', 1);
        });
    }

    function renderCommits() {
        // Clear existing commits
        g.selectAll('.commit-node').remove();
        g.selectAll('.commit-link').remove();

        const branchYMap = new Map();
        branchData.forEach((branch, i) => {
            branchYMap.set(branch.name, i * config.branchSpacing);
        });

        // Draw links first (behind commits)
        const linkData = [];
        commitData.forEach(commit => {
            commit.parentShas.forEach(parentSha => {
                const parent = commitData.find(c => c.sha === parentSha);
                if (parent) {
                    linkData.push({ source: commit, target: parent });
                }
            });
        });

        g.selectAll('.commit-link')
            .data(linkData)
            .enter()
            .append('line')
            .attr('class', 'commit-link')
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

    return {
        initialize,
        render,
        animateNewCommit,
        animateReplayCommit,
        clear: clearAll
    };
})();

// Initialize visualization
Visualization.initialize();

// Export to global scope
window.renderVisualization = () => {
    Visualization.render(window.LaniusApp.state.commits, window.LaniusApp.state.branches);
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
