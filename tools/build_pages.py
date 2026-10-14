"""Writes the public wiki as static HTML pages, the sitemap and the 404 page, from data/wiki.json (tools/build_wiki.py).

The app itself is Blazor WebAssembly: a search engine that does not run it sees only index.html's start screen. These pages
are what gets indexed: one per skill, per armor family (low and high rank together), per item category, for the most
searched items, and one for meals, each with its own title, description and canonical URL.

    python tools/build_pages.py [wwwroot]

The default output is src/MHWildsOptimizer.Browser/wwwroot, where the build runs this (the BuildWikiPages target):
wiki/ (rewritten whole), sitemap.xml and 404.html, all git-ignored. URLs end in a slash (/wiki/skills/agitator/ is
wiki/skills/agitator/index.html), which Caddy's file_server and any static server serve as they are. Uses only the
standard library.
"""
import html
import json
import os
import re
import shutil
import sys
import unicodedata
from collections import defaultdict

SITE = 'https://wildsbuilds.com'
NAME = 'Wilds Builds'
GAME = 'Monster Hunter Wilds'

# The sections listed in sitemap.xml. Armor is left out at first so Google spends its attention on the skill pages; the
# pages exist and are linked either way, the sitemap only says which ones to look at first.
SITEMAP_SECTIONS = {'home', 'wiki', 'skills', 'items', 'meals'}

# Items that people search for by name get a page of their own; the rest are shown in full on their category's page.
OWN_PAGE_ITEMS = [
    'Demondrug', 'Mega Demondrug', 'Might Seed', 'Might Pill', 'Demon Powder', 'Powercharm',
    'Armorskin', 'Mega Armorskin', 'Adamant Seed', 'Adamant Pill', 'Hardshell Powder', 'Armorcharm',
    'Mega Potion', 'Max Potion', 'Ancient Potion', 'Lifepowder', 'Dust of Life', 'Dash Juice',
]

KIND_LABEL = {'armor': 'Armor skill', 'weapon': 'Weapon skill', 'set': 'Set bonus', 'group': 'Group skill'}
KIND_PLURAL = {'armor': 'Armor skills', 'set': 'Set bonuses', 'group': 'Group skills', 'weapon': 'Weapon skills'}
KIND_ABOUT = {
    'armor': 'Skills on armor pieces, decorations and charms.',
    'set': 'Bonuses for wearing 2 or 4 pieces of one armor set.',
    'group': 'Bonuses for wearing 3 pieces that share a group skill.',
    'weapon': 'Skills on weapons and weapon decorations.',
}
PIECES = ['head', 'chest', 'arms', 'waist', 'legs']
PIECE_LABEL = {'head': 'Head', 'chest': 'Chest', 'arms': 'Arms', 'waist': 'Waist', 'legs': 'Legs'}
ELEMENTS = ['fire', 'water', 'thunder', 'ice', 'dragon']
WEAPON_LABEL = {
    'great-sword': 'Great Sword', 'long-sword': 'Long Sword', 'sword-shield': 'Sword and Shield', 'dual-blades': 'Dual Blades',
    'hammer': 'Hammer', 'hunting-horn': 'Hunting Horn', 'lance': 'Lance', 'gunlance': 'Gunlance', 'switch-axe': 'Switch Axe',
    'charge-blade': 'Charge Blade', 'insect-glaive': 'Insect Glaive', 'light-bowgun': 'Light Bowgun',
    'heavy-bowgun': 'Heavy Bowgun', 'bow': 'Bow',
}
# wilds.mhdb.io decoration icon colours -> the wiki icon file names (as GameIcons.DecoColor in the app)
DECO_COLOR = {
    'purple': 'purple', 'white': 'white', 'emerald': 'emerald', 'sky': 'light-blue', 'pink': 'pink', 'yellow': 'yellow',
    'blue': 'blue', 'gray': 'gray', 'red': 'red', 'ivory': 'tan', 'brown': 'brown', 'lemon': 'lemon', 'moss-green': 'moss',
    'rose': 'rose', 'green': 'green', 'ultramarine': 'dark-blue', 'vermilion': 'vermilion', 'dark-purple': 'dark-purple',
    'sage-green': 'light-green',
}
CONFIDENCE = {
    'high': 'Game data and an independent test or the game\'s own text agree.',
    'medium': 'One good source, or game data whose meaning is inferred.',
    'low': 'A single secondary source or an unverified reading.',
}


def esc(text):
    return html.escape(str(text), quote=True)


def slug(name):
    for greek, latin in (('α', ' alpha'), ('β', ' beta'), ('γ', ' gamma')):
        name = name.replace(greek, latin)
    text = unicodedata.normalize('NFKD', name).encode('ascii', 'ignore').decode()
    text = text.replace('&', ' and ').replace("'", '').replace('.', '').lower()
    return re.sub(r'[^a-z0-9]+', '-', text).strip('-')


