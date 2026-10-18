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
