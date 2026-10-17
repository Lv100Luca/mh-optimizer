// Times the browser app in headless Chrome or Edge over the DevTools protocol: imports a profile folder of the server
// version, runs one weapon and prints the run log. The Claude desktop browser pane cannot run the solver (it cannot start
// workers from a worker), so this stands in for it.
//   python spikes/WasmSpeedTest/serve.py 5241 publish/browser/wwwroot      (after dotnet publish ... -o publish/browser)
//   node spikes/WasmSpeedTest/edge-bench.mjs http://localhost:5241 inputs/profiles/Luca "the best" 2
// Uses Chrome when installed, else Edge (EDGE_PATH overrides). Managed Edge drops the DevTools connection partway through
// long runs, so prefer Chrome. The browser runs with a throwaway profile under the system temp directory.
import { execSync, spawn } from 'node:child_process';
import { existsSync, mkdtempSync, readdirSync, readFileSync } from 'node:fs';
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
// a straggler from an earlier run would still hold the port and answer with its own page
const killTree = (pid) => { try { execSync(process.platform === 'win32' ? `taskkill /PID ${pid} /T /F` : `kill -9 ${pid}`, { stdio: 'ignore' }); } catch { } };
if (process.platform === 'win32') {
  try { execSync(`powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \\"Name='msedge.exe' OR Name='chrome.exe'\\" | Where-Object { $_.CommandLine -like '*edge-bench-*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"`, { stdio: 'ignore' }); } catch { }
}
const profileDir = mkdtempSync(join(tmpdir(), 'edge-bench-'));
const chrome = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const browserPath = process.env.EDGE_PATH ?? (existsSync(chrome) ? chrome : 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe');
const edge = spawn(browserPath,
  ['--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${profileDir}`, '--no-first-run', '--window-size=1400,900', '--window-position=-32000,-32000', 'about:blank'],
  { stdio: 'ignore' });
process.on('exit', () => killTree(edge.pid));
for (const sig of ['SIGINT', 'SIGTERM', 'SIGBREAK']) process.on(sig, () => process.exit(130));
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

for (let i = 0; i < 50; i++) {
  try { await (await fetch(`http://127.0.0.1:${port}/json/version`)).json(); break; } catch { await sleep(200); }
}
// a tab of our own: managed Edge opens no about:blank tab, and other pages (sign-in, extensions) come first in the list
const page = await (await fetch(`http://127.0.0.1:${port}/json/new?about:blank`, { method: 'PUT' })).json();
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
      const label = b => b.innerText.trim() || b.getAttribute('aria-label') || '';
      const runButton = () => [...document.querySelectorAll('button')].find(b => label(b).endsWith('Run optimizer') && !b.disabled);
      for (let i = 0; i < 200 && !runButton(); i++) await sleep(250);
      const t0 = performance.now();
      runButton().click();
      await sleep(500);
      while ([...document.querySelectorAll('button')].some(b => label(b) === 'Cancel run') && performance.now() - t0 < 300000) await sleep(100);
      const wall = (performance.now() - t0) / 1000;
      // the log is folded away behind a button
      [...document.querySelectorAll('button')].find(b => b.innerText.trim().startsWith('Show search log'))?.click();
      for (let i = 0; i < 40 && !document.getElementById('results-runlog'); i++) await sleep(100);
      const log = document.getElementById('results-runlog')?.innerText ?? '(no search log)';
      const scores = [...document.body.innerText.matchAll(/\\b(\\d{3}\\.\\d{2})\\b/g)].map(m => m[1]).slice(0, 12);
      return log + '\\nwall ' + wall.toFixed(1) + ' s; scores on page: ' + scores.join(' ');`);
    console.log(`--- run ${run + 1}\n${log}`);
  }
} finally {
  ws.close();
  killTree(edge.pid);
}
