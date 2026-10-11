# Bow: research and model (2026-10-11, game 1.040 / 1.041)

What the optimizer models for the Bow, where the numbers come from, and what is still open. Bowguns are out of scope.
Values marked **(dm)** come from dtlnor's game data dumps (`mhws-rcol-record/Player/Wp11.csv`,
`GlobalActionParam/individual_wp11.csv`, `MHWs-in-json` Wp11 shell params); none changed in 1.041. Community sources
are named where used.

## How the Bow is scored

Every arrow is a hit with its own MV and element modifier (MV sheet 1.040, Bow tab, gid 675791945; matches the dm).

```
raw per arrow  = true raw x MV/100 x coating x critical distance x shot skill x crit x shot hitzone/100
element/arrow  = true element x element modifier x element hitzone/100 x crit element
```

| Factor | Value | Source |
|---|---|---|
| Close-range coating | raw x1.4 (also range x0.6) | `CloseBottleAttackUpRate` 1.4 (dm), unchanged since launch |
| Power coating | raw x1.35 (x1.30 until TU4) | `StrongBottleAttackUpRate` (dm), TU4 notes, game8.jp test |
| Coating on element | none assumed | only "attack" fields exist; no test found |
| Critical distance | raw x1.1 inside the window, assumed always | shell params (dm): x0.8 too close (charged shots), x0.2 too far (x0.5 Thousand Dragons) |
| Pierce coating | own low-MV arrows, each hits n times at 1.2, 1.2, 0.9, 0.5, 0.3, 0.15, 0.1, 0.05 | `Wp11PenetrateBottle_MultiHitCurve` (dm); applied to element too (assumption) |
| Normal Shots | x1.05 on charged shots and Flying Swallow Shot | in-game text, kiranico 105 |
| Spread/Power Shots | x1.05 on Quick Shot, Power Shot, Power Volley | in-game text (Volley assumed included) |
| Piercing Shots | x1.05 on Dragon Piercer, Thousand Dragons; not on pierce-coated arrows | in-game text |
| Special Ammo Boost | x1.1 / x1.2 on Dragon Piercer, Thousand Dragons, Tracer | kiranico |
| Charge Master | element x1.15 / 1.20 / 1.25 on charged shots, Power Shot, Power Volley | kiranico; bow players treat it as a real +15 % (game8 EN measured far less, probably pre-TU1) |

Every bow has exactly one damage coating (all 87 bows in the API). Gogma Artian bows take it from the focus: attack
Close-range, affinity Power, element Pierce (API variants of Calamitous Angel). Coatings are unlimited: one load is 10
shots for 30 Trick Arrow gauge, refilled mostly by Discerning Dodges, so the model treats the coating as always on.

Pierce hits per arrow are an input (`attack_profile.pierce_hits`, default 3). The count depends on the monster's size, the
angle and even the frame rate (speedrunners cap FPS for more ticks). The one count found was about 1.9 per arrow on
Nerscylla (Steam, pre-TU1); arrows that hit a Tracer do not pierce. Lv3 arrow: 6 MV x 3.3 = 19.8 at 3 hits against
12 x 1.4 = 16.8 with Close-range, so Pierce only wins from about 3 hits on.

Bow defaults (`Conditions.DefaultFor("bow")`): stamina not full (charging and the Charging Sidestep drain it, Maximum Might
has about 0 % uptime while dash dancing), charged attacks on (Charge Master).

## Moves and combos

Charge levels as the game counts them internally (Lv1 = no glow ... Lv3 = full). Every normal shot raises the next
one's level; the Charging Sidestep keeps it; a Discerning Dodge sets Lv3 at once. Power Shot and Power Volley fire at the
level after the shot they follow (Power Shot 4 / 5 / 5 arrows, Volley 5 / 6 / 6).

| Move | Arrows x MV | Element | Shot skill |
|---|---|---|---|
| Charged Shot Lv1 / 2 / 3 | 1x10 / 2x11 / 3x12 | 0.4 / 0.7 / 1.0 | Normal |
| Quick Shot (no shot before) | 3x10 | 0.4 | Spread/Power |
| Power Shot Lv2 / 3 | 4x11 / 5x12 | 0.67 / 0.8 | Spread/Power |
| Power Volley Lv2 / 3 | 5x14 / 6x15 | 0.87 / 1.0 | Spread/Power |
| Flying Swallow Shot Lv1 / 2 / 3 | 3x10 / 4x11 / 5x12 | 0.4 / 0.67 / 0.8 | Normal |
| Thousand Dragons | 6x25 | 0.5 | Piercing + Special Ammo Boost |

