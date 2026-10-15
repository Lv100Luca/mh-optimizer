// Shared render helpers of the weapon wiki mockups: icons from the website project, the app's badges, the sharpness bar,
// material chips, and the lineage walk over GS (data.js).
const ICONS = '../../src/MHWildsOptimizer.Web/client/public/icons/';
const KINDS = [
  ['great-sword', 'Great Sword'], ['sword-shield', 'Sword & Shield'], ['dual-blades', 'Dual Blades'], ['long-sword', 'Long Sword'],
  ['hammer', 'Hammer'], ['hunting-horn', 'Hunting Horn'], ['lance', 'Lance'], ['gunlance', 'Gunlance'], ['switch-axe', 'Switch Axe'],
  ['charge-blade', 'Charge Blade'], ['insect-glaive', 'Insect Glaive'], ['bow', 'Bow'], ['heavy-bowgun', 'Heavy Bowgun'], ['light-bowgun', 'Light Bowgun'],
];
const SHARP = { red: '#d9372b', orange: '#e8862b', yellow: '#e8d02b', green: '#5fbf4a', blue: '#4f8be6', white: '#f0f0f0', purple: '#b05cf0' };
const MAT_COLOR = {
  white: '#e8e6e1', gray: '#9aa1ad', yellow: '#f2d44f', sky: '#8fe3f5', blue: '#4fa3e6', red: '#e05a4f', pink: '#f39ac4', green: '#5fbf7a',
  ivory: '#f3ecd2', lemon: '#f6f08a', purple: '#b07be8', brown: '#b5804f', 'dark-purple': '#6d3fa0', rose: '#e07aa0', 'blue-purple': '#7f7ff0',
  vermilion: '#ff6a3d', orange: '#ff9b45', ultramarine: '#3f5bd6',
};
const BY = Object.fromEntries(GS.map((w) => [w.name, w]));
const fmt = (n) => n.toLocaleString('en-US');
const weaponIcon = (kind = 'great-sword') => `${ICONS}weapons/${kind}-r8.png`;
const rarity = (r) => `<span class="rarity r${r}" title="rarity ${r}">R${r}</span>`;
const slots = (s) => s.length === 0 ? '<span class="muted tiny">no slots</span>'
  : `<span class="slots">${s.map((l) => `<span class="slot" title="weapon slot level ${l}"><img class="slot-kind" src="${ICONS}ui/slot-weapon.png" alt="">${l}</span>`).join('')}</span>`;
const elementText = (w, withIcon = true) => {
  if (w.element) return `<span class="ele ${w.element}">${withIcon ? `<img src="${ICONS}elements/${w.element}.png" alt="">` : ''}${w.elementDisplay} ${cap(w.element)}</span>`;
  if (w.status) return `<span class="ele ${w.status}">${w.statusDisplay} ${cap(w.status === 'blastblight' ? 'blast' : w.status)}</span>`;
  return '<span class="muted">none</span>';
};
const cap = (s) => s.charAt(0).toUpperCase() + s.slice(1);
// the gauge, with a hatched overlay of what Handicraft 5 would add (handicraft breakpoints are the levels that extend the top colour)
function sharpBar(bar, cls = '', handicraft = null) {
  if (!bar) return '<span class="muted">-</span>';
  const order = ['red', 'orange', 'yellow', 'green', 'blue', 'white', 'purple'];
  const extra = handicraft && handicraft.length ? handicraft.reduce((a, b) => a + b, 0) * 10 : 0;
  const total = Math.max(1, order.reduce((a, c) => a + bar[c], 0) + extra);
  const segs = order.filter((c) => bar[c] > 0).map((c) => `<span class="seg" style="width:${(100 * bar[c] / total).toFixed(2)}%;background:${SHARP[c]}" title="${c} ${bar[c]}"></span>`).join('');
  const top = order.filter((c) => bar[c] > 0).at(-1);
  const hc = extra ? `<span class="seg hc" style="width:${(100 * extra / total).toFixed(2)}%;background:${SHARP[top]}" title="Handicraft 5: +${extra} ${top}"></span>` : '';
  return `<span class="sharpbar-wrap"><span class="sharpbar ${cls}">${segs}${hc}</span></span>`;
}
const topSharp = (bar) => bar ? ['purple', 'white', 'blue', 'green', 'yellow', 'orange', 'red'].find((c) => bar[c] > 0) : null;
const skillChips = (sk, tiny = false) => sk.length === 0 ? '<span class="muted small">no skills</span>'
  : sk.map(([n, l]) => `<span class="chip skill ${tiny ? 'tiny' : ''}"><img src="${ICONS}skills/${skillIcon(n)}.png" alt="">${n} <b>${l}</b></span>`).join(' ');
