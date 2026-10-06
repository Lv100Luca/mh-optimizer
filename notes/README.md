# MH Wilds build optimizer – research baseline (2026-10-06)

Game state covered: Monster Hunter Wilds up to the final base-game update, Ver 1.041 (18 Feb 2026: Arch-Tempered Arkveld / Arkveld γ, Sororal set). Title Update 4 (16 Dec 2025) added Gogmazios, Gogma Artian weapons and Armor Transcendence. The expansion *Wilds: Ascendance* (2027) is not covered. The API dump used here is dated 2026-04-15 and contains every set up to Arkveld γ, so it is complete for the current game.

## 1. How the equipment system works

**Five armor pieces + charm + weapon.** Head, chest, arms, waist, legs. Each piece carries up to three decoration slots (level 1–3), armor-kind skills, optionally one group skill and one or two set-bonus skills. A charm carries armor-kind skills only (all 184 charm skill entries in the data are armor-kind). The weapon carries weapon-kind skills (innate on normal weapons, none on Artian/Gogma) plus its own decoration slots.

**Skill kinds (hard split).** *Weapon skills* (Attack Boost, Critical Eye, Critical Boost, element attack, Critical Element, Handicraft, Focus, ...) exist only on weapons, weapon decorations and (per the data) nowhere else. *Armor skills* (Weakness Exploit, Agitator, Burst, Peak Performance, Maximum Might, Latent Power, Evade Window, ...) exist only on armor, armor decorations and charms. Decorations are split the same way; 173 of the 361 decorations are two-skill combo jewels (mostly weapon slot-3 jewels).

| Deco kind | slot 1 | slot 2 | slot 3 |
|---|---|---|---|
| armor | 45 | 14 | 7 |
| weapon | 37 | 44 | 214 |

**Set bonus skills** (25). Two tiers: tier I at 2 pieces, tier II at 4 pieces carrying that set-bonus name. Normal monster sets carry one set bonus on every piece. Gogmazios α/β pieces carry *Gogmapocalypse plus one other monster's set bonus each* (α: helm Zoh Shia's Pulse, mail Xu Wu's Vigor, arms Fulgur Anjanath's Will, waist Ebony Odogaron's Power, legs Doshaguma's Might; β: Mizutsune, Rathalos, Gravios, Blangonga, Guardian Arkveld), so one Gogmazios piece counts toward two different set bonuses. Gogmazios pieces have no group skill.

**Group skills** (17). One tier, active at 3 pieces with the same group-skill name from any sets. Offensive ones: Lord's Soul (Guts: attack ×1.05, only on the five γ sets), Lord's Fury (Resuscitate: +10 attack while afflicted), Lord's Favor (Inspiration), Fortifying Pelt (Fortify), Buttery Leathercraft (Affinity Sliding).

**Armor Transcendence** (TU4, HR 100+). Raises the Armor Sphere cap of any rarity 5–8 piece and, for rarity 5 and 6 only, upgrades decoration slots: rarity 5 gets +1 level on all three slot positions (a missing slot becomes a level-1 slot, e.g. Kut-Ku Helm β goes from 2 to 3 slots), rarity 6 gets +1 level on the first two positions, cap level 3. Rarity 7/8 slots are unchanged. The dataset stores both `slots` and `slots_transcended` (derived from the game8 rule; verify in game).

## 2. Gogma Artian weapons (the core of the optimizer)

