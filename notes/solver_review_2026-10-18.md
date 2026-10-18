# Solver review: the bigger picture (2026-10-18)

Luca's brief: the recent algorithm changes did not make runs faster, builds 2-N come back unproved and sometimes far
below build 1, and micromanaging the search is not helping. Wanted: the top 3-10 builds, good, within one or two
minutes, on all the machine's cores. This note records what the optimizer actually considers, how its speed moved over
the last week, what an exact engine of our own would need, and what was changed on `feature/solver-redesign`.

All timings: the PC (Ryzen 7 5800X3D, 8 cores / 16 threads), 8 threads, the game closed, scenarios from `bench/`.

## 1. What the search considers (the everyday GS run, `gs-fixed`)

| what | count | note |
|---|---|---|
| armor candidates per kind | Waist 19, Chest 21, Arms 23, Head 26, Legs 26 | after dominance pruning on the relevant skills |
| talismans | 13 | |
| decorations | 26 kinds | relevant single and combo jewels, levels I-III |
| skill groups in the score model | 16 units | up to a few hundred states each |
| parts of the score | 3 sides (Azure Bolt on/off x attack segments) | `gs-fixed-uptimes`: 12 sides (Latent Power x Azure Bolt x segments) |
| armor x talisman combinations | 6.2 M x 13 = 80 M | before decorations |
| distinct skill states after all five kinds | 15.4 M | dominance-pruned, as the beam engine enumerates them |

The score depends only on the final skill levels, set tiers and group skills (plus decorations that raise levels); it is
monotone in every level (`bench probe`). One evaluation of the score model costs 1.5 us (3 sides) to ~6 us (12 sides).

Across the sides of a model, **only affinity differs**: raw multipliers, flat raw, crit multiplier, element, procs and
the attack constants are identical in every side of `gs-fixed`, `gs-fixed-uptimes` and `bow-tech-stamina`
(`bench sides`). The CP model nevertheless built a full product chain (raw x crit factor, element x crit element) per side.

## 2. Speed over the last week (same scenarios, CLIs built at each commit)

| commit | date | gs-fixed | gs-fixed-uptimes | gs-pairs | bow-tech-stamina |
|---|---|---|---|---|---|
| 8cd9ad5 one solve per build, 4 workers | 10-12 | 17.3 s, 5 proved | 88.3 s | 32.5 s | (tech tree weapons not supported yet) |
| 6084b9a tightening + 10 s rank limit | 10-12 | 16.8 s | 73.1 s, 3 ranked | 33.5 s | - |
| 2e2fc1e before the split work | 10-15 | 17.3 s | 44.8 s, 3 ranked | 43.0 s | 261.9 s, 1 proved |
| 39a4301 proofs split into parts | 10-17 | 25.7 s | 69.4 s, 4 ranked | 49.1 s | 141.6 s, 1 proved |
| f5d6908 top N split into parts | 10-17 | 27.0 s | 48.9 s, 4 ranked | 43.6 s | 92.8 s, 5 proved |
| 7693883 main today | 10-18 | 29.5 s | 48.9 s, 4 ranked | 42.2 s | 67.4 s, 5 proved |

Single runs, so +-30 %. The picture is clear anyway: the split work fixed the pathological bow case (262 s -> 67 s, all
proved) but made the everyday run 70 % slower (every build 2-5 is a tie of build 2 proved again in 18 parts, 6 s each
instead of 3 s single solves), and the 12-part uptime model never gets builds 2-5 proved within their 10 s rank limit
while 100 s of the budget stay unused.

## 3. Could an engine of our own beat CP-SAT? (measured, not built)

The idea: enumerate talisman + pieces kind by kind, merge equal skill states, and cut every partial build whose upper
bound is below the Nth best build known (branch and bound), then fill decorations exactly for the survivors. The
decisive question is the bound. `bench bb` on `gs-fixed` with the bar at the 5th best build (672.2):

- Bound = score with the remaining kinds at their best per skill, decorations as the sum of the best single-jewel gains
  at that point (the beam search's bound, which is **not rigorous**: jewels on raw and crit multiply, so two gains add
  up to more than their sum): 13 M states bounded at the last kind, **3,251 survive** (107 s on 6 threads; the bound
  only bites at the last kind: 966 k of 2.8 M survive one kind earlier).