const skillIcon = (n) => ({ 'Critical Boost': 'affinity', 'Critical Eye': 'affinity', 'Attack Boost': 'attack', 'Focus': 'offense', 'Critical Element': 'element',
  'Speed Sharpening': 'handicraft', 'Handicraft': 'handicraft', 'Razor Sharp': 'handicraft', 'Protective Polish': 'handicraft', 'Darkside': 'offense', 'Shattering Reflection': 'offense',
  'Guard': 'defense', 'Guard Up': 'defense', 'Offensive Guard': 'offense', 'Fire Attack': 'element', 'Water Attack': 'element', 'Thunder Attack': 'element', 'Ice Attack': 'element', 'Dragon Attack': 'element',
  'Paralysis Attack': 'element', 'Poison Attack': 'element', 'Sleep Attack': 'element', 'Blast Attack': 'element', 'Power Prolonger': 'offense', 'Slugger': 'offense', 'Stamina Thief': 'offense' })[n] ?? 'offense';
// no part icons in the site yet: a coloured dot with the kind's initial, to be replaced by fetched icons (tools/fetch_icons.py)
const matIcon = ([name, , kind, color]) => `<span class="mat-icon" style="background:${MAT_COLOR[color] ?? '#999'}" title="${kind ?? '?'} icon, ${color}">${(kind ?? '?').slice(0, 2).toUpperCase()}</span>`;
const matRow = (m) => `<div class="mat">${matIcon(m)}<span><span>${m[0]}</span> <span class="muted tiny">${rarity(m[4])}</span></span><span class="qty">x${m[1]}</span><span class="val" title="sell value each">${fmt(m[5])}z each</span></div>`;
const matChip = (m) => `<span class="chip tiny" title="${m[0]}: sells for ${fmt(m[5])}z">${matIcon(m).replace('mat-icon', 'mat-icon tiny').replace('width', 'w')}${m[0]} <b>x${m[1]}</b></span>`;
// the forge path from a weapon you can craft directly down to this one (first entry is craftable)
function pathTo(name) {
  const path = [];
  for (let w = BY[name]; w; w = w.prev ? BY[w.prev] : null) path.unshift(w);
  return path;
}
const stepCost = (w) => w.craftable && !w.prev ? w.craftZenny : w.upgradeZenny;
const stepMats = (w) => w.craftable && !w.prev ? w.craftMats : w.upgradeMats;
function pathCost(name) {
  const p = pathTo(name);
  const zenny = p.reduce((a, w) => a + stepCost(w), 0);
  const mats = new Map();
  for (const w of p) for (const m of stepMats(w)) mats.set(m[0], (mats.get(m[0]) ?? 0) + m[1]);
  return { steps: p, zenny, mats: [...mats.entries()] };
}
const treeLabel = (t) => t.replace(/ Tree$/, '');
const finalForms = () => GS.filter((w) => w.next.length === 0 && w.rarity === 8);
const $ = (s, el = document) => el.querySelector(s);
const $$ = (s, el = document) => [...el.querySelectorAll(s)];

