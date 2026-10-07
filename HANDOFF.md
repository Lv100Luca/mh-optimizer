# Handoff: the optimizer as a browser app (2026-10-07)

## Goal

Publish the optimizer on Luca's own server with **no server-side compute**: everything runs in the visitor's browser.
Decided: Blazor WebAssembly + MudBlazor UI (replacing the React client's role), **CP-SAT only** in the browser via
[or-tools-wasm](https://github.com/Axelwickm/or-tools-wasm) (the beam search stays CLI/server-only: it does not fit
WebAssembly memory), profiles in browser storage with import/export. The user asked to test in **Chrome** (Claude in
Chrome; it needed a reconnect once).

## Where

- Worktree `.claude/worktrees/browser-cpsat`, branch `spike/browser-cpsat` (main checkout untouched; `.claude/worktrees/`
  is in `.git/info/exclude`). Nothing pushed or merged into `main`.
- Commits on the branch, oldest first: spike (`9823eb8`), Core CP-SAT changes (`1116199`), MHWildsOptimizer.Api
  extraction (`29ba526`), browser app foundation (`b05baa1`…), three merged panel ports from subagents, fixes (`668ad55`).
- The merged subagent branches `worktree-agent-a552ca9f8ca28d1ff`, `worktree-agent-a25b54d7909d05995`,
  `worktree-agent-a8cd5b624c1d4bb36` and their worktrees under `.claude/worktrees/agent-*` can be removed
  (`git worktree remove …` + `git branch -d …`).

## What is done

- **Spike** `spikes/WasmSpeedTest/` (README has all measurements): WASM Beam is ~3× slower and memory-bound; CP-SAT in
  or-tools-wasm runs at native speed; subsolver tuning; optimize-mode strategies.
- **Core** (`src/MHWildsOptimizer.Core/Optimize/CpSatBackend.cs`, `CpSatSearch.cs`, `Optimizer.cs`):
  `ICpSatBackend` (native default, browser via JS), `RunAsync`; `CpSatParameters.For(threads)` names `max_lp` /
  `reduced_costs` below 8 workers (4 workers = 16 default; 1 worker usable) and caps a solve at 4 workers; optimize mode
  = best per class first with a cutoff, then top N for the shown classes, spare threads as parallel lanes ("the best":
  94 s → 10 s natively, identical results, covered by `CpSatOptimizeModeTests`); `OptimizerOptions.ProcessorCount`;
  `CpSatParameters.ScoreChecks`; `GameDataLoader.Load(Func<string, Stream?>)`.
- **`src/MHWildsOptimizer.Api`**: catalog, resolve, evaluate, inventory, result mapping, `IProfileStore` (file
  `ProfileStore` for the server), `ProfileRules`, `Optimization.RunAsync` (shared by the server's SSE endpoint and the
  browser; takes an engine override that keeps the inputs hash). The Web project uses it; its API is unchanged.
- **`src/MHWildsOptimizer.Browser`** (Blazor WASM, MudBlazor 9.11, .NET 10): `Services/AppState.cs` (port of the React
  state.tsx: sessions, drafts in localStorage, live validation/scoring, runs, batch runs, URL routing),
  `BrowserProfileStore` (localStorage, results gzipped), `CpSatBridge` + `solver/` (vite bundle of or-tools-wasm into
  `wwwroot/solver`, lanes = pool workers, cancellation, warm-up), `ProfileTransfer` + `ImportDialog` (bundle or the files
  of a server profile folder), `wwwroot/coi-sw.js` (cross-origin isolation where the host sends no headers), all panels
  ported (WeaponPanel, SkillPair, Targets, Limits, Conditions, Talismans, Options incl. storage use, Review, Results,
  Inventory, My builds with EquipmentEditor/SlotPickers, BuildCard, BuildCompare, Delta, ScoreDelta, SkillPicker).
  Data and icons are copied into `wwwroot` on build (git-ignored; linked files are not served by the dev server).
- **Tested in Chrome**: start-up and COI via the service worker, profile import of `inputs/profiles/Luca` (4 weapons,
  9 talismans, preset), batch "Run stale" of all 4 weapons, Results/Inventory/Conditions/Options/My builds render
  correctly; "the best" gives 700.82 / 700.82 / 683.05 like native. Release publish works (`dotnet publish
  src/MHWildsOptimizer.Browser -c Release -o publish/browser`, ~4.5 MB first load). README has a "Browser app" section
  with nginx config.
- Dev server: launch config `browser` (worktree `.claude/launch.json`) = `dotnet run` on http://localhost:5240.

## Next steps

1. **Optimize mode is 27 s in the browser vs 10 s native.** The timed run log (`[ 4.0s] [1/14] …`) shows classes
   finishing one at a time ~1 s apart instead of 4 lanes at once, so the per-class C# work (Relevance, Candidates,
   ScoreDecomposition.Build, BuildModel, loadout scoring; interpreted .NET on the UI thread) serializes the lanes.
   Neither pre-started lanes nor `ScoreChecks = 20` changed it. Profile it (e.g. Stopwatch around model building in
   `CpSatSearch.RunAsync`, or browser devtools); options: AOT (`RunAOTCompilation`, bigger download), caching per class
   between phase 1 and 2, reusing phase 1's first solve, or yielding (`Task.Yield`) so lanes submit solves earlier.
2. Small UI issues seen: the profile MudMenu stays open behind the import dialog; segmented labels run together
   ("Attack200 raw"); the excluded-sets list in Options is cramped; the run log is not visible after a run finishes
   (check what the React ResultsPanel did).
3. Subagent suggestions not done: `Toggle` `HintContent` parameter (OptionsPanel core skill chips); `mhwo.scrollIntoView`
   / `scrollToBottom` helpers in `wwwroot/js/app.js` for ResultsPanel.
4. Not tested: Firefox, Safari, mobile; deployment to Luca's nginx (needs HTTPS + COOP/COEP headers, see README).
5. Decide the server Web project's future (keep for local use or retire), then merge `spike/browser-cpsat` into `main`
   (the spike folder can stay or be dropped).
6. Run the full test suite after any Core change (`dotnet test MHWildsOptimizer.slnx -p:BuildSolver=false`).
