// The scenario every page shows, plus helpers. Pieces, sets, jewels and skills come from gamedata.js (real rows of data/*.json);
// the weapon and the two finished builds are the "Current" great sword of the Luca profile as the build-comparison mockups
// recorded them (2026-10-09). Damage numbers of the draft are illustrative: the mockups cannot score.
const ICO = '../../src/MHWildsOptimizer.Browser/wwwroot/icons/';

const WEAPON = { name: 'Current', type: 'great-sword', attack: 215, affinity: -10, sharpness: 'White', slots: [3, 3, 3], setBonus: 'Soul of the Dark Knight', group: "Lord's Favor" };

const KINDS = ['head', 'chest', 'arms', 'waist', 'legs'];
const KIND_LABEL = { weapon: 'Weapon', head: 'Head', chest: 'Chest', arms: 'Arms', waist: 'Waist', legs: 'Legs', talisman: 'Talisman' };

// the hunter's random talismans (Talismans tab) and the craftable charms the picker lists after them
const TALISMANS = [
  { name: 'Golden Age Charm', rarity: 8, skills: [['Focus', 3], ['Fire Resistance', 2], ['Defense Boost', 2]], slots: [['armor', 1], ['armor', 1], ['armor', 1]], own: true },
  { name: 'Secret Charm', rarity: 8, skills: [['Attack Boost', 3], ['Stun Resistance', 2], ['Survival Expert', 1]], slots: [['armor', 1]], own: true },
  { name: 'Exploiter Charm III', rarity: 8, skills: [['Weakness Exploit', 3]], slots: [] },
  { name: 'Challenger Charm III', rarity: 8, skills: [['Agitator', 3]], slots: [] },
];

// what the hunter is sketching: a crit build around Soul of the Dark Knight, two pieces in so far
const DRAFT = {
  name: 'Crit draft',
  targets: [['Critical Boost', 5], ['Weakness Exploit', 5], ['Agitator', 5], ['Maximum Might', 3], ['Counterstrike', 3]],
  weapon: { decos: ['Critical Jewel III [3]', 'Critical Jewel II [2]', null] },
  armor: {
    head: { name: 'Arkvulcan Helm γ', decos: ['Mighty Jewel [2]', null] },
    chest: { name: 'Udra Miremail γ', decos: ['Challenger Jewel [3]', 'Challenger Jewel [3]'] },
    arms: null,
    waist: null,
    legs: null,
  },
  talisman: null,
  dmg: 1041, atk: 282, aff: 41,
};

// the two builds already on the tab
const OTHERS = [
  { name: 'In game', dmg: 1289 },
  { name: 'Optimizer build 1', dmg: 1353, picked: true },
];

// ---------------------------------------------------------------- lookups
const SKILL = GAME.skills;
const ARMOR = new Map(GAME.armor.map((p) => [p.name, p]));
const DECOS = new Map(GAME.decos.map((d) => [d.name, d]));
const SETS = new Map(GAME.sets.map((s) => [s.set, s]));
const DECO_COLOR = { purple: 'purple', white: 'white', emerald: 'emerald', sky: 'light-blue', pink: 'pink', yellow: 'yellow', blue: 'blue', gray: 'gray', red: 'red', ivory: 'tan', brown: 'brown', lemon: 'lemon', 'moss-green': 'moss', rose: 'rose', green: 'green', ultramarine: 'dark-blue', vermilion: 'vermilion', 'dark-purple': 'dark-purple', 'sage-green': 'light-green' };

const skillInfo = (s) => SKILL[s] ?? { kind: 'armor', max: 7, icon: 'utility' };
const skillIcon = (s) => `${ICO}skills/${skillInfo(s).icon ?? (skillInfo(s).kind === 'set' ? 'set' : skillInfo(s).kind === 'group' ? 'group' : 'utility')}.png`;
const armorIcon = (kind, rarity = 8) => `${ICO}armor/${kind}-r${rarity}.png`;
const armorBase = (kind) => `${ICO}armor/${kind}-base.webp`;
const talismanIcon = (rarity = 8) => `${ICO}talisman/r${rarity}.png`;
const weaponIcon = `${ICO}weapons/${WEAPON.type}-r8.png`;
const decoIcon = (name) => { const d = DECOS.get(name); return d ? `${ICO}decorations/l${d.slot}-${d.kind}-${DECO_COLOR[d.color] ?? 'white'}.png` : `${ICO}ui/decoration.png`; };
const shortDeco = (name) => name.replace(/ \[\d\]$/, '');
const slotMark = (kind) => `${ICO}ui/slot-${kind}.png`;