// the forge grid (lanes x tiers, lines along "previous") into host; onSelect(name|null) after every click. Used by the
// modal mockup (4-); 3-tech-tree.html keeps its own copy with the tooltip.
function renderTree(host, { compact = false, selected = null, onSelect = () => {} } = {}) {
  const cols = Math.max(...GS.map((w) => w.col)) + 1, rows = Math.max(...GS.map((w) => w.row)) + 1;
  const cw = compact ? 46 : 168, rh = compact ? 30 : 58, lab = compact ? 110 : 150;
  const colLabel = (c) => { const m = {}; GS.filter((w) => w.col === c).forEach((w) => m[w.rarity] = (m[w.rarity] ?? 0) + 1); const e = Object.entries(m).sort((a, b) => b[1] - a[1])[0]; return e ? 'R' + e[0] : ''; };
  const laneName = (r) => { const w = GS.filter((x) => x.row === r).sort((a, b) => a.col - b.col)[0]; return w ? treeLabel(w.tree) : ''; };
  const nodes = GS.map((w) => `<a class="tnode ${w.craftable && !w.prev ? 'craft' : ''}" data-name="${w.name}" style="--x:${w.col};--y:${w.row}" title="${compact ? w.name : ''}">
    <img src="${weaponIcon()}" alt=""><span class="t"><b>${w.name}</b><small>${w.display}${w.element ? ' · ' + w.elementDisplay + ' ' + cap(w.element) : w.status ? ' · ' + w.statusDisplay + ' ' + cap(w.status) : ''}</small></span>${rarity(w.rarity)}</a>`).join('');
  const paths = GS.filter((w) => w.prev).map((w) => {
    const p = BY[w.prev];
    const x1 = p.col * cw + cw - 7, y1 = p.row * rh + rh / 2, x2 = w.col * cw + 7, y2 = w.row * rh + rh / 2, xm = x1 + (x2 - x1) / 2;
    return `<path data-to="${w.name}" d="M${x1},${y1} H${xm} V${y2} H${x2}"></path>`;
  }).join('');
  host.innerHTML = `<div class="tree" style="--cols:${cols};--cw:${cw}px;--rh:${rh}px;--lab:${lab}px">
    <div class="tree-cols"><div>Tree</div>${Array.from({ length: cols }, (_, c) => `<div>${compact ? colLabel(c) : 'Tier ' + (c + 1) + ' · ' + colLabel(c)}</div>`).join('')}</div>
    <div class="tree-body" style="height:${rows * rh}px;width:${lab + cols * cw}px">
      <div class="tree-lanes">${Array.from({ length: rows }, (_, r) => `<div title="${laneName(r)}"><b>${laneName(r)}</b></div>`).join('')}</div>
      <svg class="tree-svg" width="${cols * cw}" height="${rows * rh}">${paths}</svg>${nodes}
    </div></div>`;
  let current = null;
  const select = (name, scroll = true) => {
    current = name;
    const lit = new Set(name ? pathTo(name).map((w) => w.name) : []);
    $$('.tnode', host).forEach((n) => { n.classList.toggle('lit', lit.has(n.dataset.name)); n.classList.toggle('sel', n.dataset.name === name); n.classList.toggle('dim', !!name && !lit.has(n.dataset.name)); });
    $$('path', host).forEach((p) => { p.classList.toggle('lit', lit.has(p.dataset.to) && p.dataset.to !== pathTo(name)[0]?.name); p.classList.toggle('dim', !!name && !lit.has(p.dataset.to)); });
    const sel = $('.tnode.sel', host);
    if (sel && scroll) { host.scrollLeft = Math.max(0, sel.offsetLeft - host.clientWidth / 2); host.scrollTop = Math.max(0, sel.offsetTop - host.clientHeight / 2); }
    onSelect(name);
  };
  $$('.tnode', host).forEach((n) => n.onclick = (e) => { e.preventDefault(); select(n.dataset.name, false); });
  if (selected) select(selected);
  return { select, get selected() { return current; } };
}
