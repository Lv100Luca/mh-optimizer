# Our builds vs the community and vs mhwilds.wiki-db.com (2026-10-17)

Great Sword, Long Sword and Bow: what the guides recommend after TU4 / Ver 1.041, what our optimizer produces under matching conditions, and what the wiki-db armor set search returns for the same skill lists. All runs: CLI `run`, CP-SAT, 8 threads, craftable charms unless stated, transcendence on, rarity 5+, every rollable skill pair and every weapon part (focus, element, parts, reinforcements up to EX) searched where the weapon is ours to choose. Inputs, full outputs and the research notes are in `bin/claude-build/community-comparison/` (git-ignored): `community_gs.md`, `community_ls.md`, `community_bow.md` hold every source with URL and date.

Score = EFR + EFE (+ proc damage) per 100 MV of the default attack against the default custom target (raw hitzone 70, element 28): GS True Charged Slash Lv3, LS Crimson Slash loop on red gauge, Bow dash dance.

## 1. What the community assumes

Sources that still had a post-TU4 build: Game8 (EN and JP), GameWith JP, Mobalytics, Icy Veins, mhwildsbuild.com (Darcblade), Switchblade, LevelUpper, QuestDuo, the r/MonsterHunterMeta Bow Compendium (Google Doc). Reddit itself blocks every crawler; YouTube only gave titles.

Shared assumptions, almost never written down explicitly:

