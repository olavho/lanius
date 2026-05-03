# Metro UI Refresh Plan (Lanius.Web)

## Objective
Improve visual clarity and usability with a flat, Metro-like design (no border radius), while keeping changes minimal and localized to frontend UI files.

## Scope
- In scope:
  - `src/Lanius.Web/wwwroot/css/styles.css`
  - `src/Lanius.Web/wwwroot/index.html` (minor structural cleanup only)
- Out of scope:
  - Backend/API/business logic changes
  - Visualization algorithm/layout logic changes

## Design Direction
- Flat, sharp edges everywhere (`border-radius: 0`)
- Strong hierarchy through spacing, contrast, and typography
- Minimal accent usage (primary UI accent + semantic graph accents)
- Reduced visual noise in dense graph views

## Implementation Phases

### Phase 1 — Visual Tokens and Global Styles
- Update CSS variables for a Metro-style palette:
  - neutrals: `#ffffff`, `#f3f3f3`, `#e6e6e6`, `#1f1f1f`, `#666666`
  - primary accent: `#0078d4`
- Set `--border-radius: 0` and remove residual element-level rounding.
- Normalize spacing scale and text sizing for controls.
- Keep UI typography sans-serif; reserve monospace for technical/readout values.

### Phase 2 — Sidebar and Control Panels
- Refine panel layout into flat sections with clear separators.
- Improve spacing and grouping in each panel.
- Standardize control sizing and alignment.
- Add sticky panel headers for better orientation while scrolling.

### Phase 3 — Header, Canvas Frame, Legend
- Simplify header styling for clearer hierarchy.
- Improve canvas header information density (title + status/readouts).
- Remove inline legend styling from `index.html`; move fully to CSS classes.
- Keep legend compact, flat, and consistently positioned.

### Phase 4 — Graph Readability
- Tune baseline contrast:
  - lighter normal edges/grid
  - clearer year markers
  - subtler month/week/day guides
- Improve node/edge interaction states:
  - stronger hover/focus on active node/path
  - fade non-relevant graph elements
- Preserve branch/merge distinction with both color and dash style.

### Phase 5 — Interaction and Accessibility
- Standardize button/input states: default, hover, active, disabled.
- Add clear keyboard focus outlines (high contrast, square style).
- Improve status line semantics (info/success/error tokens).
- Verify text/control contrast for WCAG compliance.

## Concrete File Changes

### `src/Lanius.Web/wwwroot/css/styles.css`
- Update root tokens (colors, spacing, radius).
- Restyle controls, buttons, panel sections, headers, and legend.
- Add/standardize focus-visible styles and disabled states.
- Adjust graph visual classes for contrast/hover emphasis.

### `src/Lanius.Web/wwwroot/index.html`
- Remove inline `style` attributes for edge legend.
- Use semantic class-only legend markup.
- Keep structure unchanged unless required for sticky headers/status placement.

## Acceptance Criteria
- No rounded corners anywhere in the UI.
- Sidebar is easier to scan and less visually crowded.
- Graph remains performant and becomes easier to read in dense regions.
- Controls have consistent visual states and keyboard focus behavior.
- Existing functionality (clone/filter/layout/replay/zoom/monitor) remains unchanged.

## Execution Strategy
1. Apply Phase 1 + Phase 2 first (safe visual baseline).
2. Apply Phase 3 cleanup (legend/header consistency).
3. Apply Phase 4 readability tuning.
4. Finish with Phase 5 accessibility and state consistency.
5. Smoke test all UI workflows in browser.
