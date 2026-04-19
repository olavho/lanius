# UX Refresh Plan

**Date:** 2026-04-06  
**Branch:** feature/ux-improvements  

---

## Current State Summary

| Area | Current |
|------|---------|
| Styling | Hardcoded colors in `config` object; inline SVG attrs (`stroke`, `fill`, `font-size`) throughout `visualization.js` |
| Backend model | `LayoutNode` carries pixel X/Y; backend decides visual coordinates |
| Column overlap | Not enforced; commits share X positions when timestamps are close |
| Sidebar | All panels always visible, no collapsing, Stats at bottom |
| Calendar timeline | Year/month grid lines drawn with hardcoded `stroke` values inline |
| Branch sorting | Arbitrary order from `BranchAnalyzer` |
| Split/merge edges | Diagonal lines; no direction indicator |
| Replay | Single commit blinks at computed interval |

---

## Target Architecture

```
Backend (Lanius.Business)
  └─ Logical Grid
       ├─ Row   = branch lane index (0-based, main always 0)
       ├─ Col   = time slot index (no two nodes share same column on same row)
       └─ Metadata: edge type, significance, timestamps

Frontend (Lanius.Web)
  ├─ CSS  ─── all visual styling; branch colors, commit radius, edge dash-patterns,
  │            timeline grid line opacity, replay reveal transitions
  └─ JS   ─── translates grid indices → SVG coordinates; applies semantic CSS classes;
               no inline style/attr for color, stroke-width, font-size
```

---

## Iterative Phases

---

### Phase 1 — CSS Foundation (Low risk, immediate payoff)

**Goal:** Eliminate all inline visual styling from `visualization.js`; establish CSS class contract.

**Backend changes:** None.

**Frontend changes:**

1. Add CSS custom properties and semantic classes to `styles.css`:
   - `.branch-line`, `.branch-indicator`, `.commit-node`, `.commit-node--significant`
   - `.edge--normal`, `.edge--split`, `.edge--merge`
   - `.timeline-grid-line--year`, `.timeline-grid-line--month`, `.timeline-grid-line--week`
   - `.timeline-label--year`, `.timeline-label--month`
   - Branch-type modifiers: `.branch--main`, `.branch--release`, `.branch--feature`, `.branch--fix`, `.branch--dependabot`

2. In `visualization.js`: replace all `.attr('stroke', ...)`, `.attr('fill', ...)`, `.attr('font-size', ...)`, `.attr('opacity', ...)` with `.attr('class', ...)` calls.

3. Move `config.colors` out of JS entirely; colour decisions live in CSS only.

**Deliverable:** Same visual result, but every visual property is now in CSS and overridable without touching JS.

---

#### Phase 1 Implementation Steps

Split into 9 focused sub-tasks to keep each Copilot session small.

**Step 1 — Verify CSS is complete** (`styles.css` only)  
Audit all semantic classes needed by later steps. Add any missing ones (e.g. `.timeline-grid-line--week`). No JS changes.

**Step 2 — Remove `config.colors`; add `getBranchClass()`** (`visualization.js` only)  
- Delete the `colors` block from the `config` object.
- Add `getBranchClass(branchName)` helper that returns a CSS class string (`branch--main`, `branch--release`, `branch--feature`, `branch--fix`, `branch--dependabot`, `branch--other`). Replaces `getBranchColor()` and `getNodeColor()`.

**Step 3 — Fix `renderTimelineGrid()`** (`visualization.js` only)  
Replace all `.attr('stroke',...)`, `.attr('stroke-width',...)`, `.attr('opacity',...)`, `.attr('font-size',...)`, `.attr('fill',...)` with:
- `.attr('class', 'timeline-grid-line--year')` / `--month`
- `.attr('class', 'timeline-label--year')` / `--month`

**Step 4 — Fix `renderBranchLines()`** (`visualization.js` only)  
- Branch line: remove inline `stroke`/`stroke-width`; replace D3 opacity transition with `setTimeout(() => el.classed('is-visible', true), 0)`.
- Branch indicator: replace `.attr('fill', getBranchColor(...))` + inline `stroke`/`stroke-width`/`cursor` with `getBranchClass()` on the class attribute; remove D3 `mouseenter`/`mouseleave` handlers (CSS `:hover` takes over); replace D3 fade-in with `is-visible`.

**Step 5 — Fix `renderBranchLinesForLayout()`** (`visualization.js` only)  
Identical treatment to Step 4 applied to the layout-path version of the same rendering logic.

**Step 6 — Fix `renderLogicalLayout()` edges + nodes** (`visualization.js` only)  
- Edges: remove `getEdgeColor/Width/DashArray/Opacity` calls; append line with `.attr('class', 'edge-line')` only; add `is-visible` via `setTimeout` after the loop.
- Nodes: change class string to use `is-significant` (not `significant`) and `getBranchClass()`; remove `.attr('fill',...)`, `.attr('stroke',...)`, `.attr('stroke-width',...)` from the circle; add `is-visible` via `setTimeout`.