- Monster enraged (Agitator 5 everywhere), weak points hit (Weakness Exploit), full stamina (Maximum Might 3 on GS/LS; Bow drops it and runs Constitution instead).
- **Counterstrike 3 in essentially every GS and LS build** (tackle / parry hit), and in the raw Bow builds. Our app default has it off.
- Frenzy overcome only where Gore 2-piece (Black Eclipse) is run, together with Antivirus 3.
- Health: neither full nor red is assumed in general; Peak Performance appears only on Bow (Gogmazios barrier) and in one Darcblade LS build; Resentment only in Bale builds, where red health is stated.
- Burst 1 on GS (first hit), Burst 1 to 5 on LS/Bow depending on the set.
- Raw first: dragon or "any" element on GS/LS; no element skill on any GS/LS build. Bow is the only weapon where the community builds elemental (Fire/Water/... Gogma bow per matchup, Element EX reinforcements, element jewel).
- Weapon: the Gogma Artian weapon in every build (Ostrak Oblivion / Headsman's Hamus / Calamitous Angel), 3 attack parts, reinforcements attack-heavy (2x Attack EX + 2x Attack III + one Sharpness EX is the stated ideal for GS/LS; 3 attack + 2 Element EX for Bow). Focus: GS split evenly between Attack and Affinity focus with no calculation shown; LS Attack; Bow Attack (Close-range coating) or Element (Pierce) by matchup, never Affinity.
- Rolled pair: **Lord's Soul** as group skill in every build that names one. Set bonus: Soul of the Dark Knight (GS), Gore Magala's Tyranny (LS, raw Bow), Gogmapocalypse (elemental Bow), Leviathan's Fury (Game8 LS).
- Charm: Challenger Charm III as the craftable floor, an appraised Golden Age Charm (Attack Boost 3 + Agitator 1, or Critical Eye 3 + Weakness Exploit 1, or Master's Touch 1 + Agitator 1) as the ceiling.
- Comfort skills they value and we do not score: Quick Sheathe 3 (LS), Second Wind (Fulgur 2-piece), Master's Touch, Handicraft, Offensive Guard on GS (Game8 EN), Speed Eating, Stun Resistance 3 (JP sites), Earplugs.

Conditions used for the matched runs ("G" = guide-like): app default per weapon plus `counterstrike_active: true`, `full_health: false`, `red_health: false`. GS keeps `Burst <= 1`; Bow keeps stamina not full + charged attacks and gets Counterstrike only for the raw/Gore builds ("B_CS"). Target skills: GS Focus 3 only (the Stun Resistance 3 default target was left out because no guide requires it); LS and Bow none.

Each community build was scored two ways with its exact five pieces, charm, weapon parts and rolled pair forced:

- **as published**: its skill list as targets, our optimizer only places decorations;
- **their armor, our decorations**: same pieces and charm, decorations free.

A few published lists were infeasible in our data (Bow Compendium DPS, Compendium raw, Game8 Gore bow): their skill totals need one jewel more than the slots hold as transcribed (Burst 4 / Stamina Surge 3 on the Compendium set), so only the free-decoration score exists for those.

## 2. Great Sword

| Build | Pieces (head / chest / arms / waist / legs / charm) | Score as published | Their armor, our decos |
|---|---|---|---|
| **Ours, G conditions** | Arkvulcan Helm γ / Udra Miremail γ / Sororal Vambraces α / Bale Flanchard α / Sororal Boots α / Challenger III, Dark Knight + Lord's Soul, Attack focus Fire, 3 attack parts, Attack EX x2 + Attack III x2 + Affinity EX | **703.6** | |
| Ours, G, with the two guide charms available | same armor idea, Golden Age (Critical Eye 3 + WEX 1) | 703.6 (tie) | |
| Ours, G, every possible talisman | Attack Boost 2 + Critical Eye 1 + Burst 1 charm, Element focus | 715.6 | |
| Ours, app default (no Counterstrike, full health) | Arkvulcan γ / Dahaad Shardmail γ / Sororal Vambraces α / Bale Flanchard α / Sororal Boots α | 673.3 | |
| Game8 EN "Offense Focus Arkveld γ" | Arkvulcan Helm γ / Udra Miremail γ / Gogmazios Vambraces α / Bale Flanchard α / Arkvulcan Greaves γ / Challenger III | 621.7 | 645.1 |
| Mobalytics TU4 | Rey Sandhelm γ / Udra Miremail γ / Bale Gauntlets α / Doshaguma Coil β / Gogmazios Greaves α / Challenger III, Affinity focus | 649.8 | 649.8 |
| Icy Veins TU4 | Rey Sandhelm γ / Udra Miremail γ / Bale Gauntlets α / Gore Coil β / Gore Greaves β / Golden Age (AB3 Agi1), Gore + Lord's Soul | 578.5 | 608.3 |
| Game8 JP build 2 | G. Fulgur Helm β / Arkvulcan Mail γ / Gogmazios Vambraces α / Bale Flanchard α / Arkvulcan Greaves γ / Golden Age (CE3 WEX1), Affinity focus | 640.2 | 642.5 |
| Game8 JP build 4 (craftable charm) | G. Fulgur Helm β / Udra Miremail γ / Gogmazios Vambraces α / Bale Flanchard α / Duna Wildgreaves γ / Exploiter III, Affinity focus | 647.5 | 649.8 |
| GameWith JP build 1 | Arkvulcan Helm γ / Dahaad Shardmail γ / Gogmazios Vambraces α / G. Fulgur Coil β / Bale Sollerets α / Golden Age (AB3 Agi1) | 615.9 | 643.2 |

Our skills: Critical Boost 5, Focus 3, Agitator 5, Weakness Exploit 5, Counterstrike 3, Maximum Might 3, Burst 1, Antivirus 2, Black Eclipse I (Gore 2-piece via the Sororal α pieces) + Dark Arts + Guts. 96 % affinity, attack 331.

Reading:

- Our best is 8 % above the best community set under the same conditions (703.6 vs 649.8). Skill-wise we agree completely: CB5 / Agi5 / WEX5 / MM3 / CS3 / Focus 3 / Burst 1 / Dark Arts / Guts is the community list. The gap comes from the pieces: we add Gore 2-piece through **Sororal Vambraces α + Sororal Boots α** (1.041 set, Gore Magala's Tyranny set bonus, Burst / Partbreaker / Stamina Surge) and keep every damage skill, while the guides spend slots on Quick Sheathe 3, Constitution, Earplugs, Flayer, Speed Eating or Stun Resistance. None of the GS guides uses a Sororal piece.
- Weapon: Attack focus beats Affinity focus by 23 points on our armor (650.1 vs 673.3 in the default run), which settles the guides' open Attack-vs-Affinity question in favour of Game8 EN / GameWith. 3 attack parts + Attack EX x2 + Attack III x2 + Affinity EX is exactly the GameWith / Game8 JP roll minus their Sharpness EX; sharpness costs 18 points in our model (no sharpness loss modelled yet, see `notes/sharpness_model.md`).
- Element: fire by 2.3 over dragon on the neutral target; the guides' dragon/sleep/blast choices are matchup calls the custom target does not see.
- Charms: the appraised Golden Age charms the JP sites build around do not beat Challenger Charm III on our best armor (tie at 703.6); only the theoretical all-talisman pool adds 12 points.
- Decorations: their published jewel choices cost them 0 to 30 points (Icy Veins loses 30 by taking Attack Boost 4 + Handicraft over Critical Boost / Agitator; Game8 EN loses 23 to Offensive Guard 3 + Handicraft 2 + Quick Sheathe, since Offensive Guard is not active in these conditions).

## 3. Long Sword

| Build | Pieces | Score as published | Their armor, our decos |
|---|---|---|---|
| **Ours, G conditions** | Arkvulcan Helm γ / Udra Miremail γ / Gogmazios Vambraces α / Sororal Coil α / Gogmazios Greaves α / Challenger III, Gore + Lord's Soul, **Element focus Fire**, 3 attack parts, Attack EX x2 + Attack III x2 + Element EX | **772.6** | |
| Ours, G, with the three guide charms | Golden Age (AB3 Agi1) | 774.5 | |
| Ours, G, every possible talisman | Attack Boost 2 + Critical Element 1 + WEX 1 charm, Sororal Earrings α / Dahaad Shardcoil γ | 788.7 | |
| Ours, app default (no Counterstrike, full health) | Arkvulcan γ / Gore Mail α / Gogmazios Vambraces α / Udra Mirecoil γ / Gogmazios Greaves α | 755.5 | |
| Game8 "Fulgur-Lagi Guts" (5 stars) | G. Fulgur Helm β / Udra Miremail γ / G. Fulgur Vambraces β / Lagiacrus Coil β / Duna Wildgreaves γ / Golden Age (MT1 Agi1), Leviathan's Fury + Lord's Soul | 694.7 | 695.8 |
| Game8 "Gogma-Arkveld" | Arkvulcan Helm γ / Gogmazios Mail α / Gogmazios Vambraces α / Lagiacrus Coil β / Arkvulcan Greaves γ / Challenger III | 680.0 | 701.4 |
| Icy Veins = Mobalytics = Switchblade | G. Rathalos Helm β / Udra Miremail γ / Gogmazios Vambraces α / Gore Coil β / Duna Wildgreaves γ / Challenger III, Gore + Lord's Soul | 698.8 | 725.1 |
| Darcblade "Gore Soul" | Sororal Earrings α / G. Fulgur Mail β / G. Fulgur Vambraces β / Dahaad Shardcoil γ / Duna Wildgreaves γ / Secret charm (MT1 WEX1) | 628.2 | 666.0 |

Our skills: Critical Boost 5, Fire Attack 3, Critical Element 1, Agitator 5, Weakness Exploit 5, Antivirus 3, Burst 3, Counterstrike 3, Maximum Might 3, Peak Performance 2, Black Eclipse I + Mutual Hostility I + Guts. 100 % affinity, attack 322, fire 814 display.

Reading:

- Our best is 10 % above the most-agreed community set as published (772.6 vs 698.8) and 6.5 % above it with our decorations. The pieces overlap heavily (Arkvulcan Helm γ, Udra Miremail γ, Gogmazios Vambraces α, Gore 2-piece, Lord's Soul x3); the differences are the waist (Sororal Coil α for Burst 2 + the Gore bonus instead of Gore Coil β) and the legs (Gogmazios Greaves α instead of Duna Wildgreaves γ, which the guides take for Quick Sheathe 3).
- **Element is the big modelling difference.** Every guide treats the LS as raw ("any element", no element jewel); our model puts 117 of 773 points (15 %) into Fire via Element focus, Fire Attack 3 and Critical Element, because the Crimson loop is many fast hits and the default target gives element hitzone 28. With Attack focus and no element skills, their armor scores 699 to 725; the element share is where the remaining gap comes from. Whether the real loop lands that much element is worth a dummy test (element on LS was taken from guides, see `long-sword-attacks` notes).
- Reinforcements: we want 2x Attack EX + 2x Attack III + Element EX; the guides want Sharpness EX in that fifth slot. Same caveat as GS: sharpness loss is not modelled.
- Their decorations cost 1 to 38 points: Master's Touch 1, Quick Sheathe 3 and Constitution 2 are the slots they spend on comfort; the Darcblade set additionally has no Counterstrike source.

## 4. Bow

| Build | Pieces | Conditions | Score as published | Their armor, our decos |
|---|---|---|---|---|
| **Ours, app default** | Sororal Earrings α / Blango Mail β / G. Ebony Braces β / Gogmazios Coil α / Gogmazios Greaves α / Exploiter III, Gore + any, Attack focus Fire, Attack EX x2 + Element EX x2 + Element II | B | **813.9** | |
| Ours, every possible talisman | Arkvulcan γ / Gore Mail α / Gogmazios Vambraces α / Dahaad Shardcoil γ / Gogmazios Greaves α, charm Spread/Power Shots 1 + Burst 1 | B | 829.8 | |
| Ours, with Counterstrike | Arkvulcan γ / Udra Miremail γ / Sororal Vambraces α / Gogmazios Coil α / Gogmazios Greaves α / Challenger III, Gore + Lord's Soul | B_CS | 860.0 | |
| Compendium "Gogma DPS" | G. Rathalos Helm β / Gogmazios Mail β / G. Ebony Braces β / Gogmazios Coil α / Gogmazios Greaves α / Challenger III, Gogmapocalypse (Mutual Hostility II) | B | infeasible as listed | 769.1 |
| Compendium "Gogma Comfort" | G. Ebony Helm β / Gogmazios Mail β / G. Rathalos Vambraces β / Gogmazios Coil α / Gogmazios Greaves α / Challenger III | B | 748.6 | 757.4 |
| Compendium "Raw Comfort, anti-Arkveld" | Udra Mirehelm γ / Gogmazios Mail β / G. Rathalos Vambraces β / Gore Coil β / Duna Wildgreaves γ / Challenger III, Element focus, Affinity EX x2 | B_CS | infeasible as listed | 600.3 |
| Game8 "Gogma-Gore" | Arkvulcan Helm γ / Gogmazios Mail α / Gore Vambraces β / Dahaad Shardcoil γ / Gogmazios Greaves α / Exploiter II, Gore + Lord's Soul, 5 attack reinforcements | B_CS | infeasible as listed | 801.5 |

Our skills (default): Charge Master 3, Critical Boost 3, Fire Attack 3, Critical Element 1, Agitator 5, Peak Performance 5, Weakness Exploit 5, Burst 4, Antivirus 3, Black Eclipse I + Burst Boost I + Mutual Hostility I.

Reading:

- The Bow model and the community agree on the shape: Gogmazios Coil α + Greaves α, Peak Performance 5, Agitator 5, Weakness Exploit 5, Burst 4, Charge Master 3, element jewel, Gore or Gogmapocalypse on the weapon, Attack focus with 2x Element EX. Game8's Gore + Lord's Soul set is within 7 % of our Counterstrike run, and the Compendium's DPS set within 6 % of our default.
- Where we differ: (1) we take **Gore 2-piece + Mutual Hostility I** (Sororal Earrings α) over the Compendium's 4-piece Gogmazios Mutual Hostility II; in our model MH II is worth less than Black Eclipse plus the freed slots. (2) We skip Constitution / Stamina Surge entirely; the Compendium runs Constitution 3 to 5 + Stamina Surge 3 to keep the dash dance up, and the dash-dance default already sets stamina not full. Not scoring stamina skills is the main reason our Bow set is less realistic than our GS/LS sets. (3) Counterstrike adds 46 points when active; the Compendium only trusts it on Arkveld.
- The Compendium's DPS list is infeasible as transcribed in both our data and wiki-db (Burst 4 + Stamina Surge 3 need one jewel more than the slots hold with Gogmazios Mail β / Coil α / Greaves α + two transcended Guardian pieces); the doc's screenshots probably use different Gogmazios α/β pieces than the text says.
- Tetrad Shot (Game8's Bandolier jewel) and Ballistics are not modelled, so Game8's weapon jewels were replaced by ours.

## 5. mhwilds.wiki-db.com/sim

What it is: a classic armor set search (the MHRise/MHW sim lineage, closed source). You pick weapon slot layout, the weapon's rolled series (set) skill and group skill, required skill levels, optional talismans and owned equipment; it enumerates sets that reach the skills, stopping at 200. It does **not** rank by damage: per its FAQ it "tries from the armors that best fulfil the skills", merges pieces that only contribute slots into "LV3-2-1 Slot Head" placeholders, and the first jewel assignment it finds is kept. It does count the Gogma weapon as a piece for set and group skills (Mutual Hostility II appears with three Gogmazios armor pieces). Search parameters live in the URL hash (`#skills=...&w=LV3-3-3 Slot Weapon&wss=<set>&wgs=<group>`), which made it scriptable.

Queries run (each with a 3-3-3 weapon and the matching rolled pair):

| Skill list given to wiki-db | Result |
|---|---|
| Our GS default build (CB5, Focus 3, Agi5, WEX5, MM3, PP3, Burst 1, Antivirus 2, Black Eclipse I; Dark Knight + Lord's Soul) | 200+ sets. Top rows: Sororal Earrings α / Dahaad Shardmail γ / Gogmazios Vambraces α / Numinous Overlay β or Gore Coil α / Gore Greaves β or Gogmazios Greaves α / Exploiter III: the same piece pool as our top 5. |
| Game8 EN GS skill list (CB3, Focus 3, OG3, Handicraft 2, Agi5, WEX5, CS3, MM3, QS3, Burst 1, Dark Arts, Guts) | 200+ sets. Top rows Arkvulcan Helm γ / Udra Miremail γ / Bale Gauntlets α / Dahaad Shardcoil γ / Duna Wildgreaves γ; Game8's exact pieces appear with Arkvulcan or Rey arms instead of Gogmazios Vambraces α. |
| Our LS default build (CB5, Fire Attack 3, Crit Element 1, Agi5, PP5, WEX5, Antivirus 3, MM3, Burst 2, Black Eclipse I, Mutual Hostility I; Gore + Lord's Soul) | 200+ sets, Gogmazios Coil + Greaves in every top row, Sororal Earrings α or Arkvulcan Helm γ heads. |
| Icy Veins LS skill list (CB5, MT1, Agi5, WEX5, CS3, MM3, QS3, Antivirus 3, Const 2, Burst 1, Flayer 1, Black Eclipse I, Guts) | 107 sets, 6 of them with the G. Rathalos Helm β+; Udra Miremail γ + Duna Wildgreaves γ in most rows, i.e. the Icy Veins pieces. |
| Our Bow default build (Charge Master 3, CB3, Fire Attack 3, Crit Element 1, Agi5, PP5, WEX5, Burst 4, Antivirus 3, Black Eclipse I, Burst Boost I, Mutual Hostility I) | exactly 4 sets, all Sororal Earrings α + G. Ebony Braces β+ + Gogmazios Coil α + Gogmazios Greaves; the third is our build piece for piece (Blango Mail β+, Exploiter III). |
| Bow Compendium DPS list (WEX5, PP5, Agi5, Burst 4, Const 3, SS3, Fire Attack 3, Charge Master 3, Spread 1, Mutual Hostility II) | no matching set (same verdict as our optimizer); with Mutual Hostility I instead it returns 200+ sets with two Gogmazios pieces. |

Conclusion on wiki-db: it is a feasibility checker. Every skill list our optimizer produced is reproducible there, with the same pieces, which is a good independent check of our armor/decoration data (slots, transcended slots, set bonus on the Gogma weapon, Sororal α carrying Gore Magala's Tyranny). It cannot say which of its 200 sets is best, so "its meta builds" are whatever skill list the user types in; it does not compete with the damage ranking.

## 6. Verdict and open points

- Under matching conditions our optimizer finds sets 6 to 10 % above the published community sets for all three weapons, using the same skill lists the community agrees on, by (a) using the Sororal α pieces for Gore 2-piece, (b) not spending slots on comfort skills, and (c) in the LS case by valuing element. Their armor with our decorations closes a third to a half of the gap, so roughly half the difference is decoration choice and half piece choice.
- CP-SAT proved every reported build optimal for its request (every solve "optimal" in the logs) except the Bow every-possible-talisman run, whose best solve hit the 120 s limit, so its 829.8 is a lower bound. Nothing in the community lists beats our result under our model.
- Model differences to decide on, in order of impact:
  1. **Counterstrike**: the community counts it always; our default has it off and it is worth about 30 points on GS, 17 on LS, 46 on Bow. A per-weapon uptime (like Latent Power / Offensive Guard) would match reality better than the on/off toggle.
  2. **LS element share**: 15 % of our LS score is fire; the community ignores element on LS. Worth a training-dummy check of the Crimson loop before trusting element LS over raw.
  3. **Sharpness**: every guide wants a Sharpness EX line; we never pick it because sharpness loss is not modelled (18 points on GS to drop it).
  4. **Bow stamina**: Constitution / Stamina Surge have no value in the model, so our Bow set is the least realistic of the three.
  5. Comfort skills (Quick Sheathe, Second Wind, Master's Touch, Stun Resistance) are user preferences; a "community conditions" preset (Counterstrike on, neither health state) would make our results directly comparable to the guides.
- Data caveats: reinforcement tiers and charms were transcribed from screenshots and text; where a site did not state the tier, the community's stated ideal roll (2x Attack EX + 2x Attack III + Sharpness EX) was used. Sharpness reinforcement has tiers I and EX only in our data (the guides' "+30" became tier I).

Runtimes at 8 threads: optimized-weapon runs 17 to 75 s (GS), 52 to 450 s (LS, the every-talisman run), 138 s (Bow with Counterstrike) and 66 min for the Bow every-talisman run; forced builds 1 to 2 s.
