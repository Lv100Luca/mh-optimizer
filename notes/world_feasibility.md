# MH World: Iceborne with the Wilds feature set: feasibility (2026-10-09)

Question: can the optimizer offer for World: Iceborne (final patch, Fatalis / Alatreon / Safi'jiiva / Raging Brachy) what it
offers for Wilds (profiles, weapons, targets, limits, conditions, CP-SAT set search, results, inventory, My builds,
compare, damage per attack / sequence, attack picker and monster targets), as a separate page with the same look and feel,
**without any risk to the Wilds app**?

**Answer: yes.** The engine's ideas carry over one to one (CP-SAT over pieces + decoration counts, the score model read
off a damage calculator, attack profiles and sequences, health sides, conditions as toggles). What differs is the game's
rules and data, and a handful of structural points (section 2) that the current engine hard-codes for Wilds. Recommended:
a separate World Core + browser app next to the Wilds projects, Wilds frozen by golden tests first, the engine forked
rather than generalised in place (section 5).

World facts below come from two research passes on 2026-10-09 (sources at the end). Numbers marked VERIFY are single
source or conflicting.

## 1. How World differs from Wilds

### Same (or same shape)

* 5 armor pieces + charm + weapon; skills with levels; decorations in sized slots; set bonuses counted by **name**
  across pieces (alpha+ / beta+ / related sets share one name).
* An item that counts as one set piece: the **Safi'jiiva weapon's set-bonus awakening** (one per weapon, about 31
  bonuses: Teostra, Velkhana, Silver Rathalos, Gold Rathian, Namielle, Zinogre, Glavenus, ...; VERIFY list, Fextralife
  only). MR Taroth weapons probably count toward Kulve Taroth Essence (VERIFY). This is the Gogma "weapon counts as a
  piece" rule with one bonus instead of a pair, and Safi awakenings are random offers like Artian rolls.
* Damage formula skeleton: raw = true raw x MV x sharpness x crit x hitzone; element = min(element, cap) x element crit x
  per-hit element modifier x element hitzone; Weakness Exploit needs raw hitzone >= 45; display bloat per weapon type
  (same values as Wilds); sharpness multipliers identical (raw 1.05 / 1.20 / 1.32 / 1.39, element 1.0 / 1.0625 / 1.15 /
  1.25 for green..purple); crit 1.25, negative crit 0.75, affinity capped at +-100.
* Weapon stats as user input (never optimized): World weapons are fixed crafts plus augments / custom upgrades /
  Safi awakenings / Kulve upgrades, all of which only move base attack, affinity, element, sharpness and slots.
* A datamined motion-value sheet with per-hit element modifiers (MoonBunnie & Deathcream Weapon Attack Tables, v15.01,
  all 14 weapons, ~600 rows): e.g. GS True Charged Slash 2 Lv3 211 MV x1.7 element (Power 264 x1.8); SA Zero Sum
  Discharge finisher, discharge ticks x0.5, amped burst x0.35, Power phial x1.17, Power Element phial x1.45.
* Powercharm +6 and meal attack as flat attack (World adds more consumables, below).

### Different numbers or rules

