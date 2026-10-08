# Builds side-by-side before/after images for the UI pass: python compare.py <outDir>
# Each entry: name, before file, after file, crop box (left, top, right, bottom) applied to both (clamped), optional scale.
import os, sys
from PIL import Image, ImageDraw, ImageFont

here = os.environ.get('SHOTS_DIR') or os.path.dirname(os.path.abspath(__file__))
out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, 'compare')
os.makedirs(out, exist_ok=True)

def load(folder, name):
    p = os.path.join(here, folder, name + '.png')
    return Image.open(p) if os.path.exists(p) else None

def crop(im, box):
    if im is None:
        return Image.new('RGB', (600, 200), (40, 40, 40))
    l, t, r, b = box
    return im.crop((max(0, l), max(0, t), min(im.width, r), min(im.height, b)))

try:
    font = ImageFont.truetype('segoeui.ttf', 22)
except Exception:
    font = ImageFont.load_default()

def pair(name, before, after, box, scale=1.0, box_after=None, label_before='before', label_after='after'):
    a = crop(load('before', before), box)
    b = crop(load('after', after), box_after or box)
    if scale != 1.0:
        a = a.resize((int(a.width * scale), int(a.height * scale)), Image.LANCZOS)
        b = b.resize((int(b.width * scale), int(b.height * scale)), Image.LANCZOS)
    gap, head = 24, 40
    w = a.width + b.width + gap
    h = max(a.height, b.height) + head
    im = Image.new('RGB', (w, h), (18, 20, 25))
    d = ImageDraw.Draw(im)
    d.text((8, 8), label_before.upper(), fill=(154, 161, 173), font=font)
    d.text((a.width + gap + 8, 8), label_after.upper(), fill=(227, 182, 78), font=font)
    im.paste(a, (0, head))
    im.paste(b, (a.width + gap, head))
    d.line([(a.width + gap // 2, head), (a.width + gap // 2, h)], fill=(59, 66, 84), width=2)
    im.save(os.path.join(out, name + '.png'))
    print('wrote', name, im.size)

pairs = [
    # shell
    ('01-topbar-and-nav', 'inventory-desktop', 'inventory-desktop', (0, 0, 1600, 780), 0.8),
    ('02-inventory-table', 'inventory-desktop', 'inventory-desktop', (330, 290, 1570, 1000), 0.8),
    ('03-status-bar-warning', 'x-new-results', 'x-new-results', (0, 840, 1600, 910), 0.9),
    ('04-phone-topbar-nav', 'inventory-phone', 'inventory-phone', (0, 0, 412, 560), 1.0),
    # weapon
    ('05-weapon-focus-and-infusion-segmented', 'weapon-desktop', 'weapon-desktop', (300, 980, 1000, 1420), 1.0),
    ('06-weapon-sharpness-segmented-bug', 'x-weapon-stats-mode', 'x-weapon-stats-mode', (300, 1360, 1100, 1640), 1.0),
    ('07-weapon-set-bonus-search', 'x-weapon-setbonus-open', 'x-weapon-setbonus-open', (300, 1620, 1100, 2185), 1.0),
    ('08-weapon-phone-focus', 'weapon-phone', 'weapon-phone', (0, 1280, 412, 1900), 1.0),
    # skill pair
    ('09-skill-pair-choice-cards', 'pair-desktop', 'pair-desktop', (330, 100, 1570, 520), 0.9),
    ('10-skill-pair-pool-collapsed', 'pair-desktop', 'pair-desktop', (330, 480, 1570, 1100), 0.8),
    # targets
    ('11-targets-star-legend', 'targets-desktop', 'targets-desktop', (330, 430, 1570, 560), 1.0),
    # conditions
    ('12-conditions-collapsible-cards', 'conditions-desktop', 'x-collapsed-conditions', (330, 80, 1570, 700), 0.8),
    ('13-conditions-monster-search', 'x-conditions-monster', 'x-conditions-monster', (960, 3150, 1570, 3700), 1.0),
    # talismans
    ('14-talisman-skill-search', 'x-talisman-skill-open', 'x-talisman-skill-open', (330, 1230, 1570, 1926), 0.9),
    # options
    ('15-options-excluded-sets', 'options-desktop', 'options-desktop', (1090, 100, 1580, 1080), 1.0),
    ('16-options-time-limit-label', 'options-desktop', 'options-desktop', (330, 730, 1090, 1000), 1.0),
    ('17-options-phone-time-limit', 'options-phone', 'options-phone', (0, 740, 412, 1000), 1.0),
    # review
    ('18-review-card-headings', 'review-desktop', 'review-desktop', (330, 280, 1570, 960), 0.8),
    # results
    ('19-results-decoration-columns', 'results-desktop', 'results-desktop', (330, 1030, 1570, 1560), 0.9),
    ('20-results-empty-slots', 'results-desktop', 'results-desktop', (330, 2500, 1570, 2950), 0.9),
    # builds
    ('21-builds-scorebar-and-editor', 'builds-desktop', 'builds-desktop', (330, 80, 1570, 1100), 0.8),
    ('22-profile-menu-vs-import-dialog', 'x-profile-menu', 'x-import-dialog', (400, 0, 1200, 620), 1.0),
]
for p in pairs:
    pair(*p)