* Built by upgrading a rarity-8 Artian weapon with Gogmazios materials. Base rarity-8 Artian: 190 true raw, +5% affinity, three level-3 weapon slots, no innate skills. Each of the 3 Artian parts adds +5 true attack or +5% affinity.
* Element comes from the parts (rarity-8 element bases per weapon type in `data/gogma_mechanics.json`; +30/+20 display "infusion" when all three parts share the element).
* Permanent **Focus**: Attack (+10 attack, −15% affinity), Affinity (−10 attack, +10% affinity, −10/−20 element, different sharpness bar) or Element (−5% affinity, +30..+80 display element by weapon type). The three stat lines are the three API variants in `data/gogma_weapons_base.json`. Focus also sets phial/shelling/coating/ammo/echo bubble type.
* **Reinforcements** (5 slots, confirmed from game data): Attack +5/+6/+9/+12 (I/II/III/EX), Affinity +5/+6/+8/+10 %, Element I/II/EX per weapon type (GS 80/90/110 display ... DB 20/30/50), Sharpness +20–30 / +50 (EX), Ammo +1 / +2 (EX). Attack/affinity/element categories may fill all 5 slots; sharpness/ammo at most 2. EX tiers are "rare" rolls.
* **The headline mechanic (confirmed from game data):** each Gogma weapon rolls one (set bonus, group skill) pair from a uniform table of 21 set bonuses × 14 group skills = 294 pairs (`data/gogma_skill_pool.json`). Excluded: the four festival Prayers, Festival Spirit, Glory's Favor, Master of the Fist. The weapon counts as one equipped piece for both skills: weapon + 1 matching armor piece = tier I, weapon + 3 = tier II; weapon + 2 group pieces activates the group skill. Rerolled with Tarred Devices + zenny ("Reset Skills"). The one-piece rule is confirmed by the user in game (2026-10-06).
* Because Gogmazios armor pieces each carry two set bonuses, a weapon that rolled e.g. Xu Wu's Vigor plus Gogmazios α mail (Xu Wu's Vigor + Gogmapocalypse) already gives Protein Fiend I with one armor slot, and adding Gogmazios α helm + arms gives Mutual Hostility I on top. The optimizer must therefore count set-bonus *names* across weapon + armor pieces, not armor sets.

## 3. Dataset files

| file | content |
|---|---|
| `data/raw/*.json` | untouched dumps from `https://wilds.mhdb.io/en/{armor, armor/sets, skills, decorations, weapons, charms}` (version 2026-04-15) |
| `data/armor_hr.json` | 582 high-rank pieces: set, piece, rarity, slots, slots_transcended, armor skills, set_bonus list, group_skill, defense, resistances |
| `data/armor_sets_hr.json` | 151 HR sets with pieces, set bonuses, group skill |
| `data/set_and_group_bonuses.json` | every set/group skill → tier texts, piece thresholds, carrying sets, piece counts |
| `data/skills.json` | 179 skills (71 armor, 66 weapon, 25 set, 17 group) with per-level text and max level |
| `data/skill_sources.json` | per skill: armor pieces (level), decorations (slot), charms that provide it |
| `data/decorations.json` | 361 decorations with kind, slot level, skills |
| `data/charms.json` | all charm ranks (183), `is_max_rank` flag |
| `data/gogma_weapons_base.json` | 14 Gogma weapons × 3 focus variants: raw, affinity, sharpness bar, slots, ammo/coating/phial |
| `data/gogma_mechanics.json` | focus deltas, R8 element bases, reinforcement tiers, piece-count rule, open questions |
| `data/gogma_skill_pool.json` | the 294 rollable (set bonus, group skill) pairs with probabilities |
| `data/damage_model.json` | formula, bloat dividers, sharpness/crit multipliers, element cap, numeric skill and set-bonus values |
| `data/datamine/*.json` | raw game-data files pulled from dtlnor/MHWs-in-json (SkillData, SkillCommonData, ArtianSkillGroupData, ArtianBonusData, PlayerSkillParam, PlayerStatusParam, ...) |
| `notes/skills_from_mhdb_api.md` | all skill texts per level (readable) |
| `notes/skill_params_datamine.md` | the raw `_value` parameter arrays per skill level from the game data |
| `notes/player_skill_param_extract.json` | Burst / Critical Element / set-bonus parameter arrays from PlayerSkillParam |
| `notes/skill_values_set_group.md`, `notes/skill_values_vague_skills.md` | community-sourced numbers (web research), useful to cross-check the datamine interpretations |

`tools/build_dataset.py` regenerates the normalized files from `data/raw`. `tools/html_tables*.py` are the wiki-table scrapers.

## 4. Damage model (hitzone 100, no resistances)

Per hit:

```
raw     = TrueRaw * MV/100 * SharpRaw * CritRaw
element = min(TrueEle, Cap) * SharpEle * CritEle
```