const fmt = (x, d = 0) => x.toLocaleString('en-US', { minimumFractionDigits: d, maximumFractionDigits: d });
function delta(v, d = 0, suffix = '') {
  const r = Math.round(v * 10 ** d) / 10 ** d;
  if (r === 0) return `<span class="delta same">±0${suffix}</span>`;
  return `<span class="delta ${r > 0 ? 'up' : 'down'}">${r > 0 ? '+' : '−'}${fmt(Math.abs(r), d)}${suffix}</span>`;
}

// ---------------------------------------------------------------- markup bits
const chip = (s, lvl, cls = '') => `<span class="chip skill ${cls}"><img src="${skillIcon(s)}" alt="">${s}${lvl ? ` <b>${lvl}</b>` : ''}</span>`;
const rarityBadge = (r) => `<span class="rarity r${r}">R${r}</span>`;
const slotBox = (level, kind = 'armor') => `<span class="slot ${kind}"><img src="${slotMark(kind)}" alt="">${level}</span>`;
const slotsOf = (levels, kind = 'armor') => `<span class="slots">${levels.map((l) => slotBox(l, kind)).join('')}</span>`;
const skillsText = (skills) => skills.map(([s, l]) => `${s} ${l}`).join(', ');
const decoImg = (name, size = 20) => `<img class="deco" style="width:${size}px;height:${size}px" src="${decoIcon(name)}" title="${name}" alt="">`;

/** One SlotPickers column: a picker per slot, as the app draws them. */
function slotPicks(levels, decos, kind = 'armor') {
  return `<div class="slotpicks">${levels.map((l, i) => {
    const d = decos[i];
    return `<button class="slotpick ${d ? '' : 'vacant'}">${slotBox(l, kind)}${d ? decoImg(d, 22) : `<img class="deco" src="${ICO}ui/decoration.png" alt="" style="opacity:.35">`}<span class="slotpick-name">${d ? shortDeco(d) : 'empty'}</span></button>`;
  }).join('')}</div>`;
}

// ---------------------------------------------------------------- what the draft adds up to
/** Skill levels of a draft, with how much of each comes from jewels; plus set and group piece counts and free slots. */
function totals(draft) {
  const skills = {}, fromDeco = {}, sets = {}, groups = {}, free = [];
  const add = (s, l, deco = false) => { skills[s] = (skills[s] ?? 0) + l; if (deco) fromDeco[s] = (fromDeco[s] ?? 0) + l; };
  const addDecos = (levels, decos, kind) => levels.forEach((l, i) => {
    const d = decos[i] && DECOS.get(decos[i]);
    if (d) d.skills.forEach(([s, lv]) => add(s, lv, true)); else free.push([kind, l]);
  });
  addDecos(WEAPON.slots, draft.weapon.decos, 'weapon');
  if (WEAPON.setBonus) sets[WEAPON.setBonus] = (sets[WEAPON.setBonus] ?? 0) + 1;
  if (WEAPON.group) groups[WEAPON.group] = (groups[WEAPON.group] ?? 0) + 1;
  for (const k of KINDS) {
    const a = draft.armor[k];
    if (!a) continue;
    const p = ARMOR.get(a.name);
    if (!p) continue;
    p.skills.forEach(([s, l]) => add(s, l));
    p.setBonus.forEach((b) => { sets[b] = (sets[b] ?? 0) + 1; });
    if (p.group) groups[p.group] = (groups[p.group] ?? 0) + 1;
    addDecos(p.slots, a.decos ?? [], 'armor');
  }
  if (draft.talisman) {
    const t = TALISMANS.find((x) => x.name === draft.talisman.name);
    if (t) { t.skills.forEach(([s, l]) => add(s, l)); addDecos(t.slots.map(([, l]) => l), draft.talisman.decos ?? [], 'armor'); }
  }
  return { skills, fromDeco, sets, groups, free };
}

/** The skill rows of a sheet: targets first (met or not), then the rest by level. */
function sheetRows(draft) {
  const t = totals(draft);
  const want = Object.fromEntries(draft.targets);
  const names = [...new Set([...draft.targets.map(([s]) => s), ...Object.keys(t.skills).sort((a, b) => t.skills[b] - t.skills[a])])];
  return names.map((s) => {
    const have = t.skills[s] ?? 0, deco = t.fromDeco[s] ?? 0, max = skillInfo(s).max, w = want[s];
    const state = have > max ? 'over' : w ? (have >= w ? 'met' : 'short') : '';
    return { skill: s, have, deco, max, want: w, state };
  });
}

/** Pieces of one kind (or all) that grant a skill, best level first; jewels that grant it. */
const piecesWith = (skill, kind = null) => GAME.armor
  .filter((p) => (!kind || p.piece === kind) && p.skills.some(([s]) => s === skill))
  .map((p) => ({ ...p, level: p.skills.find(([s]) => s === skill)[1] }))
  .sort((a, b) => b.level - a.level || b.rarity - a.rarity || b.slots.length - a.slots.length);