**Step 7 — Fix `renderCalendarLayout()` + `renderCalendarTimeAxis()`** (`visualization.js` only)  
- Circles: remove `.attr('fill',...)`, `.attr('stroke',...)`, `.attr('stroke-width',...)`, `.attr('opacity',...)` — CSS `.calendar-node circle` handles them.
- Text labels: remove `.attr('font-size',...)`, `.attr('font-weight',...)`, `.attr('fill',...)`, `.attr('pointer-events',...)` — CSS `.calendar-node text` handles them.
- Add `is-visible` via `setTimeout`.
- Axis labels/lines: remove inline `fill`/`font-size`/`stroke`/`opacity` — CSS `.calendar-axis` handles them.

**Step 8 — Fix `renderCommits()` + `animateNewCommit()`** (`visualization.js` only)  
- Cross-branch connections: remove inline `stroke`/`stroke-width`/`stroke-dasharray`/opacity transition; add `is-visible` after loop.
- Branch connections: same.
- Commit circles: remove `fill`/`stroke`/`stroke-width`; add `getBranchClass()` to node group class; add `is-visible`.
- `animateNewCommit`: remove `fill`/`stroke`/`stroke-width`/inline `opacity`; add `getBranchClass()`; remove opacity from pulse animation (radius pulse only).

**Step 9 — Fix tooltips + hover handlers; remove dead functions** (`visualization.js` only)  
- `showTooltip`, `showNodeTooltip`, `showCalendarNodeTooltip`: remove all `.style(...)` except `left`/`top`; replace opacity transition with `is-visible` class; fix inline `style="color:..."` → `class="tooltip-significant"`.
- `hideTooltip`: simplify to `.remove()`.
- `handleCommitHover/Unhover`, `handleNodeHover/Unhover`, `handleCalendarNodeHover/Unhover`: remove `.attr('stroke-width',...)` calls (CSS `:hover` handles them).
- Delete dead functions: `getBranchColor`, `getEdgeColor`, `getEdgeWidth`, `getEdgeDashArray`, `getEdgeOpacity`, `getNodeColor`, `getCommitColor`.

---

### Phase 2 — Backend: Logical Grid Model

**Goal:** Backend returns grid coordinates (row, column) rather than pixel coordinates. Guarantee no column overlap.

**Backend changes (`Lanius.Business`):**

1. Add `GridRow` (int) and `GridColumn` (int) to `LayoutNode` (keep `X`/`Y` for now, derived from grid).

2. Column assignment algorithm in `LogicalLayoutEngine.CalculateNodePositions`:
   - Sort all commits by timestamp.
   - Assign column indices so no two commits on the same branch row share a column.
   - In calendar mode: column = time-bucket index (day/week/month/year slot).
   - In logical mode: column = global chronological index with per-row collision detection.

3. `LayoutResult` gains `ColumnCount` and `RowCount`.

4. `X` = `GridColumn * ColumnWidth`, `Y` = `GridRow * RowHeight` — computed server-side from configurable spacing, or computed client-side from grid indices (preferred; see Phase 3).

**Frontend changes:**

- Accept `gridRow`/`gridColumn` from API; compute SVG X/Y purely on client from column/row index and current spacing.
- This decouples backend from pixel density / zoom level.

**Deliverable:** No overlapping commits. Backend is viewport-agnostic.

---

### Phase 3 — Branch Sorting

**Goal:** main/master always row 0; other branches sorted by split-date ascending.

**Backend changes:**

1. In `LogicalLayoutEngine.AssignBranchLanes`:
   - Separate main/master to index 0.
   - For remaining branches, use `BranchHierarchyInfo.MergeBaseSha` → look up that commit's timestamp → sort ascending.
   - Branches without a known split date go last, sorted alphabetically.

2. Expose `SplitTimestamp` on `BranchHierarchyInfo` (already computed during hierarchy analysis).

**Frontend changes:** None (order comes from backend).

**Deliverable:** Predictable, semantically meaningful branch ordering.

---

### Phase 4 — Vertical Split/Merge Edges with Direction

**Goal:** Split and merge edges are vertical (same X, spanning two Y lanes); merge edges carry an arrowhead.

**Backend changes:**