- A rigorous bound (every channel's change times the most the other factors can be): **5.2 M of 13 M survive**, the
  bound of the best build at the start is 897 vs a true 673.7. Far too loose.
- The greedy decoration fill (what the beam engine does) tops out at 671.8 on this run; the true optimum is 673.7, so
  survivors need an exact fill (slot by slot over distinct level vectors, cheap for thousands of states, not millions).

So an exact engine needs a bound that is both rigorous and nearly as tight as the beam's: per side, the max over
splits of the free slots between raw, crit and element jewels of (best raw from j slots) x (best crit factor from the
rest), with the per-group maxima as small knapsacks memoized by the group's levels. Rough cost estimate on gs-fixed:
~5-10 s on 16 threads; 12-side models ~4x that. It scales with cores and never leaves anything unproved, but it is one
to two days of careful work, and in the browser it needs .NET AOT plus WebAssembly threads (experimental) to not be 10x
slower than native. Not started. The two changes below are cheaper and work in the browser as is.

## 4. Done on `feature/solver-redesign`

### 4a. One damage chain per group of sides (model shrink, exact)

Sides that share every damage factor but affinity are scored as one group: one raw chain, one element chain, each
side's own clamped affinity and crit factor, and EFR = TR x sum(weight x raw factor x crit factor) as a single product.
The 12-part uptime model goes from 12 raw x crit products and 12 element products to one of each; the 3-part everyday
model from 3 to 1. Weights, sharpness factors, no-crit shares and shockwave shares fold into the sums. Falls back to one
group per side when sides differ elsewhere. `CpSatBruteForceTests` (exhaustive enumeration) and the other CP-SAT tests
pass unchanged.

A/B against the same code without it (8 threads, two rounds back to back, the solver-side tie search on in both):

| scenario | before | grouped |
|---|---|---|
| gs-fixed | 19.7 s / 9.6 s, 5 proved | 9.9 s / 10.1 s, 5 proved |
| gs-fixed-uptimes | 47.8 s / 49.5 s, 1 proved 4 ranked | 30.3 s / 34.5 s, **5 proved** |
| bow-tech-stamina | 65.7 s / 66.5 s, 5 proved | 47.8 s / 48.9 s, 5 proved |

The whole suite, main today vs 4a + 4b (one run each, `bench/results/2026-10-18_*.json`, `bench compare`: the same
builds in every scenario, every build proved):

| scenario | main | this branch | speed |
|---|---|---|---|
| gs-fixed | 13.0 s | 6.5 s | 2.0x |
| gs-fixed-uptimes | 84.5 s (this run took the split path) | 28.2 s | 3.0x |
| gs-fixed-allcharms | 23.0 s | 21.6 s | 1.1x |
| gs-heavy-targets | 2.1 s | 1.9 s | 1.1x |
| gs-pairs | 35.9 s | 22.6 s | 1.6x |
| gs-everything | 79.2 s | 59.7 s | 1.3x |
| ls-everything | 33.8 s | 17.7 s | 1.9x |
| bow-everything | 68.8 s | 29.7 s | 2.3x |
| bow-tech-stamina | 66.9 s | 53.7 s | 1.3x |

### 4b. Ties found without a solve

After a proved build, every single swap of one armor piece or the talisman for another candidate that keeps the same
capped skill levels, set tiers and group skills and still fits the decorations scores exactly the same in the model: it
is the next best build, proved by the same proof, and costs nothing (`CpSatSearch.Ties`, breadth first from the build,
`MHW_NATIVE_TIES=0` turns it off). In the split top-N search the ties of build 1 are known before the parts look for
the rest, so the parts start above them. The solver-side tie search from `feature/solver-bench` (a 1.5 s solve per tie,
`MHW_TIES=1`) is off by default now. The everyday run's builds 2-5 (three ties of build 2) used to cost 4-6 s each.

Ties are rarely one swap away: the same skills move between kinds (two pieces change, or the talisman and two pieces)
and the jewels move with them, so the finder tries one and two swaps and refills the decorations to exactly the same
capped levels (`FillTo`, a small search; the same jewels when they still fit). In the browser this enumeration (about
9,000 combinations per proved build) runs on the page thread; natively it is ~20 ms, interpreted maybe half a second.

## 5. Where this leaves the goals

- **One or two minutes, top 5 good:** with 4a + 4b every bench scenario finishes well inside two minutes on 8 threads
  with all five builds proved; see the table in 4a. The 10 s rank limit for builds 2-N stays as it is: with the smaller
  model the proofs fit in it on these runs. If a run still shows "ranked", raising `cp_sat_rank_time_limit_seconds`
  is the knob; a pooled budget for builds 2-N (each next build gets what the earlier ones left) is a small follow-up.
- **More cores:** the parts of a split search already use every lane. 16 threads = 4 lanes; the native B&B engine
  would be the way to scale linearly, and section 3 says what it needs.
- **Big gaps below build 1:** these were ranked builds found in 10 s; proved builds 2-5 are the true next best.
- **Not done:** the exact native engine (section 3), and a pooled time budget for builds 2-N.
