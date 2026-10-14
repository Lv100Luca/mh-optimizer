# Sharpness over time: what Long Sword moves cost, and what the sharpness skills are worth (2026-10-14)

Investigation for the question "when does Handicraft / Razor Sharp / Master's Touch / Protective Polish out-damage an attack
skill in the same slot, given my attack sequence?". Long Sword only for now; the data exists for every weapon.
Prototype: `tools/sharpness_sim.py` (no dependencies), charts in `notes/sharpness/`.

## TL;DR

* **Per-hit sharpness costs are datamined and already in the repo.** The 1.040 motion value sheet's *Sharpness* column
  (`notes/motion_values.md`) is the cost per hit; kuroyonhon.com (independent Japanese datamine site) lists the same values
  move by move. Long Sword: a normal input costs **1 whatever its hit count** (Crimson Slash I/II: 2 hits x 0.5, Crimson III and
  Spirit Blade III: 3 x 0.3, Foresight Whirl 2 x 0.5, Iai Slash 2 x 0.5, Iai Spirit Slash counter 1 + 0). Exceptions: Spirit
  Helm Breaker **0** for all 7 hits (sheet; kuroyonhon says hits 2-7), Spirit Release Slash 2 x 0.3 then 0 for the other 12
  hits, the Unbound Thrust focus strike 5, mount attacks 0.1.
* **Fractions accumulate.** The game keeps sharpness in tenths of a hit (`EquipDataSetting._SharpnessRatio` = 10; the MH
  encyclopedia wiki: "計算は0.1刻み"; a Lance test saw the gauge drop exactly when the summed consumption crossed the unit).
  So the Crimson loop (Crimson I + Rising Slash, 1.9 s) burns **2 hits per loop = 1.05 sharpness per second**.
* **The bar.** Hits per color come from the API (`data/gogma_weapons_base.json`). Headsman's Hamus, attack focus:
  red 140, orange 40, yellow 40, green 80, blue 30, **white 20** (350 total; the affinity focus has white 10). The gauge never
  exceeds 400 hits; every Gogma weapon has Handicraft breakpoints `[5]`, i.e. all five levels (+10 hits each) go to white.
  A **sharpness reinforcement does not lengthen the bar, it shifts it**: +30 (I) / +50 (EX) hits leave red and join white
  (MH encyclopedia wiki, Artian weapons). So Handicraft keeps its full +50 on top of rolled weapons: one roll + Handicraft 5 =
  white 100, EX + Handicraft 5 = white 120, two rolls + Handicraft 5 = white 130.
* **Skills.** Razor Sharp: 10 / 25 / 50 % chance a hit costs nothing. Master's Touch (one level): 80 % chance on a
  critical hit, so per hit `affinity x 0.8`. Protective Polish: no loss for 30 / 60 / 90 s after sharpening. Speed Sharpening:
  a whetstone takes 3 grinds (4 on the Seikret), minus 1 / 2. All four are **weapon** skills in Wilds: Handicraft Jewel [1] / II [2]
  / III [3] (Lv 1 / 2 / 3), Razor Sharp Jewel [1] / II [2] / III [3], Mastery Jewel [3], Sharp Jewel [1] / II [2] / III [3]
  (Protective Polish), plus many 3-slot composites (Rzr Sharp/Handicraft, element/Handicraft, Rzr Sharp/element, ...).
* **With a 20-white bar every sharpness skill is worth 5-15 %** of your damage, because you would otherwise sharpen every
  19 s of Crimson looping. **With one sharpness roll (white 50)** the steady-state values at 80 % affinity are: Handicraft 1 / 3 / 5
  = +1.3 / +2.7 / +3.5 %, Razor Sharp 3 = +3.5 %, Master's Touch = +4.6 % (+5.8 % at 100 % affinity), Protective Polish 3 =
  +4.7 %, Razor Sharp 3 + Master's Touch = +6.1 %. With an EX roll (white 70) about two thirds of that. These assume you sharpen
  the moment white runs out (3.5 s each time); the policy is the biggest open knob (see below).
