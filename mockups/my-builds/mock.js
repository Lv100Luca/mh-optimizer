// Shared shell of the mockups: the mockup bar, the app's top bar and side nav around each page's <main>.
// <body data-active="builds" data-weapon="Current"> picks the nav item and the weapon shown. Icons come straight from the
// app's wwwroot (ICO in data.js), so nothing is copied.
(function () {
  const body = document.body;
  const active = body.dataset.active ?? 'builds';
  const weapon = body.dataset.weapon ?? 'Current';
  const pages = [
    ['index.html', 'Overview'],
    ['1-sheet.html', '1 · Skill sheet'],
    ['2-fill.html', '2 · Fill the rest'],
    ['3-wishlist.html', '3 · Skills first'],
    ['4-board.html', '4 · Loadout board'],
    ['5-sets.html', '5 · Sets first'],
    ['6-table.html', '6 · Skill table'],
    ['7-combined.html', '7 · Round 2: board + sheet + fill'],
    ['8-drawer-targets.html', '8 · Round 3: drawer + targets'],
  ];
  const here = location.pathname.split('/').pop() || 'index.html';
  // the app's nav today (Workspace.razor)
  const nav = [
    ['Profile', 'inventory', 'Weapons', 'weapons/great-sword-base.webp', '4'],
    ['Profile', 'talismans', 'Talismans', 'talisman/r7.png', '9'],
    ['Profile', 'compare', 'Compare', 'skills/handicraft.png', '2', 'acc'],
    ['Weapon', 'weapon', 'Weapon', 'weapons/great-sword-r8.png'],
    ['Weapon', 'attack', 'Attack & target', 'ui/attack-up.png'],
    ['Weapon', 'targets', 'Targets & limits', 'skills/offense.png', '2'],
    ['Weapon', 'conditions', 'Conditions', 'skills/attack.png', '8 on'],
    ['Weapon', 'buffs', 'Buffs', 'items/drug-red.png', '1'],
    ['Weapon', 'options', 'Options', 'ui/defense-up.png'],
    ['Builds', 'review', 'Review', 'skills/item.png', 'ok', 'ok'],
    ['Builds', 'results', 'Results', 'ui/affinity-up.png', '5'],
    ['Builds', 'builds', 'My builds', 'armor/chest-r8.png', '3'],
  ];
  const main = document.querySelector('main');
  const app = document.createElement('div');
  app.className = 'app';
  let navHtml = '';
  let group = null;
  for (const [g, key, label, icon, badge, tone] of nav) {
    if (g !== group) { navHtml += `<div class="navgroup">${g}</div>`; group = g; }
    navHtml += `<a class="navitem ${key === active ? 'active' : ''}" href="#">
      <img src="${ICO}${icon}" alt=""><span class="navlabel">${label}</span>${badge ? `<span class="navbadge ${tone ?? ''}">${badge}</span>` : ''}</a>`;
  }
  app.innerHTML = `
    <header class="topbar">
      <div class="brand"><img src="${ICO}weapons/great-sword-r8.png" alt=""><div><div class="brand-title">Wilds Builds</div><div class="brand-sub">MH Wilds build optimizer · Ver 1.041</div></div></div>
      <div class="configbar">
        <span class="fakeselect"><span class="lbl">Profile</span>Luca</span>
        <span class="fakeselect"><span class="lbl">Weapon</span>${weapon}</span>
        <button class="btn sm" disabled style="opacity:.5">Save</button>
        <button class="btn sm">Save as…</button>
        <button class="btn primary">▶ Run optimizer</button>
      </div>
    </header>
    <nav class="sidenav">${navHtml}</nav>`;
  main.classList.add('content');
  app.appendChild(main);
  const bar = document.createElement('div');
  bar.className = 'mockbar';
  bar.innerHTML = `<span class="title">Mockup</span>
    ${pages.map(([href, label]) => href === here ? `<b>${label}</b>` : `<a href="${href}">${label}</a>`).join('<span class="muted">·</span>')}
    <label class="right"><input type="checkbox" id="notes-toggle" checked> design notes</label>`;
  body.prepend(app);
  body.prepend(bar);
  body.classList.add('has-mockbar');
  document.getElementById('notes-toggle').addEventListener('change', (e) => body.classList.toggle('hide-notes', !e.target.checked));

  // segmented controls switch their active button (and pages listen for "seg" events where it changes content)
  document.addEventListener('click', (e) => {
    const b = e.target.closest('.segmented button');
    if (!b) return;
    b.parentElement.querySelectorAll('button').forEach((x) => x.classList.toggle('active', x === b));
    b.parentElement.dispatchEvent(new CustomEvent('seg', { detail: b.dataset.v ?? b.textContent.trim(), bubbles: true }));
  });
  // filter pills toggle
  document.addEventListener('click', (e) => {
    const f = e.target.closest('.filter');
    if (f) f.classList.toggle('on');
  });
})();
