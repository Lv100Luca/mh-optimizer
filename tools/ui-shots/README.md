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
