# Feature: Hover Boxes + Horizontal Scrolling

**Date**: 2025-11-16  
**Type**: UX Enhancement

## Improvements

### 1. Branch Indicator Boxes ??
**Replaced** text labels with small colored boxes at the start of each branch line.

**Design**:
- **8x8px colored squares** positioned before branch line start
- **Color-coded** by branch type:
  - `main` ? Dark gray (#2d2d2d)
  - `release/*` ? Blue (#4a90e2)
  - `feature/*` ? Green (#7ed321)
  - `fix/*` ? Red (#e74c3c)
  - `dependabot/*` ? Purple (#9b59b6)
  - Others ? Rotating palette
- **Hover tooltip** shows full branch name
- **Hover effect**: Box expands and becomes more opaque

**Benefits**:
- **Cleaner UI**: No text clutter on left side
- **Visual hierarchy**: Color indicates branch purpose
- **Discoverable**: Hover reveals details
- **Space-efficient**: Boxes are tiny (8px) vs long text labels

### 2. Horizontal Scrolling ??
**Dynamic canvas width** based on timeline span.

**Algorithm**:
```javascript
const daysDiff = (maxDate - minDate) / (1000 * 60 * 60 * 24);
const calculatedWidth = Math.max(
    viewportWidth,
    Math.min(daysDiff * 2, viewportWidth * 10)
);
// Allocate ~2px per day
// Min: viewport width (no scroll needed)
// Max: 10x viewport (prevent extreme sizes)
```

**Examples**:
- **1 month**: 60 days × 2px = 120px ? uses viewport width (no scroll)
- **1 year**: 365 days × 2px = 730px ? slightly wider than viewport
- **3 years**: 1095 days × 2px = 2190px ? scrollable canvas
- **10+ years**: Capped at 10× viewport width

**UI Changes**:
- **Horizontal scrollbar** appears when needed
- **Smooth scrolling** with mouse wheel (shift+wheel on some browsers)
- **Styled scrollbar** matching minimalist theme
- **Timeline stays anchored** at top (doesn't scroll vertically)

## Implementation Details

### Branch Indicator Boxes

**Structure**:
```javascript
branchGroup.append('rect')
    .attr('class', 'branch-indicator')
    .attr('x', lineStartX - 15)  // Before line start
    .attr('y', y - 4)            // Centered on line
    .attr('width', 8)
    .attr('height', 8)
    .attr('fill', getBranchColor(branch.name, index))
    .attr('rx', 1);              // Slight rounding
```

**Hover Behavior**:
```javascript
.on('mouseenter', function() {
    d3.select(this)
        .attr('opacity', 1)
        .attr('stroke-width', 2);  // Thicker border
})
.on('mouseleave', function() {
    d3.select(this)
        .attr('opacity', 0.8)
        .attr('stroke-width', 1);
});
```

**Tooltip**:
- Uses SVG `<title>` element (native browser tooltip)
- Shows full branch name (no "origin/" prefix)
- Works with screen readers (accessible)

### Color Coding Logic

**Branch Type Detection**:
```javascript
function getBranchColor(branchName, index) {
    if (branchName.includes('release')) return '#4a90e2';  // Blue
    if (branchName.includes('feature')) return '#7ed321';  // Green
    if (branchName.includes('fix'))     return '#e74c3c';  // Red
    if (branchName.includes('dependabot')) return '#9b59b6'; // Purple
    // Fallback to palette
    const colors = ['#34495e', '#16a085', '#f39c12', '#e67e22', '#95a5a6'];
    return colors[index % colors.length];
}
```

### Dynamic Canvas Width

**Width Calculation**:
1. **Extract time span**: `maxDate - minDate` in days
2. **Calculate width**: `days × 2px per day`
3. **Apply constraints**:
   - Minimum: viewport width (prevent shrinking)
   - Maximum: 10× viewport width (prevent excessive width)
4. **Set SVG min-width**: Enable scrolling when needed

**CSS Changes**:
```css
.visualization-container {
    overflow-x: auto;  /* Enable horizontal scroll */
    overflow-y: hidden; /* No vertical scroll */
}

.commit-graph {
    min-width: XXXpx; /* Calculated dynamically */
}
```

**xScale Range Update**:
```javascript
xScale.range([0, calculatedWidth - margins]);
```

## Visual Comparison

### Before (Text Labels)
```
main                       ???????????????
origin/feature-x                   ???????
origin/very-long-branch...     ???????????
```
- Cluttered left side
- Hard to read long names
- Takes horizontal space

### After (Hover Boxes)
```
? ???????????????
?     ???????
? ???????????
```
- Clean left margin
- Hover reveals names
- Color-coded purpose

## Browser Compatibility

### Horizontal Scrolling
- ? Chrome/Edge: Shift+Wheel for horizontal scroll
- ? Firefox: Shift+Wheel or trackpad gesture
- ? Safari: Trackpad gesture
- ? All: Click-drag scrollbar

### SVG Title Tooltips
- ? All modern browsers support SVG `<title>`
- ? Native browser tooltip (no JavaScript)
- ? Screen reader accessible

## Configuration

### Adjustable Constants
```javascript
const boxSize = 8;           // Indicator box size (px)
const boxOffset = 15;        // Distance before line start
const pxPerDay = 2;          // Width allocation per day
const maxWidthMultiplier = 10; // Max canvas width (× viewport)
```

### Color Palette
Edit `getBranchColor()` function to customize:
- Branch type detection rules
- Color assignments
- Fallback palette

## Performance

### Canvas Width Limits
- **Small repos** (< 1 year): No scroll, viewport width
- **Medium repos** (1-3 years): Moderate scroll, 2-3× viewport
- **Large repos** (> 5 years): Capped at 10× viewport

**Why cap at 10×?**
- Prevents excessive DOM size
- Maintains rendering performance
- Still accommodates ~5+ years of history

### Rendering Cost
- **No change** to data loading (same commit count)
- **Slightly higher** initial render (calculate width)
- **Smoother** user experience (less cramped)

## User Experience

### Interaction Patterns
1. **Load repository** ? Canvas auto-sizes based on timespan
2. **See colored boxes** ? Understand branch types at a glance
3. **Hover box** ? See full branch name
4. **Scroll horizontally** ? Navigate long timelines
5. **Click commits** ? View details (existing behavior)

### Discoverability
- Colored boxes **hint** at interactivity (cursor changes)
- First hover **teaches** user about tooltips
- Scrollbar **indicates** more content to the right

## Future Enhancements

### Indicator Boxes
- **Branch type icons**: Use symbols instead of just colors
- **Activity indicator**: Brightness based on recent commits
- **Click to filter**: Click box to show only that branch

### Scrolling
- **Mini-map**: Small overview showing full timeline
- **Keyboard navigation**: Arrow keys to scroll
- **Jump to date**: Click month label to scroll to that period
- **Zoom controls**: +/- buttons to adjust timeline density

## Files Modified
- `src/Lanius.Web/wwwroot/js/visualization.js` - Replace labels with boxes, dynamic width
- `src/Lanius.Web/wwwroot/css/styles.css` - Enable horizontal scroll, scrollbar styling

## Testing

### What to Look For
1. **Colored boxes** at start of each branch line (8×8px)
2. **Hover tooltips** showing full branch names
3. **Horizontal scrollbar** at bottom (if timeline > viewport width)
4. **Smooth scrolling** with mouse wheel (Shift+Wheel)
5. **Color coding**: Main dark, features green, fixes red, etc.

### Console Verification
```
Timeline span: XXX days, calculated width: YYYYpx
Rendering 20 branches
Branch main: 105 commits
...
```

### Expected Behavior
- **Short timelines** (< 6 months): No scrollbar
- **Long timelines** (> 1 year): Scrollbar appears
- **Very long timelines** (> 5 years): Capped width, dense layout

## Next Steps
**Just refresh browser** (Ctrl+Shift+R)

You should see:
- ? Small colored boxes instead of text labels
- ? Hover reveals branch names
- ? Horizontal scrollbar (if timeline is long)
- ? Much cleaner, more professional UI
