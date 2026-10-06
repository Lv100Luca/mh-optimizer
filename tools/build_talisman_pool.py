"""Build data/random_talisman_pool.json from the game-data files in data/datamine
(RandomAmuletLotSkillTable, RandomAmuletAccSlot, RandomAmuletPtTable, AmuletData). Run from the repo root."""
import json
import re
import collections


def simplify(o):
    if isinstance(o, dict):
        if len(o) == 1:
            k = list(o)[0]
            if '.' in k or k.startswith('STRUCT'):
                return simplify(o[k])
        if set(o) == {'_Value'}:
            return simplify(o['_Value'])
        return {k: simplify(v) for k, v in o.items()}
    if isinstance(o, list):
        return [simplify(x) for x in o]
    return o


def load(name):
    return simplify(json.load(open(f'data/datamine/{name}.json', encoding='utf-8')))[0]['_Values']


api = json.load(open('data/raw/skills.json', encoding='utf-8'))
by_gid = {s['gameId']: s for s in api}


def skill_name(s):
    m = re.match(r'\[(-?\d+)\]', str(s))
    g = int(m.group(1)) if m else None
    return by_gid[g]['name'] if g in by_gid else None


def tag(s):
    return str(s).split(']')[1]


# ---- skills that can roll, with max level and points per level
skills = {}
for e in load('RandomAmuletLotSkillTable'):
    name = skill_name(e['_SkillType'])
    if not name:
        continue
    group = 'primary' if tag(e['_LotSkillType']) == 'LotSkill01' else 'secondary'
    entry = skills.setdefault(name, {'group': group, 'kind': by_gid[[k for k, v in by_gid.items() if v['name'] == name][0]]['kind'],
                                     'max_level': 0, 'points': {}})
    lv, pt = e['_SkillLv'], e['_SkillPt']
    entry['max_level'] = max(entry['max_level'], lv)
    entry['points'][str(lv)] = min(pt, entry['points'].get(str(lv), 99))

# ---- slot patterns (ACC_TYPE_00 = weapon decoration slot, ACC_TYPE_01 = armor decoration slot)
patterns = []
for e in load('RandomAmuletAccSlot'):
    slots = []
    for i in ('01', '02', '03'):
        lv = tag(e[f'_SlotLevel{i}'])
        if lv == 'NONE':
            continue
        slots.append({'kind': 'weapon' if tag(e[f'_SlotType{i}']) == 'ACC_TYPE_00' else 'armor', 'level': int(lv[2:])})
    patterns.append({'slots': slots, 'points': e['_SlotPt']})

# ---- point budgets per amulet type and the rarity of each type
rarity_of_type = {}
for a in load('AmuletData'):
    t = tag(a['_AmuletType'])
    if t.startswith('AT_01') and int(t[3:]) >= 189:
        rarity_of_type[t] = int(re.search(r'RARE(\d+)', a['_Rare']).group(1))
budgets = collections.defaultdict(lambda: {'skill1': set(), 'skill2': set(), 'skill3': set(), 'slot': set()})
for e in load('RandomAmuletPtTable'):
    b = budgets[tag(e['_AmuletType'])]
    b['skill1'].add(e['_SkillPt_01']); b['skill2'].add(e['_SkillPt_02']); b['skill3'].add(e['_SkillPt_03']); b['slot'].add(e['_SlotPt'])
budgets = {t: {k: sorted(v) for k, v in b.items()} | {'rarity': rarity_of_type.get(t)} for t, b in budgets.items()}

out = {
    '_about': 'What random talismans (melded charms) can roll, from the game data (dtlnor/MHWs-in-json RandomAmulet* tables). '
              'Skill slot 1 draws from the primary pool (weapon-kind skills, 1-4 points), skill slots 2-3 from the secondary pool '
              '(armor-kind skills, 5-10 points, 0 = empty). Decoration slots: weapon-kind slot = ACC_TYPE_00, armor-kind slot = ACC_TYPE_01. '
              'Used for validating user-entered talismans, not for generating hypothetical ones.',
    'rarity_by_type': rarity_of_type,
    'point_budgets_by_type': budgets,
    'slot_patterns': patterns,
    'skills': dict(sorted(skills.items())),
}
json.dump(out, open('data/random_talisman_pool.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print(len(skills), 'skills,', len(patterns), 'slot patterns, types:', rarity_of_type)
