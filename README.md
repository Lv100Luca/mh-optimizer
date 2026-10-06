# MHWildsOptimizer

Build optimizer / set maker for Monster Hunter Wilds (Ver 1.041), centred on Gogma Artian weapons that roll one set-bonus skill and one group skill and count as one armor piece for each.

* `notes/README.md` – research write-up: equipment system, Gogma mechanics, damage model, decisions, open questions, sources.
* `data/` – normalized dataset (armor, sets, skills, decorations, charms, Gogma weapon variants, roll pool, damage-model constants) plus the raw API dumps (`data/raw`) and game-data files (`data/datamine`).
* `tools/` – Python scripts that regenerate `data/*.json` from `data/raw` and scrape the wiki tables.
* `src/MHWildsOptimizer.Core` – domain model, damage calculator, optimizer (C# / .NET 10).
* `src/MHWildsOptimizer.Cli` – command-line front end.
* `tests/MHWildsOptimizer.Tests` – xunit tests.

Current scope: Great Sword and Long Sword. Hitzone 100, no monster resistances. Conditional skills are user-toggleable and only count when the loadout contains them.

```bash
dotnet build
dotnet test
```
