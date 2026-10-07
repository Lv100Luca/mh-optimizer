# Browser optimizer spike

Can the optimizer run entirely in the browser (Blazor WebAssembly), with no server-side compute? This spike measures the
Beam search and the CP-SAT engine natively, in WebAssembly, and end to end in a Blazor page, on Luca's saved configs.
Nothing here touches `src/`: experiments run on generated copies of `CpSatSearch.cs` (`make-copies.py`).

Measured 2026-10-07 on a 16-thread desktop, Chromium 152 (the Claude desktop browser pane), .NET 10, OR-Tools 9.15,
[or-tools-wasm](https://github.com/Axelwickm/or-tools-wasm) 1.0.0.

## Summary

- **CP-SAT runs in the browser at native speed.** Blazor builds the model in C# (managed OR-Tools classes, interpreted
  .NET is enough), or-tools-wasm solves it on WebAssembly threads, Blazor reads the build back. Same best builds as native,
  every solve proven optimal, ~150 MB of memory.
- **CP-SAT is badly tuned for few threads, and that is fixable.** Below 8 workers CP-SAT drops the two subsolvers that do
  all the bound proving here (`max_lp`, `reduced_costs`). Naming them explicitly makes 4 workers as fast as 16, and one
  worker with `linearization_level:2` only ~2× slower.
- **Optimize mode goes from 94 s to 10–19 s** by solving each skill-pair class's best first, cutting off classes that cannot
  reach the shown top 3, and only then filling the 5 builds for the 3 shown classes; same results.
- **Beam does not fit the browser**: AOT WebAssembly is ~3× slower than native and the search needs 2.4 GB to > 4 GB.
- **Download for a first visit: ~4.4 MB compressed** (.NET runtime + app 2.3 MB, CP-SAT 2.0 MB, game data 0.1 MB), no AOT.
- **Hosting**: static files, served with `Cross-Origin-Opener-Policy: same-origin` and
  `Cross-Origin-Embedder-Policy: require-corp` (needed for WebAssembly threads). Icons are already self-hosted.

## CP-SAT in a Blazor page

`Browser/Pages/CpSat.razor` (`/cpsat`): `CpSatAsyncSearch` (generated from `CpSatSearch`) solves through `WasmCpSolver`,
which serializes the `CpModelProto` and calls `wwwroot/ortools/bridge.js` (or-tools-wasm, bundled by `node/vite.config.js`).

| config | threads, parameters | browser | native |
|---|---|---|---|
| Test | 16, default | 6.86 s | 6.6–6.9 s |
| Test | 8, default | 5.66 s | – |
| Test | 4, default | 44.3 s | 63 s |
| Test | 4, tuned subsolvers | 5.3–5.8 s | – |
| Test | 1, `linearization_level:2` | 12.1 s | 22.5 s |
| Current | 16, default | 11.9 s | 9–12 s |
| Current | 4, tuned subsolvers | 9.9 s | – |

