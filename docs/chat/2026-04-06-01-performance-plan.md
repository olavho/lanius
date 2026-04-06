# Performance Improvement Plan

## Context

The application targets large repositories (20K+ commits, 300+ branches). The critical path is
`LogicalLayoutEngine.CalculateLayoutAsync`, which drives the main visualization.

---

## Identified Bottlenecks (from code analysis)

### B1 — Multiple `Repository` opens per request (High Impact)
Each service call independently opens and disposes a `Repository` object. A single layout request opens the git
index **three times** in sequence:
- `BranchAnalyzer.GetBranchesAsync`
- `BranchHierarchyAnalyzer.AnalyzeBranchHierarchyAsync`
- `CommitAnalyzer.GetCommitsBatchAsync`

Repository open is expensive: it reads pack-file headers, loose-object indexes, and reflogs.

### B2 — Commit iteration uses object-per-step enumeration (High Impact)
Two places walk `branch.Commits` one object at a time:
- `BranchHierarchyAnalyzer.FindBranchPoint`: counts commits since merge base via `foreach + break`
- `CommitAnalyzer.GetCommitsSinceInternal`: uses `TakeWhile(c => c.Sha != sinceCommitSha)`

LibGit2Sharp `CommitFilter` with `ExcludeReachableFrom` lets the native layer handle this as a graph walk,
avoiding per-object managed allocations.

### B3 — Diff stats computed eagerly for all commits (High Impact)
`CommitAnalyzer.GetCommitsChronologicallyAsync` (used by `CalendarLayoutEngine`) calls `MapCommit` which
computes a full git `Patch` diff for **every commit**. On 20K commits this is 20K `repo.Diff.Compare<Patch>()`
calls — each opens two tree objects and computes line-by-line diffs.
`MapCommitFast` already exists and is used in the batch path, but not here.

### B4 — `GetCommitsChronologicallyAsync` walks entire commit graph (Medium Impact)
Uses `repo.Commits` (default walker — all reachable commits) then LINQ `Where` + `OrderBy` in managed code.
A date-filtered `CommitFilter` passed to the native walker would reduce the enumerated set significantly.

### B5 — `BranchHierarchyAnalyzer` instantiated inline, bypasses DI (Medium Impact)
`LogicalLayoutEngine` does `new BranchHierarchyAnalyzer(...)` directly. Any future caching layer added via
DI (singleton/scoped) on `BranchHierarchyAnalyzer` will be bypassed.

### B6 — O(n) lookup inside progress callback (Low Impact)
`hierarchyInfo.First(b => b.Name == p.currentBranch)` in `LoadAllCommitsAsync` is called on every progress
event — O(n) per event, totalling O(B²) for B branches at fine-grained progress.

### B7 — No caching layer (Medium Impact)
Branch list, hierarchy analysis, and commit lists are recomputed on every layout request. For a polling
scenario (5s refresh), this means full re-computation even when nothing has changed. The natural cache key
is `(repositoryId, branchTipSha)` — stable until a fetch occurs.

---

## Iterative Plan

### Iteration 0: Instrumentation (prerequisite for everything else)

**Goal**: Systematic wall-time logging so each subsequent fix is measurable.

**Changes:**
- Add `Stopwatch`-based phase timing to `LogicalLayoutEngine.CalculateLayoutAsync`, logging each phase
  duration at `Information` level with structured properties (e.g. `{Phase}`, `{ElapsedMs}`).
- Same for `BranchHierarchyAnalyzer.AnalyzeBranchHierarchyAsync` (total + per-branch merge-base lookup).
- Same for `CommitAnalyzer.GetCommitsBatchAsync` (per-branch load time, map time separately).
- Use a consistent format: `[PERF] {Operation} completed in {ElapsedMs}ms ({Detail})`.

**Output**: Structured log output that shows exactly where time goes per request.

---

### Iteration 1: Integration Performance Test Suite

**Goal**: Reproducible baseline measurements against real repositories.

**Approach:**
- Add a new test project `src/Lanius.Business.Perf/` targeting `net10.0` with MSTest.
- Tests are decorated `[TestCategory("Performance")]` and skipped in regular CI.
- Each test clones (or uses a pre-cloned local) known repository at a fixed commit, then measures key
  operations with `Stopwatch` and asserts they finish within a threshold (to catch regressions) AND writes
  results to `docs/perf/` in JSON for trending.

**Test suite outline:**

| Test | Measures |
|------|----------|
| `BranchAnalyzer_GetBranches_LargeRepo` | Time to enumerate all branches |
| `BranchHierarchyAnalyzer_Analyze_ManyBranches` | Hierarchy analysis for N branches |
| `CommitAnalyzer_GetCommitsBatch_AllBranches` | Batch commit load, full set |
| `CalendarLayoutEngine_FullLoad` | End-to-end calendar layout |
| `LogicalLayoutEngine_FullLoad` | End-to-end logical layout (main bottleneck) |

