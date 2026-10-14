# UI screenshots of the browser app

Headless Edge (CDP) imports a profile folder into a published build, opens a weapon and captures every tab at full
content height, desktop (1600 px) and phone (412 px) width, plus the interaction states in `steps.json`. The same run
before and after a change gives comparable images; `compare.py` puts them side by side.

```bash
dotnet publish src/MHWildsOptimizer.Browser -c Release -p:BuildSolver=false -o bin/claude-build/browser-publish
cp -r src/MHWildsOptimizer.Browser/wwwroot/solver bin/claude-build/browser-publish/wwwroot/   # when BuildSolver=false
python spikes/WasmSpeedTest/serve.py 5251 bin/claude-build/browser-publish/wwwroot
node tools/ui-shots/shots.mjs http://127.0.0.1:5251 inputs/profiles/Luca shots/before Current tools/ui-shots/steps.json
# ... change the app, publish again ...
node tools/ui-shots/shots.mjs http://127.0.0.1:5251 inputs/profiles/Luca shots/after Current tools/ui-shots/steps.json
SHOTS_DIR=shots python tools/ui-shots/compare.py shots/compare   # reads shots/before and shots/after
```

- `ONLY_TABS=weapon,options` captures those tabs only; `ONLY_EXTRA=1` captures only the `steps.json` states.
- Every run starts a fresh Edge profile and kills stragglers of earlier runs (a leftover would answer on the debugging
  port with its own state).
- The extra steps run in order in one browser and leave state behind (a changed target, a dirty weapon), which is the
  same in a before and an after run.

## Responsiveness

`perf.mjs` measures how long the page's thread is blocked (long tasks, gaps between frames) in the same setup: `gems`
types into a decoration slot picker on My builds, `run` starts a run and prints its log, `check` drives the pickers and
prints what they show. Compare a published build before and after a change, served on two ports.

```bash
node tools/ui-shots/perf.mjs http://127.0.0.1:5251 inputs/profiles/Luca Current gems
node tools/ui-shots/perf.mjs http://127.0.0.1:5251 inputs/profiles/Luca "the best" run
```

`background.mjs` times a run whose tab is in the background. Chrome and Edge run a hidden tab's timers about once a minute
after five minutes ("intensive wake-up throttling"). Headless browsers only get the once-a-second throttling, so the
script runs a real browser window moved off the screen (it shows in the taskbar), starts a run of the weapon and prints
the run's time, tab titles and log (`HIDE=0` runs headless with the tab in front instead). A step of a run that waits on
a timer shows up as gaps of a minute in the log. Edge takes the grace period as a feature parameter (10 s here); a bare
Chromium ignores it, so give it `WAIT_BEFORE_RUN=330` (seconds hidden before the run starts).

```bash
node tools/ui-shots/background.mjs http://127.0.0.1:5251 inputs/profiles/Luca "the best"
EDGE_PATH=C:/path/to/chromium/chrome.exe WAIT_BEFORE_RUN=330 node tools/ui-shots/background.mjs http://127.0.0.1:5251 inputs/profiles/Luca "the best"
```

Every script takes `EDGE_PATH` for another Chromium browser (Chrome, a bare Chromium); they clean up `msedge.exe` and
`chrome.exe` processes of their own temporary profiles only.

`dropdowns.mjs` opens every dropdown (pickers, selects, search selects, the profile menu) on every tab and reports any
popover with more than one scrolling element; run it at a few `SIZE`s (e.g. `1600,1000`, `1366,650`, `412,850`).

`interact.mjs` times everyday edits: tab switches (each visited twice), weapon reinforcements, condition toggles, skill
limits, and duplicating and editing a build. Per step it prints the long tasks until the page is quiet again (debounced
validation and draft saving included), the browser's script / style / layout time, and how many popovers the page holds
(MudBlazor registers each one; a few hundred made every re-render slow). `REPEAT=3` runs everything three times,
`PROFILE=<step>` prints a CPU profile of that step.

Serve each build on its own port and check who answers there: `serve.py` binds with address reuse, so another session's
server on the same port can take your requests.
