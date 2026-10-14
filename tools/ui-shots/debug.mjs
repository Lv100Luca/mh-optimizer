// Loads the app in headless Edge, imports a profile, opens a URL and reports what happens, with a timeout on every step.
//   node debug.mjs <origin> <profileFolder> <path> [jsAfter] [shot.png]
import { spawn, execSync } from 'node:child_process';
import { mkdtempSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, join } from 'node:path';

const [origin, profileFolder, path, jsAfter, shotFile] = process.argv.slice(2);
const files = Object.fromEntries([
  join(profileFolder, 'profile.json'), join(profileFolder, 'talismans.json'),
  ...readdirSync(join(profileFolder, 'weapons')).filter((f) => f.endsWith('.json')).map((f) => join(profileFolder, 'weapons', f)),
].map((f) => [basename(f), readFileSync(f, 'utf8')]));
const port = 9300 + Math.floor(Math.random() * 90);
const profileDir = mkdtempSync(join(tmpdir(), 'edge-dbg-'));
const edge = spawn('C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  ['--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${profileDir}`, '--no-first-run', '--window-size=1600,1000', '--force-device-scale-factor=1', 'about:blank'], { stdio: 'ignore' });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const t0 = Date.now();
const log = (...a) => console.log(`[${((Date.now() - t0) / 1000).toFixed(1)}s]`, ...a);
let targets;
for (let i = 0; i < 50; i++) { try { targets = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json(); break; } catch { await sleep(200); } }
const ws = new WebSocket(targets.find((t) => t.type === 'page').webSocketDebuggerUrl);
await new Promise((r) => ws.addEventListener('open', r, { once: true }));
let id = 1; const waiting = new Map();
ws.addEventListener('message', ({ data }) => {
  const m = JSON.parse(data);
  if (m.id && waiting.has(m.id)) { waiting.get(m.id)(m); waiting.delete(m.id); }
  if (m.method === 'Runtime.exceptionThrown') log('[exception]', JSON.stringify(m.params.exceptionDetails).slice(0, 600));
  if (m.method === 'Runtime.consoleAPICalled' && ['error', 'warning', 'log'].includes(m.params.type)) {
    const text = m.params.args.map((a) => a.value ?? a.description).join(' ');
    if (!text.includes('Vue Devtools')) log(`[console.${m.params.type}]`, text.slice(0, 1500));
  }
});
const send = (method, params = {}, ms = 20000) => Promise.race([
  new Promise((r) => { const i = id++; waiting.set(i, r); ws.send(JSON.stringify({ id: i, method, params })); }),
  sleep(ms).then(() => ({ timeout: true, method })),
]);
async function evaluate(expression, ms = 30000) {
  const r = await send('Runtime.evaluate', { expression: `(async () => { ${expression} })()`, awaitPromise: true, returnByValue: true }, ms);
  if (r.timeout) { log('TIMEOUT evaluating', expression.slice(0, 80)); return undefined; }
  if (r.result?.exceptionDetails) log('eval error', JSON.stringify(r.result.exceptionDetails).slice(0, 500));
  return r.result?.result?.value;
}
try {
  await send('Page.enable'); await send('Runtime.enable');
  await send('Emulation.setDeviceMetricsOverride', { width: 1600, height: 1000, deviceScaleFactor: 1, mobile: false });
  await send('Page.navigate', { url: origin + '/' });
  await sleep(1500);
  log('boot', await evaluate(`const sleep = ms => new Promise(r => setTimeout(r, ms)); for (let i = 0; i < 160 && ![...document.querySelectorAll('button')].some(b => (b.getAttribute('aria-label') || '').includes('Profile actions')); i++) await sleep(250); return document.title;`, 60000));
  log('import', await evaluate(`
    const sleep = ms => new Promise(r => setTimeout(r, ms));
    [...document.querySelectorAll('button')].find(b => (b.getAttribute('aria-label') || '').includes('Profile actions')).click(); await sleep(500);
    [...document.querySelectorAll('.mud-menu-item, .mud-list-item, [role=menuitem]')].find(e => e.textContent.trim().startsWith('Import')).click(); await sleep(800);
    const files = ${JSON.stringify(files)};
    const dt = new DataTransfer();
    for (const [n, t] of Object.entries(files)) dt.items.add(new File([t], n, { type: 'application/json' }));
    const input = document.querySelector('.mud-dialog input[type=file]');
    input.files = dt.files; input.dispatchEvent(new Event('change', { bubbles: true }));
    await sleep(2500);
    [...document.querySelectorAll('.mud-dialog button')].find(b => b.innerText.trim() === 'Import').click();
    await sleep(3000);
    return location.pathname + ' | ' + document.querySelector('.content')?.innerText.slice(0, 200).replace(/\\s+/g, ' ');`, 60000));
  log('navigate', path);
  await send('Page.navigate', { url: origin + '/' + path });
  for (let i = 0; i < 12; i++) {
    await sleep(2500);
    const state = await evaluate(`return location.pathname + ' | ' + (document.querySelector('#blazor-error-ui')?.style.display || '') + ' | ' + (document.querySelector('.content')?.innerText.slice(0, 300).replace(/\\s+/g, ' ') ?? 'no content');`, 8000);
    log('state', state);
    if (state && !state.includes('Loading')) break;
  }
  if (jsAfter) log('after', await evaluate(jsAfter, 60000));
  if (shotFile) {
    const r = await send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: true }, 20000);
    if (r.result?.data) { writeFileSync(shotFile, Buffer.from(r.result.data, 'base64')); log('shot', shotFile); } else log('no shot', JSON.stringify(r).slice(0, 200));
  }
} finally {
  ws.close(); edge.kill();
  try { execSync(`powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \\"Name='msedge.exe' OR Name='chrome.exe'\\" | Where-Object { $_.CommandLine -like '*edge-dbg-*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"`); } catch { }
}