**Repo strategy**: Use the Lanius repo itself (self-hosting) plus one large well-known public repo
(e.g. `git/git` or `dotnet/runtime` cloned locally) for stress testing.

**Baseline capture mechanism**: `PerfResult` record written as JSON to `docs/perf/YYYY-MM-DD-baseline.json`
with machine name, .NET version, repo, and per-operation timings.

---

### Iteration 2: Fix B3 + B4 — Remove eager diff stats from layout paths (Quick Win)

> ⏳ **Deferred** — diff stats are a valid future feature but not needed for layout visualization.
> Revisit after perf test baseline is captured.

**Goal**: Eliminate 20K+ `Patch` computations from `CalendarLayoutEngine`.

**Changes:**
- `CommitAnalyzer.GetCommitsChronologicallyAsync`: switch to `MapCommitFast` (already exists).
- Pass a `CommitFilter` with `CommitSortStrategies.Time` and optional date range directly to
  `repo.Commits.QueryBy(filter)` to let the native walker handle filtering.

**Expected impact**: Calendar layout time should drop proportionally to number of commits.

---

### Iteration 3: Fix B2 — Use `CommitFilter` for branch-specific walks (High Impact)

**Goal**: Replace manual `TakeWhile` / `foreach+break` with native graph-bounded walks.

**Changes:**
- `CommitAnalyzer.GetCommitsSinceInternal`: replace `branch.Commits.TakeWhile(c => c.Sha != sinceCommitSha)`
  with `repo.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = branch.Tip, ExcludeReachableFrom = mergeBaseCommit })`.
- `BranchHierarchyAnalyzer.FindBranchPoint`: replace manual commit count loop with the same `CommitFilter`
  approach + `.Count()` (the native walker terminates at the exclusion boundary, not in managed code).

**Expected impact**: For branches with deep history (e.g. `origin/main` with 10K commits), this can be an
order-of-magnitude improvement.

---

### Iteration 4: Fix B1 — Repository connection scope (Medium/High Impact)

**Goal**: Open `Repository` once per layout calculation, share across analyzers.

**Approach options** (decide after measuring B1's actual cost in Iteration 0/1):
- **Option A** (lightweight): Pass an already-open `Repository` instance as a parameter through the call
  chain for operations that are already coordinated (e.g. `GetCommitsBatchAsync` already does this).
- **Option B** (DI-scoped): Introduce `IRepositorySession` (wraps `Repository`, scoped lifetime) injected
  into the analyzers, managed by the layout engine for the duration of one calculation.

Option A is simpler and avoids DI restructuring. Option B enables future caching on the session.

---

### Iteration 5: Fix B5 + B6 — DI registration + dictionary lookups

**Goal**: Register `BranchHierarchyAnalyzer` in DI, eliminate O(n) lookups.

**Changes:**
- Register `BranchHierarchyAnalyzer` as a transient service; inject it into `LogicalLayoutEngine`.
- In `LoadAllCommitsAsync` progress callback, replace `hierarchyInfo.First(...)` with a
  `Dictionary<string, BranchHierarchyInfo>` built once before the loop.

---

### Iteration 6: Fix B7 — Caching layer (Medium Impact)

> ⏳ **Deferred** — implement only after instrumentation and perf tests are in place so cache
> effectiveness can be measured. Also defer any model restructuring (e.g. shared commit dictionary
> + per-branch SHA lists) until measured bottlenecks justify the complexity.

**Goal**: Avoid redundant computation across polling cycles (layout/zoom changes should not re-trigger
full analysis).

**Cache targets** (using `IMemoryCache` with sliding expiration):
- Branch list: key = `repositoryId`, expires on fetch/update notification.
- Hierarchy info: key = `(repositoryId, hash-of-branch-tip-shas)`.
- Per-branch commit list: key = `(repositoryId, branchName, tipSha, mergeBaseSha)`.

**Invalidation**: `RepositoryStorageService.FetchUpdatesAsync` raises an event / updates a version token
that invalidates relevant cache entries.

**Model structure note**: A single `Dictionary<string, Commit>` keyed by SHA (with branches holding
only lists of SHAs) may reduce memory duplication and speed up deduplication. Evaluate after
bottlenecks are measured — premature if the hot path is elsewhere.

---

## Recommended Execution Order

```
[0] Instrumentation         ← do first, everything else measured against this
[1] Perf test suite         ← parallel with 0, needed to capture baseline
[2] Fix B3+B4 (diff stats)  ← quick win, isolated change
[3] Fix B2 (CommitFilter)   ← highest algorithmic impact
[4] Fix B1 (repo opens)     ← decide approach after seeing Iteration 0 data
[5] Fix B5+B6 (DI + dicts)  ← low risk housekeeping
[6] Fix B7 (caching)        ← do last, after the hot path is already fast
```

Each iteration: measure → change → run perf tests → compare to baseline → commit result JSON.
