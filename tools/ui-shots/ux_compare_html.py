# One self-contained HTML page with the before/after images of ux_compare.py and a line on each:
#   python ux_compare_html.py <compareDir> <out.html>
import base64, html, os, sys

folder, out = sys.argv[1], sys.argv[2]
captions = {
    '01-run-progress': '1, 2 · A run in progress: the stage line ("Weapon start 1 of 3 · armor search 1 · build 2 of 5") and the log in the cards\' damage numbers, no internal warning.',
    '02-results-default-view': '3 · Results open as cards (the choice is remembered); "not proved" builds are marked, with a note for the run.',
    '03-results-table': '3 · The table says what each build is: sets, armor, talisman, weapon, skills. "No rolled skill pair" instead of "- + -".',
    '04-results-cards': '3 · Cards: "up to … with every condition on" only when higher; over-cap skill points flagged.',
    '05-weapon-page': '4 · Weapon page: a "Let the optimizer choose" switch on each part, greyed out while chosen; reinforcement count steppers; the saved weapon\'s type as a chip.',
    '06-weapon-page-bow': '4 · The same for the bow (ammo instead of sharpness), with the Element Infusion line explained.',
    '07-targets-search': '5 · Targets: a level button per level; "speed eat" lists Speed Eating, Quick Sheathe only under "Also mentioned in the description".',
    '08-targets-preset-limit': '5 · A limit that comes with the preset says so ("from the Great Sword preset").',
    '09-attack-tab': '6 · Attack & target moved out of the long Conditions page into its own tab.',
    '10-options-scope': '6 · Options says whose settings these are; the threads hint explains the 4 threads per solve; storage moved away.',
    '11-weapons-page': '6 · "Inventory" is "Weapons", with the browser storage at its end.',
    '12-new-weapon-from-results': '6 · Picking "New weapon" on Results opens the Weapon tab, not an empty Results page.',
    '13-header': '7 · Header: profile menu left of the profile picker, a divider, Save as and Delete in the weapon ⋮ menu, no unexplained ✓.',
    '14-weapon-picker': '7 · The weapon picker lists weapons by type with icons.',
    '15-new-weapon-header': '7 · A new weapon\'s "Save…" works (it asks for a name) instead of being disabled.',
    '16-save-dialog': '7 · The name it suggests says what the weapon is.',
    '17-status-bar': '8 · No warning for a weapon without a rolled pair.',
}
parts = ['<!doctype html><meta charset="utf-8"><title>UX review: before and after</title>',
         '<style>body{background:#121419;color:#e8e6e1;font:15px Segoe UI,system-ui,sans-serif;margin:24px}h1{font-size:22px}'
         'figure{margin:0 0 36px}figcaption{margin:0 0 8px;color:#9aa1ad}img{max-width:100%;border:1px solid #2f3542}</style>',
         '<h1>UX review fixes: before and after</h1>',
         '<p>Numbers refer to the findings in notes/ux_review_2026-10-13.md. Before: main at 2d88afc. After: the working copy, 2026-10-14.</p>']
for name in sorted(os.listdir(folder)):
    if not name.endswith('.png'):
        continue
    key = name[:-4]
    data = base64.b64encode(open(os.path.join(folder, name), 'rb').read()).decode()
    parts.append(f'<figure><figcaption>{html.escape(captions.get(key, key))}</figcaption><img alt="{html.escape(key)}" src="data:image/png;base64,{data}"></figure>')
open(out, 'w', encoding='utf-8').write('\n'.join(parts))
print('wrote', out, os.path.getsize(out) // 1024, 'KB')
