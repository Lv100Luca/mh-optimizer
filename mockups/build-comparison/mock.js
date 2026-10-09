// Shared shell of the mockups: the mockup bar, the app's top bar and side nav around each page's <main>.
// <body data-active="compare" data-weapon="Current" data-title="..."> picks the nav item and the weapon shown.
(function () {
  const body = document.body;
  const active = body.dataset.active;
  const weapon = body.dataset.weapon ?? 'Current';
  const compareCount = body.dataset.compare ?? '3';
  const pages = [
    ['index.html', 'Overview'],
    ['1-compare-tab.html', '1 · Compare tab'],
    ['2-results-vs-baseline.html', '2 · Results vs your build'],
    ['3-weapon-vs-weapon.html', '3 · Weapon vs weapon'],
    ['4-theorycraft.html', '4 · Try a change'],
  ];
  const here = location.pathname.split('/').pop() || 'index.html';
  const nav = [
    ['Profile', 'inventory', 'Inventory', 'icons/weapons/great-sword-base.webp', '4'],
    ['Profile', 'talismans', 'Talismans', 'icons/talisman/r7.png', '9'],
    ['Profile', 'compare', 'Compare', 'icons/skills/item.png', compareCount, 'acc', true],
    ['Weapon', 'weapon', 'Weapon', 'icons/weapons/great-sword-r8.png'],
    ['Weapon', 'pair', 'Skill pair', 'icons/skills/set.png', 'fixed'],
    ['Weapon', 'targets', 'Target skills', 'icons/skills/offense.png', '2'],
    ['Weapon', 'limits', 'Skill limits', 'icons/skills/utility.png', '1'],
    ['Weapon', 'conditions', 'Conditions', 'icons/skills/attack.png', '11 on'],
    ['Weapon', 'options', 'Options', 'icons/ui/defense-up.png'],
    ['Builds', 'review', 'Review', 'icons/skills/item.png', 'ok', 'ok'],
    ['Builds', 'results', 'Results', 'icons/ui/affinity-up.png', '5'],
    ['Builds', 'builds', 'My builds', 'icons/armor/chest-r8.png', '2'],
  ];
  const main = document.querySelector('main');
  const app = document.createElement('div');
  app.className = 'app';
  let navHtml = '';
  let group = null;
  for (const [g, key, label, icon, badge, tone, isNew] of nav) {
    if (isNew && body.dataset.nocompare !== undefined) continue;
    if (g !== group) { navHtml += `<div class="navgroup">${g}</div>`; group = g; }
    navHtml += `<a class="navitem ${key === active ? 'active' : ''} ${isNew ? 'new' : ''}" href="${isNew ? '1-compare-tab.html' : '#'}" title="${isNew ? 'New in the mockups' : ''}">
      <img src="${icon}" alt=""><span class="navlabel">${label}</span>${badge ? `<span class="navbadge ${tone ?? ''}">${badge}</span>` : ''}</a>`;
  }
  app.innerHTML = `
    <header class="topbar">
      <div class="brand"><img src="icons/weapons/great-sword-r8.png" alt=""><div><div class="brand-title">MH Wilds Optimizer</div><div class="brand-sub">Gogma set maker · Ver 1.041</div></div></div>
      <div class="configbar">
        <span class="fakeselect"><span class="lbl">Profile</span>Luca</span>
        <span class="fakeselect"><span class="lbl">Weapon</span>${weapon} ✓</span>
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
})();
