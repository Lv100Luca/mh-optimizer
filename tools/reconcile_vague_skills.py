"""One-off: fold the community cross-check (notes/skill_values_vague_skills.md) into data/damage_model.json
and record the resolved items in notes/README.md. Run from the repo root."""
import json

p = 'data/damage_model.json'
d = json.load(open(p, encoding='utf-8'))
sv = d['skill_values']

# --- Burst: decode the raw arrays. Structure confirmed by matching the community-tested values exactly:
# melee/bow: [first_attack, first_ele, duration_s, (attack, ele) x Lv1..5]; bowguns: [first_attack, duration_s, attack x Lv1..5]
raw = sv['Burst']['raw_arrays']
decoded = {}
for w, a in raw.items():
    if len(a) == 13:
        decoded[w] = {'first_hit': {'attack': a[0], 'ele': a[1]}, 'duration_s': a[2],
                      'levels': {str(l): {'attack': a[3 + 2 * (l - 1)], 'ele': a[4 + 2 * (l - 1)]} for l in range(1, 6)}}
    else:
        decoded[w] = {'first_hit': {'attack': a[0]}, 'duration_s': a[1],
                      'levels': {str(l): {'attack': a[1 + l]} for l in range(1, 6)}}
sv['Burst'] = {
    'kind': 'armor',
    'condition': '5 hits inside the window activate the full boost; the first hit gives the small boost; the buff lasts duration_s after the last hit',
    'source': 'PlayerSkillParam _ContinuousAttackWp??Data decoded as [first_attack, first_ele, duration_s, (attack, ele) x Lv1..5] (bowguns: [first_attack, duration_s, attack x Lv1..5]); matches game8/kiranico values exactly',
    'units': 'attack = true raw, ele = true element (x10 for display)',
    'per_weapon': decoded,
    'raw_arrays': raw,
    'burst_boost': "Ebony Odogaron's Power: +8 / +18 attack while active and +1 / +2 s duration",
}

# --- Critical Element groups
sv['Critical Element'] = {
    'kind': 'weapon',
    'source': 'PlayerSkillParam _AttrPowerRateData [1.05,1.10,1.15 | 1.07,1.14,1.21]',
    'groups': {
        'fast': {'weapons': ['long-sword', 'sword-shield', 'dual-blades', 'lance', 'insect-glaive', 'light-bowgun', 'bow'], 'levels': {'1': 1.05, '2': 1.10, '3': 1.15}},
        'heavy': {'weapons': ['great-sword', 'hammer', 'hunting-horn'], 'levels': {'1': 1.07, '2': 1.14, '3': 1.21}},
    },
    'unassigned': 'gunlance, switch-axe, charge-blade, heavy-bowgun: sources disagree on the group (game8 vs gamewith.jp)',
    'note': 'community measured the heavy group as ~1.067/1.133/1.20; the game data says 1.07/1.14/1.21',
}

