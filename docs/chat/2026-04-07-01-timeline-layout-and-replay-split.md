# Timeline Layout & Replay Branch Split

## Requests

1. **Calendar layout — two variants**: the fixed timeline axis above the canvas doesn't correspond to node positions.
2. **Replay start from branch split point** instead of the very first repository commit.

---

## Layout Variants

### Variant 1 — "Calendar (grouped)" (existing, renamed)
- Current `LayoutMode.Calendar`: evenly-spaced period-bubble nodes.
- **Change**: hide the timeline axis (it was misleading — nodes are ordinal, not time-proportional).
- Dropdown label: `Calendar (grouped)`.

### Variant 2 — "Timeline (true dates)" (new)
- New `LayoutMode.Timeline`, handled by new `TimelineLayoutEngine`.
- **x** = `MarginX + (t − tMin).TotalSeconds / span.TotalSeconds × usableWidth`  (linear, time-proportional).
- **y** = branch row × BranchSpacing (same as Logical), with same-day sub-rows stacked at +12 px each.
- **Canvas width** auto-scaled: `max(canvasWidth, 2×MarginX + days×pixelsPerDay)` where `pixelsPerDay = clamp(3..10, 800/days)`.
- **Long-span edges**: when edge time gap > 14 days, `LayoutEdge.IsLongSpan = true` → frontend renders dashed.
- **Timeline axis** is shown and corresponds to node positions.

### What stays hidden where
| Mode | Timeline axis |
|------|--------------|
| Logical (branches) | hidden |
| Calendar (grouped) | hidden |
| Timeline (true dates) | **shown** |

---

## Replay — Start from branch split point

- New `ReplayOptions.StartFromBranchSplit` (bool, default `false`).
- When `true` and `BranchFilter` is a non-main/master branch:
  1. `ReplayService` calls `IBranchAnalyzer.GetBranchesByPatternAsync` + `IBranchHierarchyAnalyzer.AnalyzeBranchHierarchyAsync`.
  2. Finds `MergeBaseSha` of the selected branch.
  3. Calls `ICommitAnalyzer.GetCommitsSinceAsync(repositoryId, branch, mergeBaseSha)` — which returns only commits after the split point.
- Frontend: auto-detects non-main/master branch → sends `startFromBranchSplit: true`.

---

## Backend changes
- `LayoutMode.cs`: add `Timeline`
- `LayoutEdge.cs`: add `IsLongSpan`
- `TimelineLayoutEngine.cs`: new (mirrors Logical, time-proportional x, day-clustering y, long-span detection)
- `Program.cs`: register `TimelineLayoutEngine`
- `LayoutController.cs`: route `Timeline` → `TimelineLayoutEngine`
- `ReplayOptions.cs`: add `StartFromBranchSplit`
- `ReplayDTOs.cs`: add `StartFromBranchSplit` to `StartReplayRequest`
- `ReplayController.cs`: pass field through
- `ReplayService.cs`: implement split-point lookup

## Frontend changes
- `index.html`: dropdown → 3 options (Logical, Calendar (grouped), Timeline (true dates))
- `visualization.js`: skip `renderTimelineAxis()` for Logical/Calendar; add `isLongSpan` edge dash style; route `Timeline` mode to Logical renderer (same nodes/edges, just show axis)
- `app.js`: pass `startFromBranchSplit` when non-main/master branch; handle `timeline` mode in layout URL
