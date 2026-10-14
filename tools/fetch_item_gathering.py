"""Fetch where the wiki's items can be gathered (region and area numbers) into data/item_gathering.json.

Run from the workspace root after tools/build_wiki.py (it reads the item names from data/wiki.json), then run build_wiki.py
again so the item pages show the spots:

    python tools/build_wiki.py && python tools/fetch_item_gathering.py && python tools/build_wiki.py

Source: the "Gathering" lists of the item pages on monsterhunterwiki.org ("<item> (MHWilds)"), which come from Cola's
interactive map of the game data (https://c-ola.github.io/wildsmap/). The game files themselves only place gathering points
on the maps, so this is the one place the spots are listed per item. Uses only the standard library.
"""
import json
import re
import time
import urllib.parse
import urllib.request

API = 'https://monsterhunterwiki.org/api.php'
UA = 'MHWildsOptimizer-gathering-fetch/0.1 (build optimizer wiki; reads item pages once)'
OUT = 'data/item_gathering.json'
REGIONS = ['Windward Plains', 'Scarlet Forest', 'Oilwell Basin', 'Iceshard Cliffs', 'Ruins of Wyveria']
SPOT = re.compile(r'^\*\s*\[\[([^\]|]+)(?:\|[^\]]*)?\]\]\s*Zone\s*(\d+)', re.M)


def page_texts(titles):
    """page title -> wikitext, in batches of 40."""
    texts = {}
    for i in range(0, len(titles), 40):
        batch = titles[i:i + 40]
        params = {'action': 'query', 'prop': 'revisions', 'rvprop': 'content', 'rvslots': 'main', 'titles': '|'.join(batch),
                  'redirects': 1, 'format': 'json'}
        req = urllib.request.Request(API + '?' + urllib.parse.urlencode(params), headers={'User-Agent': UA})
        with urllib.request.urlopen(req, timeout=60) as r:
            d = json.load(r)
        for p in d['query']['pages'].values():
            revisions = p.get('revisions')
            if revisions:
                texts[p['title']] = revisions[0]['slots']['main']['*']
        time.sleep(0.2)
    return texts


def main():
    with open('data/wiki.json', encoding='utf-8') as f:
        names = [i['name'] for i in json.load(f).get('items', [])]
    texts = page_texts([f'{n} (MHWilds)' for n in names])
    items, no_page = {}, []
    for name in names:
        text = texts.get(f'{name} (MHWilds)')
        if text is None:
            no_page.append(name)
            continue
        section = re.search(r'^==\s*Gathering.*?$(.*?)(?=^==[^=]|\Z)', text, re.M | re.S)
        if not section:
            continue
        spots = {}
        for region, area in SPOT.findall(section.group(1)):
            if region not in REGIONS:
                raise SystemExit(f'{name}: unknown region {region!r} on the wiki page')
            spots.setdefault(region, []).append(int(area))
        if spots:
            items[name] = {r: sorted(set(spots[r])) for r in REGIONS if r in spots}
    out = {
        '_about': 'Where items are gathered: region -> area numbers, from the Gathering lists of the item pages on '
                  'monsterhunterwiki.org (taken from Cola\'s interactive map of the game data, https://c-ola.github.io/wildsmap/). '
                  'Written by tools/fetch_item_gathering.py; do not edit by hand.',
        'items': dict(sorted(items.items())),
    }
    with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
        f.write('\n')
    print(f'wrote {OUT}: {len(items)} of {len(names)} items have gathering spots')
    if no_page:
        print('no wiki page:', ', '.join(no_page))


if __name__ == '__main__':
    main()