def family_of(set_name):
    """'Rey Dau γ' -> 'Rey Dau': the low rank set and the high rank α / β / γ sets share one page."""
    return re.sub(r'\s*[αβγ]$', '', set_name)


def cut(text, limit=200):
    """A meta description: whole words, at most about `limit` characters."""
    text = re.sub(r'\s+', ' ', text).strip()
    if len(text) <= limit:
        return text
    return text[:limit].rsplit(' ', 1)[0].rstrip(',;:') + '…'


def game_version():
    try:
        with open('src/MHWildsOptimizer.Api/Catalog.cs', encoding='utf-8') as f:
            m = re.search(r'"(1\.\d{3})"', f.read())
            return m.group(1) if m else None
    except OSError:
        return None


# ------------------------------------------------------------------------------------------------------------ data

with open('data/wiki.json', encoding='utf-8') as f:
    WIKI = json.load(f)

SKILLS = sorted(WIKI['skills'], key=lambda s: s['name'].lower())
SKILL_BY_NAME = {s['name']: s for s in SKILLS}
SETS = {s['name']: s for s in WIKI['armor_sets']}
FAMILIES = defaultdict(list)
for armor_set in WIKI['armor_sets']:
    FAMILIES[family_of(armor_set['name'])].append(armor_set)
RANK_ORDER = {'': 0, 'α': 1, 'β': 2, 'γ': 3}
for sets in FAMILIES.values():
    sets.sort(key=lambda s: (s['rank'] != 'low', RANK_ORDER.get(s['name'][-1], 0) if s['rank'] == 'high' else 0))
CATEGORIES = WIKI['item_categories']
ITEMS = WIKI['items']
ITEM_BY_NAME = {i['name']: i for i in ITEMS}
OWN_PAGE = set(OWN_PAGE_ITEMS) & set(ITEM_BY_NAME)
VERSION = game_version()

# URL paths, all ending in a slash
def skill_url(name): return f'/wiki/skills/{slug(name)}/'
def family_url(family): return f'/wiki/armor/{slug(family)}/'
def set_url(set_name): return family_url(family_of(set_name)) + '#' + slug(set_name)
def category_url(key): return f'/wiki/items/{key}/'
def item_url(name):
    item = ITEM_BY_NAME[name]
    return f'/wiki/items/{slug(name)}/' if name in OWN_PAGE else category_url(item['category']) + '#' + slug(name)


def skill_link(name, level=None):
    label = esc(name) + (f' <b>{level}</b>' if level is not None else '')
    return f'<a href="{skill_url(name)}">{label}</a>' if name in SKILL_BY_NAME else label


def item_link(name):
    return f'<a href="{item_url(name)}">{esc(name)}</a>' if name in ITEM_BY_NAME else esc(name)


def skill_icon(skill):
    icon = skill.get('icon') or {'set': 'set', 'group': 'group'}.get(skill['kind'], 'utility')
    return f'/icons/skills/{icon}.png'


def armor_icon(piece, rarity):
    return f'/icons/armor/{piece}-r{max(1, min(8, rarity))}.png'


def item_icon(item):
    return f'/icons/items/{item["icon"]}-{item["icon_color"]}.png'


def img(src, size=20, alt=''):
    return f'<img class="icon" src="{src}" width="{size}" height="{size}" alt="{esc(alt)}" loading="lazy">'


def slots_html(slots):
    return ''.join(f'<span class="slot">{n}</span>' for n in slots) if slots else '<span class="muted">—</span>'


def source_line(source, confidence):
    if not source and not confidence:
        return ''
    conf = f'<span class="conf {esc(confidence)}" title="{esc(CONFIDENCE.get(confidence, ""))}">{esc(confidence)} confidence</span> ' if confidence else ''
    return f'<p class="source">{conf}<span class="muted">Source: {esc(source or "")}</span></p>'


def table(columns, rows, cls=''):
    head = ''.join(f'<th>{esc(c)}</th>' for c in columns)
    body = ''.join('<tr>' + ''.join(f'<td>{esc(c)}</td>' for c in row) + '</tr>' for row in rows)
    return f'<div class="scroll"><table class="{cls}"><thead><tr>{head}</tr></thead><tbody>{body}</tbody></table></div>'


# --------------------------------------------------------------------------------------------------------- layout

PAGES = []   # (path, section) for the sitemap


