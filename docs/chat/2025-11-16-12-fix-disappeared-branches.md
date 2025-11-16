# Fix: Branches Disappeared After Timeline Grid Changes

**Date**: 2025-11-16  
**Type**: Bug Fix

## Problem
After adding the timeline grid and adjusting branch line positioning, branches disappeared from the visualization. Only the timeline grid (year/month markers) was visible.

## Root Causes

### 1. Label Positioning Issue
The label positioning logic was using `text-anchor="end"` with a calculated position that could be negative or outside the visible area:
```javascript
const labelX = lineStartX - 10; // Could be negative!
```

### 2. Missing Error Handling
No check for branches with zero commits, which could cause rendering to fail silently.

### 3. Timeline Grid Clutter
Too many month gridlines made the visualization busy and potentially interfered with branch rendering.

## Solutions

### 1. Fixed Label Positioning
**Changed** from dynamic positioning to fixed left margin:
```javascript
// Before (broken)
const labelX = lineStartX - 10; // Could go off-screen
attr('text-anchor', 'end')

// After (fixed)
const labelX = 5; // Fixed at left margin
attr('text-anchor', 'start') // Left-aligned
```

### 2. Added Branch Validation
```javascript
if (branchCommits.length === 0) {
    console.warn('No commits found for branch:', branch.name);
    return; // Skip this branch
}
```

### 3. Simplified Timeline Grid
**Year markers**: Bold, prominent lines
**Month markers**: Light, subtle lines (not overwhelming)

```javascript
// Year lines: darker, more prominent
.attr('stroke', '#d0d0d0')
.attr('stroke-width', 1)
.attr('opacity', 0.4);

// Month lines: lighter, subtle
.attr('stroke', '#e8e8e8')
.attr('stroke-width', 0.5)
.attr('opacity', 0.2);
```

### 4. Enhanced Debugging
Added console logging to track:
- Number of branches being rendered
- Commits per branch
- Line coordinates (start ? end, Y position)
- Rendering completion

## Visual Hierarchy

### Timeline Grid Layers
1. **Background**: Month lines (very light, `#e8e8e8`, 20% opacity)
2. **Mid-ground**: Year lines (medium, `#d0d0d0`, 40% opacity)
3. **Foreground**: Branch lines and commits (dark, full opacity)

### Label Positioning
- **Fixed at**: X = 5px (left margin)
- **Alignment**: Start (left-aligned)
- **Font size**: 10px (compact)
- **Benefit**: Consistent, predictable positioning

## Testing & Debugging

### Console Output to Check
```
Rendering 20 branches
Branch main: 100 commits
  Line: 50 ? 1250, Y: 0
Branch agent-hosting...: 2 commits
  Line: 300 ? 400, Y: 40
...
Branch rendering complete
```

### What to Look For
1. **Number of branches** should match API response (20)
2. **Commits per branch** should be > 0
3. **Line coordinates** should be reasonable (not negative, not NaN)
4. **Y positions** should increment by `branchSpacing` (40px)

### If Branches Still Missing
Check browser console for:
- **JavaScript errors**: Red text indicating failures
- **Warning messages**: "No commits found for branch..."
- **Coordinate issues**: NaN, Infinity, or negative values

## Files Modified
- `src/Lanius.Web/wwwroot/js/visualization.js` - Fixed label positioning, added validation, simplified grid

## Quick Fix Instructions

1. **Refresh browser** (Ctrl+Shift+R)
2. **Open DevTools** (F12)
3. **Check Console** tab for errors
4. **Look for** "Rendering X branches" message
5. **Verify** branch lines and labels appear

## Expected Visual Result

### Timeline Grid
```
2024           2025
Jan Feb ... Dec Jan Feb ... Nov
 ?   ?       ?   ?   ?       ?    (light month lines)
 ?           ?                    (bold year lines)
```

### Branch Display
```
main:        ???????????????????
agent...:        ??????????
demo:    ????????
```

### Labels
- Fixed at left edge
- Truncated if > 30 chars
- Hover shows full name

## Why This Happened
The dynamic label positioning (`lineStartX - 10`) worked when lines started at the left margin, but broke when lines started at the actual first commit position. Some commits were very early (left side), causing labels to be positioned at negative X coordinates (off-screen to the left).

## Future Improvements
- **Pan & Zoom**: Allow horizontal scrolling for long timelines
- **Dynamic label positioning**: Smart placement to avoid overlaps
- **Collapsible years**: Hide/show years to focus on specific periods
- **Adaptive grid**: Show weeks when zoomed in, quarters when zoomed out
