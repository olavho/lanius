// Branch rendering helper functions for renderLayout()
// Add these functions to visualization.js after line 773 (before getEdgeColor)

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

// In renderLayout(), after g.selectAll('*').remove(); add:
//
// // Extract branch info and render branch lines/indicators
// const branchInfo = extractBranchInfo(layout.nodes);
// if (branchInfo.size > 0) {
//     renderBranchLinesForLayout(branchInfo);
// }
