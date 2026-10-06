# Monster Hunter Wilds (Ver 1.041, Feb 2026) — numeric values for vaguely described skills

Compiled 2026-10-06 for the build-optimizer damage calculator.

**How to read the "source" column.** The single most valuable primary source turned out to be
`mhwilds.kiranico.com/data/skills/<slug>` (site reports Game Ver. 1.040): every skill level there
carries the raw datamined parameter fields (e.g. Convert Element Lv1 = `8/110/150/60`). Those raw
fields are quoted verbatim as "kiranico raw". Where the meaning of a field is an inference rather
than stated by the site, it is marked *(interpretation)*. Game8 (EN and JP), GameWith JP, gamepedia.jp
and monsterhunterwiki.org supply community-tested values. Sites that returned 403/404 to every
method (icy-veins, mobalytics, gamewith.net, altema.jp) are noted where their claims surfaced in
search snippets only (confidence low).

Confidence: **high** = datamined field + at least one independent test agrees; **medium** = one
good source or datamined field with inferred meaning; **low** = single secondary source / unverified.

---

## 0. Global conventions needed by the calculator

### 0.1 Element display vs true value

| item | value | source | confidence |
|---|---|---|---|
| Displayed element = true element × 10 | Elemental formula uses `(Elem. Attack / 10)`; kiranico Dragon Attack Lv1 raw field `100/4` is shown in-game as "Dragon attack +40", Lv2 `110/5` = "+10% Bonus: +50", Lv3 `120/6` = "+20% Bonus: +60" | game8.co/archives/499631, game8.co/archives/500260, kiranico dragon-attack | high |

All "+N element" numbers in this file are **display** units unless stated otherwise.

### 0.2 Element cap (display units)

| state | formula | breakpoint | source | confidence |
|---|---|---|---|---|
| Post-TU4 (Ver 1.040+, current) | cap = max(base + 400, base × 2.3) | base ≥ ~308–310 → 2.3× governs | game8.co/archives/500260 ("adding 400 … or multiplying it by 2.3x, then selecting the larger"); corroborated by switchbladegaming + wildsbuilder (secondary) | high |
| Pre-TU4 | cap = max(base + 350, base × 1.9) | base ≥ 389 → 1.9× governs | game8.co/archives/500260 | high |
| Patch note wording | "Increased the max elemental values possible for the player's status." | — | rpgsite TU4 patch notes (Ver 1.040) | high |

Example: base 400 → old cap 760, new cap 920.

### 0.3 Sharpness modifiers

| colour | raw | element | source | confidence |
|---|---|---|---|---|
| Red | 0.50 | 0.25 | game8.co/archives/500339, Fextralife Sharpness | high |
| Orange | 0.75 | 0.50 | same | high |
| Yellow | 1.00 | 0.75 | same | high |
| Green | 1.05 | 1.00 | same | high |
| Blue | 1.20 | **1.0625** (game8 rounds to 1.063) | same (+ wildsbuilder 1.0625) | high |
| White | 1.32 | 1.15 | same | high |
| Purple | 1.39 | 1.27 | game8 formula page lists it, but purple sharpness does not exist on Wilds weapons | n/a |

Game8: "The sharpness damage multiplier in Monster Hunter Wilds has not changed from Rise and World."
So blue element = 1.0625, not 1.05.

### 0.4 Crit multiplier, Critical Boost, Attack Boost

| skill / item | Lv | numeric effect | source | confidence |
|---|---|---|---|---|
| Base critical hit | — | ×1.25 raw (negative affinity ×0.75) | game8.co/archives/499631, 501570 | high |
| Critical Boost | 1–5 | ×1.28 / 1.31 / 1.34 / 1.37 / 1.40 (kiranico raw `128…140`) | kiranico critical-boost, game8 501570 | high |
| Attack Boost | 1–3 | +3 / +5 / +7 flat (raw `100/3`, `100/5`, `100/7`) | kiranico attack-boost | high |
| Attack Boost | 4 | ×1.02 then +8 (raw `102/8`) | kiranico | high |
| Attack Boost | 5 | ×1.04 then +9 (raw `104/9`) | kiranico | high |
| Critical Eye | 1–5 | +4/8/12/16/20 % affinity | kiranico | high |
| Critical Draw | 1–3 | +50/75/100 % affinity on draw attacks | kiranico | high |
| Offensive Guard | 1–3 | ×1.05 / 1.10 / 1.15 attack (raw `105/110/115`) | kiranico | high |

---

## 1. Weapon skills

### 1.1 Critical Element

