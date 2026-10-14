// How long everyday edits block the browser app's page (headless Edge over CDP; see README.md): switching tabs, weapon
// reinforcements, condition toggles, skill limits, and duplicating and editing a build in My builds. For each step it prints
// how long until the page is quiet again (the debounced validation and draft saving included) and the long tasks on the way.
//   node interact.mjs <origin> <profileFolder> <weapon>      (ONLY=nav,weapon,conditions,limits,builds; REPEAT=n)
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
const killStragglers = () => { try { execSync(`powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \\"Name='msedge.exe' OR Name='chrome.exe'\\" | Where-Object { $_.CommandLine -like '*edge-interact-*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"`); } catch { } };
killStragglers();
const profileDir = mkdtempSync(join(tmpdir(), 'edge-interact-'));
const edge = spawn(process.env.EDGE_PATH ?? 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  ['--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${profileDir}`, '--no-first-run', '--window-size=1600,1000', 'about:blank'],
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
  // in the page: long tasks, and a step runner that waits until the DOM has been quiet for 700 ms (past the 250 / 300 ms debounces)
  const install = `
    if (!window.__long) { window.__long = []; new PerformanceObserver(l => { for (const e of l.getEntries()) window.__long.push([e.startTime, e.duration]); }).observe({ type: 'longtask' }); }
    window.__sleep = ms => new Promise(r => setTimeout(r, ms));
    window.__step = async (act) => {
      let last = performance.now(), first = null;
      const mo = new MutationObserver(() => { last = performance.now(); first ??= last; });
      mo.observe(document.body, { subtree: true, childList: true, attributes: true, characterData: true });
      const t0 = performance.now();
      last = t0;
      const ok = await act();
      while (performance.now() - last < 700 && performance.now() - t0 < 8000) await __sleep(20);
      mo.disconnect();
      const long = window.__long.filter(([s]) => s >= t0 - 5).map(([s, d]) => [Math.round(s - t0), Math.round(d)]);
      return { ok: ok !== false, first: first === null ? null : Math.round(first - t0), settled: Math.round(last - t0),
               popovers: document.querySelectorAll('.mud-popover').length, tooltips: document.querySelectorAll('.skillhover').length, blocked: long.reduce((a, [, d]) => a + d, 0), longest: Math.max(0, ...long.map(([, d]) => d)), long };
    };`;
  const results = [];
  // the browser's own time per step (script incl. .NET, style recalculation, layout), from DevTools' cumulative metrics
  await send('Performance.enable', { timeDomain: 'threadTicks' });
  const metrics = async () => Object.fromEntries((await send('Performance.getMetrics')).result.metrics.map((m) => [m.name, m.value]));
  async function step(name, act) {
    console.log(`[perf] -- ${name}`);
    const m0 = await metrics();
    const profile = process.env.PROFILE && name.includes(process.env.PROFILE);
    if (profile) { await send('Profiler.enable'); await send('Profiler.setSamplingInterval', { interval: 200 }); await send('Profiler.start'); }
    const r = await evaluate(`${install} return JSON.stringify(await __step(async () => { const sleep = __sleep; ${act} }));`);
    if (profile) report((await send('Profiler.stop')).result.profile, name);
    const m1 = await metrics();
    const o = JSON.parse(r);
    const d = (k) => Math.round((m1[k] - m0[k]) * 1000);
    o.script = d('ScriptDuration'); o.style = d('RecalcStyleDuration'); o.layout = d('LayoutDuration'); o.nodes = m1.Nodes;
    results.push([name, o]);
    console.log(`${o.ok ? '' : 'FAILED '}${name.padEnd(34)} blocked ${String(o.blocked).padStart(5)} ms  longest ${String(o.longest).padStart(4)} ms  quiet after ${String(o.settled).padStart(5)} ms  script ${o.script} style ${o.style} layout ${o.layout} popovers ${o.popovers} (tooltips ${o.tooltips})  ${JSON.stringify(o.long)}`);
  }
  // self time of a CPU profile by script and function (.NET runs as WebAssembly: its interpreter shows as wasm functions)
  function report(profile, name) {
    const byId = new Map(profile.nodes.map((n) => [n.id, n]));
    const self = new Map();
    profile.samples.forEach((id, i) => self.set(id, (self.get(id) ?? 0) + (profile.timeDeltas[i] ?? 0) / 1000));
    const files = new Map(), fns = new Map();
    for (const [id, ms] of self) {
      const f = byId.get(id).callFrame;
      const file = f.url ? f.url.split('/').pop().replace(/\.[0-9a-z]{8,}\./, '.') : f.functionName.startsWith('(') ? f.functionName : '(no url)';
      const kind = file.includes('.wasm') || /^\$|wasm-function/.test(f.functionName) ? 'dotnet.native.wasm' : file;
      files.set(kind, (files.get(kind) ?? 0) + ms);
      const key = `${kind} ${f.functionName || '(anonymous)'}`;
      fns.set(key, (fns.get(key) ?? 0) + ms);
    }
    const top = (m, n) => [...m].filter(([k]) => !k.startsWith('(idle)') && k !== '(program)').sort((a, b) => b[1] - a[1]).slice(0, n).map(([k, v]) => `      ${v.toFixed(1).padStart(7)} ms  ${k}`);
    console.log([`    profile of ${name}, by script:`, ...top(files, 10), '    by function:', ...top(fns, 25)].join('\n'));
  }
  const click = (sel, text) => `const e = [...document.querySelectorAll(${JSON.stringify(sel)})].find(e => ${text ? `e.innerText.trim() === ${JSON.stringify(text)} || (e.title || '') === ${JSON.stringify(text)}` : 'true'}); if (!e) return false; e.click();`;
  const only = process.env.ONLY?.split(',');
  const want = (g) => !only || only.includes(g);
  const repeat = Number(process.env.REPEAT ?? 1);

  for (let rep = 0; rep < repeat; rep++) {
    if (want('nav')) {
      await navigate(`${origin}/${profile}/weapon/${w}/weapon`);
      await sleep(1500);
      const seen = new Set();
      for (const label of ['Skill pair', 'Target skills', 'Skill limits', 'Conditions', 'Talismans', 'Options', 'Review', 'Results', 'My builds', 'Inventory', 'Weapon', 'My builds', 'Conditions', 'Weapon'])
        { await step(`tab ${label}${seen.has(label) ? ' (again)' : ''}`, click('.navitem', label)); seen.add(label); }
    }
    if (want('weapon')) {
      await navigate(`${origin}/${profile}/weapon/${w}/weapon`);
      await sleep(1500);
      for (const [i, pick] of [[0, 1], [0, 2]]) {
        await evaluate(`const s = [...document.querySelectorAll('.rf-table .mud-select')].filter(e => !e.parentElement.closest('.mud-select'))[${i}]; s.querySelector('.mud-input').dispatchEvent(new MouseEvent('mousedown', { bubbles: true })); await new Promise(r => setTimeout(r, 600));`);
        await step(`weapon reinforcement ${i}->${pick}`, `const items = [...document.querySelectorAll('.mud-popover-open .mud-list-item')].filter(e => !e.classList.contains('mud-list-item-disabled')); if (!items[${pick}]) return false; items[${pick}].click();`);
      }
      await step('weapon focus segment', `const b = [...document.querySelectorAll('.segmented button')].find(b => !b.classList.contains('active') && !b.disabled && b.closest('.form-grid')); if (!b) return false; b.click();`);
    }
    if (want('conditions')) {
      await navigate(`${origin}/${profile}/weapon/${w}/conditions`);
      await sleep(1500);
      for (const n of [0, 0, 3])
        await step(`condition toggle ${n}`, `const c = document.querySelectorAll('label.cond input[type=checkbox]')[${n}]; if (!c) return false; c.click();`);
    }
    if (want('limits')) {
      await navigate(`${origin}/${profile}/weapon/${w}/limits`);
      await sleep(1500);
      await step('limit add skill', click('.available .skillopt'));
      await step('limit level +', `const b = document.querySelectorAll('.selected .stepper button[aria-label=increase]'); const x = [...b].find(x => !x.disabled); if (!x) return false; x.click();`);
      await step('limit remove', click('.selected .iconbtn'));
    }
    if (want('builds')) {
      await navigate(`${origin}/${profile}/weapon/${w}/builds`);
      await sleep(2000);
      await step('build duplicate', click('button', 'Duplicate'));
      await step('build transcend toggle', `const c = document.querySelector('.piece-meta input[type=checkbox]'); if (!c) return false; c.click();`);
      await evaluate(`document.querySelectorAll('.picker-btn.slotpick')[1].click(); await new Promise(r => setTimeout(r, 700));`);
      await step('build pick decoration', `const o = document.querySelectorAll('.picker-opt:not(.clear)')[3]; if (!o) return false; o.click();`);
      await evaluate(`document.querySelectorAll('.picker-btn.piecepick')[1].click(); await new Promise(r => setTimeout(r, 700));`);
      await step('build pick armor piece', `const o = document.querySelectorAll('.picker-opt:not(.clear)')[2]; if (!o) return false; o.click();`);
      await step('build own condition toggle', `const c = document.querySelector('.build-conds label.cond input[type=checkbox]'); if (!c) return false; c.click();`);
      await step('build switch tab', `const t = [...document.querySelectorAll('.buildtab')].find(t => !t.classList.contains('active')); if (!t) return false; t.click();`);
    }
  }
  const total = results.reduce((a, [, o]) => a + o.blocked, 0);
  console.log(`\n${results.length} steps, ${total} ms blocked in all, ${results.filter(([, o]) => !o.ok).length} failed`);
} finally {
  ws.close();
  edge.kill();
  killStragglers();
}
