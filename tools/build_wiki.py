"""Builds data/wiki.json, the data behind the app's wiki section, from the mhdb dumps in data/raw and the curated
data/skill_effects.json (what each skill level does in numbers). Run from the repo root after either changes.

Contents:
- skills: every skill on any piece of equipment (armor, set, group and weapon skills), its in-game texts, the curated effects
  and where it comes from: armor pieces (low and high rank), decorations, charms, weapons (innate skills), armor sets and the
  Gogma weapon skill pool. Skills that exist only as innate weapon skills are flagged `weapon_only`.
- armor_sets: every armor set (low and high rank) with its pieces: skills, slots, defense and resistances.
"""
import json
from collections import defaultdict

RAW = 'data/raw/'
PIECES = ['head', 'chest', 'arms', 'waist', 'legs']
WEAPON_ORDER = ['great-sword', 'long-sword', 'sword-shield', 'dual-blades', 'hammer', 'hunting-horn', 'lance', 'gunlance',
                'switch-axe', 'charge-blade', 'insect-glaive', 'light-bowgun', 'heavy-bowgun', 'bow']


def load(path):
    with open(path, encoding='utf-8') as f:
        return json.load(f)


raw_skills = load(RAW + 'skills.json')
raw_armor = load(RAW + 'armor.json')
raw_sets = load(RAW + 'armor-sets.json')
raw_weapons = load(RAW + 'weapons.json')
decorations = load('data/decorations.json')
charms = load('data/charms.json')
armor_hr = {a['id']: a for a in load('data/armor_hr.json')}
pool = load('data/gogma_skill_pool.json')
effects = load('data/skill_effects.json')

kind_of = {s['name']: s['kind'] for s in raw_skills}
set_of_piece = {}
for s in raw_sets:
    for p in s['pieces']:
        set_of_piece[p['id']] = s['name']

# ---------------------------------------------------------------- armor sets

sets = {}
for a in raw_armor:
    set_name = set_of_piece.get(a['id']) or a.get('armorSet', {}).get('name')
    entry = sets.setdefault(set_name, {'name': set_name, 'rank': a['rank'], 'rarity': 0, 'set_bonus': [], 'group_skill': None, 'pieces': []})
    skills, set_bonus, group = [], [], None
    for sk in a['skills']:
        name, kind = sk['skill']['name'], sk['skill']['kind']
        if kind == 'set':
            set_bonus.append(name)
        elif kind == 'group':
            group = name
        else:
            skills.append({'skill': name, 'level': sk['level']})
    for b in set_bonus:
        if b not in entry['set_bonus']:
            entry['set_bonus'].append(b)
    entry['group_skill'] = entry['group_skill'] or group
    entry['rarity'] = max(entry['rarity'], a['rarity'])
    hr = armor_hr.get(a['id'])
    piece = {
        'id': a['id'], 'name': a['name'], 'piece': a['kind'], 'rarity': a['rarity'],
        'slots': a['slots'],
        'skills': skills,
        'defense_base': a['defense']['base'], 'defense_max': a['defense']['max'],
        'resistances': a['resistances'],
    }
    piece['set_bonus'] = set_bonus
    if hr and hr['slots_transcended'] != a['slots']:
        piece['slots_transcended'] = hr['slots_transcended']
    entry['pieces'].append(piece)

for s in sets.values():
    s['pieces'].sort(key=lambda p: PIECES.index(p['piece']))
    s['set_bonus'].sort()
    # kept per piece only where pieces differ (Gogmazios: each carries Gogmapocalypse and one other monster's set bonus)
    if len(s['set_bonus']) <= 1:
        for p in s['pieces']:
            del p['set_bonus']
armor_sets = sorted(sets.values(), key=lambda s: (s['rank'] != 'high', -s['rarity'], s['name']))

# ---------------------------------------------------------------- where each skill comes from

