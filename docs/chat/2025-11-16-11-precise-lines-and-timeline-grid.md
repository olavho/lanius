# Enhancement: Precise Branch Lines and Timeline Grid

**Date**: 2025-11-16  
**Type**: Visualization Enhancement

## Improvements

### 1. Precise Branch Lines ??
**Problem**: Branch lines extended from left margin to right side, even if commits were clustered in the middle.

**Solution**: Lines now start **20px before first commit** and end **50px after last commit** on each branch.

```javascript
// Find earliest commit on branch
const earliestCommit = branchCommits.reduce((earliest, current) => 
    new Date(current.timestamp) < new Date(earliest.timestamp) ? current : earliest
);
lineStartX = xScale(new Date(earliestCommit.timestamp)) - 20;

// Find latest commit on branch
const latestCommit = branchCommits.reduce((latest, current) => 
    new Date(current.timestamp) > new Date(latest.timestamp) ? current : latest
);
lineEndX = xScale(new Date(latestCommit.timestamp)) + 50;
```

**Visual Impact**:
- **Before**: All lines started at left margin
- **After**: Lines start where branch activity begins
- **Benefit**: Clearer visual of when branches were active

### 2. Truncated Branch Names with Hover ???
**Problem**: Long branch names like `dependabot/npm_and_yarn/dotnet/samples/Demos/ProcessFrameworkWithSignalR/form-data-4.0.4` cluttered the display.

**Solution**: 
- Truncate names **> 30 characters** to `27 chars + "..."`
- Show **full name on hover** via SVG `<title>` element
- Position labels **left of line start** (right-aligned)

```javascript
const fullName = branch.name.replace(/^origin\//, '');
const displayName = fullName.length > 30 
    ? fullName.substring(0, 27) + '...' 
    : fullName;

// Add hover tooltip
if (fullName !== displayName) {
    labelText.append('title').text(fullName);
}
```

**Examples**:
- `dependabot/npm_and_yarn/dotnet...` ? hover shows full path
- `agent-hosting-use-configuration` ? short enough, no truncation

### 3. Timeline Grid with Year/Month ??
**Problem**: Hard to identify when commits occurred without reference points.

**Solution**: Added vertical grid lines and labels:
- **Vertical lines** at each month boundary (light gray, subtle)
- **Month labels** at top (Jan, Feb, Mar...)
- **Year labels** at top (bold, only when year changes)

```javascript
function renderTimelineGrid() {
    // Generate month boundaries
    let currentDate = new Date(minDate);
    currentDate.setDate(1); // Start of month
    
    while (currentDate <= maxDate) {
        // Draw vertical line
        gridGroup.append('line')
            .attr('x1', x)
            .attr('y1', -config.margin.top + 30)
            .attr('x2', x)
            .attr('y2', yScale.range()[1])
            .attr('stroke', '#e0e0e0')
            .attr('opacity', 0.3);
        
        // Add month label
        labelGroup.append('text')
            .text(month); // "Jan", "Feb", etc.
        
        // Add year label (when year changes)
        if (year !== lastYear) {
            labelGroup.append('text')
                .attr('font-weight', 'bold')
                .text(year);
        }
    }
}
```

**Visual Layout**:
```
2024                    2025
Jan  Feb  Mar  Apr  May Jan  Feb  Mar  Apr
 |    |    |    |    |   |    |    |    |
 |    |    |    |    |   |    |    |    |
main: ???????????????????????????????????
      |    |    |    |    |   |    |    |
branch1:   ?????????????????????????????
```

## Implementation Details

### Branch Line Positioning
**Algorithm**:
1. Filter commits belonging to branch
2. Find min/max timestamps
3. Convert to X coordinates via `xScale`
4. Add padding: -20px start, +50px end
5. Draw line between these points