Multiplier applied to the elemental part of a hit **when it crits**. Weapon-dependent. Kiranico shows no raw field for this skill (per-weapon table lives elsewhere), so values are community-tested.

| level | fast/light group | heavy group | condition | duration | source | confidence |
|---|---|---|---|---|---|---|
| 1 | ×1.05 | ×~1.067 ("約1.07") | on critical hit | — | game8.co/archives/501573, game8.jp/mhwilds/672001, gamewith.jp/mhwilds/486605 | high (values) |
| 2 | ×1.10 | ×~1.133 ("約1.13") | " | — | same | high |
| 3 | ×1.15 | ×1.20 | " | — | same | high |

Group membership (sources disagree on four weapons):

| weapon | game8.co + game8.jp | gamewith.jp |
|---|---|---|
| Great Sword, Hammer, Hunting Horn | heavy (1.2) | heavy (1.2) |
| Gunlance, Switch Axe, Charge Blade | **heavy (1.2)** | **fast (1.15)** |
| Heavy Bowgun | **fast (1.15)** | **heavy (1.2)** |
| Long Sword, SnS, Dual Blades, Lance, Insect Glaive, Light Bowgun, Bow | fast (1.15) | fast (1.15) |

Recommendation: use game8 grouping (two independent game8 teams agree); flag GL/SA/CB/HBG as medium.
Rejected: switchbladegaming (1.35/1.30/1.25/1.20) and wildsbuilder (GS/Hammer 1.5, LS/CB 1.35, DB/Bow 1.25) — these are Monster Hunter World-era numbers with no Wilds test data (low).

### 1.2 Critical Status

| level | kiranico raw | numeric effect *(interpretation)* | condition | source | confidence |
|---|---|---|---|---|---|
| 1 | `110/105` | status buildup ×1.10 (field A) / ×1.05 (field B) on crit | critical hit | kiranico critical-status | medium |
| 2 | `120/110` | ×1.20 / ×1.10 | " | kiranico | medium |
| 3 | `140/120` | ×1.40 / ×1.20 | " | kiranico | medium |

