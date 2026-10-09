"""Cut the slot-kind marks (the helmet of armor decorations, the sword of weapon decorations) out of the game's decoration icons.

Run from the workspace root after tools/fetch_icons.py:  python tools/cut_slot_icons.py

The mark in the corner of a decoration icon is the same for every decoration color, while the gem and its frame take the
color: the pixels that stay the same across all level 1 and 2 icons of a kind are the mark (the biggest connected shape of
them, with its soft edge). Writes icons/ui/slot-armor.png and slot-weapon.png next to the decoration icons and adds them
to manifest.json. Needs Pillow.
"""
import glob
import json
import os
from collections import deque

from PIL import Image

ICONS = os.path.join('src', 'MHWildsOptimizer.Web', 'client', 'public', 'icons')
BOX = (78, 0, 120, 48)  # the top-right corner of a 120 px decoration icon
TOLERANCE = 6
NEIGHBOURS = [(dx, dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy]


def shared_pixels(kind):
    files = sorted(glob.glob(os.path.join(ICONS, 'decorations', f'l[12]-{kind}-*.png')))
    images = [Image.open(f).convert('RGBA').crop(BOX) for f in files]
    w, h = images[0].size
    first = images[0].load()
    loads = [im.load() for im in images]
    mark = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    out = mark.load()
    for y in range(h):
        for x in range(w):
            p = first[x, y]
            if p[3] >= 20 and all(max(abs(px[x, y][c] - p[c]) for c in range(4)) <= TOLERANCE for px in loads):
                out[x, y] = p
    return mark


def biggest_shape(im):
    w, h = im.size
    px = im.load()
    seen, best = set(), []
    for sy in range(h):
        for sx in range(w):
            if (sx, sy) in seen or px[sx, sy][3] <= 40:
                continue
            piece, todo = [], deque([(sx, sy)])
            while todo:
                x, y = todo.popleft()
                if not (0 <= x < w and 0 <= y < h) or (x, y) in seen or px[x, y][3] <= 40:
                    continue
                seen.add((x, y))
                piece.append((x, y))
                todo.extend((x + dx, y + dy) for dx, dy in NEIGHBOURS)
            if len(piece) > len(best):
                best = piece
    keep = set(best)
    result = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    out = result.load()
    for y in range(h):
        for x in range(w):
            if (x, y) in keep or (px[x, y][3] > 0 and any((x + dx, y + dy) in keep for dx, dy in NEIGHBOURS)):
                out[x, y] = px[x, y]
    return result.crop(result.getbbox())


def main():
    made = {}
    for kind, wiki in (('armor', 'Armor'), ('weapon', 'Sword')):
        rel = f'ui/slot-{kind}.png'
        biggest_shape(shared_pixels(kind)).save(os.path.join(ICONS, rel))
        made[rel] = f'corner mark of MHWilds-Decoration_Level_{{1,2}}-{wiki}_Icon_*.png (cut out by tools/cut_slot_icons.py)'
    path = os.path.join(ICONS, 'manifest.json')
    manifest = json.load(open(path, encoding='utf-8'))
    manifest['files'] = dict(sorted({**manifest['files'], **made}.items()))
    json.dump(manifest, open(path, 'w', encoding='utf-8'), indent=1)
    print('wrote', *made, sep='\n  ')


if __name__ == '__main__':
    main()
