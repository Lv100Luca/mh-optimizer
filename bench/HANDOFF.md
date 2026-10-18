# Handoff: solver rethink (2026-10-18)

Luca wants the optimizer to be fast **and** exact (accuracy matters more than speed); it runs on capable PCs, any amount of
threads is fine. Small tweaks kept making things worse, so this branch first measures, then tries bigger changes.

## Where things are

- Branch `feature/solver-bench` = origin/main (2bc8b9f, includes the split top-N search) + 3 commits:
  the bench suite (`bench/`, CLI `bench` / `bench compare` / `bench probe`), a compare fix, and a tie search (5d6c1c2).
- `archive/topn-ties-bar-prototype`: the first try, a rising-bar top-N search, built on the older main.
  Dropped: split-topn on main does the same with a cutoff in parts on every lane.
- 2026-10-18 later: `feature/solver-bench` now also holds origin/main (7693883) and `feature/score-model-speed` (merged, all
  tests pass). Unify further work here.
- `feature/score-model-speed` (097c9e9, merged into this branch): the damage model probes each uptime part once per set of timed skills;
  12-part GS model 5.4 s -> 0.9 s in the browser (natively the model takes ~0.2 s either way, so the bench barely sees it).
- `archive/split-topn-regions` (216d798): the "top N by regions" version of split-topn; main merged the other version
  ("two phases on every lane", 25dc8d2), so this one is kept for reference only.
- Nothing is merged into main.

## How to measure (see bench/README.md)

```bash
dotnet build src/MHWildsOptimizer.Cli -c Release -o bin/bench-cli
bin/bench-cli/MHWildsOptimizer.Cli.exe --data data bench --label <name>
bin/bench-cli/MHWildsOptimizer.Cli.exe --data data bench compare bench/results/<a>.json bench/results/<b>.json
```

- Runs use 8 threads. Make sure nothing else loads the CPU: the `baseline-origin-main.json` run is **invalid** (Monster Hunter
  Wilds was running, ~9.5 cores).
- Single runs vary 20-30 %; decide on A/B runs back to back on the same machine, 2-3 rounds.
- Results from another machine are not comparable with `baseline-main.json` (the PC): record a new baseline first.

## Findings so far

- Baseline on the old main (PC): build 1 is usually found and proved in 3-4 s; builds 2-N took 70-90 % of a fixed run, often
  ties (same skills, other armor) each proved again, or stayed unproved and out of order. Split-topn (now on main) proves them
  (better builds found at #3 on gs-fixed-uptimes and bow-tech-stamina) but is slower on some runs; to be measured cleanly.
- `bench probe`: the score is monotone in every skill level, 1.5 us per evaluation, but over 50 million level vectors score
  at least the best build, so a pure skill-space search with a feasibility check is not viable.
- Top N by a rising bar (prototype): 25-35 % faster on tie-heavy fixed runs, no gain where proofs are hard, a little slower in
  optimize mode. Asking "is there a build above X?" costs about as much as a proof: the bottleneck is proof hardness (the
  weak bound of the product chain in the CP model), not how the questions are ordered.
- Tie search (5d6c1c2, `MHW_TIES=0` turns it off): before proving the next build in the single-solve loop (first build proved
  within 8 s, optimize mode), look 1.5 s for a build that ties the last proved one. All tests pass.
- Laptop A/B (i5-13500H, 2 rounds off/on/on/off, `baseline-laptop-ties-off.json` / `-on.json`): ties lose no build and are
  faster on gs-fixed (30.5 -> 18.9 s), bow-everything (115 -> 99 s) and ls-everything (61 -> 55 s); the rest is within noise.
- 8 s cliff in `SplitTopAsync`: when the seed proves build 1 within `SplitAfterSeconds`, the narrowing is cancelled and the
  single-solve loop takes over, where builds 2+ get only the 10 s rank limit. gs-fixed-uptimes (build 1 at 7.2 s) then kept
  1 proved + 4 ranked and lost both 678.21 builds; at 8.3 s the same request proves all 5. Off-r1 vs off-r2 also swings
  0.6x on gs-fixed-allcharms and bow-tech-stamina for the same reason. This is most of the run-to-run noise, ties on or off.
- Cliff fixed (uncommitted): after an early build 1 the loop goes on one solve at a time; the first build 2+ it cannot
  prove hands the rest to the parts (narrowing included) within the shared budget (120 s + 10 s per further build). Forced with
  SplitAfterSeconds = 10 (`laptop-fix-split10-uptimes.json`): 5 of 5 proved, baseline scores, 130 s. Full suite x2
  (`laptop-fix-r1/r2.json`): no build lost, speed within noise of ties on. New brute-force test case covers the fallback.
- `bench compare` checks every candidate repeat against the baseline's first run (it only checked the first repeat).

## Next steps

1. Done: laptop baselines (ties off/on) and the 8 s cliff fix. Review and commit.
2. The bigger ideas: (B) exact search over skill pair classes x weapon variants instead of coordinate-descent weapon
   tuning; (C) a tighter bound for the damage model, or a custom engine (in the browser that needs AOT + web workers).
3. After any change to the search: `dotnet test --filter FullyQualifiedName~CpSat`.
