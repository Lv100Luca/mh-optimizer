# Solver benchmarks

Fixed requests in `scenarios/` (built from real runs), run natively by the CLI on 8 threads whatever the file says.

```bash
dotnet build src/MHWildsOptimizer.Cli -c Release -o bin/bench-cli
bin/bench-cli/MHWildsOptimizer.Cli.exe --data data bench --label my-change          # all scenarios -> results/<date>_<label>.json
bin/bench-cli/MHWildsOptimizer.Cli.exe --data data bench gs-fixed gs-pairs --repeat 3
bin/bench-cli/MHWildsOptimizer.Cli.exe --data data bench compare results/baseline-main.json results/<new>.json
```

`compare` prints the median time per scenario and the first place where the score lists differ; it exits 1 when a
candidate scores worse anywhere (a lost build). Each result keeps the run's timed log.

`bench probe <request> <best score>` looks at the score model in skill space: units, monotonicity, cost of one
evaluation, and how many skill-level vectors score at least the best build.

`bench sides <request>` shows which damage factors of the score model are the same across its sides (parts of an uptime,
segments of an attack): what the CP model shares in one chain per group of sides.

`bench bb <request> <bar> [--threads n]` is the branch-and-bound experiment of notes/solver_review_2026-10-18.md: how
many skill states survive a rigorous bound against a bar, kind by kind, and the exact decoration fill of the survivors.

| scenario | what it stresses |
|---|---|
| gs-fixed | the everyday run: GS, fixed pair, Stun Resistance 3 + Focus 3, own talismans |
| gs-fixed-uptimes | Latent Power + Azure Bolt uptimes (12 parts of the hunt) |
| gs-heavy-targets | many skill targets: feasibility does the work |
| gs-pairs | every rollable skill pair |
| gs-everything | skill pairs + every weapon part |
| gs-fixed-allcharms | every possible talisman (roll table) |
| ls-everything, bow-everything | other weapons, optimize everything |
| bow-tech-stamina | tech tree bow, Constitution 5 + Stamina Surge 2 (hard proofs) |
| gs-main-48parts | Luca's Main GS: 6-step sequence x Latent Power x Azure Bolt x Offensive Guard = 48 parts, 120 s / 25 s limits (the hardest proof) |