# --- Coalescence / Charge Master: SkillData values are [heavy ele%, light ele%, status%]
sv['Coalescence'] = {
    'kind': 'armor', 'condition': 'after recovering from a blight/ailment, 30 s',
    'groups': {
        'heavy': {'weapons': ['great-sword', 'hammer', 'hunting-horn', 'gunlance', 'switch-axe', 'charge-blade'], 'ele_pct': {'1': 110, '2': 120, '3': 130}},
        'light': {'weapons': ['long-sword', 'sword-shield', 'dual-blades', 'lance', 'insect-glaive', 'bow', 'light-bowgun', 'heavy-bowgun'], 'ele_pct': {'1': 105, '2': 110, '3': 115}},
    },
    'status_pct': {'1': 105, '2': 110, '3': 115},
    'source': 'SkillData [110,105,105]/[120,110,110]/[130,115,115] + game8 weapon grouping',
}
sv['Charge Master'] = {
    'kind': 'weapon', 'condition': 'charged attacks only',
    'raw': {'1': [115, 105, 105, 105], '2': [120, 110, 110, 110], '3': [125, 115, 115, 115]},
    'interpretation': 'first value = element multiplier on charged attacks for the main group (x1.15/1.20/1.25), second = other group / status (x1.05/1.10/1.15) - grouping VERIFY',
}
sv['Elemental Absorption'] = {
    'kind': 'armor', 'condition': 'after taking elemental damage; 120 s, 60 s cooldown',
    'levels': {
        '1': {'ele_pct': 105, 'ele_flat_true_by_group': {'great-sword/hunting-horn': 5, 'medium': 4, 'dual-blades/bow': 3}, 'resistance': 4},
        '2': {'ele_pct': 110, 'ele_flat_true_by_group': {'great-sword/hunting-horn': 8, 'medium': 5, 'dual-blades/bow': 4}, 'resistance': 6},
        '3': {'ele_pct': 115, 'ele_flat_true_by_group': {'great-sword/hunting-horn': 10, 'medium': 6, 'dual-blades/bow': 5}, 'resistance': 8},
    },
    'source': 'SkillData [5,5]/[10,8]/[15,12] + game8 flat values (+50/80/100 GS, +40/50/60 medium, +30/40/50 DB/Bow display); confidence medium',
}
sv['Convert Element'] = {
    'kind': 'armor', 'condition': 'elemental weapon required; extra dragon burst after enough element dealt; 120 s, 90 s cooldown',
    'levels': {'1': {'dragon_flat_true': 8, 'dragon_pct': 110, 'burst_base': 150}, '2': {'dragon_flat_true': 12, 'dragon_pct': 115, 'burst_base': 200}, '3': {'dragon_flat_true': 18, 'dragon_pct': 120, 'burst_base': 280}},
    'source': 'SkillData [8,110,150,60]...; game8 measured bursts 168/224/310; threshold 60/80/100 rises per proc',
}
sv['Flayer'] = {
    'kind': 'armor',
    'levels': {'1': {'wound_pct': 105, 'burst': 100}, '2': {'wound_pct': 110, 'burst': 120}, '3': {'wound_pct': 115, 'burst': 160}, '4': {'wound_pct': 120, 'burst': 200}, '5': {'wound_pct': 130, 'burst': 250}},
    'note': 'game8 quotes bursts 140/160/220/300/400 vs game data 100..250 - unresolved; ~50% proc chance',
}
sv["Mind's Eye"]['condition'] = 'parts whose BASE hitzone <= 44 - irrelevant at hitzone 100'
d['sharpness']['_about'] = 'Community values confirmed by two independent sources (wiggler.pet, agent cross-check): element blue 1.0625, white 1.15; purple 1.25 (1.27 in one source).'
d['formula']['ranking_metric'] = 'Decided 2026-10-06: rank builds on EFR + EFE (both, summed per 100 MV).'
json.dump(d, open(p, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('GS Burst:', json.dumps(decoded['great-sword']))
print('LS Burst:', json.dumps(decoded['long-sword']))

# --- README
p = 'notes/README.md'
s = open(p, encoding='utf-8').read()
s = s.replace("2. Burst array reading, Critical Element second triple, Elemental Absorption / Convert Element / Flayer units (see VERIFY flags in `data/damage_model.json`).",
              "2. Resolved: Burst arrays decoded (GS Lv5 = +18 attack / +200 display element, LS Lv5 = +18 / +140), Critical Element GS 1.07/1.14/1.21 vs LS 1.05/1.10/1.15, Coalescence GS x1.10-1.30 vs LS x1.05-1.15. Still fuzzy: Elemental Absorption flat values, Flayer burst size, Charge Master grouping.")
s = s.replace("* Burst values are per weapon type (see `damage_model.json`); the reading of the 13-value arrays is still to be confirmed.",
              "* Burst is per weapon type: GS Lv1..5 = +10/+12/+14/+16/+18 attack and +80/+100/+120/+160/+200 display element (5 s window), LS = +8/+10/+12/+15/+18 and +60/+80/+100/+120/+140 (5 s); first hit +5/+50 (GS) or +4/+50 (LS).")
s = s.replace("* Critical Element: 1.05/1.10/1.15 (a second triple 1.07/1.14/1.21 exists in the data; which weapon classes use it is open).",
              "* Critical Element: 1.05/1.10/1.15 for fast weapons (LS, SnS, DB, Lance, IG, LBG, Bow), 1.07/1.14/1.21 for GS, Hammer, HH.")
s = s.replace("4. EFR/EFE ranking only, or a combo-DPS mode with motion values (needs the attack-name mapping for the rcol data).",
              "4. Combo-DPS mode with motion values is a later option (needs the attack-name mapping for the rcol data).")
s = s.replace("* Stack: C# / .NET 10 (solution `MHWildsOptimizer.sln`: Core library, CLI, xunit tests).",
              "* Stack: C# / .NET 10 (solution `MHWildsOptimizer.sln`: Core library, CLI, xunit tests).\n* Ranking metric: EFR + EFE combined (effective raw plus effective element per 100 MV).")
open(p, 'w', encoding='utf-8').write(s)
print('README updated')
