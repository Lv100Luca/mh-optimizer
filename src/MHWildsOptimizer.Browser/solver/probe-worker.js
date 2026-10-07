// Checks that a worker can start a worker of its own (bridge.js environment()): CP-SAT's threads are workers started by the
// solver's worker, and a browser that cannot nest workers would wait for them forever.
if (self.name === 'inner') {
  self.postMessage(true);
} else {
  self.onmessage = () => {
    try {
      const inner = new Worker(new URL(import.meta.url), { type: 'module', name: 'inner' });
      inner.onmessage = () => { self.postMessage(true); inner.terminate(); };
      inner.onerror = () => self.postMessage(false);
    } catch {
      self.postMessage(false);
    }
  };
}