def page(path, section, title, description, crumbs, body, noindex=False):
    """Writes one page. crumbs: [(label, path)] from the wiki down to this page's parent."""
    url = SITE + path
    trail = [('Wilds Builds', '/')] + crumbs
    crumb_html = ' <span>›</span> '.join(f'<a href="{p}">{esc(label)}</a>' for label, p in trail[1:])
    ld = {
        '@context': 'https://schema.org', '@type': 'BreadcrumbList',
        'itemListElement': [{'@type': 'ListItem', 'position': i + 1, 'name': label, 'item': SITE + p}
                            for i, (label, p) in enumerate(trail + [(title.split(' · ')[0], path)])],
    }
    version = f' · data from game version {VERSION}' if VERSION else ''
    doc = f'''<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{esc(title)}</title>
<meta name="description" content="{esc(description)}">
{'<meta name="robots" content="noindex">' if noindex else f'<link rel="canonical" href="{url}">'}
<meta name="theme-color" content="#121419">
<meta property="og:type" content="website">
<meta property="og:site_name" content="{NAME}">
<meta property="og:title" content="{esc(title)}">
<meta property="og:description" content="{esc(description)}">
<meta property="og:url" content="{url}">
<meta property="og:image" content="{SITE}/og.png">
<meta name="twitter:card" content="summary_large_image">
<link rel="icon" type="image/png" href="/icons/weapons/great-sword-r8.png">
<link rel="stylesheet" href="/css/wiki.css">
<script type="application/ld+json">{json.dumps(ld, ensure_ascii=False)}</script>
</head>
<body>
<header class="top">
<a class="brand" href="/"><img src="/icons/weapons/great-sword-r8.png" width="32" height="32" alt="">{NAME}</a>
<nav aria-label="Wiki">
<a href="/wiki/skills/">Skills</a><a href="/wiki/armor/">Armor</a><a href="/wiki/items/">Items</a><a href="/wiki/meals/">Meals</a>
<a class="cta" href="/">Open the optimizer</a>
</nav>
</header>
<main>
{f'<nav class="crumbs" aria-label="Breadcrumb">{crumb_html}</nav>' if crumbs else ''}
{body}
</main>
<footer>
<p><a href="/">{NAME}</a> is a free {GAME} build optimizer and wiki{version}.</p>
<p>A fan project, not affiliated with or endorsed by Capcom. {GAME} and its names and icons are trademarks of Capcom.</p>
</footer>
</body>
</html>
'''
    target = os.path.join(OUT, path.strip('/').replace('/', os.sep), 'index.html') if path.endswith('/') else os.path.join(OUT, path.strip('/'))
    os.makedirs(os.path.dirname(target), exist_ok=True)
    with open(target, 'w', encoding='utf-8', newline='\n') as f:
        f.write(doc)
    if not noindex:
        PAGES.append((path, section))


def optimizer_box(text):
    return f'<aside class="cta-box"><p>{text}</p><a class="button" href="/">Open the optimizer</a></aside>'


# ---------------------------------------------------------------------------------------------------------- skills

def skill_summary(skill):
    """One line for lists and descriptions: the game's description, or the top level's text."""
    if skill.get('description'):
        return skill['description']
    ranks = skill.get('ranks') or []
    return ranks[-1]['description'] if ranks and ranks[-1].get('description') else ''


def skill_meta(skill):
    fx = skill.get('effects') or {}
    levels = fx.get('levels') or {}
    kind = skill['kind']
    head = f'{skill["name"]} ({KIND_LABEL[kind].lower()}) in {GAME}: '
    if kind in ('set', 'group'):
        tiers = [f'{r.get("pieces")} pieces: {r["description"]}' for r in skill['ranks'] if r.get('description')]
        text = head + ' '.join(tiers)
    elif levels:
        first, last = levels.get('1'), levels.get(str(skill['max_level']))
        text = head + (fx.get('trigger', '') + ': ' if fx.get('trigger') else '')
        text += f'Lv 1 {", ".join(first)}' if first else ''
        text += f'; Lv {skill["max_level"]} {", ".join(last)}.' if last and skill['max_level'] > 1 else '.'
    else:
        text = head + skill_summary(skill)
    return cut(text + ' Exact numbers for every level and where to get it.', 300)


