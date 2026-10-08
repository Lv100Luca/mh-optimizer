# MHWildsOptimizer

Build optimizer / set maker for Monster Hunter Wilds (Ver 1.041), centred on Gogma Artian weapons that roll one set-bonus skill and one group skill and count as one armor piece for each.

* `notes/README.md` – research write-up: equipment system, Gogma mechanics, damage model, decisions, open questions, sources.
* `data/` – normalized dataset (armor, sets, skills, decorations, charms, Gogma weapon variants, roll pool, damage-model constants) plus the raw API dumps (`data/raw`) and game-data files (`data/datamine`).
* `tools/` – Python scripts that regenerate `data/*.json` from `data/raw` and scrape the wiki tables.
* `src/MHWildsOptimizer.Core` – domain model, damage calculator, optimizer (C# / .NET 10).
* `src/MHWildsOptimizer.Cli` – command-line front end.
* `src/MHWildsOptimizer.Api` – what the web UIs show (catalog, validation, build scoring, inventory, result mapping, the profile store contract), shared by the server and the browser app.
* `src/MHWildsOptimizer.Web` – web front end: ASP.NET Core API + React client (`client/`), game icons under `client/public/icons` (fetched by `tools/fetch_icons.py`).
* `src/MHWildsOptimizer.Browser` – the same UI as a static web app (Blazor WebAssembly + MudBlazor) that runs everything in the browser: profiles in browser storage, CP-SAT in WebAssembly. See [Browser app](#browser-app).
* `tests/MHWildsOptimizer.Tests` – xunit tests; `tests/MHWildsOptimizer.Web.Tests` – API integration tests.

* `inputs/` – optimizer inputs: `request.example.json` (weapon as the game shows it, skill-pair mode, target skills, conditions, talisman file, options) and `talismans.example.json` (your random talismans with skills and decoration slots).

Current scope: Great Sword and Long Sword. The score is EFR + EFE per 100 MV of a chosen attack (Great Sword and Switch Axe have attack data from the motion value datamine: True Charged Slash, Full Release Slash, ...) against a target: a monster part, the training dummy or custom hitzones. A custom sequence (your rotation, step by step, where a step can change conditions such as Maximum Might after a stamina-costing tackle) can be scored instead of a single attack. Conditional skills are user-toggleable and only count when the loadout contains them. `conditions.skill_limits` removes skills from the optimization (`0`) or values them only up to a level (e.g. `{ "Burst": 1 }` for Great Sword). The weapon's stats are inputs and never optimized; its rolled set bonus / group skill is either fixed or searched over all 294 rollable pairs. Talismans come from the craftable charm lines (max rank) plus the random talismans you list.

Interactive editor (Spectre.Console): pick an existing configuration under `inputs/` or start a new one, click together weapon, skill pair, targets, conditions, talismans and options, then save it as `inputs/<name>.json` plus `inputs/<name>.talismans.json`.

```bash
dotnet run --project src/MHWildsOptimizer.Cli -- edit
```

Inside the editor, "Run optimizer" searches for the best builds and writes them to `inputs/<name>.results.txt`. From the command line:

```bash
dotnet run --project src/MHWildsOptimizer.Cli -- run inputs/request.example.json
```

Validate a saved configuration and show the resolved inputs:

```bash
dotnet run --project src/MHWildsOptimizer.Cli -- request inputs/request.example.json
```

The search is a dynamic program over skill states (talisman, then one armor slot at a time): partial builds with the same relevant skill levels, slot counts and set/group counts are merged, dominated and target-infeasible states are dropped, and every surviving final state gets its decorations (exact cover of the targets, then greedy damage fill) and an EFR + EFE score. `options.max_states_per_depth` (default 100000) bounds the beam; raise it for a slower, more exhaustive run. In optimize mode the 294 rollable pairs collapse into score-equivalent classes that are searched in parallel. `options.max_threads` (default 0 = all logical processors) caps the threads used, both across classes and inside each search; the results are identical for any thread count.

## Web UI

The web UI covers the same setup as the editor (weapon, rolled pair, skill pair mode, targets including set bonuses and group skills by tier, skill limits, conditions with the proc damage toggle, attack and target, talismans, options) with game icons, live validation and a weapon stat preview, runs the optimizer with live progress and renders the builds (equipment with decorations, skills with sources, stats under the requested and under all conditions). The web UI organises configurations by **profile** (an account) under `inputs/profiles/<profile>/`:

* `talismans.json` – the account's random talismans, shared by every weapon of the profile.
* `profile.json` – a condition preset per weapon type (`condition_presets`, keyed by weapon kind such as `great-sword`). A weapon's conditions follow its type's preset value by value; values that differ from the preset are that weapon's overrides and stay when the preset changes.
* `weapons/<name>.json` – one request per weapon (with `talismans.file` = `../talismans.json`, so the CLI runs it as is), plus `<name>.builds.json` (hand-entered builds; `"picked": true` marks the one the inventory compares; `"conditions": { "burst_active": true }`, `"resonance"` and `"target"` score a build under its own conditions where they differ from the weapon's) and `<name>.results.txt` / `.results.json` (the last run, with a fingerprint of its inputs so the inventory can flag it as stale).

The My builds tab scores hand-entered builds like the optimizer's; a build can carry its own conditions (toggles for the skills, set bonuses and group skills it has, the Omega Resonance phase and a training-dummy target) and shows the attack, affinity and element they give next to every active modifier, so the game's status screen and dummy hits can be checked against the model without changing what the optimizer searches for. The Inventory tab lists a profile's weapons by type with the picked build (scored under the weapon's conditions and today's talismans) and the best build of the last run, re-runs stale weapons in the background and compares two weapons side by side. The CLI editor lists profile weapons next to the stand-alone `inputs/<name>.json` requests.

```bash
dotnet run --project src/MHWildsOptimizer.Web
```

then open http://localhost:5214. Every build runs `npm run build` in `src/MHWildsOptimizer.Web/client` when a client source changed since the last one (`npm install` first if needed; needs node 20+), so the server always serves the current UI; pass `-p:BuildClient=true` to force a rebuild or `-p:BuildClient=false` to skip it. For client development run `npm run dev` in that folder and open http://localhost:5173 (hot reload, proxies `/api` to the .NET server on :5214). `--data <dir>` / `--inputs <dir>` override the dataset and configuration directories.

API: `GET /api/catalog`, `GET|PUT|DELETE /api/configs/{name}`, `GET /api/configs/{name}/results`, `POST /api/resolve`, `POST /api/optimize` (server-sent events: `validation`, `progress`…, `result`).

Icons come from monsterhunterwiki.org (Capcom's Wilds UI icons; `python tools/fetch_icons.py` refreshes them, see `client/public/icons/manifest.json`). Skill icon categories and decoration colors are the ones wilds.mhdb.io reports (`icon` in `skills.json`, `icon_color` in `decorations.json`).

## Browser app

`src/MHWildsOptimizer.Browser` is the web UI as a static site: no server code, everything runs in the visitor's browser.

* **Optimizer**: CP-SAT only, solved by [or-tools-wasm](https://github.com/Axelwickm/or-tools-wasm) on WebAssembly threads at about native speed (`solver/` is bundled into `wwwroot/solver` by npm on build). The beam search does not fit WebAssembly's memory. `options.max_threads` 0 uses every core the browser reports; a solve uses up to 4 CP-SAT workers and spare cores solve skill pair classes side by side. Measurements are in `spikes/WasmSpeedTest/README.md`.
* **Profiles** live in the browser's local storage (about 5 MB per site; results are stored gzipped). The profile menu next to the profile picker exports a profile as one `.mhwo-profile.json` file and imports such a file or the files of a server profile folder (`inputs/profiles/<profile>/profile.json`, `talismans.json`, `weapons/*.json` with `.builds.json` / `.results.json`).
* **Hosting** needs only the published files, served with two headers that make the page cross-origin isolated (WebAssembly threads need it): `Cross-Origin-Opener-Policy: same-origin` and `Cross-Origin-Embedder-Policy: require-corp`, over HTTPS (or localhost). Hosts that cannot send headers still work: `wwwroot/coi-sw.js`, a service worker, adds them and reloads the page once.
* First visit: about 4.5 MB compressed (the .NET runtime and app, the CP-SAT WebAssembly module, the dataset); the browser caches it.

```bash
dotnet run --project src/MHWildsOptimizer.Browser          # development, http://localhost:5240
dotnet publish src/MHWildsOptimizer.Browser -c Release -o publish/browser
```

In Rider, the shared run configuration **Browser app** (`.run/`) runs the development server.

The site is `publish/browser/wwwroot` (needs node 20+ for the solver bundle, like the server's client). It runs at <https://mh-optimizer.luca-diegel.de>, published by CI on every push to `main` and served by Caddy ([`DEPLOY.md`](DEPLOY.md), [`deploy/Caddyfile`](deploy/Caddyfile)). With nginx:

```nginx
server {
    listen 443 ssl;
    server_name optimizer.example.com;
    root /var/www/mhwilds-optimizer;   # the contents of publish/browser/wwwroot

    # CP-SAT runs on WebAssembly threads, which need a cross-origin isolated page
    add_header Cross-Origin-Opener-Policy "same-origin" always;
    add_header Cross-Origin-Embedder-Policy "require-corp" always;

    include mime.types;                # has application/wasm
    gzip_static on;                    # the publish output has a .gz (and .br) next to every file

    location / {
        try_files $uri $uri/ /index.html;   # page URLs such as /Luca/weapon/Current/results
    }
}
```

To serve it under a path such as `/optimizer/`, change `<base href="/">` in the published `index.html` to `<base href="/optimizer/">`.

```bash
dotnet build
dotnet test
```
