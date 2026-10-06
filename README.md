# MHWildsOptimizer

Build optimizer / set maker for Monster Hunter Wilds (Ver 1.041), centred on Gogma Artian weapons that roll one set-bonus skill and one group skill and count as one armor piece for each.

* `notes/README.md` – research write-up: equipment system, Gogma mechanics, damage model, decisions, open questions, sources.
* `data/` – normalized dataset (armor, sets, skills, decorations, charms, Gogma weapon variants, roll pool, damage-model constants) plus the raw API dumps (`data/raw`) and game-data files (`data/datamine`).
* `tools/` – Python scripts that regenerate `data/*.json` from `data/raw` and scrape the wiki tables.
* `src/MHWildsOptimizer.Core` – domain model, damage calculator, optimizer (C# / .NET 10).
* `src/MHWildsOptimizer.Cli` – command-line front end.
* `tests/MHWildsOptimizer.Tests` – xunit tests.

* `inputs/` – optimizer inputs: `request.example.json` (weapon as the game shows it, skill-pair mode, target skills, conditions, talisman file, options) and `talismans.example.json` (your random talismans with skills and decoration slots).

Current scope: Great Sword and Long Sword. Hitzone 100, no monster resistances. Conditional skills are user-toggleable and only count when the loadout contains them. The weapon's stats are inputs and never optimized; its rolled set bonus / group skill is either fixed or searched over all 294 rollable pairs. Talismans come from the craftable charm lines (max rank) plus the random talismans you list.

Interactive editor (Spectre.Console): pick an existing configuration under `inputs/` or start a new one, click together weapon, skill pair, targets, conditions, talismans and options, then save it as `inputs/<name>.json` plus `inputs/<name>.talismans.json`.

```bash
dotnet run --project src/MHWildsOptimizer.Cli -- edit
```

Validate a saved configuration and show the resolved inputs:

```bash
dotnet run --project src/MHWildsOptimizer.Cli -- request inputs/request.example.json
```

```bash
dotnet build
dotnet test
```
