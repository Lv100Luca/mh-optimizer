"""Builds data/wiki.json, the data behind the app's wiki section, from the mhdb dumps in data/raw and the curated
data/skill_effects.json (what each skill level does in numbers). Run from the repo root after either changes.

Contents:
- skills: every skill on any piece of equipment (armor, set, group and weapon skills), its in-game texts, the curated effects
  and where it comes from: armor pieces (low and high rank), decorations, charms, weapons (innate skills), armor sets and the
  Gogma weapon skill pool. Skills that exist only as innate weapon skills are flagged `weapon_only`.
- armor_sets: every armor set (low and high rank) with its pieces: skills, slots, defense and resistances.
- items, item_categories: the common consumables curated in data/item_effects.json (what they do in numbers, stacking), with the
  game's text, icon, rarity and recipes from data/raw/items.json, plus every material their recipes need (category
  "materials"). Each item says where it comes from: gathering spots (data/item_gathering.json), material retrieval, the item
  shop and the support ship (data/datamine CollectionItemData, ItemShopData, SupportShipData from dtlnor/MHWs-in-json) and
  monster rewards (data/raw/monsters.json). After adding items, run tools/fetch_item_gathering.py and this script again, then
  tools/fetch_icons.py for the new icons.
- meals: how meals work and every meal skill, curated in data/meal_skills.json.
- weapons: every crafted weapon of the forge trees (Artian weapons have no tree) with its stats, sharpness, slots, skills,
  the forge screen's position (row = lane, col = tier, previous, branches), what forging or upgrading it costs (zenny and
  materials with their sell value) and the type's own lines (phial, shelling, kinsect level, songs, coatings, ammo).

Exact texts: a skill's `text` in skill_effects.json (the game's per-level wording with the exact numbers) replaces the game's
rank description, in data/wiki.json (the game's own wording kept as `game`) and in data/skills.json (kept as
`game_description`), so the optimizer's skill cards and pickers show it too. Run this again after tools/build_dataset.py.
"""
import json
import os
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

def exact_text(name, level):
    return ((effects.get(name) or {}).get('text') or {}).get(str(level))


def rank(name, r):
    out = {'level': r['level'], 'name': r['name'], 'pieces': r['setPiecesRequired'], 'description': exact_text(name, r['level']) or r['description']}
    if out['description'] != r['description']:
        out['game'] = r['description']
    return out


in_pool = set(pool['set_bonuses_in_pool']) | set(pool['group_skills_in_pool'])
skills = []
for s in sorted(raw_skills, key=lambda s: s['name']):
    name = s['name']
    so = src[name]
    weapon_only = bool(so['weapons']) and not so['armor'] and not so['decorations'] and not so['charms']
    fx = effects.get(name)
    skill = {
        'id': s['id'], 'name': name, 'kind': s['kind'], 'icon': s['icon']['kind'],
        'description': s['description'],
        'max_level': max(r['level'] for r in s['ranks']),
        'ranks': [rank(name, r) for r in s['ranks']],
        'weapon_only': weapon_only,
        'gogma_pool': name in in_pool,
        'effects': {k: v for k, v in fx.items() if k != 'text'} if fx else None,
        'sources': {k: v for k, v in so.items() if v},
    }
    skills.append(skill)

levels_of = {s['name']: {str(r['level']) for r in s['ranks']} for s in raw_skills}
for name, fx in effects.items():
    if name != '_about' and name in levels_of and set(fx.get('text', {})) - levels_of[name]:
        raise SystemExit(f'skill_effects.json: {name} has text for levels the skill does not have: {sorted(set(fx["text"]) - levels_of[name])}')

missing = [s['name'] for s in skills if s['effects'] is None]
if missing:
    print(f'no curated effects for {len(missing)} skills: {", ".join(missing)}')
unknown = sorted(set(effects) - {s['name'] for s in skills} - {'_about'})
if unknown:
    raise SystemExit(f'skill_effects.json names skills that do not exist: {unknown}')

