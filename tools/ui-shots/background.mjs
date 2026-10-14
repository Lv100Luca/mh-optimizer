// How long a run takes when its tab is in the background, in Edge (CDP). Chrome and Edge throttle a hidden tab's
// timers to about one wake-up a minute once it has been hidden for five minutes ("intensive wake-up throttling"); this
// starts that after 10 s, imports a profile, starts a run of the weapon and reports the run's time and log. Headless Edge
// only throttles a tab hidden behind another one to a wake-up a second, so the page runs in a real Edge window moved off
// the screen, which Windows reports as hidden: that gets the minute. HIDE=0 runs headless with the tab in front instead.
// EDGE_PATH picks another Chromium browser, e.g. a bare Chromium (then WAIT_BEFORE_RUN=330).
//   node background.mjs <origin> <profileFolder> <weapon>
import { spawn, execSync } from 'node:child_process';
import { mkdtempSync, readdirSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, join } from 'node:path';

const [origin, profileFolder, weapon] = process.argv.slice(2);
const hide = process.env.HIDE !== '0';
const files = Object.fromEntries([
  join(profileFolder, 'profile.json'), join(profileFolder, 'talismans.json'),
  ...readdirSync(join(profileFolder, 'weapons')).filter((f) => f.endsWith('.json') && !f.endsWith('.results.json')).map((f) => join(profileFolder, 'weapons', f)),
].map((f) => [basename(f), readFileSync(f, 'utf8')]));
const port = 9400 + Math.floor(Math.random() * 400);
const killStragglers = () => { try { execSync(`powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \\"Name='msedge.exe' OR Name='chrome.exe'\\" | Where-Object { $_.CommandLine -like '*edge-bg-*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"`); } catch { } };
killStragglers();
const profileDir = mkdtempSync(join(tmpdir(), 'edge-bg-'));
const edge = spawn(process.env.EDGE_PATH ?? 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  [...(hide ? [] : ['--headless=new']), '--window-position=-32000,-32000', `--remote-debugging-port=${port}`, `--user-data-dir=${profileDir}`, '--no-first-run',
   '--window-size=1600,1000', '--enable-features=IntensiveWakeUpThrottling:grace_period_seconds/10', 'about:blank'],
  { stdio: 'ignore' });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

let targets;
for (let i = 0; i < 50; i++) {
  try { targets = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json(); if (targets.some((t) => t.type === 'page')) break; } catch { }
  await sleep(200);
}
const page = targets.find((t) => t.type === 'page');
const ws = new WebSocket(page.webSocketDebuggerUrl);
await new Promise((r) => ws.addEventListener('open', r, { once: true }));
let nextId = 1;
const waiting = new Map();
ws.addEventListener('message', ({ data }) => {
  const msg = JSON.parse(data);
  if (msg.id && waiting.has(msg.id)) { waiting.get(msg.id)(msg); waiting.delete(msg.id); }
  if (msg.method === 'Runtime.exceptionThrown') console.log('[exception]', msg.params.exceptionDetails.exception?.description?.slice(0, 600) ?? JSON.stringify(msg.params.exceptionDetails).slice(0, 400));
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
    await sleep(400);`);
}

await send('Page.enable');
await send('Runtime.enable');
try {
  await send('Emulation.setDeviceMetricsOverride', { width: 1600, height: 1000, deviceScaleFactor: 1, mobile: false });
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
  await navigate(`${origin}/${profile}/weapon/${encodeURIComponent(weapon)}/results`);
  // solver warm-up; WAIT_BEFORE_RUN (s) keeps the tab hidden longer first, past the browser's grace period (5 minutes in
  // Chromium, which ignores the grace_period_seconds feature parameter that Edge takes)
  await sleep(Number(process.env.WAIT_BEFORE_RUN ?? 10) * 1000);
  await evaluate(`
    const sleep = ms => new Promise(r => setTimeout(r, ms));
    for (let i = 0; i < 200 && ![...document.querySelectorAll('header button')].some(b => b.innerText.includes('Run optimizer') && !b.disabled); i++) await sleep(250);
    [...document.querySelectorAll('header button')].find(b => b.innerText.includes('Run optimizer')).click();
    window.__runStart = performance.now();`);
  const started = Date.now();
  let state = null;
  while (Date.now() - started < 30 * 60 * 1000) {
    await sleep(5000);
    state = await evaluate(`return JSON.stringify({ visibility: document.visibilityState, running: [...document.querySelectorAll('header button')].some(b => b.innerText.includes('Cancel run')), title: document.title });`);
    const s = JSON.parse(state);
    console.log(`${((Date.now() - started) / 1000).toFixed(0)} s: ${s.visibility}, ${s.running ? 'running' : 'done'} · ${s.title}`);
    if (!s.running) break;
  }
  const log = await evaluate(`
    const b = [...document.querySelectorAll('button')].find(b => b.innerText.includes('search log')); b?.click();
    await new Promise(r => setTimeout(r, 500));
    return (document.querySelector('.runlog')?.innerText ?? '') + '\\n' + (document.querySelector('.card-hint, .hint')?.innerText ?? '');`);
  console.log(`wall ${((Date.now() - started) / 1000).toFixed(0)} s, hidden: ${hide}`);
  console.log(log);
} finally {
  ws.close();
  edge.kill();
  killStragglers();
}
