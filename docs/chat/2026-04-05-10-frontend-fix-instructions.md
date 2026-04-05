# Frontend Visualization Fix - Manual Instructions

## Problem
The `renderLayout()` function is missing branch line rendering and branch indicators. This causes only one branch to be visible and commits to overlap.

## Solution
Add two helper functions and update the `renderLayout()` function.

## Step 1: Add Helper Functions

**Location**: Open `src/Lanius.Web/wwwroot/js/visualization.js`

**Find line 773** (the line right after the closing brace of `renderLayout()` that says:

```javascript
    function getEdgeColor(edgeType) {
```

**INSERT BEFORE that line** (between `renderLayout()` and `getEdgeColor()`):

```javascript
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

```

## Step 2: Update renderLayout() Function

**Find in `renderLayout()` around line 710-716** the section that looks like:

```javascript
            // Clear existing visualization
            g.selectAll('*').remove();

            // Update canvas dimensions based on layout
            const container = document.getElementById('commit-graph');
            svg.attr('width', Math.max(layout.width, container.clientWidth))
                .attr('height', Math.max(layout.height, container.clientHeight));

            // Render edges (branch lines)
            const edgeGroups = g.selectAll('.edge')
```

**REPLACE IT WITH**:

```javascript
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
```

## Result

After these changes:
- ✅ All branches will be visible with horizontal lines
- ✅ Colored indicator boxes at the start of each branch
- ✅ Tooltips showing full branch names on hover
- ✅ No overlapping commits

## Test

1. Save the file
2. Refresh the browser (Ctrl+F5 to clear cache)
3. Load a repository
4. You should see all branches with colored indicators

---

**Note**: I've also created a reference file with the functions at:
`src/Lanius.Web/wwwroot/js/visualization-branch-helpers.js`

You can copy the functions from there if needed.
