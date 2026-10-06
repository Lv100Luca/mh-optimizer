# MH Wilds – numeric values of Set Bonus skills and Group skills

Game state: Monster Hunter Wilds Ver 1.041 (Feb 2026, final base-game update). Compiled 2026-10-06.

## Conventions and data sources

* **"attack +N"** is flat **true raw** added to the weapon's base attack. Wilds shows unbloated raw on the equipment screen (a Great Sword is ~200–230, Dual Blades ~190–230), so a "+10" set bonus is +10 to the number you see, before Attack Boost %-multipliers are applied. Game8's Razor's Edge table (196 → 226 → 235) is consistent with this.
* **Element** in game8/wiki text is in *displayed* units (displayed = true × 10). Example: "1.2x + 20" means true element × 1.2 + 2. The datamine for Gogmapocalypse stores exactly `[120, 2]` / `[130, 4]`, confirming the flat part is +2 / +4 true element.
* **Datamine columns.** Three datamined sources are used:
  * `SkillData._value[0..3]` per skill level (dtlnor MHWs-in-json dump; also shown verbatim by mhwilds.kiranico.com as e.g. "15/105"). Local copy: `notes/skill_params_datamine.md`.
  * `PlayerSkillCommonEffectSetting` (named parameters such as `_ViolentData`, `_ScorchingHeatData`, `_DischargeData`, `_ResonanceData`). Local copy: `notes/player_skill_param_extract.json`. Field names are Capcom internal (Japanese-derived); the mapping to skills is inferred from matching numbers and is flagged per row.
  * Community testing on the training dummy (game8, Wycademy notebooks, Fextralife, Steam/Reddit threads).
* Confidence: **high** = datamine and at least one tested source agree; **medium** = one solid source or minor disagreement; **low** = single unverified source or interpretation of an unlabeled datamine field.
* Kiranico's pages for Gogmapocalypse and Lumenhymn Prayer are empty shells (nav only) as of this fetch; values for those come from the dtlnor dump instead.

---

# PART A – SET BONUS SKILLS (tier I at 2 pieces, tier II at 4 pieces)

## A1. Damage-relevant set bonuses

### Gore Magala's Tyranny → Black Eclipse I / II

| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| I (2 pc) | Auto-infects you with Frenzy. No attack value (datamine `[0,0]`). When Frenzy is overcome you get the normal Frenzy-overcome bonus: **+15% affinity**. With Antivirus Lv1/2/3 this becomes **+18 / +21 / +25 %** (Antivirus datamine Lv1 = `[10, 3]` → +3 affinity, Lv2 +6, Lv3 +10). | Fighting a large monster. Must overcome Frenzy by landing hits (≈20 hits with LS per game8; Antivirus reduces it). | Affinity buff **60 s** after overcoming (game8, Wycademy); one Steam guide says ≈45 s. Then you are re-infected and cycle again. | game8 502173; Wycademy; kiranico gore-magalas-tyranny; datamine `[0,0]` / Antivirus `[10,3]` | high (affinity) / medium (duration) |
| II (4 pc) | **+10 true raw while infected**, **+15 true raw after overcoming** (datamine Lv4 `[10, 15]`). Game8 phrases this as "+10 on infection, +5 more upon recovery" and its Antivirus table lists the post-cure state as "+15 Atk, +18–25 % aff". Alternative reading (only +5 after cure, i.e. less than during infection) is not supported by the datamine. Plus the same +15 % affinity (up to +25 % with Antivirus 3) after overcoming. | as above | Attack: +10 for the whole infected phase, +15 for the 60 s overcome window; affinity only during the overcome window. | game8 502173; kiranico `10/15`; Wycademy; Steam DB guide (45 s claim) | high (values) / medium (60 vs 45 s) |

