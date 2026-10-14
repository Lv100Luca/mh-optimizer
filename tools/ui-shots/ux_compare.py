# Side-by-side before/after images for the UX review fixes (notes/ux_review_2026-10-13.md):
#   python ux_compare.py <shotsDir> <outDir>
# <shotsDir> holds ux-before/<weapon>/ and ux-after/<weapon>/ from shots.mjs. Each pair: name, weapon folder, before
# shot, after shot, crop box (left, top, right, bottom) for the before and the after image (clamped), and a scale.
import os, sys
from PIL import Image, ImageDraw, ImageFont

shots, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)

try:
    font = ImageFont.truetype('segoeui.ttf', 22)
except Exception:
    font = ImageFont.load_default()

def load(side, folder, name):
    p = os.path.join(shots, 'ux-' + side, folder, name + '.png')
    return Image.open(p).convert('RGB') if os.path.exists(p) else Image.new('RGB', (600, 200), (40, 40, 40))

def crop(im, box):
    l, t, r, b = box
    return im.crop((max(0, l), max(0, t), min(im.width, r), min(im.height, b)))

def pair(name, folder, before, after, box_before, box_after=None, scale=1.0):
    a = crop(load('before', folder, before), box_before)
    b = crop(load('after', folder, after), box_after or box_before)
    if scale != 1.0:
        a = a.resize((int(a.width * scale), int(a.height * scale)), Image.LANCZOS)
        b = b.resize((int(b.width * scale), int(b.height * scale)), Image.LANCZOS)
    gap, head = 24, 40
    w, h = a.width + b.width + gap, max(a.height, b.height) + head
    im = Image.new('RGB', (w, h), (18, 20, 25))
    d = ImageDraw.Draw(im)
    d.text((8, 8), 'BEFORE', fill=(154, 161, 173), font=font)
    d.text((a.width + gap + 8, 8), 'AFTER', fill=(227, 182, 78), font=font)
    im.paste(a, (0, head))
    im.paste(b, (a.width + gap, head))
    d.line([(a.width + gap // 2, head), (a.width + gap // 2, h)], fill=(59, 66, 84), width=2)
    im.save(os.path.join(out, name + '.png'))
    print('wrote', name, im.size)

FULL = (0, 0, 1600, 8000)
PAIRS = [
    # 1, 2: a run in progress, the stage line and the readable log
    ('01-run-progress', 'fire-ls', 'x-running', 'x-running', (0, 0, 1600, 620), None, 0.6),
    # 3: results; the bow's default view (table before, cards after), the table that says what each build is
    ('02-results-default-view', 'dragon-bow', 'results-desktop', 'results-desktop', (300, 0, 1600, 1600), None, 0.5),
    ('03-results-table', 'fire-ls', 'x-results-table', 'x-results-table', (300, 0, 1600, 1100), None, 0.55),
    ('04-results-cards', 'fire-ls', 'x-results-cards', 'x-results-cards', (300, 380, 1600, 1500), None, 0.55),
    # 4: the weapon page
    ('05-weapon-page', 'fire-ls', 'weapon-desktop', 'weapon-desktop', (0, 0, 1600, 3100), None, 0.4),
    ('06-weapon-page-bow', 'dragon-bow', 'weapon-desktop', 'weapon-desktop', (0, 0, 1600, 3100), None, 0.4),
    # 5: targets
    ('07-targets-search', 'fire-ls', 'x-targets-speed-eat', 'x-targets-speed-eat', (300, 250, 1600, 760), None, 0.7),
    ('08-targets-preset-limit', 'dragon-gs', 'targets-desktop', 'targets-desktop', (300, 80, 1600, 520), None, 0.7),
    # 6: where things live
    ('09-attack-tab', 'dragon-gs', 'conditions-desktop', 'attack-desktop', (0, 0, 1600, 4400), None, 0.3),
    ('10-options-scope', 'fire-ls', 'options-desktop', 'options-desktop', (0, 0, 1600, 1800), None, 0.45),
    ('11-weapons-page', 'fire-ls', 'inventory-desktop', 'inventory-desktop', (0, 0, 1600, 2100), None, 0.4),
    ('12-new-weapon-from-results', 'fire-ls', 'x-new-from-results', 'x-new-from-results', (0, 0, 1600, 700), None, 0.5),
    # 7: header
    ('13-header', 'fire-ls', 'weapon-desktop', 'weapon-desktop', (0, 0, 1600, 82), None, 0.8),
    ('14-weapon-picker', 'fire-ls', 'x-weapon-picker-open', 'x-weapon-picker-open', (600, 0, 1600, 520), None, 0.8),
    ('15-new-weapon-header', 'fire-ls', 'x-header-dirty-new', 'x-header-dirty-new', (600, 0, 1600, 82), None, 0.9),
    ('16-save-dialog', 'fire-ls', 'x-save-as-dialog', 'x-save-as-dialog', (500, 1210, 1100, 1560), (500, 1150, 1100, 1500), 1.0),
    # 8: the warning in the status bar
    ('17-status-bar', 'fire-ls', 'x-header-dirty-new', 'x-header-dirty-new', (0, 2709, 1600, 2779), (0, 2595, 1600, 2665), 0.8),
]

for p in PAIRS:
    pair(*p)
