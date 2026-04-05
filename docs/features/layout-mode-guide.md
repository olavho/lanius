# Layout Mode Feature - Quick Reference

## Overview

Lanius now supports two visualization modes for viewing Git repository commit history:

1. **Logical Layout**: Traditional branch-based timeline view
2. **Calendar Layout**: Time-grouped aggregate view

## How to Use

### Switching Modes

1. Load a repository (clone or select from dropdown)
2. Find the **"Layout Mode"** panel in the left sidebar
3. Select your preferred mode from the dropdown:
   - **Logical (Branch Lines)**: Shows individual commits on branch timelines
   - **Calendar (Time Groups)**: Shows commits grouped by time periods

### Calendar Mode Options

When **Calendar** mode is selected:

1. A **"Calendar Granularity"** dropdown appears
2. Choose your grouping period:
   - **Day**: Groups by calendar day
   - **Week**: Groups by ISO week
   - **Month**: Groups by calendar month (default)
   - **Year**: Groups by calendar year

3. The visualization updates automatically

### Visual Differences

#### Logical Layout
- Shows every commit as a small dot
- Commits positioned on branch timelines
- Lines connect commits showing relationships
- Different line styles for:
  - **Solid gray**: Normal commit sequence
  - **Dashed green**: Branch split
  - **Dashed orange**: Merge

#### Calendar Layout
- Shows time periods as circles
- Circle size = number of commits in that period
- Large circles display commit count inside
- Time axis shows period labels (e.g., "2024-04")
- Blue circles with 70% opacity

### Interactions

Both modes support:
- **Zoom**: Mouse wheel, Ctrl + +/-, or zoom buttons
- **Pan**: Click and drag
- **Hover**: Tooltip with details
- **Click**: Full detail popup
- **Reset**: Ctrl+0 or Reset button

## API Examples

### Logical Layout
```bash
GET /api/repository/my-repo-id/layout?mode=logical&branchFilter=main,develop
```

### Calendar Layout - Monthly
```bash
GET /api/repository/my-repo-id/layout?mode=calendar&granularity=month
```

### Calendar Layout - Daily
```bash
GET /api/repository/my-repo-id/layout?mode=calendar&granularity=day
```

## Use Cases

### Logical Layout Best For:
- Understanding branch structure
- Following commit history on specific branches
- Identifying merge points and branch splits
- Detailed code review workflows

### Calendar Layout Best For:
- Identifying development activity patterns
- Finding quiet/busy periods
- High-level project timeline overview
- Capacity planning and retrospectives

## Keyboard Shortcuts

- **Ctrl+0**: Reset zoom
- **Ctrl++**: Zoom in
- **Ctrl+-**: Zoom out

## Tips

1. **Large repositories**: Calendar mode is faster for overview (fewer nodes to render)
2. **Detailed analysis**: Switch to Logical mode to see individual commits
3. **Time-based patterns**: Use Calendar mode with different granularities to spot trends
4. **Branch filtering**: Works in both modes (use "Branch Filter" panel)

## Current Limitations

1. Calendar mode shows **all branches aggregated** (no per-branch calendar breakdown)
2. No automatic granularity switching based on zoom level (manual selection only)
3. Calendar month view is MVP implementation (single horizontal row)

## Future Enhancements (Phase 4b)

- Automatic granularity switching based on zoom level
- Heatmap colors for activity intensity
- Branch breakdown within time periods
- Multi-row calendar layouts for better space utilization