Notes: Resuscitate (Lord's Fury) no longer triggers from Black Eclipse's Frenzy since patch 1.000.05.00 (10 Mar 2025). Unlabeled datamine `_BloodJoyData [15, 5]` may be the Frenzy-overcome base (+15 affinity, 5 = ?) – interpretation unknown.

### Leviathan's Fury → Azure Bolt I / II

| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| I (2 pc) | **+15 % affinity** for 20 s when the thunder burst triggers. Burst = **30 fixed + 70 thunder** (thunder part scales with monster thunder HZV; ≈51 dmg on dummy, 57 with Thunder Attack 3). Datamine `[60, 15, 20]`; `_DischargeData` contains `... 30, 70, 60, 200, 100, 30`. | Build-up from landed hits; hit count is weapon-dependent (no public table). "Excludes some attacks" – e.g. LS Helm Breaker does not build it, DB Blade Dance does (gamerant). Damage is NOT affected by weapon attack; it IS affected by Thunder Attack skill. | Affinity **20 s**; internal cooldown ≈ **30 s** between bursts (game8, Wycademy; last `_DischargeData` entry = 30). The leading `60` in the per-level datamine is unexplained (possibly build-up threshold). | game8 532643 / 532644; kiranico leviathans-fury; gamerant; Wycademy; `_DischargeData` | high (affinity, damage) / medium (cooldown) / low (hits) |
| II (4 pc) | **+15 % affinity** for 30 s. Burst = **60 fixed + 200 thunder** (≈120 on dummy, 133.8 with Thunder Attack 3). Datamine `[60, 15, 30]`. | as above | Affinity **30 s**; cooldown ≈ 30 s → max uptime ≈ 100 % only if you re-trigger exactly on cooldown; realistic ≈ 50–75 %. | same | high |

### Seregios's Tenacity → Razor's Edge I / II (requires the Adrenaline Rush skill)

| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| I (2 pc) | Adrenaline Rush duration **30 s → 45 s**; a second perfect dodge while active extends it **once by +15 s** (max 60 s). No attack change (datamine `[15, 100]` = +15 s, ×1.00). | Perfect dodge (Adrenaline Rush trigger). | up to 60 s per activation; refreshable by dodging again after it expires. | game8 532651/532652; kiranico `15/100`; Wycademy | high |
| II (4 pc) | On the second (extending) dodge, raw is additionally multiplied by **×1.05 of base raw** (datamine `[15, 105]`). Game8's text says "3–4 %", Wycademy "4 %", but game8's own table fits ×1.05 exactly: Zakun Twins I 196 → 226 (AR Lv5 +30) → 235 (= 196×1.05 + 30 = 235.8, truncated); Zoh Mikal 216 → 246 → 256; Blazing Yirmiya 226 → 256 → 267. | Second perfect dodge while Adrenaline Rush is active. | For the remaining extended window (≤ 60 s total). | kiranico `15/105`; game8 table; Wycademy | high (×1.05) |

Adrenaline Rush itself (for reference, datamine): Lv1–5 = +10/+15/+20/+25/+30 true raw, 30 s.

### Omega Resonance → Resonance I / II

| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| I (2 pc) | **Local: +20 % affinity**; **Remote: +10 true raw** (datamine `[20, 10]`). | A random mode is chosen at quest start and the active mode **alternates every 90 s** (`_ResonanceData[0] = 90`), cannot be changed manually. Local = an ally is inside the purple ring around you; Remote = an ally is outside the red ring. Support Hunters count (game8). Distance is checked every 0.5 s (`_ResonanceDistCheckTime`). | Buff "lasts until you leave combat / the area" once triggered (vortexgaming). `_ResonanceData = [90, 4, 7, 30, 3]`: 4 / 7 are probably the two ring radii and 30 a linger time (s) – unverified. | game8 553791; kiranico `20/10`; vortexgaming 558198; Fextralife; `_ResonanceData` | high (values) / low (ring radii, solo behaviour) |
| II (4 pc) | **Local: +40 % affinity**; **Remote: +20 true raw** (`[40, 20]`). Also upgrades the Omega weapon skills: Synthetic Shield +30 def / 30 s → **+45 def / 45 s**; Synergy +15 % aff / 45 s → **+25 % affinity / 60 s** (radius 30; `_ShieldOptionValue_Resonance 45`, `_CooperationValue_Resonance 25`, `_CooperationTime_Resonance 60`). | as above | as above | same + `_ShieldOption*` / `_Cooperation*` | high |

Solo caveat: no source documents whether Palico / Seikret satisfy the ring checks; Wycademy labels Remote as the "solo" mode, implying "no ally inside the red ring" may count, but that is unconfirmed. Treat solo uptime as unknown.

### Gogmapocalypse → Mutual Hostility I / II

| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| I (2 pc) | **Element × 1.20 + 2 true (= "1.2x + 20" displayed)**. Datamine `[120, 2]`. Game8 examples: 300 → 380, 600 → 740 displayed. | Large monster enraged (same trigger as Agitator; `_ChallengerAttr_MaxLagTimer 3.0` s delay). | Whole enrage window. | game8 571423; Fextralife Mutual Hostility I/II; datamine (kiranico page empty) | high |
| II (4 pc) | **Element × 1.30 + 4 true (= "1.3x + 40")** (`[130, 4]`; DB example 300 → 430). Plus a damage **barrier of 80 HP** (`_ChallengerAttr_BarrierVital 80`; game8 measured ≈75, Wycademy 78–80). | Barrier granted when the monster enrages. | Barrier lifetime **140 s** (= 2 min 20 s, `_ChallengerAttr_BarrierLifeTimeSec`) if not broken; cooldown **30 s** after it ends (`_ChallengerAttr_BarrierCoolTimeSec`) → re-grant ≈ 170 s after activation (Wycademy "2 min 50 s"; game8 says "≈2 min"). | same + `_ChallengerAttr_*` | high |

### Soul of the Dark Knight → Dark Arts (2 pc) / The Blackest Night (4 pc)

| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| Dark Arts (2 pc) | **Element × 1.20 for heavy weapons (GS, Hammer, HBG, LBG) and × 1.14 for all other weapons** (datamine `[120, 114]`; Wycademy "+20 % / +14 %"). Game8 only quotes "20 %". **Great Sword only:** every Lv3 charged slash (except Charged/Offset Rising Slash) gets an extra shockwave: **30 MV, 60 dragon element**, can crit, uses HZV and sharpness modifier, consumes no sharpness, slightly longer reach (Wycademy). | Having recoverable (red) health – same condition as Resentment. | While red health exists. | game8 553790; kiranico `120/114`; Wycademy; Fextralife | high (multipliers) / medium (which weapons get 1.20) / low (shockwave numbers) |
| The Blackest Night (4 pc) | Special action (item bar / radial): consumes HP → shield for **8 s** (datamine `[8, 6, 10]`; Wycademy: shield = 33 % max HP). Follow-up attack "Edge of Shadow". If the barrier is broken the stronger "Eventide" fires, converts the whole health bar to red and grants **Undead Redemption 30 s**: red health cannot drop and **base defense × 2**. | Manual activation. | Cooldown **≈70 s** (game8, Wycademy) or 75 s (Fextralife). Unlabeled datamine `_DarkBladeData [1.1, 30, 45, 20]` and `_KnightData [21, 12, 12]` likely belong here but are uninterpreted. | game8; Fextralife The_Blackest_Night; Wycademy; datamine | medium |

### Nu Udra's Mutiny → Bad Blood I / II (requires the Resentment skill)

| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| I (2 pc) | Extra hit of **45 × (hitzone %)** damage. Dummy test: 45 (wound) / 36 (weak point, HZV 80) / 9 (hard part, HZV 20). `_ViolentData = [2, 45, 85]`. Wycademy: "45 % of raw HZV". Not affected by weapon type (game8). | Resentment active (red health present) and landing a hit. | **2 s cooldown** between procs (`_ViolentData[0] = 2`; game8 "2–3 s"). | game8 501810; `_ViolentData`; Wycademy; kiranico (`23`, see note) | high |
| II (4 pc) | **85 × (hitzone %)**: 85 / 68 / 17 on the dummy. Wycademy says 80 %. | as above | 2 s cooldown | same | high (85) |

Note: the per-level `SkillData._value` for Nu Udra's Mutiny is `23` / `50`, which does not match any tested number; the common-effect table `_ViolentData [2, 45, 85]` matches game8's dummy tests exactly, so use 45 / 85. Whether the hit scales with weapon raw is untested (game8 says "flat regardless of weapon type").

### Doshaguma's Might → Powerhouse I / II

| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| I (2 pc) | **+10 true raw** (`[10, 180]`) | Successful Power Clash or Offset attack (offset alone suffices, no follow-up needed). | **180 s**, refreshed on each trigger; stacks with other buffs. | game8 501805/501678; kiranico `10/180`; Wycademy | high |
| II (4 pc) | **+25 true raw** (`[25, 180]`) | as above | 180 s | same | high |

### Ebony Odogaron's Power → Burst Boost I / II (requires the Burst skill)

| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| I (2 pc) | **+8 true raw while Burst is active**; Burst duration **+1 s** (`[1, 8]`). | Burst active (5 hits). | While Burst lasts. | game8 501808/501683; kiranico `1/8`; Wycademy | high |
| II (4 pc) | **+18 true raw**; Burst duration **+2 s** (`[2, 18]`). Resulting Burst duration with II: GS/HH/GL 7 s, DB & ranged 5 s, all others 6 s. | as above | as above | same | high |

### Xu Wu's Vigor → Protein Fiend I / II

| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| I (2 pc) | **+15 true raw** (`[180, 15]`) | Eating Well-done Steak or Burnt Meat (also Rations per description). | **180 s**. Does not stack with Might Seed / Might Pill – highest value wins. | game8 501958; kiranico `180/15`; Wycademy | high |
| II (4 pc) | **+30 true raw** (`[180, 30]`) | as above | 180 s | same | high |

### Jin Dahaad's Revolt → Binding Counter I / II

| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| I (2 pc) | **+25 true raw** (`[90, 25]`) | Recovering from webbed status, frostblight, being pinned, or finishing a Power Clash. | **90 s** (raised from 60 s in TU4); refreshes on re-trigger. | game8 502176; kiranico `90/25`; Wycademy | high |
| II (4 pc) | **+50 true raw** (`[90, 50]`) | as above | 90 s | same | high |

### Blangonga's Spirit → War Cry I / II

| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| I (2 pc) | Hunters (you + nearby hunters): **+3 true raw** (game8 test; `_MoraleData = [20, 3, 6]`). Datamine per-level `[90, 10]` → the **10** is most likely the Palico / Support Hunter value ("greatly affects Palicoes and Support Hunters"). | Use the "To Victory!" gesture near allies. | Duration **90 s** (datamine `[90, …]`, Wycademy) vs **60 s** (game8). `_MoraleData[0] = 20` may be range or hunter-duration – unknown. | game8 502174; kiranico `90/10`; Wycademy; `_MoraleData` | high (+3/+6) / medium (duration) / low (10/20 = Palico) |
| II (4 pc) | Hunters **+6 true raw**; datamine `[90, 20]`. | as above | as above | same | same |

### Rathalos's Flare → Scorcher I / II

| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| I (2 pc) | Extra fire hit: **20 fixed + 60 × (fire HZV %)** (TU4 values; dummy front HZV 30 → 38 dmg, back HZV 5 → 23). `_ScorchingHeatData` ends with `20, 60, 40, 120`. With Zoh Shia Whiteflame Torrent weapons: 60 fixed + 70 fire (81 / 63.5). | **33 % chance per landed hit** (`SkillData [33, …]`; Steam thread "1/3"), evaluated on a **≈2.4–2.5 s check interval** (`_ScorchingHeatData[0] = 2.4`; Wycademy "every 2.5 / 5 / 7.5 s"). Same attack exclusions as Flayer. | Permanent passive. Expected rate ≈ one proc per 7.5 s of continuous attacking. | game8 501809/501682; kiranico `33/24`; `_ScorchingHeatData`; Steam 798965065596484219; Wycademy | high (chance, 20+60 / 40+120) / medium (interval) |
| II (4 pc) | **40 fixed + 120 × (fire HZV %)** (76 front / 46 back on dummy). With Whiteflame Torrent: 80 fixed + 140 fire (122 / 87). | as above | as above | same | high |

Note: the second per-level datamine value (24 / 60) does not match the TU4 numbers; `_ScorchingHeatData = [2.4, 50, 100, 20, 24, 30, 60, 20, 60, 40, 120]` contains both the old-looking 24/60 and the current 20/60 and 40/120 pairs; game8 reports TU4 added the fixed component and ≈+20 fire. Use 20+60 / 40+120.

### Rey Dau's Voltage → Thunderous Roar I / II (requires Latent Power)

| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| I (2 pc) | Latent Power active time **+30 s** (2:00 → 2:30). Datamine `[30]`. Latent Power values themselves unchanged (Lv1–5 affinity +10/20/30/40/50 %, stamina use −30 %). | Latent Power triggers (damage taken / time). | — | game8 501679; kiranico `30`; Wycademy | high |
| II (4 pc) | **+90 s** (2:00 → 3:30). `[90]`. | as above | — | same | high |

### Festival Prayers (Blossomdance / Flamefete / Dreamspell / Lumenhymn) → Boon I / II

| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| I (2 pc) | Special items added to quest rewards only (datamine `[100]` = ×1.00 attack). | Only while the matching seasonal festival is running; no effect when joining mid-quest. | Whole quest. | game8 514832 / 536430 / 559839 / 572040; kiranico (`100`, `109/50`); dtlnor dump for Lumenhymn | high |
| II (4 pc) | **Attack × 1.09** (of base raw) and **+50 defense** (datamine `[109, 50]`; game8 "9 % Attack and 50 Defense"). Identical for all four Prayers. | as above | Whole quest, but only during the festival window → zero uptime outside events. | same | high |

---

## A2. Non-damage set bonuses (for completeness)

### Fulgur Anjanath's Will → Second Wind I / II
| tier | numeric effect | condition | duration | source | confidence |
|---|---|---|---|---|---|
| I | Additional stamina gauge of **+25** (`[25]`) | passive | permanent | game8 501957; kiranico | high |
| II | Additional long gauge of **+50** (`[50]`) | passive | permanent | same | high |

### Guardian Arkveld's Vitality → Decimator I / II
| tier | numeric effect | condition | duration | source | confidence |
|---|---|---|---|---|---|
| I | Heal **25 HP** (`[25]`) | destroying a wound on a large monster; not shared by Wide-Range | instant | game8 501684; kiranico | high |
| II | Heal **50 HP** (`[50]`) | as above | instant | same | high |

### Arkveld's Hunger → Hasten Recovery I / II
| tier | numeric effect | condition | duration | source | confidence |
|---|---|---|---|---|---|
| I | Heal per trigger by weapon class: **3 HP** (LBG/HBG/Bow), **6 HP** (LS, SnS, DB, Lance, SA, CB, IG), **14 HP** (GS, Hammer, HH, GL) – datamine `[3, 6, 14]`; game8 says 15 for the heavy group. Boosted by Recovery Up (+10/20/30 %). | ≈5 hits inside a 3–5 s window, internal 0.5–1 s between counted hits (community) | repeatable | game8 502180; kiranico `3/6/14` | high (3/6) / medium (14 vs 15) |
| II | **5 / 10 / 20 HP** (`[5, 10, 20]`) | as above | repeatable | same | high |

### Zoh Shia's Pulse → Super Recovery I / II
| tier | numeric effect | condition | duration | source | confidence |
|---|---|---|---|---|---|
| I | **1 HP every 2 s** up to max HP even without red health (`_AutoRecoverData [1, 2, 1]`) | passive | permanent | gamerant Zoh Shia's Pulse; Wycademy; datamine | high |
| II | **1 HP every 1 s** | passive; works with Recovery Up, not Recovery Speed | permanent | same | high |

### Gravios's Protection → Flawless Armor I / II
| tier | numeric effect | condition | duration | source | confidence |
|---|---|---|---|---|---|
| I | Damage taken **× 0.80** (−20 %) (`[80]`) | health full | while at full HP | game8 502177; kiranico `80` | high |
| II | Damage taken **× 0.65** (−35 %) (`[65]`) | health full | while at full HP | same | high |

### Mizutsune's Prowess → Bubbly Dance I / II
| tier | numeric effect | condition | duration | source | confidence |
|---|---|---|---|---|---|
| I | Immune to major bubbleblight; after **3 evades** you get minor bubbleblight (evade-window bonus of the ailment) | evading | minor bubbleblight **30 s** | game8 512845; Wycademy | high |
| II | Additionally, while bubbleblighted, i-frames equal to **Evade Window Lv2** (11 f @30 fps / 19 f @60 fps vs 9 / 15 base); stacks with Evade Window up to the Lv5 cap | as above | 30 s per activation | same | medium |

### Jin Dahaad / Uth Duna's Cover → Protective Veil I / II
| tier | numeric effect | condition | duration | source | confidence |
|---|---|---|---|---|---|
| I | **+25 defense** (`[25]`) | using a mantle (specialized tool) | **180 s** | game8 501680; kiranico `25` | high |
| II | **+50 defense, +12 all resistances** (`[50, 12]`) | as above | 180 s | same | high |

---

# PART B – GROUP SKILLS (3 pieces, any sets)

## B1. Damage-relevant group skills

### Lord's Soul → Guts (Tenacity)

| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| 3 pc, before trigger | **Attack × 1.05 of weapon base raw**, **defense × 0.95** (datamine `[105, 95, 105, 3]`). | Always on until Guts fires. | Whole quest until you would die. Lost on fainting. | game8 514822/514833; kiranico `105/95/105/3`; Fextralife Lord's Soul | high |
| 3 pc, on trigger | Survive one lethal hit (left at 1 HP) if HP was **above the threshold of 64 HP** (`_Guts_ActiveRemainHealth = 64`; Wycademy "at least 65 HP"; a Steam user guessed "35–40 %", i.e. the yellow line – on 150–160 max HP 64 HP is ≈40 %). Multi-hit / continuous damage can still kill. Afterwards the attack bonus ends and you get **defense × 1.05, +3 all resistances**. | One lethal hit per quest. Cat revive / food Moxie trigger first. | Post-trigger state for the rest of the quest. | `_Guts_ActiveRemainHealth`; Wycademy; Steam 755052131309997493 | high (values) / medium (threshold is flat 64 HP) |

### Lord's Fury → Resuscitate
| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| 3 pc | **+10 true raw** (`[10]`) | While afflicted by: poison, paralysis, sleep, stun, webbed, bleeding, fire/water/thunder/ice/dragon-blight, frostblight, minor or major bubbleblight. **Not Frenzy** since 1.000.05.00. | While the ailment lasts (removed when cured). Self-inflicted uptime via Mizutsune's Bubbly Dance (minor bubbleblight 30 s per 3 evades). | game8 502169/502181; kiranico `10`; Wycademy | high |

### Lord's Favor → Inspiration
| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| 3 pc | **+10 true raw** (`[20, 10]`) | Using any item or melody that affects the party: Lifepowder, Dust of Life, Herbal/Demon/Hardshell Powder, Wide-Range potions/mushrooms, most Hunting Horn melodies. | **20 s**; does not stack with itself, re-trigger resets the timer. | game8 501801; kiranico `20/10`; Wycademy | high |

### Fortifying Pelt → Fortify
| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| 3 pc | 1st faint: **attack × 1.10, defense × 1.15** (`[110, 115]`). 2nd faint: **+20 % attack, +30 % defense** (game8; additive stacking). Max 2 stacks. | Fainting (carting) during the quest. Buff is removed if you swap to armor without the group skill, restored when swapped back. | Rest of the quest. | game8 501818/501799; kiranico `110/115`; Wycademy | high (1st) / medium (2nd = 20/30 vs 21/32) |

### Buttery Leathercraft → Affinity Sliding
| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| 3 pc | **+30 % affinity** (`[60, 30]`) | Sliding down terrain for at least **1.0 s** (`_SlidingPowerUpBeginTime`). | **60 s** (datamine, game8, Wycademy; one wiki page says 30 s). | game8 501984; kiranico `60/30`; Fextralife | high |

### Master of the Fist → Satsui no Hado (Akuma α set only)
| tier | numeric effect | condition / trigger | duration / uptime | source | confidence |
|---|---|---|---|---|---|
| 3 pc | Akuma special-move motion values **+47 %**; special moves also deal stun; Drive Impact can perform an Offset; a successful Drive-Impact Offset gives **+5 % damage during the Drive Impact reload** (Wycademy only). Datamine `SkillData` is `[0,0,0,0]` – no per-level params; unlabeled `_WpOffData [1.1, 1.2, 1.4]` may be offset multipliers but is unassigned. | Using the Akuma special actions / Drive Impact. | — | Wycademy; game8 523976 (text only) | low |

## B2. Non-damage group skills

| group skill → skill | numeric effect | condition | duration | source | confidence |
|---|---|---|---|---|---|
| Scale Layering → Adrenaline | Stamina consumption **× 0.5** while HP ≤ 40 % (`_Hunki_ActiveHP 0.4`, `_Hunki_StamitaRate 0.5`; game8 "50 %"). Per-level datamine `[90]` is probably a **90 s** duration ("temporarily"); Wycademy's "10 %" reading of that 90 is contradicted by the common-effect table. | HP at or below 40 % | up to 90 s (?) , no cooldown (`_Hunki_MaxCoolTime 0`) | game8 501985; kiranico `90`; datamine | medium |
| Guardian's Protection → Ward of Wyveria | Elemental damage **× 0.90** and "unique" damage **× 0.90** (`[90, 90]`; Wycademy "10 %") | inside the Ruins of Wyveria | permanent there | kiranico; Wycademy; game8 501954 (no number) | high |
| Guardian's Pulse → Wylk Burst | Datamine `[400, 200]` → stamina recovery ×4 (?) and red-health recovery ×2 (?). Game8 says "+50 % each", Wycademy "doubled". | near Wylkrystals (created by Guardian monsters) | while nearby | kiranico; game8 501814; Wycademy | low |
| Glory's Favor → Luck | **+2 quest target rewards** (no datamine params) | quest completion, not when joining mid-quest | — | game8 512887 | medium |
| Festival Spirit → Carving Master | **+1 carve** per carcass (datamine row `[105, 95, 105, 3]` is a copy of Lord's Soul – ignore) | not when joining mid-quest | — | game8 514831 | high |
| Flexible Leathercraft → Master Gatherer | Gathering speed **× 1.4** (`[140]`), no knockback while gathering/carving | — | permanent | kiranico; Wycademy | high |
| Imparted Wisdom → Forager's Luck | Rare gathering-point chance parameter **25** (likely +25 %) | — | permanent | kiranico `25` | low |
| Alluring Pelt → Diversion | Threat (aggro) generated by your attacks **× 2** (`[200]`) | attacking | permanent | kiranico; Wycademy | medium |
| Scaling Prowess → Master Mounter | `[120, 150]` → mount build-up ×1.2 and mounted wound damage ×1.5 (Wycademy "20 % riding dmg, 50 % wound dmg while riding") | mounting | permanent | kiranico; Wycademy | medium |
| Neopteron Camouflage → Fleetfoot | `[160, 120]` → crouch move speed ×1.6 (Wycademy says +40 %), detection-escape ×1.2 | crouching | permanent | kiranico; Wycademy | low |
| Neopteron Alert → Honey Hunter | **+1 to +3 honey** per gathering point (`[1, 3]`) | gathering honey | permanent | kiranico; Wycademy | high |

---

# Quick reference for the optimizer (damage terms only)

| skill | tier | raw add | raw mult (base) | affinity | element | uptime driver |
|---|---|---|---|---|---|---|
| Black Eclipse | I | 0 | – | +15 % (60 s after overcoming; +18/21/25 % w/ Antivirus 1/2/3) | – | frenzy cycle |
| Black Eclipse | II | +10 infected / +15 overcome | – | same | – | frenzy cycle |
| Azure Bolt | I / II | 0 | – | +15 % (20 s / 30 s) | burst 30+70 / 60+200 thunder, ~30 s ICD | hit build-up |
| Razor's Edge | I / II | 0 | ×1.05 (II, 2nd dodge) | – | – | Adrenaline Rush 45–60 s |
| Resonance | I / II | +10 / +20 (Remote) | – | +20 % / +40 % (Local) | – | 90 s alternation, ally position |
| Mutual Hostility | I / II | 0 | – | – | ×1.20 + 2 / ×1.30 + 4 true | monster enraged |
| Dark Arts | 2 pc | 0 | – | – | ×1.20 (GS/Ham/HBG/LBG) or ×1.14 others | red health |
| Bad Blood | I / II | fixed hit 45 / 85 × HZV, 2 s ICD | – | – | – | red health (Resentment) |
| Powerhouse | I / II | +10 / +25 | – | – | – | 180 s after clash/offset |
| Burst Boost | I / II | +8 / +18 (while Burst) | – | – | – | Burst uptime (+1 / +2 s) |
| Protein Fiend | I / II | +15 / +30 | – | – | – | 180 s after meat |
| Binding Counter | I / II | +25 / +50 | – | – | – | 90 s after bind recovery |
| War Cry | I / II | +3 / +6 | – | – | – | 60–90 s after gesture |
| Scorcher | I / II | fixed hit 20+60 / 40+120 fire-HZV, 33 %/2.4 s | – | – | – | passive |
| Thunderous Roar | I / II | – | – | Latent Power +30 s / +90 s | – | Latent Power |
| Festival Boon | II | 0 | ×1.09 | – | – | festival only |
| Guts (Lord's Soul) | 3 pc | 0 | ×1.05 (until trigger) | – | – | until lethal hit |
| Resuscitate | 3 pc | +10 | – | – | – | while afflicted |
| Inspiration | 3 pc | +10 | – | – | – | 20 s after party item/melody |
| Fortify | 3 pc | 0 | ×1.10 / ×1.20 | – | – | per faint |
| Affinity Sliding | 3 pc | 0 | – | +30 % (60 s) | – | after 1 s slide |
| Satsui no Hado | 3 pc | – | special moves MV +47 % | – | – | Akuma set only |

# Not found / unresolved

* Azure Bolt: per-weapon hit counts to trigger; meaning of the leading `60` in `[60, 15, 20]`.
* Resonance: ring radii (probably 4 and 7 in `_ResonanceData`), exact buff linger time, and whether Palico/Seikret count in solo.
* War Cry: whether the datamine 10/20 is the Palico/Support-Hunter value; 60 s vs 90 s duration.
* Dark Arts: shockwave MV (30) and dragon element (60) come from a single source; `_KnightData [21, 12, 12]` and `_DarkBladeData [1.1, 30, 45, 20]` are unassigned.
* Blackest Night: shield size (Wycademy 33 % max HP vs datamine `[8, 6, 10]` – 8 s confirmed, 6 and 10 unknown).
* Bad Blood: whether 45/85 scale with weapon raw (game8 says no).
* Satsui no Hado: only one source for +47 % / +5 %.
* Wylk Burst, Fleetfoot, Forager's Luck: multiplier interpretation of the datamine values is guessed.

# Sources

Datamine / database
* https://mhwilds.kiranico.com/data/skills (index; per-skill pages: /seregioss-tenacity, /gore-magalas-tyranny, /leviathans-fury, /omega-resonance, /soul-of-the-dark-knight, /nu-udras-mutiny, /lords-soul, /lords-fury, /lords-favor, /fortifying-pelt, /buttery-leathercraft, /doshagumas-might, /ebony-odogarons-power, /xu-wus-vigor, /blangongas-spirit, /rathaloss-flare, /jin-dahaads-revolt, /blossomdance-prayer, /flamefete-prayer, /dreamspell-prayer, /fulgur-anjanaths-will, /guardian-arkvelds-vitality, /arkvelds-hunger, /gravioss-protection, /rey-daus-voltage, /uth-dunas-cover, /guardians-protection, /scale-layering, /flexible-leathercraft, /imparted-wisdom, /guardians-pulse, /alluring-pelt, /scaling-prowess, /neopteron-camouflage, /neopteron-alert, /festival-spirit, /master-of-the-fist)
* Local: notes/skill_params_datamine.md (dtlnor MHWs-in-json SkillData._value), notes/player_skill_param_extract.json (PlayerSkillCommonEffectSetting)

game8
* https://game8.co/games/Monster-Hunter-Wilds/archives/482548 (set bonus list)
* https://game8.co/games/Monster-Hunter-Wilds/archives/482547 (group skill list)
* https://game8.co/games/Monster-Hunter-Wilds/archives/502173 (Black Eclipse)
* https://game8.co/games/Monster-Hunter-Wilds/archives/532643 and /532644 (Azure Bolt / Leviathan's Fury)
* https://game8.co/games/Monster-Hunter-Wilds/archives/532651 and /532652 (Seregios's Tenacity / Razor's Edge)
* https://game8.co/games/Monster-Hunter-Wilds/archives/553791 (Omega Resonance)
* https://game8.co/games/Monster-Hunter-Wilds/archives/571423 (Gogmapocalypse)
* https://game8.co/games/Monster-Hunter-Wilds/archives/553790 (Soul of the Dark Knight)
* https://game8.co/games/Monster-Hunter-Wilds/archives/501810 and /501681 (Bad Blood / Nu Udra's Mutiny)
* https://game8.co/games/Monster-Hunter-Wilds/archives/501805 and /501678 (Powerhouse)
* https://game8.co/games/Monster-Hunter-Wilds/archives/501808 and /501683 (Burst Boost)
* https://game8.co/games/Monster-Hunter-Wilds/archives/501958 (Protein Fiend)
* https://game8.co/games/Monster-Hunter-Wilds/archives/502176 (Binding Counter)
* https://game8.co/games/Monster-Hunter-Wilds/archives/502174 and /502178 (War Cry)
* https://game8.co/games/Monster-Hunter-Wilds/archives/501809 and /501682 (Scorcher)
* https://game8.co/games/Monster-Hunter-Wilds/archives/501679 (Thunderous Roar)
* https://game8.co/games/Monster-Hunter-Wilds/archives/514832, /536430, /559839, /572040 (Blossomdance / Flamefete / Dreamspell / Lumenhymn Boon)
* https://game8.co/games/Monster-Hunter-Wilds/archives/501957 (Second Wind), /501684 (Decimator), /502180 (Hasten Recovery), /512846 and /512844 (Super Recovery), /502177 (Flawless Armor), /512845 (Bubbly Dance), /501680 (Protective Veil)
* https://game8.co/games/Monster-Hunter-Wilds/archives/514822 and /514833 (Guts / Lord's Soul)
* https://game8.co/games/Monster-Hunter-Wilds/archives/502169 and /502181 (Resuscitate / Lord's Fury)
* https://game8.co/games/Monster-Hunter-Wilds/archives/501801 (Inspiration / Lord's Favor)
* https://game8.co/games/Monster-Hunter-Wilds/archives/501818 and /501799 (Fortify)
* https://game8.co/games/Monster-Hunter-Wilds/archives/501984 (Affinity Sliding)
* https://game8.co/games/Monster-Hunter-Wilds/archives/523976 (Master of the Fist)
* https://game8.co/games/Monster-Hunter-Wilds/archives/501985 (Adrenaline), /501954 (Ward of Wyveria), /512887 (Luck), /501814 (Wylk Burst), /514831 (Festival Spirit)

Community compilations / wikis
* https://lescarnetsdelawycademie.fr/set-skills-wilds/ (Wycademy notebooks – full set/group value table)
* https://monsterhunterwilds.wiki.fextralife.com/Set_Bonus_Skills , /Group_Skills , /Black_Eclipse_II , /Omega_Resonance , /Mutual_Hostility_II , /Dark_Arts , /The_Blackest_Night , /Lord%27s_Soul , /Fortifying_Pelt , /Scorcher_I
* https://monsterhunterwiki.org/wiki/Seregios's_Tenacity_(MHWilds) and sibling pages (descriptions only, no numbers)
* https://gamerant.com/how-does-leviathans-fury-azure-bolt-work-mh-monster-hunter-wilds/
* https://gamerant.com/monster-hunter-mh-wilds-how-does-super-recovery-work-zoh-shia-pulse/
* https://gamerant.com/how-does-razors-edge-seregios-tenacity-set-bonus-work-mh-monster-hunter-wilds/
* https://vortexgaming.io/en/postdetail/558198 (Omega Resonance analysis)
* https://deltiasgaming.com/monster-hunter-wilds-gogmapocalypse-skill-explained/
* https://steamcommunity.com/app/2246340/discussions/0/798965065596484219/ (Scorcher vs Flayer, 1/3 chance)
* https://steamcommunity.com/app/2246340/discussions/0/755052131309997493/ (Guts threshold discussion)
* https://steamcommunity.com/sharedfiles/filedetails/?id=3453486109 (DB guide; Black Eclipse ≈45 s claim)
