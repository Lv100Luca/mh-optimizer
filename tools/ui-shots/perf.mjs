// Main-thread responsiveness of the browser app in headless Edge (CDP); see README.md. .NET shares the page's only thread,
// so long synchronous C# work shows up as long tasks and gaps between frames.
//  run   - clicks "Run optimizer" and records frame gaps and long tasks until the run ends, then prints the run log
//  gems  - opens a decoration slot picker on My builds, types a query key by key (QUERY), moves the mouse over the options
//  check - drives the slot and armor pickers (filter, arrow keys, hover, Enter, Escape) and prints what they show
//   node perf.mjs <origin> <profileFolder> <weapon> <run|gems|check|both>
import { spawn, execSync } from 'node:child_process';
import { mkdtempSync, readdirSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, join } from 'node:path';

const [origin, profileFolder, weapon, what = 'both'] = process.argv.slice(2);
const files = Object.fromEntries([
  join(profileFolder, 'profile.json'), join(profileFolder, 'talismans.json'),
  ...readdirSync(join(profileFolder, 'weapons')).filter((f) => f.endsWith('.json') && !f.endsWith('.results.json')).map((f) => join(profileFolder, 'weapons', f)),
].map((f) => [basename(f), readFileSync(f, 'utf8')]));
const port = 9400 + Math.floor(Math.random() * 400);
// a straggler from an earlier run would still answer on its port with its own state
const killStragglers = () => { try { execSync(`powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \\"Name='msedge.exe'\\" | Where-Object { $_.CommandLine -like '*edge-perf-*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"`); } catch { } };
killStragglers();
const profileDir = mkdtempSync(join(tmpdir(), 'edge-perf-'));
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

// frame gaps and long tasks, kept on window.__perf
const probe = `
  window.__perf = { frames: [], long: [] };
  new PerformanceObserver(l => { for (const e of l.getEntries()) window.__perf.long.push([e.startTime, e.duration]); }).observe({ type: 'longtask', buffered: false });
  const tick = t => { window.__perf.frames.push(performance.now()); requestAnimationFrame(tick); };
  requestAnimationFrame(tick);`;