Combos (loop times from the Math Hunter calculator presets):

* **Dash dance** (default): Charging Sidestep, Charged Shot Lv3, Power Shot Lv3, Power Volley Lv3. 14 arrows, 186 MV,
  about 3.1 s. Every serious source calls it the core loop. Power Shot and Volley carry 81 % of its raw.
* **Charged Shot into Power Shot**: the short-window version without the Volley's long recovery, about 1.5 s.
* **Ramp-up**: Lv1, Lv2, Power Shot, Power Volley from no charge, about 3.9 s.
* Sequence presets: **ten-shot combo** (ramp-up + dash dance x2, 42 arrows, 10.16 s; the benchmark of community build
  comparisons) and **dash dance into Thousand Dragons** (downed-monster burst; Thousand Dragons is not a charged attack).

All arrows of a Power Shot and Volley are assumed to hit; they fan out (about +-12 degrees), so outer arrows miss narrow
parts beyond about 10 m.

## Not modeled yet

* **Dragon Piercer**: 25 MV (18 quick), its own multi-hit curve (1, 1, 0.75 x8, 0.35 ...), and it uses the **cut**
  hitzone, not the shot one; so do Arc Shot rain and the Focus Strike wound follow-up. Needs a per-hit hitzone type.
* **Tracer**: 30 MV arrow + 30 MV blast (x1.1 / x1.2 placed at Lv2 / 3, no element); Tracer-guided shots have their own
  MVs (charged 13, Power Shot 13, Volley 16, Thousand Dragons 28) and only x1.0 critical distance. Most top players call
  it a damage loss on paper.
* **Tetrad Shot**: counts coated shots after each load (affinity from the 4th, attack on the 4th and 6th).
* **Ballistics**: widens the critical-distance window (charged shots near x0.9/0.8/0.65, far x1.1/1.15/1.25); Lv3 adds
  `_Ballistic_Wp11AttackAdd` 5 (most likely +5 raw on Lv3 shots at critical distance, game8 measured about +2 %).
* Shots outside critical distance, Power Shot arrows that miss, Arc Shot, Focus Strike, status coatings.

## Meta (TU4 / TU4.5) for checking results

Gogma Artian bow only, mostly attack focus (Close-range), element-focus Pierce bows for big monsters. Two build families:
4-piece Gogmazios (Mutual Hostility, the barrier keeps Peak Performance up) for elemental matchups, Gore Magala + Lord's
Soul for monsters that resist element. Weapon slots: element attack (often with Tetrad Shot), Spread/Power Shots,
Critical Boost 3 or Charge Master on low-affinity element builds. Armor: Agitator, Weakness Exploit, Burst, Adrenaline
Rush, Peak Performance, Antivirus with Gore, Constitution (comfort, mandatory to most), Stamina Surge. Element is about
25-35 % of bow damage since TU4 (Tidus69).

## Sources

* https://github.com/dtlnor/mhws-rcol-record (Player/Wp11.csv, Player/GlobalActionParam/individual_wp11.csv)
* https://github.com/dtlnor/MHWs-in-json (Wp11 Shell/MainParam, Wp11GlobalActionParam, Enums_Internal, Common/Weapon/Bow)
* MV sheet 1.040: https://docs.google.com/spreadsheets/d/1F_bOwQT6A6mFtCN7WCetnZDrmvLYu-ne4y97IsG6Fkk (Bow tab)
* Math Hunter bow calculator (combo presets and loop times): https://www.mathhunter.ca/bow.html
* game8.jp coatings https://game8.jp/mhwilds/683246, controls https://game8.jp/mhwilds/641077; game8.co bow guide
  https://game8.co/games/Monster-Hunter-Wilds/archives/481399, critical distance .../500373, Ballistics .../501559
* kuroyonhon bow memo: https://kuroyonhon.com/mhwilds/memo/16.php
* TU4 notes: https://www.rpgsite.net/news/19151-monster-hunter-wilds-title-update-4-full-patch-notes-balance-adjustments-list-revealed-version-1-040-00-00
* Tidus69: Is Pierce now Meta? https://www.youtube.com/watch?v=r5lIm48C_IU, TU4 builds https://www.youtube.com/watch?v=EKYalHMmPOs,
  Is Affinity Bad on Bow? https://www.youtube.com/watch?v=pGTDnejLHSc
* Grand Sky Ace, final meta bow builds (uptime assumptions): https://www.youtube.com/watch?v=6s6sc-tIsXY
* Steam pierce hit count test: https://steamcommunity.com/app/2246340/discussions/0/596270978061862041
