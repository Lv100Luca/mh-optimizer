// Builds of the Luca profile used by the mockups. Equipment, skills and the stats of "In game" and the optimizer builds are
// the real ones (Current.builds.json, the last runs, the app's My builds tab on 2026-10-09); per-attack numbers of builds
// other than "Optimizer build 1" are scaled from it by their score, so they are close but not exact.
const ATTACKS = [
  ['Moves', 'True Charged Slash Lv3', 2, 283, 1353, 27061, true],
  ['Moves', 'Strong Charged Slash Lv3', 1, 187, 926, 22218],
  ['Moves', 'Charged Slash Lv3', 1, 176, 877, 21040],
  ['Moves', 'Offset Rising Slash Lv3', 1, 154, 665, 15965],
  ['Moves', 'Follow-up Cross Slash Lv3', 2, 288, 1209, 36258],
  ['Moves', 'Strong Wide Slash Lv3', 1, 124, 563, 16894],
  ['Combos', 'Charge combo', 5, 694, 3338, 22254],
  ['Sequences', 'Offset into True Charged Slash', 8, 1114, 4928, 22232],
];

const SKILL_ICON = {
  'Attack Boost': 'attack', 'Critical Boost': 'affinity', 'Critical Eye': 'affinity', 'Focus': 'offense', 'Agitator': 'offense',
  'Resentment': 'attack', 'Counterstrike': 'attack', 'Maximum Might': 'attack', 'Speed Eating': 'utility', 'Stun Resistance': 'defense',
  'Weakness Exploit': 'attack', 'Defense Boost': 'defense', 'Fire Resistance': 'defense', 'Latent Power': 'offense', 'Constitution': 'stamina',
  'Divine Blessing': 'defense', 'Ice Attack': 'element', 'Dragon Attack': 'element', 'Burst': 'offense', 'Bind Resistance': 'defense',
  'Survival Expert': 'health',
};
// offense first when listing skill differences
const DAMAGE_SKILLS = ['Attack Boost', 'Critical Eye', 'Critical Boost', 'Weakness Exploit', 'Burst', 'Latent Power', 'Agitator', 'Maximum Might',
  'Counterstrike', 'Resentment', 'Dragon Attack', 'Ice Attack', 'Focus'];

const DECO = {
  'Attack Jewel III': 'l3-weapon-red', 'Focus/Frost Jewel': 'l3-weapon-white', 'Critical Jewel III': 'l3-weapon-purple', 'Expert Jewel III': 'l3-weapon-purple',
  'Focus/Dragon Jewel': 'l3-weapon-white', 'Protection Jewel': 'l1-armor-yellow', 'Challenger Jewel': 'l3-armor-red', 'Chain Jewel': 'l3-armor-red',
  'Tenderizer Jewel': 'l3-armor-red', 'Furor Jewel': 'l2-armor-red', 'Mighty Jewel': 'l2-armor-red', 'Steadfast Jewel': 'l1-armor-yellow',
  'Expert Jewel': 'l1-armor-purple', 'Counter Jewel': 'l2-armor-red',
};

const PIECES = ['weapon', 'head', 'chest', 'arms', 'waist', 'legs', 'talisman'];
const PIECE_LABEL = { weapon: 'Weapon', head: 'Head', chest: 'Chest', arms: 'Arms', waist: 'Waist', legs: 'Legs', talisman: 'Talisman' };
const PIECE_ICON = { weapon: 'icons/weapons/great-sword-r8.png', head: 'icons/armor/head-r8.png', chest: 'icons/armor/chest-r8.png', arms: 'icons/armor/arms-r8.png', waist: 'icons/armor/waist-r8.png', legs: 'icons/armor/legs-r8.png', talisman: 'icons/talisman/r8.png' };