* For comparison, in the same 3-slot: Critical Boost 3 is about +6 % at 80 % affinity, Attack Boost 3 about +2.5-3 %; in a
  1-slot Attack Boost 1 is about +1 % and Handicraft 1 / Protective Polish 1 are +1.3 / +2.7 % on the white-50 bar.

## 1. Sources

| What | Source | Confidence |
|---|---|---|
| Per-hit cost per Long Sword move (and every other weapon) | Motion value sheet for 1.040 (datamine 1.02.0), tab "Long Sword", column *Sharpness*: https://docs.google.com/spreadsheets/d/1F_bOwQT6A6mFtCN7WCetnZDrmvLYu-ne4y97IsG6Fkk (CSV: `.../gviz/tq?tqx=out:csv&gid=646073038`); thread https://gamefaqs.gamespot.com/boards/439901-monster-hunter-wilds/81028359 ("Victory Strike Resolve now consumes 1/3 sharpness, to account for the fact it's a multihit") | high |
| Same values, independent | https://kuroyonhon.com/mhwilds/memo/4.php (太刀, notes "斬れ味の消費量は0.5" etc.) | high |
| Fractions in 0.1 steps, accumulated | MH encyclopedia wiki システム/斬れ味 (https://wikiwiki.jp/nenaiko/システム/斬れ味): "1ヒット辺り0.1～0.5で斬れ味消費", "小数点0.1刻みで斬れ味減少が計算される"; `data/datamine/EquipDataSetting.json` `_SharpnessRatio: 10.0`; Lance test https://techunt.site/lance-sharpness-exception/ (gauge dropped at 4.6 → 4.9 → 5.2 accumulated) | high |
| Gauge = hits per color, max 400, Handicraft breakpoints | https://docs.wilds.mhdb.io/ (weapon `sharpness`, `handicraft`); API check of all 33 Gogma melee variants: 350 hits, `handicraft [5]` | high |
| Sharpness reinforcement shifts 30 from red to the top color; Insect Glaive +20 (→ +30 on Gogma); affinity focus −10 sharpness | MH encyclopedia wiki 武器/アーティア武器 footnotes ("赤ゲージが30削られ、右端の色が30増える"); EX = 50 from `data/datamine/ArtianBonusData.json` `_SharpnessValue` | medium (one source for the shift; matches the API bars of the focus variants) |
| Skill numbers | `data/skill_effects.json` (SkillData datamine, in-game text); Master's Touch breakdown on the encyclopedia wiki スキル/達人芸 (expected gauge = 1 / (1 − crit x 0.8); it also says Master's Touch does not cover Gunlance shells or guard chip) | high |
| Whetstone | `data/datamine/PlayerItemParam.json`: `_Toishi_SharpenCount 3`, `_Toishi_SharpenCountRide 4`, `_Toishi_Pre_HealValue [200, 300, 600]`, `_Toishi_HealValue 1500` (tenths: 20 / 30 / 60 hits per grind, 150 on the last; a full sharpen restores 200-260 hits, i.e. the whole used part in practice), Whetfish fin −1 grind, upper fin −2 | high (amounts) |
| Whetstone duration | not found anywhere; the prototype uses 3.5 s for 3 grinds. **Measure it** (and with Speed Sharpening 2) | low |
| Sharpness damage multipliers | `data/damage_model.json` (raw 1.32 white / 1.20 blue / 1.05 green; element 1.15 / 1.0625 / 1.00) | high |

## 2. Long Sword costs per input

From the sheet (1.040) with kuroyonhon's notes; "per use" is what one input costs when every hit lands.

