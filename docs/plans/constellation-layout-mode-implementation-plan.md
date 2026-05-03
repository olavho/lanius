# Constellation Layout Mode — Implementation Plan

## Goal
Add a new **Constellation** visualization mode that prioritizes aesthetics over chronological/geometric accuracy while still preserving branch/merge relationships.

## Design Principles
- **Aesthetic-first** composition (smooth ribbons, clustered nodes, visual rhythm)
- **Topology-aware** (parent/branch/merge relationships remain meaningful)
- **Scalable** for large repos (aggregation at low zoom, detail on demand)
- **Non-destructive**: existing `logical`, `calendar`, `timeline` modes remain unchanged

---

## 1) Product/UX Definition

### 1.1 Mode Behavior
- New layout mode option: `Constellation (aesthetic topology)`
- Not date-accurate in x/y placement
- Preserves:
  - branch identity
  - merge/split events
  - local parent-child continuity

### 1.2 Visual Language
- Main branch: strongest stroke/contrast
- Other branches: color-coded ribbons with lower weight
- Merge nodes: larger/outlined junction nodes
- Optional density glow for high-commit regions

### 1.3 Interactions
- Hover commit: spotlight connected path, dim unrelated nodes/edges
- Branch filter: acts as visual spotlight (not hard hide by default)
- Click commit: existing detail popup behavior unchanged

---

## 2) Architecture Changes by Layer

## 2.1 `Lanius.Business` (layout generation)
### New/extended contracts
- Extend layout mode enum to include `Constellation`
- Add a constellation layout service/strategy (e.g. `ConstellationLayoutService`)

### Algorithm (initial version)
1. Build branch lanes as spline/ribbon guide curves (not strict horizontal lanes)
2. Place commits along guide curves using weighted spacing:
   - denser spacing for high activity clusters
   - increased spacing around merge/split events
3. Run collision-relax pass (light force iteration) to reduce overlap
4. Emit synthetic control points for curved edges/ribbons
5. Compute visual metadata:
   - `clusterId`
   - `visualWeight`
   - `isJunction`
   - `focusGroup`

### DTO additions (backward-compatible)
Extend layout node/edge payloads with optional fields used by Constellation only:
- Node:
  - `visualWeight` (number)
  - `clusterId` (string/int)
  - `isJunction` (bool)
  - `glowStrength` (number)
- Edge:
  - `curve` (control points / path hints)
  - `edgeVisualType` (`flow`, `merge`, `split`)

> Existing modes should continue emitting current fields; new fields optional.

## 2.2 `Lanius.Api`
- Accept `mode=constellation` in layout endpoint
- Route to business-layer constellation strategy
- Keep API response shape compatible with existing frontend parser

## 2.3 `Lanius.Web` (D3 rendering)
### UI
- Add `<option value="constellation">Constellation (aesthetic topology)</option>` to layout selector
- Update mode description text for this mode

### Renderer
- In `visualization.js`, branch render path on `layout.mode === 'Constellation'`
- Render order:
  1. background density/glow layer
  2. branch ribbons/curved flow edges
  3. nodes (size from `visualWeight`)
  4. interaction highlight overlay

### Styling (`styles.css`)
- Add constellation-specific classes:
  - `.constellation-node`
  - `.constellation-flow`
  - `.constellation-junction`
  - `.constellation-dimmed`
  - `.constellation-highlighted`
- Keep Metro constraints (flat controls, no border radius)

---

## 3) Performance Strategy

## 3.1 Progressive detail
- Zoomed out: aggregate nearby commits into cluster nodes
- Zoomed in: expand to individual commits

## 3.2 Bounded layout cost
- Cap force-relax iterations (small fixed count)
- Cache computed layout per:
  - repo id
  - branch filter
  - mode
  - granularity params (if relevant)

## 3.3 Rendering optimization
- Use path simplification for long curves at low zoom
- Prefer opacity/class toggles over full rebind on hover

---

## 4) Testing Plan

## 4.1 `Lanius.Business.Test`
Add unit tests for constellation layout service:
- `GenerateLayout_EmptyInput_ReturnsEmptyLayout`
- `GenerateLayout_LargeInput_ProducesStableCoordinates`
- `GenerateLayout_MergesPresent_FlagsJunctionNodes`
- `GenerateLayout_SameInput_ProducesDeterministicOutput` (if deterministic seed)
- `GenerateLayout_NoOverlapsBeyondThreshold`

## 4.2 API tests
- `GET /layout?mode=constellation` returns valid payload
- Backward compatibility for other modes unchanged

## 4.3 Frontend smoke tests
- Mode switch works without reload
- Hover spotlight works on dense graph
- Replay and detail panel still work in constellation mode

---

## 5) Rollout Phases

### Phase A — Vertical Slice (MVP)
- Add mode wiring end-to-end
- Basic curved branch flow + node rendering
- No clustering yet

### Phase B — Readability Improvements
- Junction styling
- Spotlight interactions
- Refined color/opacity tuning

### Phase C — Scale Enhancements
- Cluster aggregation + zoom expansion
- Cached layouts
- Curve simplification

### Phase D — Polish
- Optional cinematic replay path pulses
- Final visual balancing and accessibility checks

---

## 6) Acceptance Criteria
- New mode available and selectable in UI
- Constellation renders significantly different aesthetic from existing modes
- Commit detail and branch filtering continue to function
- No regressions in existing modes
- Large repositories remain interactive (no major UI freeze)

---

## 7) Risks & Mitigations
- **Risk:** Layout instability across refreshes
  - **Mitigation:** deterministic seed for pseudo-random offsets
- **Risk:** Overdraw/performance in huge repos
  - **Mitigation:** progressive clustering + capped iterations
- **Risk:** User confusion due non-accurate placement
  - **Mitigation:** mode description text: “aesthetic topology, not strict timeline”