Which weapons use field A vs B is **not documented** (likely a fast/heavy split like Critical Element; wildsbuilder's "generally 1.4x on most melee" is low confidence). Game8 training-area test (100 % affinity): ranged Lv2 poison needed 15/13/12 hits at Lv1/2/3; hammer sleep 2/2/2 hits (game8.co/archives/501577). Game8 also notes the skill only boosts buildup on crits, not base status.

### 1.3 Charge Master

| level | kiranico raw | numeric effect *(interpretation)* | condition | source | confidence |
|---|---|---|---|---|---|
| 1 | `115/105/105/105` | element ×1.15 on charged attacks; status ×1.05 (3 further fields, likely weapon groups) | charged attacks only | kiranico charge-master | medium |
| 2 | `120/110/110/110` | element ×1.20; status ×1.10 | " | kiranico | medium |
| 3 | `125/115/115/115` | element ×1.25; status ×1.15 | " | kiranico | medium |

Game8 EN training-area table (game8.co/archives/501578) gives **different, per-weapon** element multipliers: GS 1.175/1.25/1.3, Hammer 1.15/1.2/1.25, IG 1.15/1.2/1.25, CB 1.15/1.2/1.25, SnS 1.05/1.10/1.15, Lance 1.2/1.25/1.3, Bow 1.005/1.015/1.020, Gunlance 1.01/1.015/1.0175. These look like observed damage ratios that include charge-level element modifiers, so treat as **low–medium**; the kiranico 1.15/1.20/1.25 field is the clean skill parameter. Affected moves (game8): GS charged slashes + charged offset; Hammer charged/mighty charge swings; Bow charged shots, Power Shot, Power Volley; IG descending slashes; SnS charged chop/slash; CB charged rising/double slash; Lance charge counter; GL charged shelling.

### 1.4 Mind's Eye

| level | kiranico raw | numeric effect | condition | source | confidence |
|---|---|---|---|---|---|
| 1 | `50/110` | 50 % no-deflect; ×1.10 damage vs hard parts | hitzone (base) ≤ 44 | kiranico minds-eye, game8.co/archives/501592 | high |
| 2 | `100/115` | 100 % no-deflect; ×1.15 | " | same | high |
| 3 | `100/130` | 100 % no-deflect; ×1.30 | " | same | high |

Hard-target rule (Steam thread 693120026285331190, training-area test + game8 comment): "mind's eye does nothing for hitzones of 45+"; it "will always take the **base** value of a hitzone" — a 40 part that becomes 100 when wounded still gets the bonus. Test: 40-HZ part 38→43 (no crit), 47→55 (crit) at Lv3. Confidence high.

### 1.5 Bludgeoner

| level | kiranico raw | numeric effect | condition | source | confidence |
|---|---|---|---|---|---|
| 1 | `105` | ×1.05 damage | sharpness yellow or lower | kiranico, game8 501595 | high |
| 2 | `110` | ×1.10 | yellow or lower | same | high |
| 3 | `110` | ×1.10 | **green** or lower | same | high |

### 1.6 Normal Shots / Piercing Shots / Spread-Power Shots

| skill | level | kiranico raw | numeric effect | applies to | source | confidence |
|---|---|---|---|---|---|---|
| Normal Shots | 1 | `105/105` | ×1.05 | Normal Ammo, normal arrows, Flying Swallow Shot | kiranico, game8 503080 | high |
| Piercing Shots | 1 | `105/105` | ×1.05 | Pierce ammo / Dragon Piercer-type arrows | kiranico | high |
| Spread-Power Shots | 1 | `105/105` | ×1.05 | Spread ammo / Power Shot arrows | kiranico | high |

(Two fields = bowgun ammo / bow arrows, both 1.05.)

### 1.7 Special Ammo Boost

| level | kiranico raw | numeric effect | applies to | source | confidence |
|---|---|---|---|---|---|
| 1 | `110/110` | ×1.10 | bowgun special ammo (Wyvern etc.); Bow Dragon Piercer, Thousand Dragons, Tracer | kiranico, game8 501584 | high |
| 2 | `120/120` | ×1.20 | " | same | high |

### 1.8 Opening Shot

| level | kiranico raw | numeric effect | condition | source | confidence |
|---|---|---|---|---|---|
| 1 | `5/5/110/110` | reload speed up; **+5 flat raw** on the first shot of a fully loaded magazine | first shot after full reload | kiranico, game8.co/archives/501583 | medium |
| 2 | `10/10/110/110` | +10 flat raw | " | same | medium |
| 3 | `15/15/110/110` | +15 flat raw | " | same | medium |

The trailing `110/110` fields are not explained by any source. A search snippet (pcgamesn/destructoid) claims "5/10/15 % reload + 2.5/5/7.5 % damage" — low confidence, unverified.

### 1.9 Ballistics

| level | numeric effect | source | confidence |
|---|---|---|---|
| 1 | range: bowgun +~1 Large Barrel Bomb; bow ×~1.15 | game8.co/archives/501559 | medium |
| 2 | range: bow ×~1.25; point-blank counts as critical range for Normal/Spread (since Ver 1.021) | same | medium |
| 3 | range: bowgun +~2 LBB; bow ×~1.40; **+~2 % raw** on shots fired inside critical range (added TU4: "Now also boosts the raw damage of Level 3 shots made at critical range") | game8 501559, rpgsite TU4 notes | medium |

Kiranico shows no raw field for Ballistics; the exact Lv3 multiplier (1.02?) is a game8 estimate.

### 1.10 Artillery

| level | kiranico raw | numeric effect | source | confidence |
|---|---|---|---|---|
| 1 | `5/120/3/110` | Shelling fire attack +30 display (true 3); CB impact phial +10 %; Sticky ammo +10 %; Wyvern's Fire faster | kiranico artillery, game8.co/archives/501585 | high (shell element, phial, sticky) |
| 2 | `10/150/6/120` | +60 display; phial +20 %; sticky +20 % | same | high |
| 3 | `15/200/9/130` | +90 display; phial +30 %; sticky +30 % | same | high |

Field mapping *(interpretation)*: field 3 = shelling fire element (×10 display); field 4 = ×1.10/1.20/1.30 explosive multiplier; fields 1 and 2 (`5/10/15`, `120/150/200`) are most likely Wyvern's Fire charge-speed % and another WF/wyrmstake factor — **not confirmed**. Game8 WF test at 160 raw: 76 → 80 → 84 → 89 (Lv0–3). Not affected: Switch Axe phials, Bow Tracer/Fuse, element-phial CB.

### 1.11 Focus

| level | kiranico raw | numeric effect | source | confidence |
|---|---|---|---|---|
| 1 | `105/95/1/105` | gauge fill ×1.05; charge time ×0.95 (−5 %) | kiranico focus, game8.co/archives/501564 | high |
| 2 | `110/90/2/107` | gauge fill ×1.10; charge time ×0.90 | same | high |
| 3 | `120/85/3/110` | gauge fill ×1.20; charge time ×0.85 | same | high |

Game8 examples at Lv3: GS charge 2.5 s → 2.16 s; Hammer Big Bang 3.0 → 2.55 s, Mighty Charge 1.7 → 1.4 s; Bow charged shot 1.8 → 1.5 s; LS spirit / DB demon gauge ×1.2 fill. 4th field (`105/107/110`) unexplained.

### 1.12 Rapid Morph

| level | kiranico raw | numeric effect | source | confidence |
|---|---|---|---|---|
| 1 | `110/100` | morph speed +10 %; morph attack ×1.00 | kiranico | high |
| 2 | `120/110` | speed +20 %; morph attack ×1.10 | kiranico | high |
| 3 | `130/120` | speed +30 %; morph attack ×1.20 | kiranico | high |

### 1.13 Punishing Draw

| level | numeric effect | source | confidence |
|---|---|---|---|
| 1 | draw attacks: +3 flat attack, "small" stun | kiranico, game8 501562 | high (attack) |
| 2 | +5 flat attack, "medium" stun | same | high |
| 3 | +7 flat attack, "large" stun | same | high |

Stun amounts: no datamined value found.

### 1.14 Airborne

| level | kiranico raw | numeric effect | source | confidence |
|---|---|---|---|---|
| 1 | `110` | jumping attacks ×1.10 | kiranico | high |

### 1.15 Slugger / Stamina Thief

| skill | Lv1 | Lv2 | Lv3 | source | confidence |
|---|---|---|---|---|---|
| Slugger (stun) | ×1.20 | ×1.30 | ×1.40 (raw `120/130/140`) | kiranico | high |
| Stamina Thief (exhaust) | ×1.20 | ×1.30 | ×1.40 (raw `120/130/140`) | kiranico | high |

### 1.16 Power Prolonger

Duration multipliers for powered-up states (gamepedia.jp/mh-wilds/skill/2166); game8 EN gives seconds (game8.co/archives/501563).

| weapon | Lv1 | Lv2 | Lv3 | game8 seconds (Lv1 / Lv2 / Lv3) | confidence |
|---|---|---|---|---|---|
| Long Sword | ×1.1 | ×1.2 | ×1.4 | white 215/234/272 s, yellow 148/162/190, red 55/60/68 | medium |
| Dual Blades | ×1.3 | ×1.5 | ×1.8 | Archdemon 117/135/162 s; Demon Boost 25/30/35 s | medium |
| Switch Axe | ×1.3 | ×1.6 | ×2.0 | Amped 60/77/90 s | medium |
| Charge Blade | ×1.1 | ×1.2 | ×1.4 | shield/elem boost 137/–/175 s; overcharged 220/–/280; axe boost 130/–/168; sword boost 100/–/125 | medium |
| Insect Glaive | ×1.1 | ×1.2 | ×1.4 | red 100/–/125 s; white 130/–/170; orange 165/–/210; triple 100/–/125 | medium |

### 1.17 Horn Maestro

| level | numeric effect | source | confidence |
|---|---|---|---|
| 1 | melody duration up (e.g. Self-Improvement +80 s = +29.6 %; Attack Up (L) +70 s = +64.8 %); Echo Bubble 60 → 90 s; larger flat heals | game8.co/archives/501572 | medium |
| 2 | Self-Improvement +180 s (+66.6 %); Attack Up (L) +130 s (+120.3 %); Echo Bubble 120 s | same | medium |

No single global multiplier — extension differs per melody.

---

## 2. Armor skills

### 2.1 Burst (per weapon) — full table

Mechanics (game8.jp/mhwilds/672018, game8.co/archives/501644): first hit grants a small buff; 5 hits with ≤ 3 s between hits upgrades it to the full buff; buff expires `duration` seconds after the last hit; Lv only changes the 5-hit values. Does **not** raise status values.

First-hit buff (all levels): attack +5 for every weapon; element +50 (DB and Bow +30; **LBG/HBG +0** per game8.jp).

5-hit buff (attack flat raw / element display):

| weapon | Lv1 | Lv2 | Lv3 | Lv4 | Lv5 | duration | source | confidence |
|---|---|---|---|---|---|---|---|---|
| Great Sword | +10 / +80 | +12 / +100 | +14 / +120 | +16 / +160 | +18 / +200 | 5 s | game8.jp 672018, game8.co 501644, gamewith.jp 486597, Fextralife | high |
| Hunting Horn | +10 / +80 | +12 / +100 | +14 / +120 | +16 / +160 | +18 / +200 | 5 s | same | high |
| Gunlance | +8 / +60 | +10 / +80 | +12 / +100 | +15 / +120 | +18 / +140 | 5 s | same | high |
| Long Sword | +8 / +60 | +10 / +80 | +12 / +100 | +15 / +120 | +18 / +140 | 4 s | same | high |
| Sword & Shield | +8 / +60 | +10 / +80 | +12 / +100 | +15 / +120 | +18 / +140 | 4 s | same | high |
| Hammer | +8 / +60 | +10 / +80 | +12 / +100 | +15 / +120 | +18 / +140 | 4 s | same | high |
| Lance | +8 / +60 | +10 / +80 | +12 / +100 | +15 / +120 | +18 / +140 | 4 s | same | high |
| Switch Axe | +8 / +60 | +10 / +80 | +12 / +100 | +15 / +120 | +18 / +140 | 4 s | same | high |
| Charge Blade | +8 / +60 | +10 / +80 | +12 / +100 | +15 / +120 | +18 / +140 | 4 s | same | high |
| Insect Glaive | +8 / +60 | +10 / +80 | +12 / +100 | +15 / +120 | +18 / +140 | 4 s | same | high |
| Dual Blades | +8 / +40 | +10 / +60 | +12 / +80 | +15 / +100 | +18 / +120 | 3 s | same | high |
| Bow | +6 / +40 | +7 / +60 | +8 / +80 | +9 / +100 | +10 / +120 | 3 s | same | high |
| Light Bowgun | +6 / — | +7 / — | +8 / — | +9 / — | +10 / — | 3 s | game8.jp (no element for bowguns) | medium* |
| Heavy Bowgun | +6 / — | +7 / — | +8 / — | +9 / — | +10 / — | 3 s | game8.jp | medium* |

\*game8.co EN lumps "ranged" as +6…+10 / +40…+120 element; game8.jp's per-weapon table shows the element bonus only for Bow and "—" for both bowguns. Trust game8.jp (more granular) but verify in-game for elemental bowgun ammo.

Burst Boost (Ebony Odogaron's Power set bonus): 2 pc = +1 s duration and +8 attack while Burst active; 4 pc = +2 s and +18 attack (game8.co/archives/501808). Fextralife lists a flat "4 s default" duration — superseded by the per-weapon table above.

### 2.2 Elemental Absorption

| level | kiranico raw | numeric effect | condition | duration / cooldown | source | confidence |
|---|---|---|---|---|---|---|
| 1 | `5/5` | flat element (game8 test): GS/HH +50; LS/SnS/Hammer/Lance/GL/SA/CB/IG +40; DB/Bow +30; resistance +4 to element taken | take elemental damage (any of fire/water/thunder/ice/dragon; works with dragon-phial SA) | 120 s active; 60 s cooldown (was 90 s before TU4: "Reduced the cooldown time") | kiranico elemental-absorption, game8.co/archives/502021, rpgsite TU4 notes | medium |
| 2 | `10/8` | GS/HH +80; medium +50; DB/Bow +40; res +6 | " | " | same | medium |
| 3 | `15/12` | GS/HH +100; medium +60; DB/Bow +50; res +8 | " | " | same | medium |

Conflict note: kiranico's two raw fields (`5/10/15` and `5/8/12`) look like "+5/10/15 %" and "+50/80/120 display flat"; game8's measured GS values (+50/+80/+100) match the second field at Lv1–2 but not Lv3, and game8 says the bonus is flat and weapon-dependent. Treat the game8 per-weapon flats as the practical values (medium) and re-test Lv3.

### 2.3 Coalescence

| level | kiranico raw | numeric effect | condition | duration | source | confidence |
|---|---|---|---|---|---|---|
| 1 | `110/105/105` | element ×1.10 (GS, Hammer, HH, GL, SA, CB) or ×1.05 (LS, SnS, DB, Lance, IG, LBG, HBG, Bow); status buildup ×1.05 (all weapons) | after recovering **naturally** from a blight / abnormal status (Nulberry cure does not trigger; Frenzy auto-cure does) | 30 s | kiranico coalescence, game8.co/archives/501948 | medium–high |
| 2 | `120/110/110` | element ×1.20 / ×1.10; status ×1.10 | " | 30 s | same | medium–high |
| 3 | `130/115/115` | element ×1.30 / ×1.15; status ×1.15 | " | 30 s | same | medium–high |

The three kiranico fields map cleanly to [element heavy group, element fast group, status]. No source gives attack (raw) bonus — unlike World/Rise, Wilds Coalescence has **no raw component**.

### 2.4 Convert Element

| level | kiranico raw | numeric effect | condition | duration / cooldown | source | confidence |
|---|---|---|---|---|---|---|
| 1 | `8/110/150/60` | dragon attack +80 display (field 1 × 10); dragon attack ×1.10 *(interp.)*; extra dragon burst base 150 (game8 measured 168 on training dummy) | activates on taking any elemental damage; burst requires an **elemental weapon** (dragon-phial SA counts) and accumulates elemental damage dealt | 120 s active; 90 s cooldown | kiranico convert-element, game8.co/archives/502022, game8.jp/mhwilds/701797 | medium |
| 2 | `12/115/200/80` | +120 display; ×1.15; burst 200 (measured 224) | " | " | same | medium |
| 3 | `18/120/280/100` | +180 display; ×1.20; burst 280 (measured 310) | " | " | same | medium |

Burst trigger data (game8.jp test): SnS first burst after 11 hits / 646 element dmg, second after 15–17 hits / 922–957; Gunlance first after 8–9 hits / 522–559, second after 12–13 / 790–819 — i.e. the threshold **rises after each burst**; game8.jp says thresholds are not universal across weapons. Field 4 (`60/80/100`) is plausibly the threshold scalar but this is unconfirmed. Weapon element is **not** converted to dragon. Measured/base ratio ≈ 1.11–1.12 (likely dummy dragon hitzone). A mobalytics guide (403, snippet only) claims the amount of dragon damage dealt is irrelevant to the burst — low.

### 2.5 Flayer

| level | kiranico raw | numeric effect | condition | source | confidence |
|---|---|---|---|---|---|
| 1 | `105/100` | wound buildup ×1.05 on proc; burst 140 fixed dmg (game8, post-TU4) | proc chance ~50 % per hit (TU4 raised from ~33 %); burst fires after enough buildup | kiranico flayer, game8.co/archives/501647, rpgsite TU4 notes | medium |
| 2 | `110/120` | ×1.10; burst 160 | " | same | medium |
| 3 | `115/160` | ×1.15; burst 220 | " | same | medium |
| 4 | `120/200` | ×1.20; burst 300 | " | same | medium |
| 5 | `130/250` | ×1.30; burst 400 | " | same | medium |

Burst damage "is fixed, regardless of weapon type and weapon damage" (game8). Kiranico's second field (100…250) does not match game8's 140…400 by a constant ratio, so one of them is pre-TU4 or the field is a scalar — re-verify in-game. Since TU4 the burst also fires on parts past their wound limit. Pre-TU4 Steam test (lunarknight64): ~+25 % wound progress at Lv5, ~1/5 proc — obsolete, low.

### 2.6 Antivirus

| level | kiranico raw | numeric effect | condition | duration | source | confidence |
|---|---|---|---|---|---|---|
| 1 | `10/3` | Frenzy recovery +10 %; affinity +3 % after overcoming Frenzy | cure Frenzy | ~60 s (Frenzy-cured buff) | kiranico, game8.co/archives/502023 | high |
| 2 | `20/6` | +20 %; affinity +6 % | " | " | same | high |
| 3 | `30/10` | +30 %; affinity +10 % (older in-game text said +15 %; tested +10 %) | " | " | same | high |

### 2.7 Latent Power

| level | kiranico raw | numeric effect | trigger | duration | source | confidence |
|---|---|---|---|---|---|---|
| 1 | `10/70` | affinity +10 %; stamina depletion ×0.70 | 2 min in combat with a large monster **or** 130 total damage taken (any number of hits) | 120 s; 2 min cooldown; Thunderous Roar 2 pc +30 s, 4 pc +90 s | kiranico latent-power, game8.co/archives/501638 | medium–high |
| 2 | `20/70` | +20 %; ×0.70 | " | " | same | medium–high |
| 3 | `30/50` | +30 %; ×0.50 | " | " | same | medium–high |
| 4 | `40/50` | +40 %; ×0.50 | " | " | same | medium–high |
| 5 | `50/50` | +50 %; ×0.50 | " | " | same | medium–high |

A blog (dtgre.com) states "180 damage or 5 minutes" — those are World/Rise values; low.

### 2.8 Agitator

| level | kiranico raw | numeric effect | condition | source | confidence |
|---|---|---|---|---|---|
| 1–5 | `4/3`, `8/5`, `12/7`, `16/10`, `20/15` | attack +4/8/12/16/20; affinity +3/5/7/10/15 % | large monster enraged | kiranico, game8 501657 | high |

### 2.9 Peak Performance

| level | numeric effect | condition | source | confidence |
|---|---|---|---|---|
| 1–5 | attack +3 / +6 / +10 / +15 / +20 (raw `3/6/10/15/20`) | health full; deactivates on any damage, reactivates when healed to full | kiranico, game8 501635 | high |

### 2.10 Maximum Might

| level | numeric effect | condition | source | confidence |
|---|---|---|---|---|
| 1–3 | affinity +10 / +20 / +30 % (raw `10/20/30`) | activates **3 s after** stamina registers full; stays active for a **2 s grace period** after stamina is used; only the base stamina bar is checked (Second Wind extra bar ignored) | kiranico, game8.co/archives/501646 | medium–high |

### 2.11 Adrenaline Rush

| level | numeric effect | trigger | duration | source | confidence |
|---|---|---|---|---|---|
| 1–5 | attack +10 / +15 / +20 / +25 / +30 (raw `10…30`) | perfectly-timed evade (timing is lenient) | 30 s, all levels; does not stack or refresh while active; Razor's Edge set bonus → 45 s + one re-activation (~60 s total) | kiranico, game8.co/archives/502018, gamepedia.jp skill/2100 | high |

### 2.12 Counterstrike

| level | numeric effect | trigger | duration | source | confidence |
|---|---|---|---|---|---|
| 1 | attack +10 (raw `10`) | knockback reaction (also triggers via hyper-armor moves, successful offsets, Rocksteady) | 30 s | kiranico, game8.co/archives/502017, gamepedia.jp skill/2096 | high |
| 2 | attack +15 | " | 35 s | same | high |
| 3 | attack +25 | " | 45 s | same | high |

### 2.13 Resentment

| level | numeric effect | condition | source | confidence |
|---|---|---|---|---|
| 1–5 | attack +5 / +10 / +15 / +20 / +25 | while any red (recoverable) health is present | kiranico, gamepedia.jp | high |

### 2.14 Heroics

| level | kiranico raw | numeric effect | condition | source | confidence |
|---|---|---|---|---|---|
| 1 | `100/50` | defense +50 | HP ≤ 35 % | kiranico, game8 501640 | high |
| 2 | `105/50` | attack ×1.05; defense +50 | " | same | high |
| 3 | `105/100` | attack ×1.05; defense +100 | " | same | high |
| 4 | `110/100` | attack ×1.10; defense +100 | " | same | high |
| 5 | `130` | attack ×1.30; defense-up effects negated | " | same | high |

### 2.15 Foray

| level | kiranico raw | numeric effect | condition | source | confidence |
|---|---|---|---|---|---|
| 1 | `6` | attack +6 | large monster poisoned or paralysed | kiranico | high |
| 2 | `8/5` | attack +8; affinity +5 % | " | kiranico | high |
| 3 | `10/10` | +10; +10 % | " | kiranico | high |
| 4 | `12/15` | +12; +15 % | " | kiranico | high |
| 5 | `15/20` | +15; +20 % | " | kiranico | high |

### 2.16 Weakness Exploit

| level | kiranico raw | numeric effect | condition | source | confidence |
|---|---|---|---|---|---|
| 1 | `5/3` | affinity +5 % on weak points; extra +3 % on wounds | weak point = part whose **raw hitzone ≥ 45** (game8: "3–4 star" parts; "a few 3-star parts may not activate the skill due to hitzone values"); wound bonus applies on any wounded part | kiranico, game8.co/archives/501642; threshold is the complement of the Mind's Eye "<45" rule | medium (threshold) / high (values) |
| 2 | `10/5` | +10 %; +5 % | " | same | high |
| 3 | `15/10` | +15 %; +10 % | " | same | high |
| 4 | `20/15` | +20 %; +15 % | " | same | high |
| 5 | `30/20` | +30 %; +20 % (max +50 % on wounded weak point) | " | same | high |

### 2.17 Partbreaker

| level | kiranico raw | numeric effect | condition | source | confidence |
|---|---|---|---|---|---|
| 1 | `110/110` | part-break damage ×1.10; damage when destroying a wound with a Focus Strike ×1.10 | — | kiranico, game8.co/archives/501633 | high |
| 2 | `120/120` | ×1.20 / ×1.20 | — | same | high |
| 3 | `130/130` | ×1.30 / ×1.30 | — | same | high |

---

## 3. Not found / open items

- **Critical Status weapon grouping** (which weapons use the 1.10/1.20/1.40 vs 1.05/1.10/1.20 field): no datamined mapping found.
- **Charge Master**: kiranico field vs game8 per-weapon test disagree; the extra status fields are unmapped.
- **Opening Shot** trailing `110/110` fields, **Artillery** fields 1–2, **Focus** field 4, **Convert Element** field 4: no documentation of meaning.
- **Ballistics Lv3** exact multiplier: only "around 2 %" (game8); no datamined value found.
- **Punishing Draw** stun values: no datamined value found.
- **Flayer** burst: kiranico 100…250 vs game8 140…400 unresolved (possibly pre/post-TU4).
- **Elemental Absorption** Lv3 for GS/HH: kiranico field implies +120, game8 measured +100.
- **Critical Element** for GL/SA/CB/HBG: game8 vs gamewith.jp disagree (see 1.1).
- **dtlnor datamine notes**: no public repository with skill parameters located.

---

## Sources

- https://mhwilds.kiranico.com/data/skills (and per-skill pages: /data/skills/critical-status, charge-master, minds-eye, bludgeoner, normal-shots, piercing-shots, spreadpower-shots, special-ammo-boost, opening-shot, ballistics, artillery, focus, rapid-morph, punishing-draw, airborne, slugger, stamina-thief, power-prolonger, horn-maestro, burst, elemental-absorption, coalescence, convert-element, flayer, antivirus, latent-power, agitator, peak-performance, maximum-might, adrenaline-rush, counterstrike, resentment, heroics, foray, weakness-exploit, partbreaker, attack-boost, critical-boost, critical-eye, critical-draw, offensive-guard, dragon-attack)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501644 (Burst)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501808 (Burst Boost)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501573 (Critical Element)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501577 (Critical Status)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501578 (Charge Master)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501592 (Mind's Eye)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501595 (Bludgeoner)
- https://game8.co/games/Monster-Hunter-Wilds/archives/503080 (Normal Shots)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501584 (Special Ammo Boost)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501583 (Opening Shot)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501559 (Ballistics)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501585 (Artillery)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501564 (Focus)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501562 (Punishing Draw)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501563 (Power Prolonger)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501572 (Horn Maestro)
- https://game8.co/games/Monster-Hunter-Wilds/archives/502021 (Elemental Absorption)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501948 (Coalescence)
- https://game8.co/games/Monster-Hunter-Wilds/archives/502022 (Convert Element)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501647 (Flayer)
- https://game8.co/games/Monster-Hunter-Wilds/archives/502023 (Antivirus)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501638 (Latent Power)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501646 (Maximum Might)
- https://game8.co/games/Monster-Hunter-Wilds/archives/502018 (Adrenaline Rush)
- https://game8.co/games/Monster-Hunter-Wilds/archives/502017 (Counterstrike)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501640 (Heroics)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501635 (Peak Performance)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501642 (Weakness Exploit)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501633 (Partbreaker)
- https://game8.co/games/Monster-Hunter-Wilds/archives/501570 (Critical Boost)
- https://game8.co/games/Monster-Hunter-Wilds/archives/499631 (Damage formula)
- https://game8.co/games/Monster-Hunter-Wilds/archives/500260 (Elemental damage / element cap)
- https://game8.co/games/Monster-Hunter-Wilds/archives/500339 (Sharpness)
- https://game8.jp/mhwilds/672001 (会心撃【属性】 per-weapon table)
- https://game8.jp/mhwilds/672018 (連撃 per-weapon table and durations)
- https://game8.jp/mhwilds/701797 (属性変換 test data)
- https://gamewith.jp/mhwilds/486605 (会心撃【属性】)
- https://gamewith.jp/mhwilds/486597 (連撃)
- https://gamepedia.jp/mh-wilds/skill/2166 (強化持続 multipliers)
- https://gamepedia.jp/mh-wilds/skill/2100 (巧撃 duration)
- https://gamepedia.jp/mh-wilds/skill/2096 (逆襲 durations)
- https://monsterhunterwiki.org/wiki/Mind%27s_Eye_(MHWilds)
- https://monsterhunterwiki.org/wiki/Latent_Power_(MHWilds)
- https://monsterhunterwiki.org/wiki/Weakness_Exploit_(MHWilds)
- https://monsterhunterwilds.wiki.fextralife.com/Burst
- https://monsterhunterwilds.wiki.fextralife.com/Sharpness
- https://steamcommunity.com/app/2246340/discussions/0/693120026285331190/ (Mind's Eye hitzone <45, base-hitzone test)
- https://steamcommunity.com/app/2246340/discussions/0/599646660884201316 (Flayer pre-TU4 test)
- https://www.rpgsite.net/news/19151-monster-hunter-wilds-title-update-4-full-patch-notes-balance-adjustments-list-revealed-version-1-040-00-00 (TU4 patch notes)
- https://www.switchbladegaming.com/monster-hunter-wilds/elemental-damage-guide/ (element cap; Critical Element values rejected)
- https://wildsbuilder.com/blog/elemental-status-guide/ (display ×10, sharpness 1.0625; Critical Element values rejected)
