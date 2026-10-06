# MHWildsOptimizer

Build optimizer / set maker for Monster Hunter Wilds (Ver 1.041), centred on Gogma Artian weapons that roll one set-bonus skill and one group skill and count as one armor piece for each.

* `notes/README.md` – research write-up: equipment system, Gogma mechanics, damage model, decisions, open questions, sources.
* `data/` – normalized dataset (armor, sets, skills, decorations, charms, Gogma weapon variants, roll pool, damage-model constants) plus the raw API dumps (`data/raw`) and game-data files (`data/datamine`).
* `tools/` – Python scripts that regenerate `data/*.json` from `data/raw` and scrape the wiki tables.
* `src/MHWildsOptimizer.Core` – domain model, damage calculator, optimizer (C# / .NET 10).
* `src/MHWildsOptimizer.Cli` – command-line front end.
* `tests/MHWildsOptimizer.Tests` – xunit tests.

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

The search is a dynamic program over skill states (talisman, then one armor slot at a time): partial builds with the same relevant skill levels, slot counts and set/group counts are merged, dominated and target-infeasible states are dropped, and every surviving final state gets its decorations (exact cover of the targets, then greedy damage fill) and an EFR + EFE score. `options.max_states_per_depth` (default 100000) bounds the beam; raise it for a slower, more exhaustive run. In optimize mode the 294 rollable pairs collapse into score-equivalent classes that are searched in parallel.

```bash
dotnet build
dotnet test
```
