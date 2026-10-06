"""Download the game UI icons the web UI uses from monsterhunterwiki.org into the client's public folder.

Run from the workspace root:  python tools/fetch_icons.py [--force]

Sources: monsterhunterwiki.org (MediaWiki). The icon images are Capcom's Monster Hunter Wilds UI assets as uploaded
there. Only small (100-120 px) UI icons are fetched: skill-category icons, decoration icons per level / slot kind /
color, armor-piece and weapon icons per rarity, talisman icons, element (blight) icons.
Writes src/MHWildsOptimizer.Web/client/public/icons/<category>/<name> and a manifest.json next to them.
"""
import json
import os
import sys
import time
import urllib.parse
import urllib.request
from datetime import datetime, timezone

API = 'https://monsterhunterwiki.org/api.php'
UA = 'MHWildsOptimizer-icon-fetch/0.1 (build optimizer web UI; fetches small UI icons once)'
OUT = os.path.join('src', 'MHWildsOptimizer.Web', 'client', 'public', 'icons')
FORCE = '--force' in sys.argv

# mhdb skill icon kind -> wiki icon name
SKILL_KINDS = {
    'attack': 'Attack', 'affinity': 'Affinity', 'element': 'Element', 'handicraft': 'Sharpness', 'ranged': 'Ammo',
    'defense': 'Defense', 'health': 'Health', 'stamina': 'Stamina', 'offense': 'Empower', 'utility': 'Reinforce',
    'item': 'Item', 'gathering': 'Gathering', 'group': 'Group', 'set': 'Set',
}
DECO_COLORS = ['Blue', 'Brown', 'Dark_Blue', 'Dark_Purple', 'Emerald', 'Gray', 'Green', 'Lemon', 'Light_Blue', 'Light_Green',
               'Moss', 'Orange', 'Pink', 'Purple', 'Red', 'Rose', 'Tan', 'Vermilion', 'Violet', 'White', 'Yellow']
ARMOR = {'head': 'Helmet', 'chest': 'Chestplate', 'arms': 'Armguards', 'waist': 'Waist', 'legs': 'Leggings'}
WEAPONS = {
    'great-sword': 'Great_Sword', 'sword-shield': 'Sword_and_Shield', 'dual-blades': 'Dual_Blades', 'long-sword': 'Long_Sword',
    'hammer': 'Hammer', 'hunting-horn': 'Hunting_Horn', 'lance': 'Lance', 'gunlance': 'Gunlance', 'switch-axe': 'Switch_Axe',
    'charge-blade': 'Charge_Blade', 'insect-glaive': 'Insect_Glaive', 'bow': 'Bow', 'heavy-bowgun': 'Heavy_Bowgun',
    'light-bowgun': 'Light_Bowgun',
}
ELEMENTS = ['Fire', 'Water', 'Thunder', 'Ice', 'Dragon']


def kebab(s):
    return s.replace('_', '-').lower()


def wanted():
    """relative output path -> wiki file title (without the File: prefix)."""
    files = {}
    for kind, wiki in SKILL_KINDS.items():
        files[f'skills/{kind}.png'] = f'MHWA-{wiki}_Skill_Icon.png'
    for level in (1, 2, 3):
        for kind, wiki in (('armor', 'Armor'), ('weapon', 'Sword')):
            for color in DECO_COLORS:
                files[f'decorations/l{level}-{kind}-{kebab(color)}.png'] = f'MHWilds-Decoration_Level_{level}-{wiki}_Icon_{color}.png'
    for kind, wiki in ARMOR.items():
        files[f'armor/{kind}-base.webp'] = f'MHWA-{wiki}_Icon_Base.webp'
        for r in range(1, 9):
            files[f'armor/{kind}-r{r}.png'] = f'MHWA-{wiki}_Icon_Rare_{r}.png'
    for kind, wiki in WEAPONS.items():
        files[f'weapons/{kind}-base.webp'] = f'MHWA-{wiki}_Icon_Base.webp'
        files[f'weapons/{kind}-r8.png'] = f'MHWA-{wiki}_Icon_Rare_8.png'
    for r in range(0, 9):
        files[f'talisman/r{r}.png'] = f'MHWA-Talisman_Icon_Rare_{r}.png'
    for e in ELEMENTS:
        files[f'elements/{e.lower()}.png'] = f'MHWA-{e}blight_Icon.png'
    files['ui/affinity-up.png'] = 'MHWA-Affinity_Up_Icon.png'
    files['ui/attack-up.png'] = 'MHWA-Attack_Up_Icon.png'
    files['ui/defense-up.png'] = 'MHWA-Defense_Up_Icon.png'
    files['ui/sharpness.png'] = 'MHWA-Restore_Sharpness_Icon.png'
    files['ui/decoration.png'] = 'UI-Decoration_Icon.png'
    return files


def api(params):
    req = urllib.request.Request(API + '?' + urllib.parse.urlencode(params), headers={'User-Agent': UA})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.load(r)


def resolve_urls(titles):
    """wiki title -> direct image URL, via prop=imageinfo in batches of 50."""
    urls = {}
    titles = list(titles)
    for i in range(0, len(titles), 50):
        batch = titles[i:i + 50]
        d = api({'action': 'query', 'titles': '|'.join('File:' + t for t in batch), 'prop': 'imageinfo', 'iiprop': 'url', 'format': 'json'})
        normalized = {n['to']: n['from'] for n in d.get('query', {}).get('normalized', [])}
        for page in d.get('query', {}).get('pages', {}).values():
            title = page.get('title', '')
            original = normalized.get(title, title)
            name = original[len('File:'):] if original.startswith('File:') else original
            info = page.get('imageinfo')
            if info:
                urls[name.replace(' ', '_')] = info[0]['url']
        time.sleep(0.2)
    return urls


def main():
    files = wanted()
    os.makedirs(OUT, exist_ok=True)
    todo = {rel: title for rel, title in files.items() if FORCE or not os.path.exists(os.path.join(OUT, rel))}
    print(f'{len(files)} icons wanted, {len(todo)} to download')
    urls = resolve_urls(sorted(set(todo.values())))
    missing = sorted(set(todo.values()) - set(urls))
    if missing:
        print('not found on the wiki:', *missing, sep='\n  ')
    done = 0
    for rel, title in sorted(todo.items()):
        url = urls.get(title)
        if not url:
            continue
        path = os.path.join(OUT, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        req = urllib.request.Request(url, headers={'User-Agent': UA})
        try:
            with urllib.request.urlopen(req, timeout=60) as r, open(path, 'wb') as f:
                f.write(r.read())
            done += 1
        except Exception as e:  # keep going; report at the end
            print(f'failed {title}: {e}')
        time.sleep(0.15)
    manifest = {
        'source': 'https://monsterhunterwiki.org (MediaWiki file uploads)',
        'note': 'Monster Hunter Wilds UI icons are Capcom assets, fetched for a personal, non-commercial build optimizer UI.',
        'fetched_at': datetime.now(timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ'),
        'files': {rel: title for rel, title in sorted(files.items()) if os.path.exists(os.path.join(OUT, rel))},
    }
    json.dump(manifest, open(os.path.join(OUT, 'manifest.json'), 'w', encoding='utf-8'), indent=1)
    present = len(manifest['files'])
    print(f'downloaded {done}, {present}/{len(files)} icons present under {OUT}')


if __name__ == '__main__':
    main()