def skill_page(skill):
    kind, fx, src = skill['kind'], skill.get('effects') or {}, skill.get('sources') or {}
    bonus = kind in ('set', 'group')
    flags = []
    if not bonus:
        flags.append(f'max level {skill["max_level"]}')
    if skill.get('weapon_only'):
        flags.append('only on weapons')
    if skill.get('gogma_pool'):
        flags.append('Gogma Artian weapon roll')
    out = [f'''<article>
<div class="head">{img(skill_icon(skill), 40)}<div><h1>{esc(skill["name"])}</h1>
<p class="sub">{KIND_LABEL[kind]} · {GAME}{''.join(' · ' + esc(f) for f in flags)}</p></div></div>''']
    if skill.get('description'):
        out.append(f'<p class="lead">{esc(skill["description"])}</p>')

    out.append(f'<h2>{"Tiers" if bonus else "Levels"}</h2>')
    if fx.get('trigger'):
        out.append(f'<p><b>When:</b> {esc(fx["trigger"])}</p>')
    rows = []
    for r in skill['ranks']:
        lines = (fx.get('levels') or {}).get(str(r['level'])) or []
        label = f'<b>{esc(r.get("name") or "Tier " + str(r["level"]))}</b><br><span class="muted">{r["pieces"]} pieces</span>' \
            if bonus and r.get('pieces') else f'<b>{esc(r.get("name") or r["level"])}</b>'
        numbers = '<br>'.join(esc(x) for x in lines) or '<span class="muted">—</span>'
        rows.append(f'<tr><td>{label}</td><td>{esc(r.get("description") or "")}</td><td>{numbers}</td></tr>')
    out.append(f'''<div class="scroll"><table class="levels"><thead><tr><th>{"Tier" if bonus else "Lv"}</th><th>Effect</th>
<th>In numbers</th></tr></thead><tbody>{''.join(rows)}</tbody></table></div>''')
    for t in fx.get('tables') or []:
        out.append(f'<h3>{esc(t["title"])}</h3>' + table(t['columns'], t['rows']))
    if fx.get('notes'):
        out.append('<ul class="notes">' + ''.join(f'<li>{esc(n)}</li>' for n in fx['notes']) + '</ul>')
    out.append(source_line(fx.get('source'), fx.get('confidence')))

    out.append('<h2>Where to get it</h2>')
    if src.get('decorations'):
        chips = ''.join(
            f'<li>{img("/icons/decorations/l%d-%s-%s.png" % (max(1, min(3, d["slot"])), "weapon" if d["kind"] == "weapon" else "armor", DECO_COLOR.get(d.get("icon_color") or "", "white")))}'
            f'{esc(d["name"])} <b>Lv {d["level"]}</b> <span class="muted">rarity {d["rarity"]}</span></li>' for d in src['decorations'])
        out.append(f'<h3>Decorations</h3><ul class="chips">{chips}</ul>')
    if src.get('charms'):
        chips = ''.join(f'<li>{img("/icons/talisman/r%d.png" % max(0, min(8, c["rarity"])))}{esc(c["name"])} <b>Lv {c["level"]}</b></li>'
                        for c in src['charms'])
        out.append(f'<h3>Charms</h3><p class="muted">The highest rank of each craftable charm.</p><ul class="chips">{chips}</ul>')
    if src.get('sets'):
        chips = []
        for set_name in src['sets']:
            s = SETS.get(set_name)
            icon = img(armor_icon(s['pieces'][0]['piece'], s['rarity'])) if s else ''
            chips.append(f'<li>{icon}<a href="{set_url(set_name)}">{esc(set_name)}</a></li>')
        out.append(f'<h3>Armor sets</h3><ul class="chips">{"".join(chips)}</ul>')
        if skill.get('gogma_pool'):
            what = 'set bonus' if kind == 'set' else 'group skill'
            out.append(f'<p class="muted">A Gogma Artian weapon can roll this {what}; the weapon then counts as one piece.</p>')
    if src.get('armor'):
        rows = ''.join(
            f'<tr><td>{img(armor_icon(a["piece"], a["rarity"]), 22, a["piece"])}</td><td>{esc(a["name"])}</td>'
            f'<td><a href="{set_url(a["set"])}">{esc(a["set"])}</a></td><td>{"High" if a["rank"] == "high" else "Low"} rank, rarity {a["rarity"]}</td>'
            f'<td class="num"><b>Lv {a["level"]}</b></td></tr>' for a in src['armor'])
        out.append(f'<h3>Armor pieces <span class="muted">({len(src["armor"])})</span></h3>'
                   f'<div class="scroll"><table class="armor-src"><tbody>{rows}</tbody></table></div>')
    if src.get('weapons'):
        groups = defaultdict(list)
        for w in src['weapons']:
            groups[w['kind']].append(w)
        blocks = ''.join(
            f'<div class="weapon-group"><h4>{img("/icons/weapons/%s-r8.png" % k)}{esc(WEAPON_LABEL.get(k, k))}</h4><p>'
            + ', '.join(esc(w['name']) + (f' (Lv {w["level"]})' if skill['max_level'] > 1 else '') for w in ws) + '</p></div>'
            for k, ws in sorted(groups.items(), key=lambda kv: list(WEAPON_LABEL).index(kv[0]) if kv[0] in WEAPON_LABEL else 99))
        out.append(f'<h3>Weapons with it built in <span class="muted">({len(src["weapons"])})</span></h3>{blocks}')
    if not any(src.get(k) for k in ('decorations', 'charms', 'sets', 'armor', 'weapons')):
        out.append('<p class="muted">Nothing in the game data carries this skill.</p>')

    out.append(optimizer_box(f'Want {esc(skill["name"])} in your build? The optimizer finds the armor, decorations and talismans '
                             'that get you there with the most damage.'))
    out.append('</article>')
    page(skill_url(skill['name']), 'skills', f'{skill["name"]} {KIND_LABEL[kind].lower()} · {GAME} · {NAME}', skill_meta(skill),
         [('Wiki', '/wiki/'), ('Skills', '/wiki/skills/')], '\n'.join(out))