* TrueRaw = display / bloat (GS 4.8, LS 3.3, SnS/DB 1.4, Hammer 5.2, HH 4.2, Lance/GL 2.3, SA 3.5, CB 3.6, IG 3.1, LBG 1.3, HBG 1.5, Bow 1.2); the API already gives true raw. True element = display / 10.
* Game-data constants: crit multiplier 1.25, negative crit 0.75, Critical Boost 1.28/1.31/1.34/1.37/1.40, element cap = max(base × 2.3, base + 40 true), Frenzy overcome +15% affinity for 60 s, weak-point threshold hitzone 45.
* Critical Element: 1.05/1.10/1.15 for fast weapons (LS, SnS, DB, Lance, IG, LBG, Bow), 1.07/1.14/1.21 for GS, Hammer, HH.
* Sharpness (community table): raw green 1.05, blue 1.20, white 1.32, purple 1.39; element green 1.00, blue 1.0625, white 1.15, purple 1.25.
* Element attack skills: ×1.00 +4, ×1.10 +5, ×1.20 +6 true. Coalescence ×1.10/1.20/1.30, Charge Master ×1.15/1.20/1.25 (charged attacks), Gogmapocalypse ×1.2 +2 / ×1.3 +4 when enraged.
* Burst is per weapon type: GS Lv1..5 = +10/+12/+14/+16/+18 attack and +80/+100/+120/+160/+200 display element (5 s window), LS = +8/+10/+12/+15/+18 and +60/+80/+100/+120/+140 (5 s); first hit +5/+50 (GS) or +4/+50 (LS).

Recommended optimizer metric: **Effective Raw + Effective Element per 100 MV**, independent of the attack combo. Motion values are available in the game data (`Wp??_Attack.rcol` `_Attack` field, 126 entries for Great Sword) for a later combo-DPS mode; the attack-name mapping still has to be built.

Conditional skills (Agitator, Peak Performance, Maximum Might, Weakness Exploit, Burst, Adrenaline Rush, Latent Power, most set bonuses) need an *uptime* weight; the dataset keeps the condition text so the optimizer can expose sliders.

## 5. Decisions and open questions

Decided (2026-10-06):
* The Gogma weapon counts as exactly one piece for its set bonus and its group skill (confirmed in game).
* Scope for now: Great Sword and Long Sword only.
* Conditional skills (Weakness Exploit, Agitator, Maximum Might, Peak Performance, Burst, set bonuses, ...) are user-toggleable in the optimizer; a toggle can only take effect when the evaluated loadout actually contains the skill.
* Stack: C# / .NET 10 (solution `MHWildsOptimizer.sln`: Core library, CLI, xunit tests).
* Ranking metric: EFR + EFE combined (effective raw plus effective element per 100 MV).

Still open:
1. Transcendence slot rule for rarity-5 pieces with fewer than 3 slots (assumed: new level-1 slots appear).
2. Resolved: Burst arrays decoded (GS Lv5 = +18 attack / +200 display element, LS Lv5 = +18 / +140), Critical Element GS 1.07/1.14/1.21 vs LS 1.05/1.10/1.15, Coalescence GS x1.10-1.30 vs LS x1.05-1.15. Still fuzzy: Elemental Absorption flat values, Flayer burst size, Charge Master grouping.
3. Exact Wilds element sharpness multipliers (blue 1.0625 vs 1.05, purple 1.25 vs 1.27).
4. Combo-DPS mode with motion values is a later option (needs the attack-name mapping for the rcol data).

## 6. Optimizer inputs (implemented in `src/MHWildsOptimizer.Core/Inputs`)

A request file (`inputs/request.example.json`, snake_case) carries everything:

* `weapon`: the Gogma weapon as the game shows it, either as a `spec` (type, focus, element, infusion, number of attack parts, the five reinforcement lines such as `"attack III"`) or as direct stats (`type`, `attack` display, `affinity`, `element`, `element_display`, `sharpness`). Plus the currently rolled `set_bonus` / `group_skill`. Weapon stats are never optimized.
* `skill_pair`: `"fixed"` (use the rolled pair) or `"optimize"` (search all 294 rollable pairs and report the best `top_n`).
* `target_skills`: required minimum levels. The weapon's core skills are merged in by default (`options.require_weapon_core_skills`): Great Sword Focus 3, Long Sword Quick Sheathe 3.
* `conditions`: the toggles of `Damage/Conditions.cs` (enraged, weak point, wound, full/red/low health, stamina, Burst, Frenzy, Resonance mode, …). A toggle only acts when the loadout carries the skill.
* `conditions.skill_limits`: per-skill caps for the score. `0` removes the skill from the optimization, `n` values it only up to level n (e.g. `{ "Burst": 1 }` on Great Sword, which rarely lands five consecutive hits). Levels above the cap are still shown, marked "valued at n". A target above its limit is rejected.
* `talismans`: a file with the random talismans you own (`inputs/talismans.example.json`: name, rarity, skills with levels, decoration slots as `armor1` / `weapon1`) and whether the craftable charm lines (max rank) are also considered. Entries are validated against the random-talisman pool from the game data (`data/random_talisman_pool.json`): skill slot 1 is a weapon-kind skill worth 1-4 points (Attack Boost max 3, Critical Eye 3, Critical Boost 1, element attack 3), skill slots 2-3 are armor-kind skills worth 5-10 points (Weakness Exploit / Agitator / Burst / Latent Power / Adrenaline Rush max 1, Maximum Might / Peak Performance 2); decoration slots are up to three armor slots or, on rarity 7, one weapon Lv1 slot plus armor slots.
* `options`: transcendence on/off, number of results, minimum rarity, excluded sets.