const BUILDS = [
  {
    id: 'ingame', name: 'In game', weapon: 'Current', pair: ['Soul of the Dark Knight', "Lord's Favor"], source: 'mine',
    dmg: 1289, perMin: 25788, everyOn: 1343, score: 650.7, atk: 339, aff: 58, crit: 1.34, ele: 58, procs: 81.2, factor: 0.9527,
    skills: { 'Attack Boost': 5, 'Critical Boost': 3, 'Focus': 3, 'Ice Attack': 1, 'Agitator': 5, 'Resentment': 5, 'Counterstrike': 3, 'Divine Blessing': 3, 'Maximum Might': 3, 'Weakness Exploit': 1, 'Burst': 1, 'Stun Resistance': 3, 'Survival Expert': 1 },
    sets: ['Soul of the Dark Knight I', "Nu Udra's Mutiny I", "Lord's Soul"],
    equip: {
      weapon: ['Current', ['Attack Jewel III', 'Focus/Frost Jewel', 'Critical Jewel III']],
      head: ['Bale Burgeonet α', ['Protection Jewel', 'Protection Jewel']],
      chest: ['Udra Miremail γ', ['Challenger Jewel', 'Challenger Jewel']],
      arms: ['G. Fulgur Vambraces β', ['Chain Jewel', 'Tenderizer Jewel'], true],
      waist: ['Dahaad Shardcoil γ', ['Protection Jewel']],
      legs: ['Udra Miregreaves γ', ['Furor Jewel']],
      talisman: ['Secret Charm (Attack Boost 3, Stun Res. 2, Survival Expert 1)', ['Steadfast Jewel']],
    },
  },
  {
    id: 'opt1', name: 'Optimizer build 1 (picked)', weapon: 'Current', pair: ['Soul of the Dark Knight', "Lord's Favor"], source: 'mine', picked: true,
    dmg: 1353, perMin: 27061, everyOn: 1412, score: 683.0, atk: 321, aff: 84, crit: 1.40, ele: 58, procs: 84.8, factor: 1,
    skills: { 'Critical Boost': 5, 'Critical Eye': 4, 'Focus': 3, 'Agitator': 5, 'Resentment': 5, 'Counterstrike': 3, 'Maximum Might': 3, 'Speed Eating': 3, 'Stun Resistance': 3, 'Weakness Exploit': 3, 'Defense Boost': 2, 'Fire Resistance': 2, 'Latent Power': 1 },
    sets: ['Soul of the Dark Knight I', "Nu Udra's Mutiny I", "Lord's Soul"],
    equip: {
      weapon: ['Current', ['Critical Jewel III', 'Critical Jewel III', 'Expert Jewel III']],
      head: ['Arkvulcan Helm γ', ['Mighty Jewel', 'Steadfast Jewel']],
      chest: ['Udra Miremail γ', ['Challenger Jewel', 'Challenger Jewel']],
      arms: ['Bale Gauntlets α', ['Challenger Jewel']],
      waist: ['Blango Coil β', ['Furor Jewel', 'Furor Jewel'], true],
      legs: ['Udra Miregreaves γ', ['Mighty Jewel']],
      talisman: ['Golden Age Charm (Focus 3, Fire Res. 2, Defense Boost 2)', ['Expert Jewel', 'Steadfast Jewel', 'Steadfast Jewel']],
    },
  },
  {
    id: 'opt3', name: 'Run build 3', weapon: 'Current', pair: ['Soul of the Dark Knight', "Lord's Favor"], source: 'run', ago: '2 days ago',
    dmg: 1351, perMin: 27023, everyOn: 1405, score: 682.0, atk: 321, aff: 83, crit: 1.40, ele: 62, procs: 84.5, factor: 0.9986,
    skills: { 'Critical Boost': 5, 'Focus': 3, 'Dragon Attack': 1, 'Agitator': 5, 'Resentment': 5, 'Weakness Exploit': 5, 'Counterstrike': 3, 'Maximum Might': 3, 'Stun Resistance': 3, 'Bind Resistance': 2 },
    sets: ['Soul of the Dark Knight I', "Nu Udra's Mutiny I", "Lord's Soul"],
    equip: {
      weapon: ['Current', ['Critical Jewel III', 'Critical Jewel III', 'Focus/Dragon Jewel']],
      head: ['Bale Burgeonet α', []],
      chest: ['Udra Miremail γ', ['Challenger Jewel', 'Tenderizer Jewel']],
      arms: ['Udra Mirebraces γ', ['Tenderizer Jewel', 'Steadfast Jewel', 'Steadfast Jewel']],
      waist: ['Dahaad Shardcoil γ', ['Steadfast Jewel']],
      legs: ['Blango Greaves α', ['Mighty Jewel']],
      talisman: ['Exploiter Charm III', []],
    },
  },
  {
    id: 'reroll1', name: 'Run build 1', weapon: 'Current new reroll', pair: ["Leviathan's Fury", "Lord's Soul"], source: 'run', ago: '2 days ago', stale: true, runScore: 681.6,
    dmg: 1353, perMin: 27061, everyOn: 1390, score: 683.0, atk: 321, aff: 84, crit: 1.40, ele: 58, procs: 84.8, factor: 1,
    skills: { 'Critical Boost': 5, 'Critical Eye': 4, 'Focus': 3, 'Agitator': 5, 'Resentment': 5, 'Counterstrike': 3, 'Maximum Might': 3, 'Speed Eating': 3, 'Stun Resistance': 3, 'Constitution': 2, 'Defense Boost': 2, 'Fire Resistance': 2, 'Latent Power': 1 },
    sets: ["Leviathan's Fury I", 'Soul of the Dark Knight I', "Nu Udra's Mutiny I", "Lord's Soul"],
    equip: {
      weapon: ['Current new reroll', ['Critical Jewel III', 'Critical Jewel III', 'Expert Jewel III']],
      head: ['Lagiacrus Helm β', ['Challenger Jewel']],
      chest: ['Udra Miremail γ', ['Challenger Jewel', 'Challenger Jewel']],
      arms: ['Bale Gauntlets α', ['Challenger Jewel']],
      waist: ['Bale Flanchard α', ['Challenger Jewel', 'Furor Jewel', 'Steadfast Jewel']],
      legs: ['Udra Miregreaves γ', []],
      talisman: ['Golden Age Charm (Focus 3, Fire Res. 2, Defense Boost 2)', []],
    },
  },
];