def skills_index():
    out = [f'<h1>{GAME} skills</h1>',
           f'<p class="lead">All {len(SKILLS)} skills in {GAME}: what every level does in exact numbers, and every decoration, '
           'charm, armor piece and weapon that carries them.</p>']
    out.append('<p class="toc">' + ' · '.join(f'<a href="#{k}">{KIND_PLURAL[k]}</a>' for k in KIND_PLURAL) + '</p>')
    for kind in KIND_PLURAL:
        skills = [s for s in SKILLS if s['kind'] == kind]
        items = ''.join(f'<li>{img(skill_icon(s))}<a href="{skill_url(s["name"])}">{esc(s["name"])}</a>'
                        f'<span class="muted">{esc(cut(skill_summary(s), 110))}</span></li>' for s in skills)
        out.append(f'<h2 id="{kind}">{KIND_PLURAL[kind]} <span class="muted">({len(skills)})</span></h2>'
                   f'<p class="muted">{KIND_ABOUT[kind]}</p><ul class="list">{items}</ul>')
    page('/wiki/skills/', 'skills', f'All skills · {GAME} · {NAME}',
         f'Every {GAME} skill with exact numbers per level: {len([s for s in SKILLS if s["kind"] == "armor"])} armor skills, '
         f'set bonuses, group skills and weapon skills, and where to get each one.',
         [('Wiki', '/wiki/')], '\n'.join(out))


# ----------------------------------------------------------------------------------------------------------- armor

def set_label(armor_set):
    return f'{armor_set["name"]} (low rank)' if armor_set['rank'] == 'low' else armor_set['name']


def bonus_html(armor_set):
    rows = []
    bonuses = list(dict.fromkeys(armor_set.get('set_bonus') or []))
    for piece in armor_set['pieces']:
        for b in piece.get('set_bonus') or []:
            if b not in bonuses:
                bonuses.append(b)
    for name in bonuses:
        skill = SKILL_BY_NAME.get(name)
        tiers = '; '.join(f'{r.get("pieces")} pieces: {r.get("name") or ""}' for r in skill['ranks']) if skill else ''
        rows.append(f'<li><span class="tag set">Set bonus</span> {skill_link(name)} <span class="muted">{esc(tiers)}</span></li>')
    if armor_set.get('group_skill'):
        name = armor_set['group_skill']
        skill = SKILL_BY_NAME.get(name)
        tiers = '; '.join(f'{r.get("pieces")} pieces: {r.get("name") or ""}' for r in skill['ranks']) if skill else ''
        rows.append(f'<li><span class="tag group">Group skill</span> {skill_link(name)} <span class="muted">{esc(tiers)}</span></li>')
    return f'<ul class="bonuses">{"".join(rows)}</ul>' if rows else ''


def set_section(armor_set):
    pieces = sorted(armor_set['pieces'], key=lambda p: PIECES.index(p['piece']))
    rows = []
    totals = defaultdict(int)
    for p in pieces:
        for s in p['skills']:
            totals[s['skill']] += s['level']
        skills = ', '.join(skill_link(s['skill'], s['level']) for s in p['skills']) or '<span class="muted">—</span>'
        if p.get('set_bonus'):
            skills += '<br><span class="muted">' + ', '.join(esc(b) for b in p['set_bonus']) + '</span>'
        res = ''.join(f'<td class="num {"neg" if p["resistances"][e] < 0 else "pos" if p["resistances"][e] > 0 else ""}">'
                      f'{p["resistances"][e]:+d}</td>' if p['resistances'][e] else '<td class="num muted">0</td>' for e in ELEMENTS)
        rows.append(f'<tr><td class="piece"><div>{img(armor_icon(p["piece"], p["rarity"]), 24, p["piece"])}<span>{esc(p["name"])}'
                    f'<br><span class="muted">{PIECE_LABEL[p["piece"]]} · rarity {p["rarity"]}</span></span></div></td>'
                    f'<td>{skills}</td><td class="slots">{slots_html(p["slots"])}</td>'
                    f'<td class="num">{p["defense_base"]}<span class="muted"> → {p["defense_max"]}</span></td>{res}</tr>')
    head = ''.join(f'<th class="num" title="{e.title()} resistance">{img("/icons/elements/%s.png" % e, 16, e)}</th>' for e in ELEMENTS)
    total = ', '.join(skill_link(k, v) for k, v in sorted(totals.items(), key=lambda kv: (-kv[1], kv[0])))
    rank = '' if armor_set['rank'] == 'low' else 'high rank · '
    return f'''<section id="{slug(armor_set["name"])}">
<h2>{esc(set_label(armor_set))} <span class="muted">{rank}rarity {armor_set["rarity"]}</span></h2>
{bonus_html(armor_set)}
<div class="scroll"><table class="armor"><thead><tr><th>Piece</th><th>Skills</th><th>Slots</th><th class="num">Defense</th>{head}</tr></thead>
<tbody>{''.join(rows)}</tbody></table></div>
{f'<p class="totals"><b>All {len(pieces)} pieces:</b> {total}</p>' if len(pieces) > 1 and total else ''}
</section>'''


