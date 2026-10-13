// Screenshots of wiki pages in headless Edge (CDP): imports a profile folder so the app has a profile, then opens each page
// (<profile>/wiki/<section>/<entry>) and captures it at full content height, desktop and phone width.
//   node wiki-shots.mjs <origin> <profileFolder> <outDir> skills/Counterstrike "bonuses/Doshaguma's Might" armor/...
// The page target can change after a navigation (the cross-origin-isolation service worker reloads the page), so every
// step reconnects to the current page target.
import { spawn, execSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, join } from 'node:path';

const [origin, profileFolder, outDir, ...pages] = process.argv.slice(2);
mkdirSync(outDir, { recursive: true });
const files = Object.fromEntries([
  join(profileFolder, 'profile.json'), join(profileFolder, 'talismans.json'),
  ...readdirSync(join(profileFolder, 'weapons')).filter((f) => f.endsWith('.json')).map((f) => join(profileFolder, 'weapons', f)),
].map((f) => [basename(f), readFileSync(f, 'utf8')]));
const port = 9400 + Math.floor(Math.random() * 400);
const killStragglers = () => { try { execSync(`powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \\"Name='msedge.exe'\\" | Where-Object { $_.CommandLine -like '*edge-shots-*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"`); } catch { } };
killStragglers();
const profileDir = mkdtempSync(join(tmpdir(), 'edge-shots-'));
const edge = spawn(process.env.EDGE_PATH ?? 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  ['--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${profileDir}`, '--no-first-run', '--window-size=1600,1000', '--hide-scrollbars', '--force-device-scale-factor=1', 'about:blank'],
  { stdio: 'ignore' });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

let ws, nextId = 1;
const waiting = new Map();
async function connect() {
  ws?.close();
  let targets;
  for (let i = 0; i < 50; i++) {
    try { targets = (await (await fetch(`http://127.0.0.1:${port}/json/list`)).json()).filter((t) => t.type === 'page'); if (targets.length) break; } catch { }
    await sleep(200);
  }
  // Edge can open its own pages next to ours (edge://sync-confirmation-dialog); take the app's
  const target = targets.find((t) => t.url.startsWith(origin)) ?? targets.find((t) => !t.url.startsWith('edge://')) ?? targets[0];
  ws = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((r) => ws.addEventListener('open', r, { once: true }));
  ws.addEventListener('message', ({ data }) => {
    const msg = JSON.parse(data);
    if (msg.id && waiting.has(msg.id)) { waiting.get(msg.id)(msg); waiting.delete(msg.id); }
    if (msg.method === 'Runtime.exceptionThrown') console.log('[exception]', JSON.stringify(msg.params.exceptionDetails).slice(0, 400));
  });
  await send('Page.enable');
  await send('Runtime.enable');
}
const send = (method, params = {}) => new Promise((r) => { const id = nextId++; waiting.set(id, r); ws.send(JSON.stringify({ id, method, params })); });
async function evaluate(expression) {
  const r = await send('Runtime.evaluate', { expression: `(async () => { ${expression} })()`, awaitPromise: true, returnByValue: true });
  if (r.result?.exceptionDetails) throw new Error(JSON.stringify(r.result.exceptionDetails).slice(0, 800));
  return r.result?.result?.value;
}
async function navigate(url, ready) {
  await send('Page.navigate', { url });
  await sleep(2500);
  await connect();
  await evaluate(`
    const sleep = ms => new Promise(r => setTimeout(r, ms));
    for (let i = 0; i < 160 && !document.querySelector(${JSON.stringify(ready)}); i++) await sleep(250);
    await sleep(800);`);
}
async function setViewport(width, height, mobile) {
  await send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile, screenWidth: width, screenHeight: height });
  await send('Emulation.setTouchEmulationEnabled', mobile ? { enabled: true, maxTouchPoints: 5 } : { enabled: false });
}
async function shot(name, width, mobile) {
  await setViewport(width, 900, mobile);
  await sleep(400);
  const height = await evaluate(`
    const zoom = parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--ui-zoom')) || 1;
    const content = document.querySelector('.content');
    const app = document.querySelector('.app');
    if (!content) return Math.min(6000, document.documentElement.scrollHeight);
    const extra = app.getBoundingClientRect().height - content.getBoundingClientRect().height;
    return Math.min(8000, Math.ceil((content.scrollHeight + 2) * zoom + extra + 4));`);
  await setViewport(width, Math.max(600, height), mobile);
  await sleep(600);
  const full = await evaluate(`return Math.min(8000, Math.ceil(document.documentElement.scrollHeight));`);
  const r = await send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: true, clip: { x: 0, y: 0, width, height: Math.max(600, full), scale: 1 } });
  writeFileSync(join(outDir, name + '.png'), Buffer.from(r.result.data, 'base64'));
  console.log('shot', name, width + 'x' + Math.max(600, full));
}

try {
  await connect();
  await setViewport(1600, 1000, false);
  await navigate(origin + '/', '.app');
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
  for (const page of pages) {
    const [section, ...rest] = page.split('/');
    const name = (section + '-' + rest.join('-')).replace(/[^A-Za-z0-9-]+/g, '_');
    await setViewport(1600, 1000, false);
    await navigate(`${origin}/${profile}/wiki/${section}/${encodeURIComponent(rest.join('/'))}`, '.wiki-page');
    await shot(`${name}-desktop`, 1600, false);
    await shot(`${name}-phone`, 412, true);
  }
} finally {
  ws?.close();
  edge.kill();
  killStragglers();
}
