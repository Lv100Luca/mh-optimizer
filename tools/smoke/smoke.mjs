// Browser smoke test of the published site in headless Chrome (CDP, Node 22, no packages): the app boots cross-origin
// isolated, the static wiki pages and the in-app wiki render, and the example weapon (inputs/request.example.json with
// inputs/talismans.example.json, imported like a profile folder) runs to results. Script errors on the page fail it too.
//   node tools/smoke/smoke.mjs <origin> [--no-run]
// CHROME_PATH picks the browser (default: google-chrome on Linux, Chrome on Windows); RUN_TIMEOUT is the run's limit in
// seconds (default 300). Prints one line per check and exits with 1 when any failed. CI runs it on the publish output
// before deploying and on the live site after (.github/workflows/deploy.yml).
import { spawn, execSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const args = process.argv.slice(2);
const origin = args.find((a) => !a.startsWith('--'))?.replace(/\/$/, '');
if (!origin) { console.error('usage: node tools/smoke/smoke.mjs <origin> [--no-run]'); process.exit(2); }
const doRun = !args.includes('--no-run');
const runTimeout = Number(process.env.RUN_TIMEOUT ?? 300);
const repo = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const files = {
  'talismans.json': readFileSync(join(repo, 'inputs', 'talismans.example.json'), 'utf8'),
  'smoke.json': readFileSync(join(repo, 'inputs', 'request.example.json'), 'utf8'),
};

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const failures = [];
function check(name, ok, detail = '') {
  console.log(`${ok ? 'ok  ' : 'FAIL'} ${name}${detail ? ': ' + detail : ''}`);
  if (!ok) failures.push(name);
}

// ---------------------------------------------------------------- browser

const tag = 'mhwo-smoke-';
const killStragglers = () => {
  if (process.platform !== 'win32') return;
  try { execSync(`powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \\"Name='chrome.exe' OR Name='msedge.exe'\\" | Where-Object { $_.CommandLine -like '*${tag}*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"`); } catch { }
};
const chromePath = process.env.CHROME_PATH
  ?? (process.platform === 'win32' ? 'C:/Program Files/Google/Chrome/Application/chrome.exe' : 'google-chrome');
const port = 9400 + Math.floor(Math.random() * 400);
const profileDir = mkdtempSync(join(tmpdir(), tag));
const chrome = spawn(chromePath, [
  '--headless=new', `--remote-debugging-port=${port}`, `--user-data-dir=${profileDir}`, '--no-first-run',
  '--no-default-browser-check', '--window-size=1600,1000', '--window-position=-32000,-32000',
  // CI runners (Ubuntu 24.04) do not allow Chrome's sandbox
  ...(process.env.CI ? ['--no-sandbox'] : []),
  'about:blank'], { stdio: 'ignore' });

function cleanup() {
  ws?.close();
  chrome.kill();
  killStragglers();
  try { rmSync(profileDir, { recursive: true, force: true }); } catch { }
}
const watchdog = setTimeout(() => { console.log('FAIL the smoke test as a whole timed out'); cleanup(); process.exit(1); },
  (runTimeout + 300) * 1000);

// script errors on the page, from every connection (a navigation can swap the page target)
const pageErrors = [];
let ws, nextId = 1;
const waiting = new Map();
async function connect() {
  ws?.close();
  let targets = [];
  for (let i = 0; i < 100 && !targets.length; i++) {
    try { targets = (await (await fetch(`http://127.0.0.1:${port}/json/list`)).json()).filter((t) => t.type === 'page'); } catch { }
    if (!targets.length) await sleep(200);
  }
  if (!targets.length) throw new Error(`no browser page on port ${port} (CHROME_PATH=${chromePath})`);
  const target = targets.find((t) => t.url.startsWith(origin)) ?? targets[0];
  ws = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((r, j) => { ws.addEventListener('open', r, { once: true }); ws.addEventListener('error', j, { once: true }); });
  ws.addEventListener('message', ({ data }) => {
    const msg = JSON.parse(data);
    if (msg.id && waiting.has(msg.id)) { waiting.get(msg.id)(msg); waiting.delete(msg.id); }
    if (msg.method === 'Runtime.exceptionThrown') {
      const d = msg.params.exceptionDetails;
      pageErrors.push('exception: ' + (d.exception?.description ?? d.text));
    }
    if (msg.method === 'Runtime.consoleAPICalled' && msg.params.type === 'error')
      pageErrors.push('console.error: ' + msg.params.args.map((a) => a.value ?? a.description).join(' '));
  });
  await send('Page.enable');
  await send('Runtime.enable');
  await send('Emulation.setFocusEmulationEnabled', { enabled: true });
  await send('Page.bringToFront');
}
const send = (method, params = {}) => new Promise((r) => { const id = nextId++; waiting.set(id, r); ws.send(JSON.stringify({ id, method, params })); });
async function evaluate(expression) {
  const r = await send('Runtime.evaluate', { expression: `(async () => { const sleep = ms => new Promise(r => setTimeout(r, ms)); ${expression} })()`, awaitPromise: true, returnByValue: true });
  if (r.result?.exceptionDetails) throw new Error(JSON.stringify(r.result.exceptionDetails).slice(0, 800));
  return r.result?.result?.value;
}
/** Opens a URL and waits up to `seconds` for the selector; true when it showed up. */
async function navigate(url, selector, seconds = 60) {
  await send('Page.navigate', { url });
  await sleep(2000);
  await connect();
  return await evaluate(`
    for (let i = 0; i < ${seconds * 4} && !document.querySelector(${JSON.stringify(selector)}); i++) await sleep(250);
    return !!document.querySelector(${JSON.stringify(selector)});`);
}

// ---------------------------------------------------------------- checks

try {
  // plain HTTP: the static pages are what search engines get. Each must be its own page: a server that answers a folder URL
  // with the app's index.html also says 200
  for (const [path, needle] of [
    ['/wiki/', '<title>Monster Hunter Wilds wiki'], ['/wiki/skills/agitator/', '<title>Agitator'],
    ['/wiki/items/materials/', '<title>Materials'], ['/sitemap.xml', '<urlset'],
  ]) {
    let status = 0, body = '';
    try { const r = await fetch(origin + path); status = r.status; body = await r.text(); } catch (e) { body = String(e); }
    const title = body.match(/<title>[^<]*<\/title>/)?.[0] ?? body.slice(0, 120);
    check(`static page ${path}`, status === 200 && body.includes(needle), status === 200 && body.includes(needle) ? '' : `HTTP ${status}, got ${title}`);
  }

  await connect();
  const t0 = Date.now();
  const booted = await navigate(origin + '/', '.app', 90);
  check('app boots', booted, booted ? `${((Date.now() - t0) / 1000).toFixed(1)} s` : 'no .app after 90 s: ' + (await evaluate('return document.body.innerText.slice(0, 200);')));
  if (!booted) throw new Error('the app did not boot');
  check('cross-origin isolated (WebAssembly threads)', await evaluate('return self.crossOriginIsolated === true;'));

  // the example weapon, imported like the files of a profile folder (into the current profile, Default)
  const imported = await evaluate(`
    for (let i = 0; i < 120 && ![...document.querySelectorAll('button')].some(b => (b.getAttribute('aria-label') || '').includes('Profile actions')); i++) await sleep(250);
    [...document.querySelectorAll('button')].find(b => (b.getAttribute('aria-label') || '').includes('Profile actions')).click(); await sleep(500);
    [...document.querySelectorAll('.mud-menu-item, .mud-list-item, [role=menuitem]')].find(e => e.textContent.trim().startsWith('Import')).click();
    for (let i = 0; i < 40 && !document.querySelector('.mud-dialog input[type=file]'); i++) await sleep(250);
    const files = ${JSON.stringify(files)};
    const dt = new DataTransfer();
    for (const [n, t] of Object.entries(files)) dt.items.add(new File([t], n, { type: 'application/json' }));
    const input = document.querySelector('.mud-dialog input[type=file]');
    input.files = dt.files; input.dispatchEvent(new Event('change', { bubbles: true }));
    const importButton = () => [...document.querySelectorAll('.mud-dialog button')].find(b => b.innerText.trim() === 'Import' && !b.disabled);
    for (let i = 0; i < 40 && !importButton(); i++) await sleep(250);
    const summary = document.querySelector('.import-summary')?.innerText ?? '';
    if (!importButton()) return 'no Import button: ' + summary;
    importButton().click();
    for (let i = 0; i < 40 && document.querySelector('.mud-dialog'); i++) await sleep(250);
    return summary.replace(/\\s+/g, ' ').trim();`);
  check('import the example weapon', /1 weapon/.test(imported ?? ''), imported);

  const wikiOk = await navigate(`${origin}/Default/wiki/skills/Agitator`, '.wiki-page', 30);
  check('in-app wiki page', wikiOk && await evaluate(`return document.querySelector('.wiki-page').innerText.includes('Agitator');`));

  if (doRun) {
    const opened = await navigate(`${origin}/Default/weapon/smoke/results`, '.configrun', 60);
    // the button is enabled once the request validated and the solver loaded
    const ready = opened && await evaluate(`
      for (let i = 0; i < 240 && document.querySelector('.configrun')?.disabled !== false; i++) await sleep(250);
      return document.querySelector('.configrun')?.disabled === false;`);
    check('Run optimizer is enabled', ready, ready ? '' : await evaluate(`return document.querySelector('.configrun')?.title ?? 'no run button';`));
    if (ready) {
      const run = JSON.parse(await evaluate(`
        const t0 = performance.now();
        document.querySelector('.configrun').click();
        const running = () => !!document.querySelector('.progress') || document.querySelector('.configrun')?.innerText.includes('Cancel');
        for (let i = 0; i < 80 && !running(); i++) await sleep(250);
        const started = running();
        while (running() && performance.now() - t0 < ${runTimeout * 1000}) await sleep(500);
        // result cards come in one per turn of the page after the run ends
        for (let i = 0; i < 40 && !running() && !document.querySelector('.result-row, article.build'); i++) await sleep(250);
        const error = [...document.querySelectorAll('.alert-box')].map(a => a.innerText.trim()).filter(Boolean).join(' | ');
        return JSON.stringify({ started, finished: !running(), seconds: +((performance.now() - t0) / 1000).toFixed(1),
          rows: document.querySelectorAll('.result-row, article.build').length, error,
          first: document.querySelector('.result-row, article.build .build-head')?.innerText.replace(/\\s+/g, ' ').slice(0, 160) ?? '',
          panel: document.querySelector('.content')?.innerText.replace(/\\s+/g, ' ').slice(0, 600) ?? '' });`));
      check('optimizer run', run.started && run.finished && run.rows > 0 && !run.error,
        `${run.seconds} s, ${run.rows} builds${run.error ? ', alert: ' + run.error : ''}${run.started ? '' : ', never started'}${run.finished ? '' : `, still running after ${runTimeout} s`}`);
      if (run.first) console.log(`     best: ${run.first}`);
      else console.log(`     page: ${run.panel}`);
    }
  }
} catch (e) {
  check('smoke test', false, e.message);
} finally {
  check('no script errors on the page', pageErrors.length === 0, pageErrors.slice(0, 5).map((e) => e.slice(0, 300)).join(' || '));
  clearTimeout(watchdog);
  cleanup();
}
console.log(failures.length ? `\n${failures.length} check(s) failed: ${failures.join(', ')}` : '\nall checks passed');
process.exit(failures.length ? 1 : 0);
