"""Normalize the raw wilds.mhdb.io dumps (data/raw/*.json) into compact optimizer-ready files in data/.

Run from the workspace root:  python tools/build_dataset.py
"""
import json
import collections


def load(n):
    return json.load(open(f'data/raw/{n}.json', encoding='utf-8'))


def dump(n, obj):
    json.dump(obj, open(f'data/{n}.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)


armor = load('armor')
skills = load('skills')
decos = load('decorations')
weapons = load('weapons')
charms = load('charms')
skill_by_id = {s['id']: s for s in skills}

# ---------------------------------------------------------------- skills
out_skills = []
for s in skills:
    out_skills.append({
        'id': s['id'], 'name': s['name'], 'kind': s['kind'], 'description': s['description'],
        'max_level': max(r['level'] for r in s['ranks']),
        'ranks': [{'level': r['level'], 'name': r.get('name'), 'pieces_required': r.get('setPiecesRequired'),
                   'description': r['description']} for r in sorted(s['ranks'], key=lambda r: r['level'])],
    })
dump('skills', sorted(out_skills, key=lambda s: (s['kind'], s['name'])))


# ---------------------------------------------------------------- armor (high rank only)
def transcended_slots(rarity, slots):
    """Armor Transcendence (TU4, HR100+).
    Source: game8 archives/571459 - R5: +1 level to all three slot positions, R6: +1 to first two positions,
    level cap 3. R7/R8: no slot change (only defense). VERIFY IN GAME before relying on it."""
    s = list(slots) + [0] * (3 - len(slots))
    if rarity == 5:
        s = [min(3, x + 1) for x in s]
    elif rarity == 6:
        s = [min(3, s[0] + 1), min(3, s[1] + 1), s[2]]
    return [x for x in s if x > 0]


out_armor = []
for a in armor:
    if a['rank'] != 'high':
        continue
    armor_skills, set_bonus, group = [], [], []
    for sk in a['skills']:
        k = skill_by_id[sk['skill']['id']]['kind']
        if k == 'set':
            set_bonus.append(sk['skill']['name'])
        elif k == 'group':
            group.append(sk['skill']['name'])
        else:
            armor_skills.append({'skill': sk['skill']['name'], 'skill_id': sk['skill']['id'], 'level': sk['level']})
    out_armor.append({
        'id': a['id'], 'name': a['name'],
        'set': a['armorSet']['name'] if a.get('armorSet') else None,
        'set_id': a['armorSet']['id'] if a.get('armorSet') else None,
        'piece': a['kind'], 'rarity': a['rarity'], 'slots': a['slots'],
        'slots_transcended': transcended_slots(a['rarity'], a['slots']),
        'skills': armor_skills, 'set_bonus': set_bonus, 'group_skill': group[0] if group else None,
        'defense_base': a['defense']['base'], 'defense_max': a['defense']['max'], 'resistances': a['resistances'],
    })
dump('armor_hr', out_armor)

# ---------------------------------------------------------------- armor sets summary (HR)
out_sets = {}
for p in out_armor:
    s = out_sets.setdefault(p['set'], {'set': p['set'], 'set_id': p['set_id'], 'rarity': p['rarity'], 'pieces': [],
                                       'set_bonus': set(), 'group_skill': None})
    s['pieces'].append({'piece': p['piece'], 'id': p['id'], 'name': p['name']})
    s['set_bonus'].update(p['set_bonus'])
    s['group_skill'] = s['group_skill'] or p['group_skill']
for s in out_sets.values():
    s['set_bonus'] = sorted(s['set_bonus'])
    s['piece_count'] = len(s['pieces'])
dump('armor_sets_hr', sorted(out_sets.values(), key=lambda s: (-s['rarity'], s['set'])))

# ---------------------------------------------------------------- set bonus / group index
bonus_index = {}
for p in out_armor:
    for b in p['set_bonus']:
        e = bonus_index.setdefault(b, {'kind': 'set', 'sets': set(), 'pieces': []})
        e['sets'].add(p['set']); e['pieces'].append(p['id'])
    if p['group_skill']:
        e = bonus_index.setdefault(p['group_skill'], {'kind': 'group', 'sets': set(), 'pieces': []})
        e['sets'].add(p['set']); e['pieces'].append(p['id'])
for k, v in bonus_index.items():
    sk = next(s for s in out_skills if s['name'] == k)
    v['sets'] = sorted(v['sets'])
    v['ranks'] = sk['ranks']
    v['armor_piece_count'] = len(v['pieces'])
dump('set_and_group_bonuses', dict(sorted(bonus_index.items())))

# ---------------------------------------------------------------- decorations
out_decos = [{'id': d['id'], 'name': d['name'], 'kind': d['kind'], 'slot': d['slot'], 'rarity': d['rarity'],
              'skills': [{'skill': x['skill']['name'], 'skill_id': x['skill']['id'], 'level': x['level']} for x in d['skills']]}
             for d in decos]
dump('decorations', sorted(out_decos, key=lambda d: (d['kind'], d['slot'], d['name'])))

# ---------------------------------------------------------------- charms (all ranks; optimizer normally uses max rank)
out_charms = []
for c in charms:
    ranks = [r for r in c['ranks'] if r['skills']]
    if not ranks:
        continue
    top = max(r['level'] for r in ranks)
    for r in ranks:
        out_charms.append({'charm_id': c['id'], 'name': r['name'], 'level': r['level'], 'rarity': r['rarity'],
                           'is_max_rank': r['level'] == top,
                           'skills': [{'skill': x['skill']['name'], 'skill_id': x['skill']['id'], 'level': x['level']} for x in r['skills']]})
dump('charms', out_charms)

# ---------------------------------------------------------------- skill sources index
src = {}
for s in out_skills:
    src[s['name']] = {'kind': s['kind'], 'max_level': s['max_level'], 'armor': [], 'decorations': [], 'charms': [], 'weapons_innate': 0}
for p in out_armor:
    for sk in p['skills']:
        src[sk['skill']]['armor'].append({'armor_id': p['id'], 'name': p['name'], 'piece': p['piece'], 'level': sk['level']})
for d in out_decos:
    for sk in d['skills']:
        src[sk['skill']]['decorations'].append({'deco': d['name'], 'slot': d['slot'], 'level': sk['level']})
for c in out_charms:
    if c['is_max_rank']:
        for sk in c['skills']:
            src[sk['skill']]['charms'].append({'charm': c['name'], 'level': sk['level']})
for w in weapons:
    for sk in w['skills']:
        src[sk['skill']['name']]['weapons_innate'] += 1
dump('skill_sources', src)

# ---------------------------------------------------------------- gogma weapons: base stat variants (3 per type = focus)
GOG = {'great-sword': 'Ostrak Oblivion', 'dual-blades': 'Eternal Cusp', 'charge-blade': 'Promised Abyss',
       'switch-axe': 'Wicked Regnum', 'long-sword': "Headsman's Hamus", 'gunlance': 'Auguring Omen',
       'insect-glaive': 'Limbo Llor', 'bow': 'Calamitous Angel', 'heavy-bowgun': 'Trembling Hels',
       'lance': 'Aether Pike', 'hammer': 'Bound Admonition', 'light-bowgun': 'Bethorned Agony',
       'hunting-horn': 'Onyx Choros', 'sword-shield': 'Kyrie Verd'}


def focus_of(w):
    # base R8 Artian = raw 190 / aff +5. Attack focus +10 atk -15 aff; Affinity focus -10 atk +10 aff; Element focus -5 aff
    return {(200, -10): 'attack', (180, 15): 'affinity', (190, 0): 'element'}[(w['damage']['raw'], w['affinity'])]


gog = collections.defaultdict(dict)
for w in weapons:
    if w['name'] == GOG.get(w['kind']):
        gog[w['kind']][focus_of(w)] = {
            'api_id': w['id'], 'name': w['name'], 'raw': w['damage']['raw'], 'display_attack': w['damage']['display'],
            'affinity': w['affinity'], 'sharpness': w.get('sharpness'), 'slots': w['slots'],
            'ammo': w.get('ammo'), 'coatings': w.get('coatings'), 'melodies': w.get('melodies'),
            'phial': w.get('phial'), 'shell': w.get('shell'), 'kinsect': w.get('kinsect'),
        }
dump('gogma_weapons_base', gog)
print('gogma variants per type:', {k: sorted(v) for k, v in gog.items()})
print('armor_hr', len(out_armor), '| sets', len(out_sets), '| skills', len(out_skills), '| decos', len(out_decos), '| charm ranks', len(out_charms))