# ---------------------------------------------------------------- items and meals

raw_items = {i['name']: i for i in load(RAW + 'items.json')}
item_fx = load('data/item_effects.json')
used_in = defaultdict(list)
for i in raw_items.values():
    for r in i['recipes']:
        for part in r['inputs']:
            if i['name'] not in used_in[part['name']]:
                used_in[part['name']].append(i['name'])


def recipes_of(name):
    return [[p['name'] for p in r['inputs']] for r in raw_items[name]['recipes'] if r['inputs']]


# The materials: everything the curated items are made from, down to what is gathered, bought or carved, so every ingredient
# on an item page links to a page that says where to get it.
materials = []
for name in item_fx['items']:
    if name not in raw_items:
        raise SystemExit(f'item_effects.json: no item called {name} in data/raw/items.json')
todo = list(item_fx['items'])
while todo:
    for part in (p for r in recipes_of(todo.pop()) for p in r):
        if part not in item_fx['items'] and part not in materials:
            materials.append(part)
            todo.append(part)
materials.sort()

# Where an item comes from: curated lines (`obtain` in item_effects.json), gathering spots (data/item_gathering.json, tools/fetch_item_gathering.py), material retrieval,
# the item shop and the support ship (data/datamine, dtlnor/MHWs-in-json Facility tables), monster rewards (mhdb).
REGIONS = ['Windward Plains', 'Scarlet Forest', 'Oilwell Basin', 'Iceshard Cliffs', 'Ruins of Wyveria']  # ST101..ST105
DROP_KINDS = {
    'target-reward': 'quest reward', 'carve': 'carve', 'carve-severed': 'carve (severed part)',
    'carve-rotten': 'carve (rotted corpse)', 'carve-rotten-severed': 'carve (rotted severed part)',
    'carve-crystallized': 'carve (crystallized corpse)', 'broken-part': 'break', 'broken-fragment': 'broken fragment',
    'wound-destroyed': 'destroy a wound', 'tempered-wound-destroyed': 'destroy a wound (tempered)',
}


def datamine(name):
    def simplify(o):
        if isinstance(o, dict):
            if len(o) == 1 and ('.' in next(iter(o)) or next(iter(o)) == '_Value'):
                return simplify(next(iter(o.values())))
            return {k: simplify(v) for k, v in o.items()}
        return [simplify(x) for x in o] if isinstance(o, list) else o
    return simplify(load(f'data/datamine/{name}.json'))[0]['_Values']


def game_id(value):  # "[170]ITEM_0168" -> 170, mhdb's gameId
    return int(value[1:value.index(']')])


item_by_game_id = {i['gameId']: i['name'] for i in raw_items.values()}
in_shop = {item_by_game_id.get(game_id(r['_ItemId'])) for r in datamine('ItemShopData')}
on_ship = {item_by_game_id.get(game_id(r['_ItemId'])) for r in datamine('SupportShipData')}
retrieval = {}
for r in datamine('CollectionItemData'):
    # one level per retrieval spot (the five regions' camps) and environment state; level 0 brings none
    levels = zip(r['_FertilityLevel'], r['_AbnormalLevel'], r['_RuinLevel'])
    regions = [REGIONS[k] for k, ls in enumerate(levels) if any(not l.endswith('LEVEL_00') for l in ls)]
    retrieval[item_by_game_id.get(game_id(r['_ItemId']))] = regions
gathering = load('data/item_gathering.json')['items'] if os.path.exists('data/item_gathering.json') else {}
drops = defaultdict(dict)
for m in load(RAW + 'monsters.json'):
    for reward in m['rewards']:
        for c in reward['conditions']:
            how = DROP_KINDS.get(c['kind'], c['kind'].replace('-', ' '))
            if c['part']:
                how += ' ' + c['part'].replace('-', ' ')
            key = (m['name'], c['rank'], how)
            drops[reward['item']['name']][key] = max(drops[reward['item']['name']].get(key, 0), c['chance'])