const summary = `
  const since = t0 => {
    const f = window.__perf.frames.filter(x => x >= t0);
    let max = 0, at = 0; for (let i = 1; i < f.length; i++) if (f[i] - f[i - 1] > max) { max = f[i] - f[i - 1]; at = f[i - 1] - t0; }
    const long = window.__perf.long.filter(([s]) => s >= t0 - 5);
    return { firstFrame: f.length ? +(f[0] - t0).toFixed(0) : null, maxGap: +max.toFixed(0), maxGapAt: +at.toFixed(0),
      longTasks: long.length, longTotal: +long.reduce((a, [, d]) => a + d, 0).toFixed(0), longMax: +Math.max(0, ...long.map(([, d]) => d)).toFixed(0),
      over200: long.filter(([, d]) => d > 200).map(([s, d]) => [+(s - t0).toFixed(0), +d.toFixed(0)]) };
  };`;

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

  if (what === 'gems' || what === 'both') {
    await navigate(`${origin}/${profile}/weapon/${w}/builds`);
    await sleep(1500);
    const query = process.env.QUERY ?? 'attack boost';
    const r = await evaluate(`
      const sleep = ms => new Promise(r => setTimeout(r, ms));
      // the time of the last DOM change after t, once the page has been quiet for 150 ms
      const settle = async t => { let last = t; const mo = new MutationObserver(() => { last = performance.now(); }); mo.observe(document.body, { subtree: true, childList: true, attributes: true, characterData: true });
        while (performance.now() - last < 150) await new Promise(r => setTimeout(r, 10)); mo.disconnect(); return last - t; };
      ${probe}
      ${summary}
      await sleep(500);
      const out = { pickers: document.querySelectorAll('.picker-btn').length };
      const btn = [...document.querySelectorAll('.picker-btn.slotpick')].find(b => b.innerText.trim().startsWith('3')) ?? document.querySelectorAll('.picker-btn.slotpick')[0];
      let t0 = performance.now();
      btn.click();
      for (let i = 0; i < 400 && !document.querySelector('.picker-opt:not(.clear)'); i++) await sleep(5);
      const shown = performance.now() - t0;
      const settled = await settle(t0);
      out.open = { ms: +shown.toFixed(0), settled: +settled.toFixed(0), options: document.querySelectorAll('.picker-opt').length, ...since(t0) };
      await sleep(300);
      const input = document.querySelector('.picker-pop-body input');
      const keys = [];
      let text = '';
      t0 = performance.now();
      for (const ch of ${JSON.stringify(query)}) {
        text += ch;
        const k0 = performance.now();
        input.value = text; input.dispatchEvent(new Event('input', { bubbles: true }));
        const sync = performance.now() - k0;
        keys.push([+sync.toFixed(0), +(await settle(k0)).toFixed(0)]);
      }
      await sleep(300);
      out.typing = { perKey: keys, total: keys.reduce((a, [, b]) => a + b, 0), options: document.querySelectorAll('.picker-opt').length, ...since(t0) };
      input.value = ''; input.dispatchEvent(new Event('input', { bubbles: true })); await sleep(400);
      const rects = [...document.querySelectorAll('.picker-opt')].slice(0, 25).map(o => { const r = o.getBoundingClientRect(); return [r.x + 20, r.y + r.height / 2]; });
      window.__rects = rects;
      return JSON.stringify(out);`);
    console.log('gems', r);
    // hover: real mouse moves over the first options (the list is unfiltered again)
    const rects = await evaluate('return window.__rects;');
    await evaluate(`window.__hover0 = performance.now(); return 0;`);
    for (const [x, y] of rects) await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x, y });
    const hover = await evaluate(`
      ${summary}
      await new Promise(r => setTimeout(r, 300));
      return JSON.stringify({ moves: window.__rects.length, ms: +(performance.now() - window.__hover0).toFixed(0), ...since(window.__hover0) });`);
    console.log('hover', hover);
  }

  if (what === 'check') {
    await navigate(`${origin}/${profile}/weapon/${w}/builds`);
    await sleep(1500);
    const step = async (js) => evaluate(`const sleep = ms => new Promise(r => setTimeout(r, ms)); ${js}`);
    const key = async (k) => { await send('Input.dispatchKeyEvent', { type: 'keyDown', key: k, code: k, windowsVirtualKeyCode: { ArrowDown: 40, ArrowUp: 38, Enter: 13, Escape: 27 }[k] }); await send('Input.dispatchKeyEvent', { type: 'keyUp', key: k, code: k }); await sleep(250); };
    const state = () => step(`return JSON.stringify({ open: !!document.querySelector('.picker-pop-body'), n: document.querySelectorAll('.picker-opt:not(.clear)').length,
      active: document.querySelector('.picker-opt.active .opt-name')?.innerText ?? null,
      names: [...document.querySelectorAll('.picker-opt:not(.clear) .opt-name')].slice(0, 6).map(e => e.innerText),
      groups: [...document.querySelectorAll('.picker-group')].map(e => e.innerText) });`);
    // a level 3 slot picker
    const before = await step(`const b = [...document.querySelectorAll('.picker-btn.slotpick')].find(b => b.innerText.trim().startsWith('3')); window.__btn = [...document.querySelectorAll('.picker-btn.slotpick')].indexOf(b); b.click(); await sleep(600); return b.innerText.replace(/\s+/g, ' ');`);
    console.log('slot before:', before);
    console.log('opened:', await state());
    await step(`const i = document.querySelector('.picker-pop-body input'); i.value = 'crit'; i.dispatchEvent(new Event('input', { bubbles: true })); await sleep(400);`);
    console.log('query crit:', await state());
    await key('ArrowDown'); await key('ArrowDown');
    console.log('after 2x down:', await state());
    const r = await step(`const o = document.querySelectorAll('.picker-opt:not(.clear)')[4]; const b = o.getBoundingClientRect(); return [b.x + 30, b.y + b.height / 2, o.querySelector('.opt-name').innerText];`);
    await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: r[0], y: r[1] - 30 });
    await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: r[0], y: r[1] });
    await sleep(300);
    console.log('hovered 5th (' + r[2] + '):', await state());
    await key('Enter');
    console.log('after enter:', await state());
    console.log('slot after:', await step(`await sleep(300); return document.querySelectorAll('.picker-btn.slotpick')[window.__btn].innerText.replace(/\s+/g, ' ');`));
    await step(`document.querySelectorAll('.picker-btn.slotpick')[window.__btn].click(); await sleep(600);`);
    console.log('reopened:', await state());
    await step(`const i = document.querySelector('.picker-pop-body input'); i.value = 'zzzz'; i.dispatchEvent(new Event('input', { bubbles: true })); await sleep(400);`);
    console.log('no match:', await step(`return document.querySelector('.picker-empty')?.innerText ?? 'no empty message';`));
    await key('Escape');
    console.log('after escape:', await state());
    // an armor piece picker: search and click
    await step(`document.querySelectorAll('.picker-btn.piecepick')[0].click(); await sleep(600);`);
    console.log('armor opened:', await state());
    await step(`const i = document.querySelector('.picker-pop-body input'); i.value = 'gore'; i.dispatchEvent(new Event('input', { bubbles: true })); await sleep(400);`);
    console.log('armor gore:', await state());
    console.log('armor picked:', await step(`const o = document.querySelectorAll('.picker-opt:not(.clear)')[1]; const n = o.querySelector('.opt-name').innerText; o.click(); await sleep(800); return n + ' -> ' + document.querySelectorAll('.picker-btn.piecepick')[0].innerText.replace(/\s+/g, ' ');`));
  }

  if (what === 'run' || what === 'both') {
    await navigate(`${origin}/${profile}/weapon/${w}/results`);
    await sleep(Number(process.env.WARM ?? 10000)); // solver warm-up and lanes
    const r = await evaluate(`
      const sleep = ms => new Promise(r => setTimeout(r, ms));
      ${probe}
      ${summary}
      for (let i = 0; i < 200 && ![...document.querySelectorAll('button')].some(b => b.innerText.trim() === 'Run optimizer' && !b.disabled); i++) await sleep(250);
      await sleep(300);
      const t0 = performance.now();
      [...document.querySelectorAll('button')].find(b => b.innerText.trim() === 'Run optimizer').click();
      const afterClick = performance.now() - t0;
      let running = null;
      for (let i = 0; i < 6000 && running === null; i++) {
        if ([...document.querySelectorAll('button')].some(b => b.innerText.trim() === 'Cancel run')) running = performance.now() - t0;
        else await sleep(10);
      }
      await sleep(300);
      let log = '';
      while ([...document.querySelectorAll('button')].some(b => b.innerText.trim() === 'Cancel run') && performance.now() - t0 < 300000) { log = document.querySelector('.runlog')?.innerText ?? log; await sleep(100); }
      const wall = performance.now() - t0;
      const lines = log.split(String.fromCharCode(10));
      return JSON.stringify({ clickHandlerMs: +afterClick.toFixed(0), cancelButtonAt: running && +running.toFixed(0), wall: +(wall / 1000).toFixed(1), ...since(t0), firstLines: lines });`);
    const o = JSON.parse(r); const lines = o.firstLines; delete o.firstLines; console.log('run', JSON.stringify(o)); console.log(lines.join(String.fromCharCode(10)));
  }
} finally {
  ws.close();
  edge.kill();
  killStragglers();
}