Decoration slots on armor (base or transcended), on the weapon and on talismans are all part of the loadout model; the optimizer fills them. Decorations are assumed to be available in unlimited quantity.

## 7. Optimizer (implemented in `src/MHWildsOptimizer.Core/Optimize`)

* `Relevance`: the skills, set bonuses and group skills that can change the score under the requested conditions (plus the targets). Everything else is ignored. Target-only skills (e.g. Focus) are capped at the target level.
* `Candidates`: armor per slot kind and talismans projected onto those features; a piece is dropped when another piece of the same kind is at least as good in every feature (skills, cumulative slot counts, set bonuses, group skill). Rarity 5/6 pieces use their transcended slots when allowed.
* `DecorationFiller`: exact search over the few decorations per target skill to cover the deficits with the fewest slots (packing biggest jewels into the smallest fitting slot of the right kind), then a greedy fill of the remaining slots by score gain. Decorations are unlimited.
* `Optimizer`: dynamic programming over skill-state signatures (talisman first, then armor kinds ordered by candidate count). States with identical signature merge (keeping a few predecessors so several concrete builds can be reconstructed); set/group counts that can no longer reach their threshold are zeroed; states that cannot reach the targets even with the best remaining pieces and all possible slots are dropped; a bounded Pareto check removes dominated states; a beam (`options.max_states_per_depth`) caps the rest by partial score + slot value. Final states are decorated and scored once, the best are reconstructed into loadouts and re-scored with the full calculator.
* Optimize mode: the 294 rollable (set bonus, group skill) pairs are grouped into classes with identical score behaviour (irrelevant sets/groups collapse into "(any other)"), one search per class in parallel, ranked by best build.
* Measured on the example request (GS, six targets, 62 talismans): 10 s, identical best build to the exhaustive run (214 s).

## Sources

* API: https://wilds.mhdb.io (docs https://docs.wilds.mhdb.io)
* Game data dump: https://github.com/dtlnor/MHWs-in-json (`natives/STM/GameDesign/Common/Equip/*`, `GameDesign/Facility/Artian*`, `GameDesign/Player/ActionData/Common/GlobalParam/**`, `GameDesign/Player/ActionData/Wp??/Collision/Collider/*_Attack.rcol.38.json`)
* Gogma Artian weapons: https://monsterhunterwiki.org/wiki/Gogma_Artian_Weapons_(MHWilds), https://game8.co/games/Monster-Hunter-Wilds/archives/571311, https://monsterhunterwilds.wiki.fextralife.com/Gogma_Artian_Weapons, https://nixzxin.com/guides/gogma-weapon-mechanics, https://steamcommunity.com/app/2246340/discussions/0/729154531022131987/
* Artian weapon bases: https://monsterhunterwiki.org/wiki/Artian_Weapons_(MHWilds)
* Gogmapocalypse values: https://game8.co/games/Monster-Hunter-Wilds/archives/571423
* Armor Transcendence: https://game8.co/games/Monster-Hunter-Wilds/archives/571459, https://www.icy-veins.com/monster-hunter-wilds/armor-transcendence
* Patch notes: https://info.monsterhunter.com/wilds/update/en-us/Ver.1.040.00.00.html (TU4), https://info.monsterhunter.com/wilds/update/en-us/Ver.1.041.00.00.html (final update)
* Skill system: https://www.rpgsite.net/guide/16918-monster-hunter-wilds-equipment-skills-guide-all-armor-skills-detailed-what-they-do
* Damage formula: https://wiggler.pet/other/guides/intermediate_damage_calculation, https://wildsbuilder.com/blog/damage-formula/, https://www.switchbladegaming.com/monster-hunter-wilds/elemental-damage-guide/
* Skill texts: https://monsterhunterwiki.org/wiki/MHWilds/Skills, https://mhwilds.kiranico.com/data/skills
