// Times the browser app in headless Edge (Chromium) over the DevTools protocol: imports a profile folder of the server
// version, runs one weapon and prints the run log. The Claude desktop browser pane cannot run the solver (it cannot start
// workers from a worker), so this stands in for it.
//   python spikes/WasmSpeedTest/serve.py 5241 publish/browser/wwwroot      (after dotnet publish ... -o publish/browser)
//   node spikes/WasmSpeedTest/edge-bench.mjs http://localhost:5241 inputs/profiles/Luca "the best" 2
// Edge runs with a throwaway profile under the system temp directory (EDGE_PATH overrides the executable).
import { spawn } from 'node:child_process';
import { mkdtempSync, readdirSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, join } from 'node:path';

const [origin, profileFolder, weapon, runsArg] = process.argv.slice(2);
// the files the import dialog takes from a profile folder (results are left out: the run makes new ones)
const files = Object.fromEntries([
  join(profileFolder, 'profile.json'), join(profileFolder, 'talismans.json'),
  ...readdirSync(join(profileFolder, 'weapons')).filter((f) => f.endsWith('.json') && !f.endsWith('.results.json')).map((f) => join(profileFolder, 'weapons', f)),
].map((f) => [basename(f), readFileSync(f, 'utf8')]));
const runs = Number(runsArg ?? 1);
const port = 9333;
const profileDir = mkdtempSync(join(tmpdir(), 'edge-bench-'));
const edge = spawn(process.env.EDGE_PATH ?? 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  ['--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${profileDir}`, '--no-first-run', '--window-size=1400,900', 'about:blank'],
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
  if (msg.method === 'Runtime.consoleAPICalled' && process.env.CONSOLE) console.log('[console]', msg.params.args.map((a) => a.value).join(' '));
});
const send = (method, params = {}) => new Promise((r) => { const id = nextId++; waiting.set(id, r); ws.send(JSON.stringify({ id, method, params })); });
async function evaluate(expression) {
  const r = await send('Runtime.evaluate', { expression: `(async () => { ${expression} })()`, awaitPromise: true, returnByValue: true });
  if (r.result?.exceptionDetails) throw new Error(JSON.stringify(r.result.exceptionDetails).slice(0, 800));
  return r.result?.result?.value;
}
async function navigate(url) { await send('Page.navigate', { url }); await sleep(1500); }

await send('Page.enable');
await send('Runtime.enable');
try {
  await navigate(origin + '/');
  console.log('env', await evaluate(`
    const sleep = ms => new Promise(r => setTimeout(r, ms));
    for (let i = 0; i < 120 && ![...document.querySelectorAll('button')].some(b => (b.getAttribute('aria-label') || '').includes('Profile actions')); i++) await sleep(250);
    const m = await import('/solver/bridge.js');
    return JSON.stringify(await m.environment());`));
  await evaluate(`
    const sleep = ms => new Promise(r => setTimeout(r, ms));
    [...document.querySelectorAll('button')].find(b => (b.getAttribute('aria-label') || '').includes('Profile actions')).click(); await sleep(500);
    [...document.querySelectorAll('.mud-menu-item, .mud-list-item, [role=menuitem]')].find(e => e.textContent.trim().startsWith('Import')).click(); await sleep(800);
    const files = ${JSON.stringify(files)};
    const dt = new DataTransfer();
    for (const [n, t] of Object.entries(files)) dt.items.add(new File([t], n, { type: 'application/json' }));
    const input = document.querySelector('.mud-dialog input[type=file]');
    input.files = dt.files; input.dispatchEvent(new Event('change', { bubbles: true }));
    await sleep(1500);
    [...document.querySelectorAll('.mud-dialog button')].find(b => b.innerText.trim() === 'Import').click();
    await sleep(1500);`);
  await navigate(`${origin}/Imported/weapon/${encodeURIComponent(weapon)}/results`);
  await sleep(10000); // solver warm-up and lanes
  for (let run = 0; run < runs; run++) {
    const log = await evaluate(`
      const sleep = ms => new Promise(r => setTimeout(r, ms));
      for (let i = 0; i < 200 && ![...document.querySelectorAll('button')].some(b => b.innerText.trim() === 'Run optimizer' && !b.disabled); i++) await sleep(250);
      const lines = new Set();
      const t0 = performance.now();
      [...document.querySelectorAll('button')].find(b => b.innerText.trim() === 'Run optimizer').click();
      await sleep(500);
      while ([...document.querySelectorAll('button')].some(b => b.innerText.trim() === 'Cancel run') && performance.now() - t0 < 300000) {
        for (const m of document.body.innerText.matchAll(/^\\[ *\\d+\\.\\ds\\].*$/gm)) lines.add(m[0].replace(/^\\[ +/, '['));
        await sleep(100);
      }
      for (const m of document.body.innerText.matchAll(/^\\[ *\\d+\\.\\ds\\].*$/gm)) lines.add(m[0].replace(/^\\[ +/, '['));
      const scores = [...document.body.innerText.matchAll(/\\b(\\d{3}\\.\\d{2})\\b/g)].map(m => m[1]).slice(0, 12);
      return [...lines].sort((a, b) => parseFloat(a.slice(1)) - parseFloat(b.slice(1))).join('\\n') + '\\nwall ' + ((performance.now() - t0) / 1000).toFixed(1) + ' s; scores on page: ' + scores.join(' ');`);
    console.log(`--- run ${run + 1}\n${log}`);
  }
} finally {
  ws.close();
  edge.kill();
}
