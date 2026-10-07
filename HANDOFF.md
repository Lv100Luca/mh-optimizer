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
- The merged subagent branches and their worktrees are removed.

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
- Dev server: launch config `browser` (worktree `.claude/launch.json`) = `dotnet run` on http://localhost:5240. The
  desktop app's preview tools read the **main** checkout's launch.json (`preview_start {name: "browser"}` started `web`),
  so run it with `dotnet run --project src/MHWildsOptimizer.Browser --launch-profile http` in the background instead.
- **Optimize mode is fast now** (second session): "the best" in Chrome 13.3 s (was 27 s; native 8.2 s, was 9.1 s), same
  results. The per-class C# work was the score model (`ScoreDecomposition.Build`, ~20k damage-calculator probes, ~18 ms
  native, ~0.4 s interpreted) built again for each of the 17 searches on the single .NET thread. Now
  `ScoreDecompositionCache` builds it once per run (the classes differ only in the weapon's set bonus / group skill, which
  the model counts as pieces), and phase 2 continues the shown classes' phase-1 searches (`CpSatSearch.ExtendAsync`: the
  cutoff constraint is cleared, the found build stays excluded) instead of rebuilding and re-solving. The run log shows
  per class `candidates / model / solves` times. What is left is phase 2's five sequential solves per shown class.
- **Solver lanes queue their jobs** (`bridge.js` `enqueue`): a run started while the lanes were still warming up failed
  with `SolverExecutorBusyError` (seen in Chrome). Cancelling a solve that waits for its lane works too.
- **Browsers that cannot nest workers** (the Claude desktop browser pane, at least now) hung forever in "loading-workers";
  `environment()` probes it (`solver/probe-worker.js`), the footer explains it and the run buttons are disabled.
- **Testing without Claude in Chrome**: `spikes/WasmSpeedTest/edge-bench.mjs` drives headless Edge over CDP (imports a
  profile folder, runs a weapon, prints the run log); serve the publish with `spikes/WasmSpeedTest/serve.py 5241
  publish/browser/wwwroot`. The desktop pane cannot run the solver and does not register service workers.

## Next steps

1. Optional speed-ups left: AOT (`RunAOTCompilation`; mostly helps C# work, which is small now, and costs download
   size), more workers per solve in phase 2 when lanes outnumber the shown classes (`MaxWorkersPerSolve` says more than 4
   does not help a single solve, so measure first).
2. Small UI issues seen: the profile MudMenu stays open behind the import dialog; segmented labels run together
   ("Attack200 raw"); the excluded-sets list in Options is cramped; the run log is not visible after a run finishes
   (check what the React ResultsPanel did).
3. Subagent suggestions not done: `Toggle` `HintContent` parameter (OptionsPanel core skill chips); `mhwo.scrollIntoView`
   / `scrollToBottom` helpers in `wwwroot/js/app.js` for ResultsPanel.
4. Not tested: Firefox, Safari, mobile; deployment to Luca's nginx (needs HTTPS + COOP/COEP headers, see README).
5. Decide the server Web project's future (keep for local use or retire), then merge `spike/browser-cpsat` into `main`
   (the spike folder can stay or be dropped).
6. Run the full test suite after any Core change (`dotnet test MHWildsOptimizer.slnx -p:BuildSolver=false`).
