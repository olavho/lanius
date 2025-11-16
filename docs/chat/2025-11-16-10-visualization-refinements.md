# Refinements: Cleaner Branch Visualization

**Date**: 2025-11-16  
**Type**: UI/UX Improvements

## Problems
1. **Branch lines too long** - Extended to screen edge even after branch head
2. **Branch names cluttered** - Showed full "origin/branch-name" making labels long
3. **Branches alphabetically sorted** - Created visual mess with crossing lines

## Solutions

### 1. Shorten Branch Lines (`visualization.js`)

**Before**: Lines extended to right edge of screen
**After**: Lines end 50px after the branch head commit

```javascript
// Find latest commit on this branch
const branchCommits = commitData.filter(c => 
    c.branches && c.branches.includes(branch.name)
);

const latestCommit = branchCommits.reduce((latest, current) => 
    new Date(current.timestamp) > new Date(latest.timestamp) ? current : latest
);

// End line 50px after head
lineEndX = xScale(new Date(latestCommit.timestamp)) + 50;
```

**Visual Result**: Clean, precise lines that don't clutter the right side of the screen.

### 2. Strip "origin/" Prefix (`visualization.js`)

**Before**: 
- `origin/agent-hosting-use-configuration`
- `origin/copilot/fix-d794ae03...`

**After**:
- `agent-hosting-use-configuration`
- `copilot/fix-d794ae03...`

```javascript
const displayName = branch.name.replace(/^origin\//, '');
```

**Note**: Internal data still uses full names for lookups, only display is simplified.

### 3. Sort by Divergence Time (`BranchAnalyzer.cs`)

**Before**: Alphabetical order (agent, bentho, copilot, demo, dependabot...)
**After**: Chronological divergence order (oldest splits at top, newest at bottom)

**Algorithm**:
1. **Main branch first** (always at top)
2. **Other branches sorted** by their merge base timestamp
3. **Branches diverging earlier** ? higher in the list
4. **Branches diverging later** ? lower in the list

```csharp
// Create map of branch -> merge base timestamp
var branchDivergenceTime = new Dictionary<string, DateTimeOffset>();
foreach (var rel in relationships)
{
    var mergeBaseTime = significantCommits[rel.CommitSha].Timestamp;
    branchDivergenceTime[rel.Branch2] = mergeBaseTime;
}

// Sort by divergence time (oldest first)
var sortedBranches = otherBranches
    .OrderBy(b => branchDivergenceTime.ContainsKey(b.Name) 
        ? branchDivergenceTime[b.Name] 
        : DateTimeOffset.MaxValue)
    .ToList();
```

**Visual Benefit**: Reduces line crossings - branches appear in the order they split off from main.

## Visual Comparison

### Before (Alphabetical)
```
main:       ????????????????????????????????????????????????
              ?           ?           ?
agent:         ????????????????????????????????????????????? (crosses others)
                 ?          ?           ?
demo:             ??????????????????????????????????????????
                             ?           ?
bentho:                       ???????????????????????????????
                                          ?
copilot:                                   ???????????????????
```

### After (Chronological)
```
main:       ?????????????????
              ?     ?     ?
agent:         ???????????????????         (no crossing)
                    ?     ?
bentho:              ?????????????         (no crossing)
                          ?
demo:                      ????????        (no crossing)
copilot:                          ??????   (no crossing)
```

## Implementation Details

### Branch Line Calculation
```javascript
// For each branch:
1. Filter commits belonging to this branch
2. Find commit with latest timestamp (rightmost)
3. Get X position: xScale(latestCommit.timestamp)
4. Add padding: x + 50px
5. Draw line from left margin to this endpoint
```

### Branch Name Display
- **Internal**: Keep full name (`origin/branch-name`) for data integrity
- **Display**: Strip prefix for UI clarity
- **Regex**: `/^origin\//` removes only the "origin/" at the start

### Divergence Time Sorting
**Data Flow**:
1. Backend calculates merge bases for each branch
2. Creates `relationships` array with merge base SHAs
3. Maps branch ? merge base ? timestamp
4. Sorts branches by these timestamps
5. Returns ordered list to frontend

**Edge Cases**:
- **Main branch**: Always first (index 0)
- **Branches without merge base**: Sorted last (DateTimeOffset.MaxValue)
- **Same divergence time**: Maintains stable sort order

## Benefits

### 1. Visual Clarity
- **Less clutter** on right side (no infinite lines)
- **Shorter labels** easier to read
- **Fewer crossings** easier to follow

### 2. Logical Ordering
- **Chronological flow** matches actual development timeline
- **Related branches grouped** (diverged around same time)
- **Easier to spot** when feature branches were created

### 3. Performance
- **No change** to data volume
- **Same rendering cost** (just reordered)
- **Slightly better** cache locality (related branches adjacent)

## Files Modified
- `src/Lanius.Web/wwwroot/js/visualization.js` - Shorten lines, strip origin/ prefix
- `src/Lanius.Business/Services/BranchAnalyzer.cs` - Sort by divergence time

## Testing

### Expected Behavior
1. **Branch lines**: Should stop shortly after the rightmost commit on each branch
2. **Branch labels**: Should not show "origin/" prefix
3. **Branch order**: Should match chronological divergence (oldest at top, newest at bottom)

### Visual Checks
- ? No long horizontal lines extending to screen edge
- ? Branch names are concise and readable
- ? Diagonal lines flow from top-left to bottom-right (minimal crossing)

### Console Verification
No changes to console output - ordering is transparent to logging.

## Next Steps
1. **Stop API** (if running)
2. **Restart API**: `dotnet run --project src/Lanius.Api`
3. **Refresh browser** (Ctrl+Shift+R)
4. **Click APPLY** with empty filter
5. **Verify**:
   - Lines end near branch heads
   - No "origin/" in labels
   - Branches ordered by divergence time

## Future Enhancements
- **Dynamic padding**: Calculate based on commit size/zoom level
- **Label positioning**: Move labels to avoid overlapping with commits
- **Color coding**: Different colors for different branch types (feature, release, etc.)
- **Interactive sorting**: Allow user to switch between alphabetical/chronological/manual