| Input | Hits | Per hit | Per use |
|---|---|---|---|
| Overhead Slash I / II, Crescent Slash, Thrust, Rising Slash, Fade Slash, Spirit Step Slash, Jumping Slash | 1 | 1 | 1 |
| Crimson Slash I | 2 | 0.5 | 1 |
| Crimson Slash II | 2 | 0.5 | 1 |
| Crimson Slash III | 3 | 0.3 | 0.9 |
| Spirit Blade I, Spirit Blade II (also without gauge) | 1 | 1 | 1 |
| Directional Spirit Blade II, Jumping Spirit Blade II | 2 | 0.5 | 1 |
| Spirit Blade III, Jumping Spirit Blade III | 3 | 0.3 | 0.9 |
| Spirit Roundslash (any), Spinning Crimson Slash | 1 | 1 | 1 |
| Foresight Slash (hit or countered) | 1 | 1 | 1 |
| Foresight Whirl Slash | 2 | 0.5 | 1 |
| Iai Slash | 2 | 0.5 | 1 |
| Iai Spirit Slash (countered) | 2 | 1, 0 | 1 |
| Spirit Thrust | 1 | 1 | 1 |
| Spirit Helm Breaker | 7 | 0 (kuroyonhon: hits 2-7 are 0, so maybe 1 on the first) | 0 (or 1) |
| Spirit Release Slash | 14 | 0.3 x 2, then 0 | 0.6 |
| Focus Strike: Unbound Thrust | 6 | 1 x 5, last 0 | 5 |
| Sneak Attack, Seikret Attack I | 2 | 0.5 | 1 |
| Mount attacks and finishers | 1 | 0.1 | 0.1 |

Per sequence of the app (timings are the rough `Seconds` of `Attacks.cs`):

| Sequence | Sharpness per cycle | Seconds | Sharpness / s | Hits / s |
|---|---|---|---|---|
| Crimson Slash I into Rising Slash | 2 | 1.9 | 1.05 | 1.6 |
| Crimson Slash I into Spirit Blade I | 2 | 2.0 | 1.00 | 1.5 |
| Iai counter, then 4 Crimson loops (app preset) | 9 | 9.6 | 0.94 | 1.5 |
| Spirit Blade I-III, red Roundslash, Spinning Crimson | 4.9 | 6.0 | 0.82 | 1.2 |
| Iai counter, 3 loops, Foresight, 3 loops, Helm Breaker + Release | 16.6 | 22.9 | 0.72 | 2.0 |

So on a fresh Headsman's Hamus (white 20, no skills) white lasts **19 s** of Crimson looping; 47 s with one roll (white 50),
66 s with an EX roll (white 70); Handicraft 5 adds 48 s on any of them.

## 3. Mechanics the model uses

* State: sharpness `s` in hits (0.1 steps), full bar `F` = sum of the colors. Color = the segment `s` is in (at exactly the
  white/blue boundary the gauge is blue). Thresholds from the bar: white above 330 on Hamus (above 300 with one roll).