def family_page(family, sets):
    names = [set_label(s) for s in sets]
    ranks = []
    if any(s['rank'] == 'low' for s in sets):
        ranks.append('low rank')
    high = [s['name'][-1] for s in sets if s['rank'] == 'high' and s['name'][-1] in 'αβγ']
    if high:
        ranks.append('high rank ' + ', '.join(high[:-1]) + (' and ' if len(high) > 1 else '') + high[-1])
    bonus_names = list(dict.fromkeys(b for s in sets for b in (s.get('set_bonus') or []) + ([s['group_skill']] if s.get('group_skill') else [])))
    skills = list(dict.fromkeys(sk['skill'] for s in sets for p in s['pieces'] for sk in p['skills']))
    lead = (f'Every piece of the {family} armor in {GAME}, in {" and ".join(ranks)}: skills, decoration slots, defense and '
            f'elemental resistances.')
    out = [f'<article><div class="head">{img(armor_icon(sets[-1]["pieces"][0]["piece"], sets[-1]["rarity"]), 40)}<div>'
           f'<h1>{esc(family)} armor</h1><p class="sub">Armor · {GAME}</p></div></div>', f'<p class="lead">{esc(lead)}</p>']
    if len(sets) > 1:
        out.append('<p class="toc">' + ' · '.join(f'<a href="#{slug(s["name"])}">{esc(set_label(s))}</a>' for s in sets) + '</p>')
    out += [set_section(s) for s in sets]
    out.append(optimizer_box(f'The optimizer weighs every {esc(family)} piece against all other armor for your weapon and the skills you want.'))
    out.append('</article>')
    meta = f'{family} armor in {GAME}: ' + (f'{", ".join(bonus_names)}; ' if bonus_names else '') + \
           f'skills {", ".join(skills[:6])}{" and more" if len(skills) > 6 else ""}. Slots, defense and resistances for {", ".join(names)}.'
    page(family_url(family), 'armor', f'{family} armor · {GAME} · {NAME}', cut(meta, 300),
         [('Wiki', '/wiki/'), ('Armor', '/wiki/armor/')], '\n'.join(out))


def armor_index():
    rows = []
    for family in sorted(FAMILIES, key=str.lower):
        sets = FAMILIES[family]
        rar = sorted({s['rarity'] for s in sets})
        rarity = f'{rar[0]}–{rar[-1]}' if len(rar) > 1 else str(rar[0])
        bonuses = list(dict.fromkeys(b for s in sets for b in (s.get('set_bonus') or []) + ([s['group_skill']] if s.get('group_skill') else [])))
        rows.append(f'<tr><td>{img(armor_icon(sets[-1]["pieces"][0]["piece"], sets[-1]["rarity"]), 22)}</td>'
                    f'<td><a href="{family_url(family)}">{esc(family)}</a></td>'
                    f'<td class="muted">{esc(", ".join(set_label(s).replace(family, "").strip() or "—" for s in sets))}</td>'
                    f'<td class="num">{rarity}</td><td>{", ".join(skill_link(b) for b in bonuses) or "<span class=muted>—</span>"}</td></tr>')
    out = [f'<h1>{GAME} armor</h1>',
           f'<p class="lead">All {len(FAMILIES)} armor families in {GAME}, low and high rank on one page each: skills, decoration slots, '
           'defense, resistances and set bonuses of every piece.</p>',
           '<div class="scroll"><table class="armor-index"><thead><tr><th></th><th>Armor</th><th>Versions</th><th class="num">Rarity</th>'
           f'<th>Set bonus / group skill</th></tr></thead><tbody>{"".join(rows)}</tbody></table></div>']
    page('/wiki/armor/', 'wiki', f'All armor sets · {GAME} · {NAME}',
         f'Every {GAME} armor set, low and high rank: skills, decoration slots, defense, resistances and set bonuses of each piece.',
         [('Wiki', '/wiki/')], '\n'.join(out))


# ----------------------------------------------------------------------------------------------------------- items

def item_facts(item):
    facts = [('Effect', item.get('boost')), ('Lasts', item.get('duration')), ('Rarity', item.get('rarity')),
             ('Carry limit', item.get('carry_limit'))]
    rows = ''.join(f'<tr><th>{k}</th><td>{esc(v)}</td></tr>' for k, v in facts if v not in (None, ''))
    return f'<table class="facts"><tbody>{rows}</tbody></table>' if rows else ''


def item_details(item, level=2):
    h = f'h{level}'
    out = []
    if item.get('effects'):
        out.append(f'<{h}>What it does</{h}><ul>' + ''.join(f'<li>{esc(e)}</li>' for e in item['effects']) + '</ul>')
    if item.get('stacking'):
        out.append(f'<{h}>Stacking</{h}><p>{esc(item["stacking"])}</p>')
    if item.get('notes'):
        out.append('<ul class="notes">' + ''.join(f'<li>{esc(n)}</li>' for n in item['notes']) + '</ul>')
    if item.get('recipes'):
        out.append(f'<{h}>Recipe{"s" if len(item["recipes"]) > 1 else ""}</{h}><ul>'
                   + ''.join('<li>' + ' + '.join(item_link(x) for x in r) + '</li>' for r in item['recipes']) + '</ul>')
    if item.get('used_in'):
        used = item['used_in'] if isinstance(item['used_in'], list) else [item['used_in']]
        out.append(f'<{h}>Used in</{h}><p>' + ', '.join(item_link(x) if isinstance(x, str) else esc(x) for x in used) + '</p>')
    if item.get('related'):
        out.append(f'<p class="related"><b>Related:</b> ' + ', '.join(item_link(x) for x in item['related']) + '</p>')
    out.append(source_line(item.get('source'), item.get('confidence')))
    return '\n'.join(out)


