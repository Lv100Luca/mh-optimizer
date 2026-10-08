// Screenshots of every tab of the browser app in headless Edge (CDP): imports a profile folder (with results),
// opens a weapon and captures each tab at full content height, desktop and phone width.
//   node shots.mjs <origin> <profileFolder> <outDir> [weapon] [steps.json]
import { spawn, execSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, readdirSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, join } from 'node:path';

const [origin, profileFolder, outDir, weaponArg, stepsFile] = process.argv.slice(2);
const weapon = weaponArg ?? 'Current';
mkdirSync(outDir, { recursive: true });
const files = Object.fromEntries([
  join(profileFolder, 'profile.json'), join(profileFolder, 'talismans.json'),
  ...readdirSync(join(profileFolder, 'weapons')).filter((f) => f.endsWith('.json')).map((f) => join(profileFolder, 'weapons', f)),
].map((f) => [basename(f), readFileSync(f, 'utf8')]));
const port = 9400 + Math.floor(Math.random() * 400);
// a straggler from an earlier run would still answer on its port with its own state
const killStragglers = () => { try { execSync(`powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \\"Name='msedge.exe'\\" | Where-Object { $_.CommandLine -like '*edge-shots-*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"`); } catch { } };
killStragglers();
const profileDir = mkdtempSync(join(tmpdir(), 'edge-shots-'));
const edge = spawn(process.env.EDGE_PATH ?? 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  ['--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${profileDir}`, '--no-first-run', '--window-size=1600,1000', '--hide-scrollbars', '--force-device-scale-factor=1', 'about:blank'],
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
  if (msg.method === 'Runtime.consoleAPICalled' && msg.params.type === 'error') console.log('[console.error]', msg.params.args.map((a) => a.value ?? a.description).join(' ').slice(0, 400));
});
const send = (method, params = {}) => new Promise((r) => { const id = nextId++; waiting.set(id, r); ws.send(JSON.stringify({ id, method, params })); });
async function evaluate(expression) {
  const r = await send('Runtime.evaluate', { expression: `(async () => { ${expression} })()`, awaitPromise: true, returnByValue: true });
  if (r.result?.exceptionDetails) throw new Error(JSON.stringify(r.result.exceptionDetails).slice(0, 800));
  return r.result?.result?.value;
}
async function navigate(url) { await send('Page.navigate', { url }); await sleep(1500); await waitReady(); }
async function waitReady() {
  await evaluate(`
    const sleep = ms => new Promise(r => setTimeout(r, ms));
    for (let i = 0; i < 120 && !document.querySelector('.app') ; i++) await sleep(250);
    for (let i = 0; i < 40 && document.body.innerText.includes('Validating…'); i++) await sleep(250);
    await sleep(400);`);
}
async function setViewport(width, height, mobile) {
  await send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile, screenWidth: width, screenHeight: height });
  if (mobile) await send('Emulation.setTouchEmulationEnabled', { enabled: true, maxTouchPoints: 5 });
  else await send('Emulation.setTouchEmulationEnabled', { enabled: false });
}
/** Captures the page with the viewport grown to the content's full height (the .content pane is the scroller). */
async function shot(name, width, mobile) {
  await setViewport(width, 900, mobile);
  await sleep(300);
  const height = await evaluate(`
    const zoom = parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--ui-zoom')) || 1;
    const content = document.querySelector('.content');
    const app = document.querySelector('.app');
    if (!content) return Math.min(6000, document.documentElement.scrollHeight);
    const extra = app.getBoundingClientRect().height - content.getBoundingClientRect().height;
    return Math.min(8000, Math.ceil((content.scrollHeight + 2) * zoom + extra + 4));`);
  await setViewport(width, Math.max(600, height), mobile);
  await sleep(500);
  const full = await evaluate(`return Math.min(8000, Math.ceil(document.documentElement.scrollHeight));`);
  const r = await send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: true, clip: { x: 0, y: 0, width, height: Math.max(600, full), scale: 1 } });
  writeFileSync(join(outDir, name + '.png'), Buffer.from(r.result.data, 'base64'));
  console.log('shot', name, width + 'x' + Math.max(600, full));
}

await send('Page.enable');
await send('Runtime.enable');
try {
  await setViewport(1600, 1000, false);
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
  const steps = stepsFile ? JSON.parse(readFileSync(stepsFile, 'utf8')) : null;
  const tabs = ['inventory', 'weapon', 'pair', 'targets', 'limits', 'conditions', 'talismans', 'options', 'review', 'results', 'builds'];
  const w = encodeURIComponent(weapon);
  for (const tab of (process.env.ONLY_EXTRA ? [] : process.env.ONLY_TABS ? process.env.ONLY_TABS.split(',') : tabs)) {
    const url = tab === 'inventory' ? `${origin}/${profile}/inventory` : `${origin}/${profile}/weapon/${w}/${tab}`;
    await setViewport(1600, 1000, false);
    await navigate(url);
    await sleep(800);
    if (steps?.[tab]) { await evaluate(steps[tab]); await sleep(600); }
    await shot(`${tab}-desktop`, 1600, false);
    await shot(`${tab}-phone`, 412, true);
  }
  if (steps?.extra && !process.env.ONLY_TABS) {
    for (const [name, step] of Object.entries(steps.extra)) {
      await setViewport(1600, 1000, false);
      await navigate(step.url.replace('{origin}', origin).replace('{profile}', profile).replace('{weapon}', w));
      await sleep(800);
      await evaluate(step.js);
      await sleep(600);
      await shot(name, step.width ?? 1600, !!step.mobile);
    }
  }
} finally {
  ws.close();
  edge.kill();
  killStragglers();
}
