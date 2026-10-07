# MHWildsOptimizer

Build optimizer / set maker for Monster Hunter Wilds (Ver 1.041), centred on Gogma Artian weapons that roll one set-bonus skill and one group skill and count as one armor piece for each.

* `notes/README.md` – research write-up: equipment system, Gogma mechanics, damage model, decisions, open questions, sources.
* `data/` – normalized dataset (armor, sets, skills, decorations, charms, Gogma weapon variants, roll pool, damage-model constants) plus the raw API dumps (`data/raw`) and game-data files (`data/datamine`).
* `tools/` – Python scripts that regenerate `data/*.json` from `data/raw` and scrape the wiki tables.
* `src/MHWildsOptimizer.Core` – domain model, damage calculator, optimizer (C# / .NET 10).
* `src/MHWildsOptimizer.Cli` – command-line front end.
* `src/MHWildsOptimizer.Web` – web front end: ASP.NET Core API + React client (`client/`), game icons under `client/public/icons` (fetched by `tools/fetch_icons.py`).
* `tests/MHWildsOptimizer.Tests` – xunit tests; `tests/MHWildsOptimizer.Web.Tests` – API integration tests.

* `inputs/` – optimizer inputs: `request.example.json` (weapon as the game shows it, skill-pair mode, target skills, conditions, talisman file, options) and `talismans.example.json` (your random talismans with skills and decoration slots).

Current scope: Great Sword and Long Sword. Hitzone 100, no monster resistances. Conditional skills are user-toggleable and only count when the loadout contains them. `conditions.skill_limits` removes skills from the optimization (`0`) or values them only up to a level (e.g. `{ "Burst": 1 }` for Great Sword). The weapon's stats are inputs and never optimized; its rolled set bonus / group skill is either fixed or searched over all 294 rollable pairs. Talismans come from the craftable charm lines (max rank) plus the random talismans you list.

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

The web UI covers the same setup as the editor (weapon, rolled pair, skill pair mode, targets including set bonuses and group skills by tier, skill limits, conditions with the proc damage toggle and attack profile, talismans, options) with game icons, live validation and a weapon stat preview, runs the optimizer with live progress and renders the builds (equipment with decorations, skills with sources, stats under the requested and under all conditions). Configurations are the same `inputs/<name>.json` + `inputs/<name>.talismans.json` files the CLI uses; a run also writes `inputs/<name>.results.txt` and `.results.json`.

```bash
dotnet run --project src/MHWildsOptimizer.Web
```

then open http://localhost:5214. The first `dotnet build` runs `npm install` + `npm run build` in `src/MHWildsOptimizer.Web/client` (needs node 20+); pass `-p:BuildClient=true` to rebuild the client or `-p:BuildClient=false` to skip it. For client development run `npm run dev` in that folder and open http://localhost:5173 (proxies `/api` to the .NET server). `--data <dir>` / `--inputs <dir>` override the dataset and configuration directories.

API: `GET /api/catalog`, `GET|PUT|DELETE /api/configs/{name}`, `GET /api/configs/{name}/results`, `POST /api/resolve`, `POST /api/optimize` (server-sent events: `validation`, `progress`…, `result`).

Icons come from monsterhunterwiki.org (Capcom's Wilds UI icons; `python tools/fetch_icons.py` refreshes them, see `client/public/icons/manifest.json`). Skill icon categories and decoration colors are the ones wilds.mhdb.io reports (`icon` in `skills.json`, `icon_color` in `decorations.json`).

```bash
dotnet build
dotnet test
```