src = defaultdict(lambda: {'armor': [], 'decorations': [], 'charms': [], 'weapons': [], 'sets': []})
for s in armor_sets:
    for p in s['pieces']:
        for sk in p['skills']:
            src[sk['skill']]['armor'].append({'name': p['name'], 'set': s['name'], 'piece': p['piece'], 'rank': s['rank'], 'rarity': p['rarity'], 'level': sk['level']})
        for b in p.get('set_bonus', s['set_bonus']):
            if s['name'] not in src[b]['sets']:
                src[b]['sets'].append(s['name'])
        if s['group_skill'] and s['name'] not in src[s['group_skill']]['sets']:
            src[s['group_skill']]['sets'].append(s['name'])

for d in decorations:
    for sk in d['skills']:
        src[sk['skill']]['decorations'].append({'name': d['name'], 'kind': d['kind'], 'slot': d['slot'], 'rarity': d['rarity'], 'icon_color': d['icon_color'], 'level': sk['level']})

# a charm line once, at its highest rank that has the skill
best_charm = {}
for c in charms:
    for sk in c['skills']:
        key = (sk['skill'], c['name'].rsplit(' ', 1)[0] if c['name'].split()[-1] in ('I', 'II', 'III', 'IV', 'V') else c['name'])
        if key not in best_charm or sk['level'] > best_charm[key]['level']:
            best_charm[key] = {'name': c['name'], 'rarity': c['rarity'], 'level': sk['level']}
for (skill, _), c in best_charm.items():
    src[skill]['charms'].append(c)

for w in raw_weapons:
    for sk in w['skills']:
        src[sk['skill']['name']]['weapons'].append({'name': w['name'], 'kind': w['kind'], 'rarity': w['rarity'], 'level': sk['level']})

for v in src.values():
    v['armor'].sort(key=lambda a: (a['rank'] != 'high', -a['level'], -a['rarity'], a['name']))
    v['decorations'].sort(key=lambda d: (d['slot'], -d['level'], d['name']))
    v['charms'].sort(key=lambda c: (-c['level'], c['name']))
    v['weapons'].sort(key=lambda w: (WEAPON_ORDER.index(w['kind']), -w['rarity'], w['name']))
    v['sets'].sort()

# ---------------------------------------------------------------- skills

in_pool = set(pool['set_bonuses_in_pool']) | set(pool['group_skills_in_pool'])
skills = []
for s in sorted(raw_skills, key=lambda s: s['name']):
    name = s['name']
    so = src[name]
    weapon_only = bool(so['weapons']) and not so['armor'] and not so['decorations'] and not so['charms']
    skill = {
        'id': s['id'], 'name': name, 'kind': s['kind'], 'icon': s['icon']['kind'],
        'description': s['description'],
        'max_level': max(r['level'] for r in s['ranks']),
        'ranks': [{'level': r['level'], 'name': r['name'], 'pieces': r['setPiecesRequired'], 'description': r['description']} for r in s['ranks']],
        'weapon_only': weapon_only,
        'gogma_pool': name in in_pool,
        'effects': effects.get(name),
        'sources': {k: v for k, v in so.items() if v},
    }
    skills.append(skill)

missing = [s['name'] for s in skills if s['effects'] is None]
if missing:
    print(f'no curated effects for {len(missing)} skills: {", ".join(missing)}')
unknown = sorted(set(effects) - {s['name'] for s in skills} - {'_about'})
if unknown:
    raise SystemExit(f'skill_effects.json names skills that do not exist: {unknown}')

out = {
    '_about': 'Generated by tools/build_wiki.py from data/raw (wilds.mhdb.io dumps) and data/skill_effects.json; do not edit by hand.',
    'skills': skills,
    'armor_sets': armor_sets,
}
with open('data/wiki.json', 'w', encoding='utf-8', newline='\n') as f:
    json.dump(out, f, ensure_ascii=False, separators=(',', ':'))
print(f'wrote data/wiki.json: {len(skills)} skills ({sum(s["weapon_only"] for s in skills)} weapon-only), {len(armor_sets)} armor sets')
