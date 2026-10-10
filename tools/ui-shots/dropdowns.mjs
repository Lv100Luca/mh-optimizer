// Opens every dropdown of the browser app in headless Edge (CDP) and lists the elements inside its popover that scroll:
// Pickers (armor, talismans, decorations), selects, search selects and the profile menu, on every tab plus the states that
// show more of them (stats entry, monster target, custom sequence, talisman editor). More than one scroller in a popover is
// reported (a list that scrolls inside a popover that scrolls too). See README.md.
//   SIZE=1366,650 node dropdowns.mjs <origin> <profileFolder> <weapon>     (ONLY=builds,conditions limits the states)
import { spawn, execSync } from 'node:child_process';
import { mkdtempSync, readdirSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, join } from 'node:path';

const [origin, profileFolder, weapon] = process.argv.slice(2);
const files = Object.fromEntries([
  join(profileFolder, 'profile.json'), join(profileFolder, 'talismans.json'),
  ...readdirSync(join(profileFolder, 'weapons')).filter((f) => f.endsWith('.json') && !f.endsWith('.results.json')).map((f) => join(profileFolder, 'weapons', f)),
].map((f) => [basename(f), readFileSync(f, 'utf8')]));
const port = 9400 + Math.floor(Math.random() * 400);
// a straggler from an earlier run would still answer on its port with its own state
const killStragglers = () => { try { execSync(`powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \\"Name='msedge.exe'\\" | Where-Object { $_.CommandLine -like '*edge-dropdowns-*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"`); } catch { } };
killStragglers();
const profileDir = mkdtempSync(join(tmpdir(), 'edge-dropdowns-'));
const edge = spawn(process.env.EDGE_PATH ?? 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  ['--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${profileDir}`, '--no-first-run', `--window-size=${process.env.SIZE ?? '1600,1000'}`, 'about:blank'],
  { stdio: 'ignore' });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

let targets;
for (let i = 0; i < 50; i++) {
  try { targets = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json(); break; } catch { await sleep(200); }
}
const page = targets.find((t) => t.type === 'page');
const ws = new WebSocket(page.webSocketDebuggerUrl);
await new Promise((r) => ws.addEventListener('open', r, { once: true }));
let nextId = 1;
const waiting = new Map();
ws.addEventListener('message', ({ data }) => {
  const msg = JSON.parse(data);
  if (msg.id && waiting.has(msg.id)) { waiting.get(msg.id)(msg); waiting.delete(msg.id); }
  if (msg.method === 'Runtime.exceptionThrown') console.log('[exception]', JSON.stringify(msg.params.exceptionDetails).slice(0, 400));
  // errors, and lines the app logs with a [perf] prefix (Console.WriteLine in C# while measuring)
  if (msg.method === 'Runtime.consoleAPICalled' && (msg.params.type === 'error' || String(msg.params.args[0]?.value).startsWith('[perf]'))) console.log('[console]', msg.params.args.map((a) => a.value ?? a.description).join(' ').slice(0, 400));
});
const send = (method, params = {}) => new Promise((r) => { const id = nextId++; waiting.set(id, r); ws.send(JSON.stringify({ id, method, params })); });
async function evaluate(expression) {
  const r = await send('Runtime.evaluate', { expression: `(async () => { ${expression} })()`, awaitPromise: true, returnByValue: true });
  if (r.result?.exceptionDetails) throw new Error(JSON.stringify(r.result.exceptionDetails).slice(0, 800));
  return r.result?.result?.value;
}
async function navigate(url) {
  await send('Page.navigate', { url });
  await sleep(1500);
  await evaluate(`
    const sleep = ms => new Promise(r => setTimeout(r, ms));
    for (let i = 0; i < 120 && !document.querySelector('.app') ; i++) await sleep(250);
    for (let i = 0; i < 40 && document.body.innerText.includes('Validating…'); i++) await sleep(250);
    await sleep(400);`);
}

await send('Page.enable');
await send('Runtime.enable');
await send('Emulation.setFocusEmulationEnabled', { enabled: true });
await send('Page.bringToFront');
try {
  await navigate(origin + '/');
  await evaluate(`
    const sleep = ms => new Promise(r => setTimeout(r, ms));
    for (let i = 0; i < 120 && ![...document.querySelectorAll('button')].some(b => (b.getAttribute('aria-label') || '').includes('Profile actions')); i++) await sleep(250);
    [...document.querySelectorAll('button')].find(b => (b.getAttribute('aria-label') || '').includes('Profile actions')).click(); await sleep(500);
    [...document.querySelectorAll('.mud-menu-item, .mud-list-item, [role=menuitem]')].find(e => e.textContent.trim().startsWith('Import')).click(); await sleep(800);
    const files = ${JSON.stringify(files)};
    const dt = new DataTransfer();
    for (const [n, t] of Object.entries(files)) dt.items.add(new File([t], n, { type: 'application/json' }));
    const input = document.querySelector('.mud-dialog input[type=file]');
    input.files = dt.files; input.dispatchEvent(new Event('change', { bubbles: true }));
    await sleep(2500);
    [...document.querySelectorAll('.mud-dialog button')].find(b => b.innerText.trim() === 'Import').click();
    await sleep(2000);`);
  const profile = basename(profileFolder);
  const w = encodeURIComponent(weapon);
  const [vw, vh] = (process.env.SIZE ?? '1600,1000').split(',').map(Number);
  await send('Emulation.setDeviceMetricsOverride', { width: vw, height: vh, deviceScaleFactor: 1, mobile: vw < 600 });
  const key = async (k) => { await send('Input.dispatchKeyEvent', { type: 'keyDown', key: k, code: k, windowsVirtualKeyCode: 27 }); await send('Input.dispatchKeyEvent', { type: 'keyUp', key: k, code: k }); };
  const scenarios = [
    ['inventory', `${origin}/${profile}/inventory`, ''],
    ['weapon', `${origin}/${profile}/weapon/${w}/weapon`, ''],
    ['weapon-stats', `${origin}/${profile}/weapon/${w}/weapon`, `[...document.querySelectorAll('.segmented button')].find(b => b.innerText.trim() === 'Enter stats')?.click(); await sleep(600);`],
    ['pair', `${origin}/${profile}/weapon/${w}/pair`, ''],
    ['targets', `${origin}/${profile}/weapon/${w}/targets`, ''],
    ['limits', `${origin}/${profile}/weapon/${w}/limits`, ''],
    ['conditions', `${origin}/${profile}/weapon/${w}/conditions`, ''],
    ['conditions-monster', `${origin}/${profile}/weapon/${w}/conditions`, `[...document.querySelectorAll('.target-col .segmented button')].find(b => b.innerText.trim() === 'Monster')?.click(); await sleep(700);`],
    ['conditions-sequence', `${origin}/${profile}/weapon/${w}/conditions`, `const sel = document.querySelector('.attack-col .mud-select'); sel.querySelector('.mud-input').dispatchEvent(new MouseEvent('mousedown', { bubbles: true })); await sleep(600); [...document.querySelectorAll('.mud-popover-open .mud-list-item')].find(e => e.innerText.includes('Custom sequence'))?.click(); await sleep(800);`],
    ['talismans-editor', `${origin}/${profile}/weapon/${w}/talismans`, `[...document.querySelectorAll('button')].find(b => b.innerText.trim() === 'Edit')?.click(); await sleep(600);`],
    ['options', `${origin}/${profile}/weapon/${w}/options`, ''],
    ['results', `${origin}/${profile}/weapon/${w}/results`, ''],
    ['builds', `${origin}/${profile}/weapon/${w}/builds`, ''],
  ];
  const only = process.env.ONLY?.split(',');
  const findings = [];
  for (const [name, url, setup] of scenarios) {
    if (only && !only.includes(name)) continue;
    await navigate(url);
    await sleep(800);
    if (setup) await evaluate(`const sleep = ms => new Promise(r => setTimeout(r, ms)); ${setup}`);
    // every visible dropdown trigger on the page (the header's selects and menu only on the first scenario)
    const triggers = await evaluate(`
      const vis = e => e.offsetParent !== null && e.getBoundingClientRect().width > 0;
      const all = [];
      document.querySelectorAll('.picker-btn').forEach(e => vis(e) && all.push(['picker', e]));
      document.querySelectorAll('.mud-select').forEach(e => vis(e) && !e.parentElement.closest('.mud-select') && all.push([e.classList.contains('mud-autocomplete') || e.querySelector('.mud-autocomplete') || e.closest('.mud-autocomplete') ? 'autocomplete' : 'select', e]));
      document.querySelectorAll('button[aria-label="Profile actions"]').forEach(e => vis(e) && all.push(['menu', e]));
      const header = e => !!e.closest('.topbar');
      const keep = all.filter(([, e]) => ${JSON.stringify(name)} === 'inventory' || !header(e));
      window.__triggers = keep.map(([, e]) => e);
      return keep.map(([k, e]) => [k, (e.innerText || e.querySelector('input')?.value || '').replace(/\s+/g, ' ').trim().slice(0, 40)]);`);
    for (let t = 0; t < triggers.length; t++) {
      const [kind, label] = triggers[t];
      const r = await evaluate(`
        const sleep = ms => new Promise(r => setTimeout(r, ms));
        const e = window.__triggers[${t}];
        if (!e || !e.isConnected) return null;
        e.scrollIntoView({ block: 'center' }); await sleep(150);
        const open = () => [...document.querySelectorAll('.mud-popover.mud-popover-open')].filter(p => p.offsetHeight > 0);
        if (${JSON.stringify(kind)} === 'picker' || ${JSON.stringify(kind)} === 'menu') e.click();
        else if (${JSON.stringify(kind)} === 'autocomplete') { const i = e.querySelector('input'); i.focus(); i.click(); }
        else e.querySelector('.mud-input').dispatchEvent(new MouseEvent('mousedown', { bubbles: true }));
        await sleep(700);
        const pops = open();
        if (pops.length === 0) return { opened: false };
        const p = pops[pops.length - 1];
        const scrolls = el => { const s = getComputedStyle(el); return { y: /(auto|scroll)/.test(s.overflowY) && el.scrollHeight > el.clientHeight, x: /(auto|scroll)/.test(s.overflowX) && el.scrollWidth > el.clientWidth }; };
        const found = [p, ...p.querySelectorAll('*')].map(el => [el, scrolls(el)]).filter(([, s]) => s.y || s.x)
          .map(([el, s]) => ({ el: (el === p ? 'POPOVER ' : '') + el.tagName.toLowerCase() + '.' + [...el.classList].filter(c => !c.startsWith('mud-popover-')).slice(0, 4).join('.'),
                               y: s.y ? el.scrollHeight - el.clientHeight : 0, x: s.x ? el.scrollWidth - el.clientWidth : 0 }));
        const r = p.getBoundingClientRect();
        return { opened: true, popH: Math.round(r.height), maxH: getComputedStyle(p).maxHeight, scrollers: found };`);
      // close it again
      await key('Escape');
      await evaluate(`const sleep = ms => new Promise(r => setTimeout(r, ms)); await sleep(250);
        for (const o of document.querySelectorAll('.mud-overlay')) o.click(); await sleep(250);
        if (document.activeElement && document.activeElement !== document.body) document.activeElement.blur(); await sleep(150);`);
      const bad = r?.opened && (r.scrollers.length > 1 || r.scrollers.some(s => s.el.startsWith('POPOVER') && s.y > 0 && s.y < 40) || r.scrollers.some(s => s.x > 0));
      const line = `${bad ? 'DOUBLE ' : r?.opened ? 'ok     ' : 'closed '} ${name} ${kind} "${label}" ${r?.opened ? `h=${r.popH} max=${r.maxH} ` + JSON.stringify(r.scrollers) : ''}`;
      console.log(line);
      if (bad) findings.push(line);
    }
  }
  console.log(`\n${findings.length} dropdowns with more than one scroller`);
  findings.forEach((f) => console.log(f));
} finally {
  ws.close();
  edge.kill();
  killStragglers();
}
