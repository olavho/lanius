# Fix: indicatorBox.append is not a function

**Date**: 2025-11-16  
**Type**: Bug Fix

## Problem
Same D3 transition issue as before:
```
TypeError: indicatorBox.append is not a function
```

## Root Cause
Trying to append `<title>` element and add event handlers to a transition object:

```javascript
// WRONG
const indicatorBox = branchGroup.append('rect')
    .attr(...)
    .transition()  // ? Returns transition
    .duration(500)
    .attr('opacity', 0.8);

indicatorBox.append('title')  // ? ERROR!
indicatorBox.on('mouseenter')  // ? ERROR!
```

## Solution
Append title and add event handlers **before** starting transition:

```javascript
// CORRECT
const indicatorBox = branchGroup.append('rect')
    .attr(...)
    .style('cursor', 'help');

// Append title BEFORE transition
indicatorBox.append('title').text(fullName);

// Add event handlers BEFORE transition
indicatorBox.on('mouseenter', function() {...});
indicatorBox.on('mouseleave', function() {...});

// Start transition AFTER everything else
indicatorBox
    .attr('opacity', 0)
    .transition()
    .duration(500)
    .attr('opacity', 0.8);
```

## Key Rule
**D3 transitions can only animate attributes - they cannot modify the DOM structure or add event handlers.**

## Order of Operations
1. ? Create element (`.append('rect')`)
2. ? Set initial attributes (`.attr(...)`)
3. ? Set styles (`.style(...)`)
4. ? Append children (`.append('title')`)
5. ? Add event handlers (`.on(...)`)
6. ? **THEN** start transition (`.transition()`)

## Files Modified
- `src/Lanius.Web/wwwroot/js/visualization.js`

## Next Steps
**Refresh browser** (Ctrl+Shift+R)

All branches should now render with colored indicator boxes!