1. `LayoutEdge`: add `IsVertical` bool; for `EdgeType.Branch` (split) and `EdgeType.Merge`, set `X1 == X2` (vertical connector at the commit's X coordinate).

2. Edge direction: `LayoutEdge` already has `FromCommitId`/`ToCommitId`; add `Direction` enum (`Upward`, `Downward`, `Horizontal`) derived from Y positions.

**Frontend changes:**

1. Render vertical edges as SVG `<line>` with `x1 == x2`.

2. Add SVG `<defs><marker>` arrowhead marker; apply `marker-end` to `.edge--merge` and `marker-start` to `.edge--split` via CSS (`marker-end: url(#arrow-merge)`).

3. CSS classes `.edge--split`, `.edge--merge` control dash pattern and arrow visibility.

**Deliverable:** Clean vertical split/merge visuals; directional merge arrows.

---

### Phase 5 — Sidebar UX Overhaul

**Goal:** Collapsible panels; context-sensitive visibility; Stats moved up.

**Frontend changes (`index.html`, `styles.css`, `app.js`):**

1. **Panel structure:** Add `<button class="panel-toggle">` inside each `.control-panel` header; CSS handles collapsed state via `.control-panel--collapsed` class (hides `.panel-body`, rotates chevron).

2. **Context visibility rules** (driven by `app.js` state):
   | Panel | Visible when |
   |-------|-------------|
   | Repository | Always |
   | Statistics | Repository selected |
   | Branch Filter | Repository selected |
   | Layout Mode | Repository selected |
   | Calendar Granularity | Layout = Calendar |
   | Replay Mode | Repository selected |
   | Zoom Controls | Repository selected |
   | Real-Time Monitor | Repository selected |

3. **Panel order** (top to bottom):
   1. Repository
   2. Statistics ← moved up
   3. Branch Filter
   4. Layout Mode
   5. Replay Mode
   6. Zoom Controls
   7. Real-Time Monitor

4. Persist collapsed state in `localStorage` per panel ID.

**Deliverable:** Clean, context-aware sidebar; less visual noise for new users.

---

### Phase 6 — Calendar Timeline Axis

**Goal:** Proper multi-level time axis at top of canvas; subtle CSS-styled vertical grid lines.

**Frontend changes:**

1. Replace the current ad-hoc `renderTimelineGrid()` with a D3 axis approach:
   - Primary axis: years (always shown)
   - Secondary axis: months (when zoom > 0.3)
   - Tertiary axis: weeks (when zoom > 1.0)
   - Quaternary axis: days (when zoom > 3.0)
   - Axis rows stacked at the top of the SVG (above branch lanes), sticky during pan.

2. Grid lines: rendered as `.timeline-grid-line--year`, `--month`, `--week`, `--day` SVG elements — opacity, stroke, dash controlled entirely by CSS.

3. Zoom-responsive: `renderTimelineGrid()` called on every zoom event, switching axis granularity.

**Backend changes:** None (time range already in `LayoutResult.MinTimestamp` / `MaxTimestamp`).

**Deliverable:** Professional multi-level time axis; subtle, styleable grid lines.

---

### Phase 7 — Replay Mode Overhaul

**Goal:** Pre-render all commits hidden; reveal them progressively per replay timing.

**Current:** Single commit blinks each tick via `ReplaySignalRBridge` → SignalR → JS.

**New approach:**

1. **Frontend pre-render:** When replay starts, fetch full layout, render all nodes with CSS class `.commit-node--hidden` (opacity 0, no pointer events). Layout is complete and frozen.

2. **Reveal stream:** SignalR still streams `CommitRevealed` events with the commit SHA and a `revealIndex`. JS adds `.commit-node--visible` class (CSS transition: fade + scale-in). No DOM creation during replay — only class toggling.

3. **CSS transitions:** `.commit-node--hidden → .commit-node--visible` uses `transition: opacity 0.3s ease, transform 0.3s ease`. Fully styleable.

4. **Backend (`ReplayService`):** Emit `CommitRevealed` events instead of `CommitBlinking`. No behavior change needed in `RunReplay` — just rename the event type and remove blink toggling.

5. **Branch lines:** Also pre-rendered hidden; reveal when first commit on branch becomes visible.

**Deliverable:** Smooth, visually coherent replay; no layout thrash during playback.

---

## Dependency Order

```
Phase 1 (CSS)  ──► Phase 5 (Sidebar)        independent
     │
     └──► Phase 2 (Grid Model) ──► Phase 3 (Branch Sort)
                                └──► Phase 4 (Vertical Edges)
                                └──► Phase 6 (Timeline Axis)

Phase 7 (Replay) depends on Phase 1 (CSS classes for reveal)
```

Phases 1 and 5 can start immediately in parallel. Phase 2 is the key enabler for 3, 4, and 6.

---

## Effort Estimates

| Phase | Scope | Notes |
|-------|-------|-------|
| 1 — CSS Foundation | S | Mostly mechanical; high confidence |
| 2 — Grid Model | M | Core refactor; needs test updates |
| 3 — Branch Sorting | S | Few lines in `AssignBranchLanes` |
| 4 — Vertical Edges | S | Backend trivial; frontend needs SVG marker |
| 5 — Sidebar UX | S | HTML/CSS/JS only |
| 6 — Calendar Axis | M | D3 axis API, zoom-responsive |
| 7 — Replay Overhaul | M | Touches SignalR bridge + frontend |

S = ~half day, M = 1–2 days.

---

## Open Questions

- Should grid column width be fixed (e.g. 20px per commit) or proportional to time gap? Proportional preserves the time axis semantics; fixed avoids very sparse/dense areas.
  => It should be fixed, but the actual width should be a styling issue.
- Should branch lane height be configurable per-session (compact vs. spacious)?
  => This should also be a styling issue, both spacious and compact should be possible.
- For Phase 7: should the pre-rendered layout use the same layout call as the main view, or a separate lighter endpoint?
  => I think it is OK to use the same layout call as the main view.