const decosWith = (skill) => GAME.decos.filter((d) => d.skills.some(([s]) => s === skill)).sort((a, b) => b.slot - a.slot);

/** Pips of one skill: level squares, jewel levels in blue, the wanted level dashed, anything over the max red. */
function pips(have, deco, max, want) {
  const n = Math.max(max, have);
  return `<span class="pips">${Array.from({ length: n }, (_, i) => {
    const on = i < have, isDeco = on && i >= have - deco, isWant = want && i < want && !on, over = i >= max;
    return `<span class="${on ? 'on' : ''} ${isDeco ? 'deco' : ''} ${isWant ? 'want' : ''} ${over && on ? 'over' : ''}"></span>`;
  }).join('')}</span>`;
}

/** The "Skills" block of a sheet. */
function sheetHtml(draft, selected = null) {
  const rows = sheetRows(draft);
  const t = totals(draft);
  const freeByLevel = {};
  for (const [k, l] of t.free) { const key = `${k}${l}`; freeByLevel[key] = (freeByLevel[key] ?? 0) + 1; }
  const freeHtml = Object.entries(freeByLevel).sort().map(([key, n]) => `${slotBox(key.slice(-1), key.slice(0, -1))}<span class="muted">×${n}</span>`).join(' ');
  const setRows = Object.entries(t.sets).map(([name, n]) => {
    const info = SKILL[name];
    const steps = info?.ranks?.map((r) => r.pieces).filter(Boolean) ?? [2, 4];
    return `<div class="setrow"><img class="ico16" src="${ICO}skills/set.png" alt=""><span>${name}</span><span class="steps">${steps.map((s) => `<span class="${n >= s ? 'on' : ''}" title="${s} pieces"></span>`).join('')}</span><span class="cnt">${n} pc</span></div>`;
  }).join('');
  const groupRows = Object.entries(t.groups).map(([name, n]) => {
    const info = SKILL[name];
    const steps = info?.ranks?.map((r) => r.pieces).filter(Boolean) ?? [3];
    return `<div class="setrow"><img class="ico16" src="${ICO}skills/group.png" alt=""><span>${name}</span><span class="steps">${steps.map((s) => `<span class="${n >= s ? 'on' : ''}" title="${s} pieces"></span>`).join('')}</span><span class="cnt">${n} pc</span></div>`;
  }).join('');
  return `
    <h4>Skills <span class="muted" style="text-transform:none;letter-spacing:0;font-weight:400">· click one to see where to get it</span></h4>
    ${rows.map((r) => `<div class="skillrow ${r.want ? 'target' : ''} ${r.state} ${r.skill === selected ? 'sel' : ''}" data-skill="${r.skill}">
      <img src="${skillIcon(r.skill)}" alt="">
      <span><span class="nm">${r.skill}</span>${pips(r.have, r.deco, r.max, r.want)}</span>
      <span class="lv">${r.have}<small>/${r.want ?? r.max}</small></span>
    </div>`).join('')}
    <h4>Free slots</h4>
    <div class="freeslots">${freeHtml || '<span class="muted">none</span>'}</div>
    <h4>Set and group pieces</h4>
    ${setRows}${groupRows}`;
}

/** The "where to get it" list for a skill: pieces by kind and jewels, marking what the draft already wears. */
function sourcesHtml(draft, skill) {
  const inBuild = new Set(KINDS.map((k) => draft.armor[k]?.name).filter(Boolean));
  const pieces = piecesWith(skill).slice(0, 8);
  const jewels = decosWith(skill);
  return `
    <h4>Where to get ${skill}</h4>
    <div class="sources">
      ${jewels.map((d) => `<div class="source"><img src="${decoIcon(d.name)}" alt=""><span class="nm">${shortDeco(d.name)}<small>needs a level ${d.slot} ${d.kind} slot</small></span><span class="lv">+${d.skills.find(([s]) => s === skill)[1]}</span><button class="btn sm">Put in a slot</button></div>`).join('')}
      ${pieces.map((p) => {
        const worn = inBuild.has(p.name), replaces = !worn && draft.armor[p.piece];
        return `<div class="source ${worn ? 'inbuild' : ''} ${replaces ? 'replace' : ''}"><img src="${armorIcon(p.piece, p.rarity)}" alt=""><span class="nm">${p.name}<small>${KIND_LABEL[p.piece]} · ${skillsText(p.skills)} · ${slotsOf(p.slots)}</small></span><span class="lv">+${p.level}</span>${worn ? '<span class="muted small">worn</span>' : `<button class="btn sm">${replaces ? `Swap ${KIND_LABEL[p.piece].toLowerCase()}` : 'Equip'}</button>`}</div>`;
      }).join('')}
    </div>`;
}
