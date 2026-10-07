"""Monster hitzones for the target picker: data/raw/monsters.json (wilds.mhdb.io/en/monsters) -> data/monster_hitzones.json.

Run from the workspace root:  python tools/build_monster_hitzones.py

Per large monster, per part: raw hitzones (slash, blunt, pierce) and element hitzones (fire, water, thunder, ice, dragon)
in percent, the base state of the part (mhdb has no broken / enraged / wounded states). Left and right parts with the same
values are merged ("Left wing" + "Right wing" -> "Wing"), identical duplicates are dropped, and parts that still share a
name are numbered. Parts are sorted by slash hitzone, weakest point first.
"""
import json

SIDES = ('left-', 'right-')
KEYS = ('slash', 'blunt', 'pierce', 'fire', 'water', 'thunder', 'ice', 'dragon')


def label(kind):
    return kind.replace('-', ' ').capitalize()


def values(part):
    return tuple(round(part['multipliers'][k] * 100) for k in KEYS)


monsters = json.load(open('data/raw/monsters.json', encoding='utf-8'))
out = []
for m in sorted((m for m in monsters if m['kind'] == 'large'), key=lambda m: m['name']):
    parts = []  # (kind, values), identical duplicates dropped
    for p in m['parts']:
        entry = (p['kind'], values(p))
        if entry not in parts:
            parts.append(entry)

    # merge left/right pairs with the same values
    merged = []
    for kind, v in parts:
        side = next((s for s in SIDES if kind.startswith(s)), None)
        if side:
            base = kind[len(side):]
            other = (SIDES[1] if side == SIDES[0] else SIDES[0]) + base
            if (other, v) in parts:
                if (base, v) not in merged:
                    merged.append((base, v))
                continue
        merged.append((kind, v))

    names = [label(k) for k, _ in merged]
    counts = {n: names.count(n) for n in names}
    seen = {}
    rows = []
    for (kind, v), name in zip(merged, names):
        if counts[name] > 1:
            seen[name] = seen.get(name, 0) + 1
            name = f'{name} {seen[name]}'
        rows.append({'name': name, **dict(zip(KEYS, v))})
    rows.sort(key=lambda r: (-r['slash'], r['name']))
    out.append({'name': m['name'], 'parts': rows})

json.dump(out, open('data/monster_hitzones.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print(f'{len(out)} monsters, {sum(len(m["parts"]) for m in out)} parts')