**Edge Cases**:
- **Single commit**: Line is 70px long (20px before + commit + 50px after)
- **No commits**: Falls back to left margin (shouldn't happen with current data)

### Label Positioning
**Strategy**: Place labels **to the left** of branch line start
- Uses `text-anchor="end"` for right-alignment
- Positions 10px left of line start
- Prevents overlap with commits

**Truncation Logic**:
```javascript
fullName.length > 30 
    ? fullName.substring(0, 27) + '...' 
    : fullName
```
- **Threshold**: 30 characters
- **Display**: First 27 chars + ellipsis
- **Hover**: Shows full name via `<title>` element

### Timeline Grid Rendering
**Month Generation**:
```javascript
let currentDate = new Date(minDate);
currentDate.setDate(1); // Start of month
currentDate.setHours(0, 0, 0, 0);

while (currentDate <= maxDate) {
    months.push(new Date(currentDate));
    currentDate.setMonth(currentDate.getMonth() + 1);
}
```

**Label Placement**:
- **Y position**: Above the visualization (`-config.margin.top + 25/45`)
- **X position**: Aligned with month boundary
- **Offset**: +5px to the right of grid line

## Visual Benefits

### Before
```
|??????????????????????????????????????????????|
long-branch-name-that-goes-on-forever: ??????????????????????????????
                                         (hard to see when activity occurred)
```

### After
```
          2024            2025
          Oct Nov Dec Jan Feb Mar
           |   |   |   |   |   |
truncated...: ???????????????
              ?           ?
              (start)     (end)
              (hover shows full name)
```

## Configuration

### Adjustable Constants
```javascript
const config = {
    branchLinePaddingStart: 20,  // px before first commit
    branchLinePaddingEnd: 50,    // px after last commit
    branchNameMaxLength: 30,     // chars before truncation
    branchNameTruncateLength: 27, // chars to show when truncating
    gridColor: '#e0e0e0',        // timeline grid lines
    gridOpacity: 0.3,            // grid line transparency
    labelFontSize: 10,           // month label size
    labelFontSizeYear: 12        // year label size (bold)
};
```

## Browser Compatibility

### SVG `<title>` Element
- ? **All modern browsers** support hover tooltips via SVG title
- ? **Accessible**: Screen readers can read the title
- ? **No JavaScript** required for hover behavior

### Date Manipulation
- Uses standard `Date` object methods
- `.setMonth()` automatically handles year rollover
- `.toLocaleDateString()` for internationalized month names

## Future Enhancements

### Branch Names
- **Smart truncation**: Keep meaningful parts (e.g., `...ProcessFrameworkWithSignalR/form-data-4.0.4`)
- **Category grouping**: Group by prefix (dependabot/, copilot/, feature/)
- **Collapsible groups**: Hide/show related branches

### Timeline Grid
- **Week boundaries**: Add lighter grid lines for weeks
- **Day boundaries**: Show on zoom
- **Custom periods**: Allow quarterly/yearly grids
- **Interactive labels**: Click to filter by time range

### Branch Lines
- **Fade effect**: Lower opacity for inactive periods
- **Thickness**: Thicker for more active branches
- **Color coding**: By branch type or recency

## Files Modified
- `src/Lanius.Web/wwwroot/js/visualization.js` - All changes

## Testing

### What to Look For
1. **Branch lines**: Should start near first commit, end near last commit
2. **Branch labels**: Long names should show "..." with hover revealing full name
3. **Timeline grid**: Vertical lines at month boundaries
4. **Timeline labels**: Year (bold) and month at top

### Console Verification
No changes to console output.

### Visual Checks
- ? Branch lines don't extend unnecessarily
- ? Labels are readable and positioned clearly
- ? Grid provides temporal context
- ? Hover shows full branch names

## Next Steps
**Just refresh browser** (Ctrl+Shift+R) - all changes are frontend!

Expected improvements:
- Cleaner branch lines (start/end precisely)
- Readable branch names (truncated with hover)
- Temporal context (month/year grid)