Whole searches (5 solves, model built in C#), all with best 681.59 and every solve optimal. C# side: score model 0.4–0.6 s
interpreted, serializing ~5 ms, parsing < 5 ms per solve. Solver runtime start-up: 3.2 s on first load.

- **Cancellation** works: aborting a solve returns at once, and the next solve runs without a restart.
- **Several solves at once**: or-tools-wasm's executor takes one job at a time; `bridge.js` also has "lanes", dedicated
  workers with their own solver instance. Three lanes ran three searches concurrently at full speed once started, but each
  lane loads its own runtime (5–9 s when three start together), so lanes should be started when the page loads.
- Not measured: browser memory (the memory API is unavailable in this browser; natively CP-SAT peaks at 120–210 MB),
  Firefox, Safari, mobile. Browsers without JSPI use or-tools-wasm's asyncify build (2.5 MB instead of 2.0 MB).

## CP-SAT tuning (native, the 10 saved models of Test and Current)

`Native solve` solves the models saved by `Native dump` (exact bytes of every solve of a real run). The log of an 8-worker
solve shows the optimum found at 0.3 s and the rest spent proving it, with every bound coming from `max_lp` and
`reduced_costs`; with 4 workers CP-SAT does not run either of them.

| workers | parameters | all 10 models |
|---|---|---|
| 1 | default | ~90 s per model |
| 1 | `linearization_level:2` | 33.0 s |
| 2 | default | 62.9 s for 2 models |
| 2 | subsolvers `max_lp, reduced_costs` | 25.4 s |
| 3 | + `quick_restart` | 19.2 s |
| 4 | default | 152.3 s |
| 4 | subsolvers `max_lp, reduced_costs, quick_restart, default_lp` | 17.3–20.0 s |
| 4 | subsolvers `max_lp, reduced_costs, pseudo_costs, quick_restart` | **13.4 s** |
| 6 | 6 tuned subsolvers | 16.0 s |
| 8 | default / 8 tuned subsolvers | 22.7 s / 18.4 s |
| 16 | default | 14.8 s |

All optimal. `sweep-subsolvers.sh` and `sweep-linearization.sh` rerun these; `cpsat_log.py` summarizes CP-SAT logs.

## Optimize mode ("the best": 14 classes, top 3 shown with 5 builds each)

`Native optimize` (`OptimizeExperiment.cs`, using `CpSatCutoffSearch`):

| strategy | workers | lanes | time | solves |
|---|---|---|---|---|
| baseline (as `Optimizer` today: every class, 5 builds each) | 16 default | 1 | 94–99 s | 70 |
| two-phase (each class's best, then 5 builds for the top 3) | 16 default | 1 | 27.7 s | 29 |
| cutoff (two-phase; a class must beat the 3rd best so far, else infeasible) | 16 default | 1 | 21.4 s | 29 |
| cutoff | 1, `linearization_level:2` | 1 | 33.0 s | 29 |
| cutoff | 4 tuned | 1 | 18.6 s | 29 |
| cutoff | 4 tuned | 4 | **10.3 s** | 29 |

Every strategy returns the same three classes with the same five builds. With the cutoff, 7–9 of the 14 classes are proved
unable to reach the top 3 in ~0.2 s each. Not tried yet: reusing phase 1's solve as the first of phase 2's five.

## Beam in WebAssembly (before the CP-SAT focus)

| case | native 1 thread | native 16 threads | WASM interpreted | WASM AOT | browser memory |
|---|---|---|---|---|---|
| Test, beam 20k | 5.9 s | – | 60.0 s | 15.5 s | 0.6 GB |
| Test, beam 100k | 19–20 s | 7.0 s | – | 60.3 s | 2.35 GB |
| Current, beam 300k | 50 s (peak 4.7 GB) | 20–23 s | – | crashes at 4 GB | > 4 GB |

Beam found 676.3 where CP-SAT finds 681.6. WebAssembly memory is capped at 4 GB per worker (the .NET default is 2 GB,
`EmccMaximumHeapSize` raises it with the `wasm-tools` workload) and the browser GC needs about twice the live heap. Most of
it is `StateSearch.Expand` buffering a cloned key per child before merging.

## Layout

- `Native/` – console app on `MHWildsOptimizer.Core`: beam/CP-SAT benches, `dump`, `solve`, `optimize`.
- `Browser/` – Blazor WebAssembly app compiling Core from source: `/` beam bench, `/cpsat` CP-SAT end to end.
- `Shared/` – bench cases and the fixed-pair search setup, used by both.
- `node/` – `bridge.js` + `pool-worker.js` (bundled into `Browser/wwwroot/ortools`), `solve.mjs` (CP-SAT models in Node).
- `serve.py` – static server with the cross-origin isolation headers and Blazor route fallback (launch config `wasm-speedtest`).
- `make-copies.py` – regenerates `Native/Generated` and `Browser/Generated` from Core's `CpSatSearch.cs`.

## Running it

```bash
cd spikes/WasmSpeedTest/node && npm install && npx vite build    # bundles or-tools-wasm into Browser/wwwroot/ortools
dotnet run --project spikes/WasmSpeedTest/Native -c Release -- dump Test 16 spikes/WasmSpeedTest/out/models
dotnet run --project spikes/WasmSpeedTest/Native -c Release -- solve spikes/WasmSpeedTest/out/models 4 60 "" "linearization_level:2"
dotnet run --project spikes/WasmSpeedTest/Native -c Release -- optimize "the best" 4 cutoff "subsolvers:'max_lp', subsolvers:'reduced_costs', subsolvers:'pseudo_costs', subsolvers:'quick_restart'" 4
dotnet publish spikes/WasmSpeedTest/Browser -c Release -o spikes/WasmSpeedTest/out/interp
```

Change the published `index.html`'s `<base href>` to `/interp/wwwroot/`, start the `wasm-speedtest` launch config and open
`http://localhost:5231/interp/wwwroot/cpsat?run=Test&threads=4&params=...` (`concurrent=3` runs that many at once on lanes).