def item_meta(item):
    text = f'{item["name"]} in {GAME}: '
    text += (item.get('boost') or '') + (f' for {item["duration"]}' if item.get('duration') and item['duration'] not in ('—', '') else '')
    text += '. ' + (item.get('stacking') or '')
    if item.get('recipes'):
        text += ' Recipe: ' + ' + '.join(item['recipes'][0]) + '.'
    return cut(text, 300)


def item_page(item):
    category = next(c for c in CATEGORIES if c['key'] == item['category'])
    out = [f'<article><div class="head">{img(item_icon(item), 40)}<div><h1>{esc(item["name"])}</h1>'
           f'<p class="sub">Item · <a href="{category_url(category["key"])}">{esc(category["title"])} items</a> · {GAME}</p></div></div>']
    if item.get('description'):
        out.append(f'<blockquote class="game-text">{esc(item["description"])}<cite>In-game description</cite></blockquote>')
    out.append(item_facts(item))
    out.append(item_details(item))
    out.append(optimizer_box('Item boosts count in the optimizer: switch them on as conditions and it scores builds with them.'))
    out.append('</article>')
    page(item_url(item['name']), 'items', f'{item["name"]} · {GAME} · {NAME}', item_meta(item),
         [('Wiki', '/wiki/'), ('Items', '/wiki/items/'), (f'{category["title"]} items', category_url(category['key']))], '\n'.join(out))


def category_page(category):
    items = [i for i in ITEMS if i['category'] == category['key']]
    out = [f'<article><h1>{esc(category["title"])} items</h1><p class="lead">{esc(category["about"])}</p>']
    summary = ''.join(f'<tr><td>{img(item_icon(i), 24)}</td><td>{item_link(i["name"])}</td><td>{esc(i.get("boost") or "")}</td>'
                      f'<td>{esc(i.get("duration") or "")}</td></tr>' for i in items)
    out.append(f'<div class="scroll"><table class="items"><thead><tr><th></th><th>Item</th><th>Effect</th><th>Lasts</th></tr></thead>'
               f'<tbody>{summary}</tbody></table></div>')
    for i in items:
        if i['name'] in OWN_PAGE:
            continue
        out.append(f'<section id="{slug(i["name"])}"><h2>{img(item_icon(i), 28)} {esc(i["name"])}</h2>')
        if i.get('description'):
            out.append(f'<blockquote class="game-text">{esc(i["description"])}<cite>In-game description</cite></blockquote>')
        out.append(item_facts(i) + item_details(i, 3) + '</section>')
    out.append('</article>')
    names = ', '.join(i['name'] for i in items)
    page(category_url(category['key']), 'items', f'{category["title"]} items · {GAME} · {NAME}',
         cut(f'{category["title"]} items in {GAME}: {names}. {category["about"]}', 300),
         [('Wiki', '/wiki/'), ('Items', '/wiki/items/')], '\n'.join(out))


def items_index():
    out = [f'<h1>{GAME} items</h1>',
           f'<p class="lead">What the common consumables in {GAME} do in exact numbers, how long they last and what stacks with what.</p>']
    for c in CATEGORIES:
        items = [i for i in ITEMS if i['category'] == c['key']]
        lis = ''.join(f'<li>{img(item_icon(i))}{item_link(i["name"])}<span class="muted">{esc(i.get("boost") or "")}</span></li>' for i in items)
        out.append(f'<h2><a href="{category_url(c["key"])}">{esc(c["title"])} items</a></h2><p class="muted">{esc(c["about"])}</p>'
                   f'<ul class="list">{lis}</ul>')
    page('/wiki/items/', 'items', f'Items · {GAME} · {NAME}',
         f'{GAME} consumables in exact numbers: Demondrug, Might Seed, Demon Powder, potions, food and cures, and how they stack.',
         [('Wiki', '/wiki/')], '\n'.join(out))


# ----------------------------------------------------------------------------------------------------------- meals