def sources_of(name, fx):
    found = {
        'other': fx.get('obtain'),
        'gather': [{'region': r, 'areas': a} for r, a in gathering.get(name, {}).items()] or None,
        'retrieval': retrieval.get(name) or None,
        'shop': name in in_shop or None,
        'support_ship': name in on_ship or None,
        'monsters': [{'monster': m, 'rank': rank, 'how': how, 'chance': chance}
                     for (m, rank, how), chance in sorted(drops[name].items(), key=lambda kv: (kv[0][0], kv[0][1] != 'low', kv[0][2]))]
                    or None,
    }
    return {k: v for k, v in found.items() if v} or None


def item_entry(name, category, fx):
    i = raw_items[name]
    return {
        'id': i['id'], 'name': name, 'category': category, 'icon': i['icon']['kind'], 'icon_color': i['icon']['color'],
        'rarity': i['rarity'], 'carry_limit': i['carryLimit'], 'description': i['description'],
        'boost': fx.get('boost'), 'duration': fx.get('duration'), 'effects': fx.get('effects', []), 'stacking': fx.get('stacking'),
        'notes': fx.get('notes'),
        'recipes': recipes_of(name) or None,
        'used_in': used_in.get(name) or None,
        'related': fx.get('related'),
        'sources': sources_of(name, fx),
        'source': fx.get('source'), 'confidence': fx.get('confidence'),
    }


categories = item_fx['categories']
if 'materials' not in {c['key'] for c in categories}:
    raise SystemExit('item_effects.json: the materials category is missing')
items = []
for name, fx in item_fx['items'].items():
    if fx['category'] not in {c['key'] for c in categories} or fx['category'] == 'materials':
        raise SystemExit(f'item_effects.json: {name} has an unknown category {fx["category"]}')
    for other in fx.get('related', []):
        if other not in item_fx['items']:
            raise SystemExit(f'item_effects.json: {name} relates to {other}, which the wiki does not have')
    items.append(item_entry(name, fx['category'], fx))
items += [item_entry(name, 'materials', {}) for name in materials]
unsourced = [i['name'] for i in items if not i['sources'] and not i['recipes']]
if unsourced:
    print(f'no source and no recipe for {len(unsourced)} items (run tools/fetch_item_gathering.py?): {", ".join(unsourced)}')

meal_fx = load('data/meal_skills.json')
meals = {
    'about': meal_fx['about'], 'tables': meal_fx.get('tables'), 'notes': meal_fx.get('notes'), 'source': meal_fx.get('source'),
    'skills': [{'name': name, 'icon': m['icon'], 'description': m.get('description'), 'effects': m['effects'], 'notes': m.get('notes'),
                'from': m.get('from'), 'source': m.get('source'), 'confidence': m.get('confidence')}
               for name, m in sorted(meal_fx['skills'].items())],
}

# ---------------------------------------------------------------- weapons: the forge trees (Artian weapons have no series)

def material(m):
    it = m['item']
    ic = it.get('icon') or {}
    return {'name': it['name'], 'quantity': m['quantity'], 'rarity': it['rarity'], 'icon': ic.get('kind'), 'icon_color': ic.get('color'), 'value': it['value']}


def cost(zenny, materials):
    return {'zenny': zenny, 'materials': [material(m) for m in materials]} if zenny or materials else None