| Concept | World: Iceborne |
|---|---|
| Decoration slots | levels **1-4**; any jewel fits **any** slot of its size or bigger, weapon or armor (no weapon/armor split) |
| Decorations | 404: 58 / 31 / 16 / 299 by size. Size 4: 65 "+" jewels (one skill at 2), Hard jewels (one skill at 3, defensive/utility only), 234 two-skill combo jewels. No WEX+Attack or Crit Boost+Crit Eye combos |
| Decoration supply | random drops (feystones, tickets, melding). Rarity-12 jewels (Attack+, Expert+, Critical/Tenderizer combos) are rare, so the optimizer **needs an owned-decoration inventory** (the Wilds app assumes unlimited) |
| Charms | fixed craftable lines I-V (110 lines / 317 levels), no slots, no randomness; 16 lines carry two skills. Simpler than Wilds talismans |
| Set bonuses | 1 or 2 tiers with **thresholds anywhere from 1 to 5 pieces** (Teostra 3, Velkhana 2 / 4, Safi'jiiva 3 / 5, Fatalis 2 / 4, Frostfang 1 / 3), each tier grants a one-level skill |
| Skill caps | **depend on the build**: "Secret" set skills raise 12 skills by 2 levels (Agitator 5->7, Latent Power 5->7, Heroics 5->7, Maximum Might 3->5, ...); Fatalis 2-piece (Inheritance) unlocks all of them |
| Critical Boost | 3 levels: 1.30 / 1.35 / 1.40 |
| Critical Element | **no jewel**: only Velkhana 2-piece, Silver Rathalos 4-piece (True) or a Kjarr weapon; per class GS/Hammer/HH/HBG 1.50 (True 1.70), fast weapons 1.35 (1.55), LBG 1.25 (1.40) |
| Element cap | max(1.6 x base, base + 15) true; **Dragonvein raises it to 2.2x / 2.55x** (Safi'jiiva 3 / 5 pieces) |
| Element attack skills | 6 levels: +3 / +6 / +10 true, then x1.05 / x1.10 / x1.20 + 10 |
| Attack Boost / Critical Eye | 7 levels: +3..+21 flat (+5 % affinity from Lv4) / 5..40 % |
| Weakness Exploit | 10 / 15 / 30 % on weak points, + 5 / 15 / 20 % more on tenderized parts (15 / 30 / 50 %) |
| Wounds | Iceborne **tenderizing**: hitzone = floor(0.75 x hz) + 25 (+-5 for some monsters), raw hitzones only, also used for the 45 check |
| Sharpness | **Handicraft** extends the bar (+10 per level); many weapons only reach white / purple with it, so Handicraft is a scoring skill (the Wilds model takes the weapon's top color as fixed and does not model Handicraft) |
| Attack | capped at 2 x base attack (never binds in practice); base multipliers vs flat bonuses split like Wilds |
| Consumables | Powercharm +6 **and** Powertalon +9, Demondrug +5 / Mega +7, Might Seed +10 / Pill +25, Demon Powder +10, canteen +5/+10/+15, Hunting Horn Attack Up x1.10 / 1.15 / 1.20 on base attack (VERIFY stacking groups) |

### Wilds-only (drop for World)

Gogma Artian spec (focus, parts, reinforcements), group skills, the 294 skill pairs, random talismans and their pool,
Armor Transcendence, the weapon/armor decoration split, Burst, Charge Master, Frenzy / Antivirus / Gore set,
Omega Resonance, festival prayers, Lord's Soul et al., Dark Arts shockwave and Bad Blood procs.

### World-only (new for World)

Secret skills and Inheritance; Dragonvein / True Dragonvein (element, affinity, element cap; HP drain); hidden element +
Free Element (33 / 66 / 100 %); Element Acceleration; Non-elemental Boost (x1.05); Bludgeoner (by sharpness color);
Critical Draw (30 / 60 / 100 % on draw attacks); Frostcraft (charge tiers); Alatreon Element Conversion (base element +5 %
of total positive elemental resistance, so armor resistances become a scoring feature); (Elemental) Airborne; Fortify;
Coalescence triggered by status / blight recovery (VERIFY); Master's Touch / Razor Sharp (sharpness use, no damage effect
in this model); weapon innate skills (Kjarr Critical Element / Status, Lunastra line Guts / Hasten Recovery / Razor Sharp);
mantles and boosters (Evasion Mantle, Affinity Booster) as toggles.

## 2. Compatibility with the current structures

Line counts: Core 5.8k, Api 1.3k, Browser 7.6k, tests 2.9k (~120 tests), tools 0.8k.

### Reusable as is (game-agnostic today)

* CP-SAT plumbing: `ICpSatBackend`, `NativeCpSatBackend`, `CpSatParameters` (`Core/Optimize/CpSatBackend.cs`), the
  browser solver bridge (`Browser/Services/CpSatBridge.cs`, `solver/` bundle of or-tools-wasm, lanes, warm-up,
  cancellation, nested-worker probe) and `coi-sw.js`. They take a `CpModel`, nothing Wilds.
* UI shell and look: `Theme.cs`, `wwwroot/css/app.css`, most of `Components/Common` (Section, Field, Toggle, Stepper,
  NumberInput, SearchSelect, Segmented, SharpnessBar, Alert, NameDialog, ...). A few reference Wilds types or icon paths
  (KindTag / SkillChip use `SkillKind`, DecoIcon / Slots / SkillIcon use `GameIcons`).
* Patterns: browser profile store (localStorage, gzipped results), profile export / import, drafts, URL routing, batch
  runs, inventory staleness by inputs hash, `tools/ui-shots`, the CI + rsync deploy.

### Reusable after generalising (engine)

* **`ScoreDecomposition`** (the CP-SAT score model). Its key design is reusable: per-feature contributions are *probed*
  from the damage calculator, interacting features are detected and tabulated jointly, and the composition is checked
  against the calculator before every search. Coupling to Wilds: it calls the static `DamageCalculator` and reads Wilds
  `Conditions` (health sides, sequence segments, `DarkArtsShockwave`, `SkillNames.SoulOfTheDarkKnight`). World also needs
  two new channels: **element cap as a function of skills** (Dragonvein; `ScoreSide.ElementCap` is a constant today) and
  **sharpness as a function of skills** (Handicraft; `ScoreSide.RawFactor` / `SharpEle` are constants today).
* **`CpSatSearch`** (the model). Hard-coded Wilds structure: slot levels 1-3 (`for level 1..3`, `int[3]` slot counts),
  two slot pools (armor + talisman vs weapon + talisman weapon slots), set tiers at fixed 2 / 4 pieces
  (`SkillAggregator.SetTierOnePieces/TwoPieces`, feature max 2), group skills at 3, the weapon's (set bonus, group skill)
  pair. World needs: one slot pool with levels 1-4; per-set tier thresholds; no group skills; per-decoration upper bounds
  (inventory, trivial in CP-SAT); and **skill caps that depend on set bonuses**. The caps belong in the model as
  constraints (level = min(points, base cap + 2 x secret-or-Inheritance)), not in the score tables: tabulating Agitator,
  Latent Power, Maximum Might, Heroics with their Secrets and Inheritance jointly would be ~100k states, above
  `ScoreDecomposition.MaxUnitStates` (20k).
* `Candidates`, `DecorationFiller` (slot kinds, levels 1-3), `Relevance` (its skill and set lists are Wilds names),
  `SkillAggregator` / `ActiveSkills` (fixed thresholds, caps at `MaxLevel`), `Optimizer`'s class orchestration (skill pair
  classes -> Safi awakening classes; the beam search is not needed: the browser runs CP-SAT only).
* `AttackProfile` / `Target` / sequences: the hit model (MV, element modifier, count, fixed sharpness, segments) is
  generic; `AttackHit` carries Wilds flags (`SwordMode`, `ChargedLv3`, `Power`), and `Target` needs the tenderize formula.

### World-specific (new code, replaces the Wilds version)

`Conditions` (a sealed record of Wilds toggles; `ConditionCatalog` lists every bool property by reflection),
`DamageCalculator` + `DamageConstants` + `SkillNames` (every skill and value is Wilds), `Attacks` (Wilds 1.040 MVs in C#),
the weapon model (`GogmaWeaponSpec` / `GogmaWeaponStats` / `WeaponStatsInput`; World: pick a weapon from the list +
augments / awakenings, or direct stats, with the sharpness bar per Handicraft level and hidden element),
`Talisman` / `TalismanInput` / `RandomTalismanPool` (World: charms + decoration inventory), `GameData` / `GameDataLoader`
(fixed Wilds file names, Gogma variants), `OptimizationRequest` / `ResolvedRequest` / `Catalog` / `Resolving` /
`BuildEvaluation` / result DTOs, the data pipeline (`tools/*.py`), and the panels that edit those (Weapon, Skill pair ->
Safi awakening, Targets with per-set tiers, Conditions, Talismans -> Charms + Decorations, Options without
transcendence). Results, Inventory, My builds, Compare, Build card, damage breakdown and the sequence editor port with
type changes only.

## 3. Ways World work could break Wilds (and how to rule each out)

1. **Inputs hash churn.** `ProfileRules.InputsHash` serializes the whole request (`ApiJson.Options` writes every
   property, defaults and nulls included). Adding *any* property to `OptimizationRequest`, `Conditions`,
   `OptimizerOptions`, `WeaponStatsInput` or `TalismanSettings` changes every hash, so every saved Wilds result turns
   "stale" and "Run stale" re-runs everything. Adding a bool to `Conditions` also adds a toggle to the Wilds Conditions
   panel (`ConditionCatalog` reflects over all bool properties). Rule: World never adds members to Wilds types; a test
   pins the hash of a fixture request.
2. **Changing the shared engine.** `CpSatSearch`, `ScoreDecomposition`, `Candidates`, `SkillAggregator` are the most
   sensitive Wilds code. Rule: World forks them (section 5); any later unification only with golden tests green.
3. **Same origin.** localStorage is per origin with ~5 MB shared quota (Wilds stores gzipped results there); the Wilds app
   routes `/{*Path}` with the first segment as the profile name (a `/world/...` link served by the Wilds `index.html`
   opens a profile called "world"); `BrowserProfileStore.StorageUsed` sums every key starting with `mhwo`; `coi-sw.js` is
   registered at `/`. Rule: host World on its **own subdomain** (own origin, quota, service worker), or under a path with
   its own `index.html` + Caddy `try_files` for that path and a different key prefix (e.g. `mhw:`).
4. **Deploy.** CI rsyncs `publish/browser/wwwroot` to `/opt/mh-optimizer/site` with `--delete`, so a World build copied
   into that folder by any other route is wiped on the next Wilds deploy. Rule: separate target folder + Caddy block +
   CI job (or one job that publishes both into one tree); the Wilds job stays as is.
5. **Data folder.** The Wilds browser csproj copies `data/*.json` (non-recursive) into `wwwroot/data`, and
   `GameDataLoader.FindDataDirectory` looks for `data/armor_hr.json`. World data in `data/world/` touches neither.
6. **Build / CI time.** A World project in `MHWildsOptimizer.slnx` runs in the same `dotnet test` step; a failing World
   test would block Wilds deploys. Fine if World tests are kept green, or run as a separate CI step.

Guard rails before the first World commit (small, Wilds-only, no behaviour change):

* Golden tests: for a few fixture requests (GS fixed pair, GS optimize mode, SA sequence, element weapon), pin the
  CP-SAT top build scores and armor (CP-SAT proves the optimum, so scores must match to 1e-6), the inputs hash, and the
  JSON of a serialized request / result.
* A UI-shots baseline of all tabs (`tools/ui-shots`) to compare after any shared-UI move.
* Existing: ~120 tests incl. `StatusScreenAttackMatchesTheGame`, `CpSatBruteForceTests`, `CpSatOptimizeModeTests`,
  `ResultsDoNotDependOnTheThreadCount`, API integration tests; CI runs them on every PR.

## 4. Data sources (World has no single mhdb-like API)

| Need | Source | Notes |
|---|---|---|
| Armor (1,638 pieces, 819 MR incl. Fatalis / Alatreon / Safi / Raging Brachy / Guardian), weapons (3,123; true raw, element / 10, sharpness as [Handicraft 0 bar, +Handicraft 5], hidden element), hitzones (71 monsters incl. tenderized values) | monsterhunterwiki.org `Module:MHWI/data/{Armor,Weapons,Monsters/DamageEffectiveness}.json` (`action=raw`), CC BY-SA 4.0, updated 2026 | the same wiki the Wilds icon pipeline uses. Its `Skills.json`, `Decorations.json`, `Talismans.json` hold **Wilds** data: do not use |
| Skills (178, per-level text, Secret levels), set bonuses with thresholds (69), decorations (404), charms (317 rows), weapon innate skills | gatheringhallstudios/MHWorldData CSVs or its V46 `mhw.db` (MIT, last update 2021-03, Fatalis patch) | no Guardian / collab sets; no motion values |
| Motion values, element modifiers, skill / set / augment / awakening numbers, formulas, deco drop tables | MoonBunnie & Deathcream Weapon Attack Tables (v15.01), General Data Sheet (v15.01), Damage Formula sheet (v13.50); published Google Sheets, CSV export per tab | no licence stated; credit the authors. Replaces both the Wilds MV sheet and the dtlnor skill-parameter dumps |
| Cross-check | mhw-db.com (still up; frozen mid-Iceborne: no MR weapons, no hitzones), Kiranico | |
| Icons | monsterhunterwiki `MHWI-*` files (skill / set colors, decoration levels 1-4, weapon and armor rarity icons) via the imageinfo API, like `tools/fetch_icons.py` | MHWorldData ships MIT SVGs as a fallback |

Joins need name normalisation (alpha / "Alpha +", "(MR)" suffixes on Kulve weapons, "Transcendance", "Slinger Ammo
Secret" vs "Slinger Capacity Secret", "Explosion" vs blast). Patches after 15.01 (15.02 - 15.21) look like fixes and
platform updates; VERIFY that none changed equipment numbers.

## 5. Recommended approach

**Separate projects, Wilds untouched:**

```
src/MHWorldOptimizer.Core      World rules, data model, damage calculator, conditions, attacks, and a fork of the
                               CP-SAT engine adapted to World (references Google.OrTools like Wilds Core)
src/MHWorldOptimizer.Browser   Blazor WASM app with the Wilds layout, theme and components (copied first)
tests/MHWorldOptimizer.Tests
data/world/                    normalized World JSON (+ raw/ dumps), never copied into the Wilds site
tools/world/                   fetch + normalize scripts
```

Why fork the engine instead of generalising it in place: World needs real changes in exactly the most sensitive Wilds
code (slot pools and levels, per-set thresholds, skill caps from set bonuses, inventory bounds, element cap and sharpness
channels, a pluggable damage model and conditions). Done in place, each is a refactor of the Wilds engine; forked, Wilds
cannot change. The cost is ~2k duplicated lines (CP-SAT search, score model, candidates, decoration filler, attack
profile and target, class orchestration) whose later fixes need porting; the CP-SAT backends and parameters need no copy
(World Core can reference them read-only). Once World is stable, the two engines can be
merged behind interfaces (`IDamageModel`, a conditions abstraction, slot / set rules from data), with the golden tests as
the gate.

Why a separate app instead of a game switch inside the Wilds app: the Wilds app loads Wilds data before it renders,
`AppState` and every panel are typed on Wilds requests, routing owns every path, storage is one origin. A World bug in a
shared app could take the Wilds page down; a separate app cannot. The same look comes from the copied theme / CSS /
components; a link in both headers switches games. A shared Razor class library for Theme + CSS + common components +
solver bridge is a later, separately verified move (UI-shots before / after, all tests).

Hosting: a subdomain such as `world.mh-optimizer.luca-diegel.de` (own origin, own deploy folder, own Caddy block, own CI
job; the Wilds job unchanged).

**Phases**

0. Freeze Wilds: golden tests + hash pin + UI-shots baseline (section 3). Small, Wilds-only, no behaviour change.
1. World data: fetch + normalize skills (with Secret caps), set bonuses with thresholds, decorations, charms, MR armor,
   weapons (sharpness per Handicraft level), hitzones (+ tenderized), icons. Medium; mostly joining three sources.
2. World Core: domain + loader, World `Conditions` and damage calculator (cross-checked against the Damage Formula sheet
   and in-game status screens, like `StatusScreenAttackMatchesTheGame`), engine fork with one slot pool 1-4, per-set
   tiers, Secret caps as constraints, decoration inventory bounds, Dragonvein element cap and Handicraft sharpness
   channels, Safi set-bonus awakening fixed / optimize. Large.
3. World browser app: copy the shell, adapt the panels (weapon picker + augments / awakenings, charms + decoration
   inventory, Safi awakening instead of skill pair, World conditions). Large.
4. Attacks: Switch Axe, Great Sword and Long Sword from the Weapon Attack Tables, then more weapon types. Medium per
   weapon.
5. Deploy: subdomain, Caddy block, CI job.

Out of scope at first (each is a later add-on): Alatreon Element Conversion (needs armor resistances as a scoring
feature), hidden element + Free Element (base element and cap change with a skill), Frostcraft charge tiers, Dragonvein's
HP drain, gunner ammo, status builds, a CLI / beam search for World.

## 6. Decisions (2026-10-09)

1. Hosting: open. Recommended: a subdomain. A `/world/` path needs no DNS record, but it changes the Wilds deploy
   (rsync `--delete`, both apps in one tree), adds a second Caddy `try_files` rule, shares the ~5 MB storage quota and
   the service worker with Wilds, and needs its own key prefix. A subdomain costs one DNS A record, a copy of the Caddy
   block and the DEPLOY.md setup once more (own folder and deploy key), and leaves the Wilds deploy as it is.
2. Decoration inventory: **from the start** (owned count per jewel; the CP model bounds each decoration's count).
3. Weapons: **a picker over all weapons**, with augments, custom upgrades, Safi'jiiva awakenings and Kulve upgrades
   computed into the stats (direct stats stay as a fallback). Needs the weapon data of every type, sharpness per
   Handicraft level, and the augment / awakening tables from the General Data Sheet.
4. Attack picker at launch: **Switch Axe, Great Sword and Long Sword** (Long Sword is new compared with Wilds: Spirit
   gauge levels x1.05 / 1.10 / 1.20 as a condition, Spirit Helm Breaker, Iai / Foresight moves from the Weapon Attack
   Tables). Other weapon types use the average-hit model, as in Wilds.

## Sources

* MHWorldData: https://github.com/gatheringhallstudios/MHWorldData
* monsterhunterwiki MHWI data modules: https://monsterhunterwiki.org/wiki/Module:MHWI
* Weapon Attack Tables (v15.01): https://bit.ly/MHWIWeaponAttackTables
* General Data Sheet (v15.01): https://bit.ly/MHWIGeneralDataSheet
* Damage Formula (v13.50): https://bit.ly/MHWIDamageFormula
* mhw-db.com: https://mhw-db.com, https://docs.mhw-db.com
* Kiranico: https://mhworld.kiranico.com (skill trees, monsters, damage guide)
* Fextralife: Awakened Abilities, Augmentations and Upgrades, Elder Melder (Safi set awakenings, augment costs)
* Athena's ASS (pre-Iceborne, not usable): https://github.com/AthenaADP/MHW-ASS