const fmt = (x, d = 0) => x.toLocaleString('en-US', { minimumFractionDigits: d, maximumFractionDigits: d });
function delta(v, d = 0, suffix = '') {
  const r = Math.round(v * 10 ** d) / 10 ** d;
  if (r === 0) return `<span class="delta same">±0${suffix}</span>`;
  return `<span class="delta ${r > 0 ? 'up' : 'down'}">${r > 0 ? '+' : '−'}${fmt(Math.abs(r), d)}${suffix}</span>`;
}
function pct(v, base) { const p = (v - base) / base * 100; return `<span class="pct">(${p >= 0 ? '+' : '−'}${fmt(Math.abs(p), 1)}%)</span>`; }
const skillIcon = (s) => `icons/skills/${SKILL_ICON[s] ?? 'utility'}.png`;
const chip = (s, lvl, cls = '') => `<span class="chip skill ${cls}"><img src="${skillIcon(s)}" alt="">${s}${lvl ? ` <b>${lvl}</b>` : ''}</span>`;
const decoImg = (d) => `<img class="deco" src="icons/decorations/${DECO[d] ?? 'l1-armor-white'}.png" title="${d}" alt="">`;
const setChip = (s) => `<span class="chip skill ${s.startsWith('Lord') ? 'group' : 'set'} tiny"><img src="icons/skills/${s.startsWith('Lord') ? 'group' : 'set'}.png" alt="">${s}</span>`;

/** What it takes to get from build a to build b: pieces to swap, decorations to change, skills gained and lost. */
function changes(a, b) {
  const swap = [], redeco = [];
  for (const p of PIECES) {
    const [na, da] = a.equip[p], [nb, db] = b.equip[p];
    if (na !== nb) swap.push(p);
    else if (da.join() !== db.join()) redeco.push(p);
  }
  const names = [...new Set([...Object.keys(a.skills), ...Object.keys(b.skills)])];
  const rank = (s) => { const i = DAMAGE_SKILLS.indexOf(s); return i < 0 ? 99 : i; };
  const diff = names.map((s) => [s, (b.skills[s] ?? 0) - (a.skills[s] ?? 0)]).filter(([, d]) => d !== 0).sort((x, y) => rank(x[0]) - rank(y[0]) || Math.abs(y[1]) - Math.abs(x[1]));
  return { swap, redeco, gain: diff.filter(([, d]) => d > 0), loss: diff.filter(([, d]) => d < 0) };
}