* Each landed hit costs its `cost` unless protected. Protection per hit (Razor Sharp and Master's Touch assumed to roll
  independently): `P(loss) = (1 − rs) x (1 − crit x 0.8 x mt)`; Protective Polish makes it 0 for 30 / 60 / 90 s after the
  last grind. The expected-value model subtracts `cost x P(loss)` per hit; the random mode rolls it. Both give the same mean
  (checked: median time to leave white 38 s vs 38 s for Razor Sharp 3, 53 vs 53 s for Master's Touch at 80 %); the spread is
  p10-p90 = 32-46 s (Razor Sharp 3), 44-65 s (Master's Touch), 84-131 s (both).
* Bar = base bar, reinforcement hits moved from red to the top color, Handicraft x 10 added to the top color, capped at 400.
* Damage per hit = MV x spirit multiplier x sharpness multiplier of the current color (raw; `--element-share` mixes in the
  element multipliers). The *damage factor* of a configuration is its damage over a 30-minute hunt divided by the damage of
  always-white-never-sharpen, so it already contains the sharpening downtime.
* Sharpening policy: sharpen at the end of the attack in which the gauge dropped below the chosen color (default white),
  `3.5 s x grinds / 3`, restores the full bar, restarts Protective Polish. The hunt starts freshly sharpened.
* Not modelled: deflects (extra loss), Bladescale Honing (40 hits of special sharpness at raw x1.40 after a perfect dodge),
  Seikret sharpening, misses (every hit lands), the monster leaving.

## 4. What the prototype says (Headsman's Hamus, attack focus, Crimson loop, 80 % affinity, sharpen as soon as white is gone)

Damage factor and value against "no sharpness skill"; crossover = seconds of hunt after which the skill build stays ahead of a
build whose slot gives +g % damage instead.

| Skills | white 20 (no roll) | white 50 (one roll) | white 70 (EX roll) | white 80 (two rolls) |
|---|---|---|---|---|
| no sharpness skill | 0.844 | 0.932 | 0.951 | 0.957 |
| Handicraft 1 [1] | +5.5 % | +1.3 % | +0.6 % | +0.4 % |
| Handicraft 2 [2] | +8.5 % | +2.1 % | +1.0 % | +0.8 % |
| Handicraft 3 [3] | +10.4 % | +2.7 % | +1.4 % | +1.2 % |
| Handicraft 5 [3]+[2] | +12.7 % | +3.5 % | +2.0 % | +1.6 % |
| Razor Sharp 1 [1] | +2.4 % | +0.8 % | +0.4 % | +0.4 % |
| Razor Sharp 2 [2] | +4.7 % | +1.8 % | +1.2 % | +1.0 % |
| Razor Sharp 3 [3] | +8.5 % | +3.5 % | +2.5 % | +2.2 % |
| Master's Touch [3] at 80 % | +11.3 % | +4.6 % | +3.2 % | +2.8 % |
| Master's Touch at 100 % | | +5.8 % | | |
| Protective Polish 1 / 2 / 3 [1]/[2]/[3] | +10.5 / +13.5 / +14.9 % | +2.7 / +3.9 / +4.7 % | +1.6 / +2.4 / +2.8 % | +1.2 / +2.0 / +2.4 % |
| Speed Sharpening 2 [2] | +11.6 % | +4.7 % | +3.3 % | +2.9 % |
| Razor Sharp 3 + Handicraft 1 (one [3] composite) | +11.7 % | +4.2 % | +2.9 % | +2.4 % |
| Handicraft 3 + Master's Touch | +15.4 % | +5.6 % | +3.9 % | +3.3 % |
| Razor Sharp 3 + Master's Touch | +14.7 % | +6.1 % | +4.3 % | +3.6 % |

Crossover on the white-50 bar (one roll): Master's Touch is ahead of a +1 % / +2 % / +3 % slot after 48 / 48 / 150 s and
never catches a +5 % slot; Handicraft 5 after 100 / 201 / 507 s; Razor Sharp 3 the same as Handicraft 5; Protective Polish 3
after 48 / 48 / 150 s. On the white-20 bar everything up to +5 % is beaten from the first drop (20 s). Per sequence the
values hardly move (the Iai preset: Master's Touch +4.2 %, Handicraft 5 +3.1 %; the long rotation with Helm Breaker:
+3.2 % / +2.6 %) because all of them consume about 1 sharpness per second.

Policy sensitivity: playing through blue and sharpening only when it turns green (`--sharpen-below blue`) lowers every
number by about a quarter (one roll: Master's Touch +2.8 %, Handicraft 5 +3.0 %, Protective Polish 3 +4.3 %), and Speed
Sharpening's value shows that the sharpening *time* is most of what the skills buy under the "sharpen at once" policy. A
realistic policy sharpens at the next opening (monster downed, repositioning), which costs no damage time but means
fighting on blue until then: the truth is between the two tables. This is the one input the model needs from the player.

Charts (`tools/sharpness_sim.py --out notes/sharpness`): `notes/sharpness/sharpness_crimson_bonus30.svg` (gauge over
time, six configurations) and `notes/sharpness/ratio_crimson_bonus30.svg` (cumulative damage ratio against no skill over the
first 15 minutes, with +3 % and +6 % reference lines); the `bonus0` pair is the unrolled bar.

## 5. The model for the optimizer

1. **Data.** `AttackHit` gets a `Sharpness` cost (default 1) filled from the sheet for the GS / LS / SA attacks of the
   picker (bow: none). `GogmaWeaponStats` already carries the bar and the reinforcement hits (`SharpnessBonus`, unused by the
   damage model so far); apply the shift (red → top color) and Handicraft (`[5]` for all Gogma weapons, cap 400).
2. **`Damage/SharpnessModel`** with a closed form instead of a simulation: for a sequence with cost `C` and duration `T`
   per cycle under `P(loss)`, white lasts `W x T / (C x P(loss))` seconds (plus the Polish window), then `t_sharpen` (or a
   blue phase of `B x T / (C x P(loss))` seconds at x1.20 when the policy plays through blue). Time shares per color give the
   raw factor `sum(share x raw multiplier)`, the element factor and the uptime `1 − sharpening share`. Inputs: bar, Handicraft,
   Razor Sharp, Master's Touch, Protective Polish, Speed Sharpening, the build's affinity, the attack profile's hits, and a
   `conditions.sharpness` block (sharpen below which color, whetstone seconds, start sharpened).
3. **`DamageCalculator`** replaces the constant `TopSharpness` multipliers with the model's time-weighted factors (per
   health side / segment, like the Spirit Gauge today); a build's own conditions can still pin a color for status-screen checks.
4. **`ScoreDecomposition` / CP-SAT.** Handicraft (0-5), Razor Sharp (0-3), Protective Polish (0-3) and Speed Sharpening
   (0-2) interact (the factor is not a product of per-skill factors): tabulate them jointly as one unit (6 x 4 x 4 x 3 = 288
   states, well under `MaxUnitStates`). Master's Touch depends on the final affinity: the solver already looks the crit factor
   up from the clamped affinity sum (`CpSatSearch`, the `cf` table keyed on affinity); add Master's Touch as a second table on
   the same affinity variable (factor with and without it, chosen by the feature). `ScoreDecomposition.Verify` checks the
   composition against the calculator as usual.
5. **UI.** Conditions tab: a Sharpness card (policy, whetstone seconds, use Protective Polish). Results / Compare: time in
   white, sharpens per 10 min, the time-weighted sharpness factor next to EFR, and the gauge-over-time chart per build (the
   prototype's SVG is trivial to port).
6. **Other weapons** come for free once the costs are on the hits (GS mostly 1, Plunging Thrust 0.2; SA, CB, ... from the sheet).

## 6. Hit-based view, no sharpening (Luca's framing, 2026-10-14)

Luca's rules for the comparison: leave sharpening and Protective Polish out, count hits instead of seconds, assume the red
Spirit Gauge is always up, and compare only the one-jewel options in a 3-slot: Handicraft Jwl III [3] (Handicraft 3), Rzr
Sharp Jwl III [3] (Razor Sharp 3) and Mastery Jewel [3] (Master's Touch), the last at 50 / 75 / 100 % affinity. This is the
simulator's default mode now: `python tools/sharpness_sim.py --bonus 30 --out notes/sharpness` (`--mode time` is section 4).

Model: a fresh bar, the loop lands hit after hit, each hit is dealt at the color the gauge is in and then costs its sharpness
(times `P(loss)` for the two chance skills), and nobody sharpens. The chart is the cumulative damage ratio against the no-jewel
build by hits landed, with the slot alternatives as reference lines: Attack Boost 3 = +7 flat = +2.8 % at 250 true raw,
Critical Boost 3 = (1 + 0.34 a) / (1 + 0.25 a) − 1 = +4.0 / +5.7 / +7.2 % at 50 / 75 / 100 % affinity. The horizon that
matters is the number of hits between two sharpens, i.e. one phase of the fight.

Charts: `notes/sharpness/hits_ratio_crimson_bonus0.svg` and `hits_ratio_crimson_bonus30.svg` (damage ratio by hits),
`hits_sharpness_crimson_bonus0.svg` and `hits_sharpness_crimson_bonus30.svg` (sharpness left by hits).

**No sharpness roll (white 20), Crimson loop (3 hits and 2 sharpness per loop):**

| One jewel | Hits at white | vs no jewel at 60 / 150 / 300 hits | Beats Attack Boost 3 after | Beats Critical Boost 3 at 50 / 75 / 100 % after |
|---|---|---|---|---|
| no jewel | 30 | | | |
| Handicraft 3 | 75 | +4.8 / +7.0 / +8.2 % | 43 hits | 52 / 262 / 284 hits |
| Razor Sharp 3 | 60 | +4.8 / +8.6 / +10.0 % | 43 hits | 76 / 94 / 265 hits |
| Master's Touch at 50 % | 51 | +3.3 / +5.9 / +8.5 % | 76 hits | 87 / 266 / 283 hits |
| Master's Touch at 75 % | 76 | +4.8 / +9.8 / +12.5 % | 43 hits | 52 / 73 / 91 hits |
| Master's Touch at 100 % | 150 | +4.8 / +14.9 / +20.6 % | 43 hits | 52 / 73 / 82 hits |

Who is ahead of whom (hit count after which the row stays ahead of the column): Handicraft 3 leads everyone for the first 50
white hits it adds; Razor Sharp 3 passes it at 133 hits and Master's Touch at 50 % only at 280; Master's Touch at 75 % passes
Razor Sharp 3 at 61 hits and Handicraft 3 at 76; at 100 % it passes Master's Touch at 75 % at 77 hits. Master's Touch at 50 %
never passes Razor Sharp 3.

**One sharpness roll (white 50), Crimson loop:**

| One jewel | Hits at white | vs no jewel at 60 / 150 / 300 hits | Beats Attack Boost 3 after | Beats Critical Boost 3 at 50 / 75 / 100 % after |
|---|---|---|---|---|
| no jewel | 75 | | | |
| Handicraft 3 | 120 | 0 / +5.4 / +4.2 % | 107 hits | 127 / never / never |
| Razor Sharp 3 | 150 | 0 / +7.3 / +8.8 % | 107 hits | 124 / 136 / 149 hits |
| Master's Touch at 50 % | 126 | 0 / +5.8 / +6.3 % | 107 hits | 124 / 149 / never |
| Master's Touch at 75 % | 189 | 0 / +7.3 / +12.9 % | 107 hits | 124 / 136 / 149 hits |
| Master's Touch at 100 % | 300 | 0 / +7.3 / +16.8 % | 107 hits | 124 / 136 / 149 hits |

Order: nothing differs for the first 75 hits (25 loops); Handicraft 3 leads from hit 76 to 121, then Razor Sharp 3 and
Master's Touch pass it; Master's Touch at 75 % passes Razor Sharp 3 at 151 hits, at 100 % it passes 75 % at 190. "Never"
means not within 300 hits; Handicraft's lead shrinks after everyone has left white because its 30 extra hits are a fixed
amount while the chance skills stretch blue and green too. With an EX roll (white 70) the same picture shifts right by about
45 hits (no jewel keeps white for 105 hits; every jewel beats Attack Boost 3 after 150). The Iai-counter preset (14 hits and 9
sharpness per cycle) gives the same numbers within a few hits.

Reading for a phase of the fight: on a rolled weapon a phase shorter than 25 Crimson loops never leaves white, so no sharpness
jewel does anything and Critical Boost 3 wins outright; past about 45 loops (136 hits) Razor Sharp 3 or Master's Touch at 75 %
or more beat Critical Boost 3 at 75 % affinity, and past 50 loops they beat it at 100 % affinity; Handicraft 3 only beats
Critical Boost 3 when the affinity is around 50 % or the bar has no roll. The chance skills have a spread: Razor Sharp 3's
white on the 20-hit bar ends between 32 and 46 s (p10-p90, time view), Master's Touch at 80 % between 44 and 65 s.

## 7. Open questions and how to check them in the training area

1. **Do Razor Sharp and Master's Touch roll independently?** Assumed (Rise did). Count Crimson loops from fresh white 50
   with Razor Sharp 3 + Master's Touch at a known affinity: expected 25 x 2 / P(loss) hits-worth.
2. **Count the Crimson loop.** Fresh white 20, no skills: the gauge should turn blue after exactly 10 loops (it blinks 5 hits
   before, i.e. after 7-8 loops). That confirms both the 0.5 per Crimson hit and the accumulation.
3. **Spirit Helm Breaker's first hit**: 0 (sheet) or 1 (kuroyonhon). 20 Helm Breakers on white 20 settle it.
4. **Whetstone seconds** with 0 / 1 / 2 grinds removed, and whether an interrupted sharpen starts Protective Polish.
5. **The reinforcement shift**: compare the equipment-screen bar of a rolled weapon with an unrolled one (red shorter, white
   longer, same length) and the Handicraft preview on top.
6. **Policy**: how long do you actually fight on blue before an opening? That decides whether the skills are worth the
   "sharpen at once" numbers or the "play through blue" numbers.
7. Not in the sheet: deflect cost, the Seikret's 4 grinds, Bladescale Honing's special gauge.