def meals_page():
    m = WIKI['meals']
    out = [f'<article><h1>Meals in {GAME}</h1>']
    out += [f'<p class="lead">{esc(p)}</p>' if i == 0 else f'<p>{esc(p)}</p>' for i, p in enumerate(m['about'])]
    for t in m.get('tables') or []:
        out.append(f'<h2>{esc(t["title"])}</h2>' + table(t['columns'], t['rows']))
    rows = ''.join(f'<tr id="{slug(s["name"])}"><td>{img("/icons/skills/%s.png" % (s.get("icon") or "utility"))} <b>{esc(s["name"])}</b></td>'
                   f'<td>{esc(s.get("description") or "")}</td><td>{"<br>".join(esc(e) for e in s.get("effects") or [])}</td>'
                   f'<td class="muted">{esc(s.get("from") or "")}</td></tr>' for s in sorted(m['skills'], key=lambda s: s['name']))
    out.append(f'<h2>Meal skills <span class="muted">({len(m["skills"])})</span></h2><div class="scroll"><table class="meal-skills">'
               f'<thead><tr><th>Skill</th><th>In game</th><th>In numbers</th><th>From</th></tr></thead><tbody>{rows}</tbody></table></div>')
    if m.get('notes'):
        out.append('<ul class="notes">' + ''.join(f'<li>{esc(n)}</li>' for n in m['notes']) + '</ul>')
    out.append(source_line(m.get('source'), None))
    out.append('</article>')
    page('/wiki/meals/', 'meals', f'Meals and meal skills · {GAME} · {NAME}',
         cut(f'How meals work in {GAME}: what each meal gives (health, stamina, attack, defense, duration) and all '
             f'{len(m["skills"])} meal skills with their effects.', 300),
         [('Wiki', '/wiki/')], '\n'.join(out))


# ------------------------------------------------------------------------------------------------------ hub, 404

def wiki_index():
    cards = [
        ('/wiki/skills/', 'Skills', f'{len(SKILLS)} skills: exact numbers per level, and every decoration, charm and armor piece with them.'),
        ('/wiki/armor/', 'Armor', f'{len(FAMILIES)} armor families, low and high rank: skills, slots, defense and set bonuses.'),
        ('/wiki/items/', 'Items', f'{len(ITEMS)} consumables: boosts, durations, recipes and what stacks.'),
        ('/wiki/meals/', 'Meals', f'How meals work and all {len(WIKI["meals"]["skills"])} meal skills.'),
    ]
    out = [f'<h1>{GAME} wiki</h1>',
           f'<p class="lead">The game reference behind the {NAME} optimizer: skills, armor, items and meals in {GAME}, with the exact '
           'numbers from the game data.</p>',
           '<div class="cards">' + ''.join(f'<a class="card" href="{u}"><h2>{t}</h2><p>{esc(d)}</p></a>' for u, t, d in cards) + '</div>',
           optimizer_box(f'{NAME} also finds the best build for your weapon: armor, decorations and talismans, scored by damage per attack.')]
    page('/wiki/', 'wiki', f'{GAME} wiki: skills, armor, items · {NAME}',
         f'{GAME} wiki with exact numbers: every skill and level, every armor piece in low and high rank, items and meal skills.',
         [], '\n'.join(out))


def not_found():
    body = ('<h1>Page not found</h1><p class="lead">There is no page at this address.</p>'
            '<p><a href="/">Open the optimizer</a> or browse the <a href="/wiki/">wiki</a>: '
            '<a href="/wiki/skills/">skills</a>, <a href="/wiki/armor/">armor</a>, <a href="/wiki/items/">items</a>, '
            '<a href="/wiki/meals/">meals</a>.</p>')
    page('/404.html', '404', f'Page not found · {NAME}', 'This page does not exist.', [], body, noindex=True)


def sitemap():
    urls = [f'{SITE}/'] + [SITE + p for p, section in PAGES if section in SITEMAP_SECTIONS]
    with open(os.path.join(OUT, 'sitemap.xml'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n')
        f.writelines(f'  <url><loc>{esc(u)}</loc></url>\n' for u in urls)
        f.write('</urlset>\n')
    return len(urls)


# ------------------------------------------------------------------------------------------------------------ main

OUT = sys.argv[1] if len(sys.argv) > 1 else 'src/MHWildsOptimizer.Browser/wwwroot'

if __name__ == '__main__':
    wiki_dir = os.path.join(OUT, 'wiki')
    if os.path.isdir(wiki_dir):
        shutil.rmtree(wiki_dir)

    urls = defaultdict(list)
    for s in SKILLS:
        urls[skill_url(s['name'])].append(s['name'])
    for f in FAMILIES:
        urls[family_url(f)].append(f)
    clashes = {u: n for u, n in urls.items() if len(n) > 1}
    if clashes:
        sys.exit(f'Two pages would share a URL: {clashes}')

    wiki_index()
    skills_index()
    for s in SKILLS:
        skill_page(s)
    armor_index()
    for family, sets in FAMILIES.items():
        family_page(family, sets)
    items_index()
    for c in CATEGORIES:
        category_page(c)
    for name in OWN_PAGE_ITEMS:
        if name in ITEM_BY_NAME:
            item_page(ITEM_BY_NAME[name])
    meals_page()
    not_found()
    listed = sitemap()
    print(f'{len(PAGES)} pages in {OUT}, {listed} in sitemap.xml')