def extras(w):
    """The type's own lines (phial, shelling, kinsect, songs, coatings, ammo) as label / value pairs."""
    out = []
    phial = w.get('phial')
    if phial:
        p = phial if isinstance(phial, dict) else {'kind': phial}
        out.append(['Phial', p['kind'].title() + (f" {p['damage']['display']}" if p.get('damage') else '')])
    if w.get('shell'):
        out.append(['Shelling', f"{w['shell'].title()} Lv {w.get('shellLevel') or '?'}"])
    if w.get('kinsectLevel'):
        out.append(['Kinsect level', str(w['kinsectLevel'])])
    if w.get('melody'):
        out.append(['Songs', ', '.join(s['name'] for s in w['melody']['songs'])])
    if w.get('echoBubble'):
        out.append(['Echo bubble', w['echoBubble']['name']])
    if w.get('echoWave'):
        out.append(['Echo wave', w['echoWave']['name']])
    if w.get('coatings'):
        out.append(['Coatings', ', '.join(c.replace('-', ' ').title() for c in w['coatings'])])
    if w.get('specialAmmo'):
        out.append(['Special ammo', w['specialAmmo'].title()])
    if w.get('ammo'):
        out.append(['Ammo', ', '.join(f"{a['kind'].title()} {a['level']} x{a['capacity']}{' (rapid)' if a.get('rapid') else ''}" for a in w['ammo'])])
    return out


weapons = []
for w in raw_weapons:
    if not w.get('series'):
        continue
    c = w['crafting']
    element = next((s for s in w['specials'] if s['kind'] == 'element'), None)
    status = next((s for s in w['specials'] if s['kind'] == 'status'), None)
    weapons.append({
        'id': w['id'], 'name': w['name'], 'kind': w['kind'], 'rarity': w['rarity'], 'tree': w['series']['name'],
        'row': c['row'], 'col': c['column'], 'previous': (c['previous'] or {}).get('name'), 'branches': [b['name'] for b in c['branches']],
        'craftable': c['craftable'],
        'raw': w['damage']['raw'], 'display_attack': w['damage']['display'], 'affinity': w['affinity'], 'defense_bonus': w['defenseBonus'],
        'element': element['element'] if element else None, 'element_display': element['damage']['display'] if element else 0,
        'status': status['status'] if status else None, 'status_display': status['damage']['display'] if status else 0,
        'sharpness': w.get('sharpness'), 'handicraft': w.get('handicraft'), 'slots': w['slots'],
        'skills': [{'skill': s['skill']['name'], 'level': s['level']} for s in w['skills']],
        'description': w['description'],
        'craft': cost(c['craftingZennyCost'], c['craftingMaterials']) if c['craftable'] else None,
        'upgrade': cost(c['upgradeZennyCost'], c['upgradeMaterials']) if c['previous'] else None,
        'extras': extras(w),
    })
weapons.sort(key=lambda w: (WEAPON_ORDER.index(w['kind']), w['row'], w['col']))

out = {
    '_about': 'Generated by tools/build_wiki.py from data/raw (wilds.mhdb.io dumps), data/skill_effects.json, data/item_effects.json '
              'and data/meal_skills.json; do not edit by hand.',
    'skills': skills,
    'armor_sets': armor_sets,
    'items': items,
    'item_categories': categories,
    'meals': meals,
    'weapons': weapons,
}
with open('data/wiki.json', 'w', encoding='utf-8', newline='\n') as f:
    json.dump(out, f, ensure_ascii=False, separators=(',', ':'))
print(f'wrote data/wiki.json: {len(skills)} skills ({sum(s["weapon_only"] for s in skills)} weapon-only), {len(armor_sets)} armor sets, '
      f'{len(items)} items, {len(meals["skills"])} meal skills, {len(weapons)} weapons')

# ---------------------------------------------------------------- exact texts into the optimizer's skills.json

game_text = {(s['name'], r['level']): r['description'] for s in raw_skills for r in s['ranks']}
dataset = load('data/skills.json')
changed = 0
for s in dataset:
    for r in s['ranks']:
        game = game_text.get((s['name'], r['level']), r.get('game_description', r['description']))
        text = exact_text(s['name'], r['level']) or game
        r.pop('game_description', None)
        r['description'] = text
        if text != game:
            r['game_description'] = game
            changed += 1
with open('data/skills.json', 'w', encoding='utf-8', newline='\r\n') as f:
    json.dump(dataset, f, ensure_ascii=False, indent=1)
print(f'wrote data/skills.json: exact texts on {changed} skill levels')
