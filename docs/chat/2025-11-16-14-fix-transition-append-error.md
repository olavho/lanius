# Fix: TypeError - labelText.append is not a function

**Date**: 2025-11-16  
**Type**: Bug Fix

## Problem
Only 2 branches were rendering, and console showed error:
```
TypeError: labelText.append is not a function
```

## Root Cause
The code was trying to append a `<title>` element to a D3 **transition** object:

```javascript
// WRONG
const labelText = branchGroup.append('text')
    .attr(...)
    .transition()  // ? Returns transition, not selection!
    .duration(500)
    .attr('opacity', 1);

labelText.append('title')  // ? ERROR! Can't append to transition
```

## Solution
Append the `<title>` element **before** starting the transition:

```javascript
// CORRECT
const labelText = branchGroup.append('text')
    .attr(...)
    .text(displayName);

// Append title BEFORE transition
if (fullName !== displayName) {
    labelText.append('title').text(fullName);
}

// Start transition AFTER appending title
labelText
    .attr('opacity', 0)
    .transition()
    .duration(500)
    .attr('opacity', 1);
```

## Why This Broke Rendering
When the error occurred at line 269 (appending title), it threw an exception that stopped the entire `renderBranchLines()` function. This meant:
- First 2 branches rendered successfully (before the title check)
- 3rd+ branches never rendered (exception stopped execution)
- Console showed "Rendering 20 branches" but only 2 appeared

## D3 Transition vs Selection

### D3 Selection
- **Can**: append child elements, set attributes, bind data
- **Example**: `selection.append('title')`

### D3 Transition  
- **Can**: animate attribute changes over time
- **Cannot**: append child elements
- **Example**: `selection.transition().attr('opacity', 1)`

## Testing

### Expected Console Output
```
Rendering 20 branches
Branch main: 105 commits
  Line: -20 ? 897, Y: 0
Branch origin/agent-hosting-use-configuration: 2 commits
  Line: -17 ? 70, Y: 40
Branch origin/demo: X commits
  Line: Y ? Z, Y: 80
...
Branch rendering complete  ? Should see this now!
```

### Expected Visual
- **20 branches** visible with labels
- **Truncated names** with "..." for long branches
- **Hover tooltips** showing full branch names
- **Timeline grid** with year labels at top

## Files Modified
- `src/Lanius.Web/wwwroot/js/visualization.js` - Fixed title append order

## Next Steps
**Just refresh browser** (Ctrl+Shift+R)

All 20 branches should now render correctly!
