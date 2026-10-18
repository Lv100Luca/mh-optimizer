# Handoff: solver rethink (2026-10-18)

Luca wants the optimizer to be fast **and** exact (accuracy matters more than speed); it runs on capable PCs, any amount of
threads is fine. Small tweaks kept making things worse, so this branch first measures, then tries bigger changes.

## Where things are

- Branch `feature/solver-bench` = origin/main (2bc8b9f, includes the split top-N search) + 3 commits:
  the bench suite (`bench/`, CLI `bench` / `bench compare` / `bench probe`), a compare fix, and a tie search (5d6c1c2).
- `archive/topn-ties-bar-prototype` (local only on the PC): the first try, a rising-bar top-N search, built on the older main.
  Dropped: split-topn on main does the same with a cutoff in parts on every lane.
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
  within 8 s, optimize mode), look 1.5 s for a build that ties the last proved one. All tests pass; not benchmarked cleanly yet.

## Next steps

1. Clean baseline of this branch with `MHW_TIES=0`, and the same with ties on: A/B, 2 rounds.
2. Then the bigger ideas: (B) exact search over skill pair classes x weapon variants instead of coordinate-descent weapon
   tuning; (C) a tighter bound for the damage model, or a custom engine (in the browser that needs AOT + web workers).
3. After any change to the search: `dotnet test --filter FullyQualifiedName~CpSat`.
