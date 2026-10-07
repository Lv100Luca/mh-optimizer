// CP-SAT for the Blazor app (CpSatBridge.cs): a serialized CpModelProto in, a serialized CpSolverResponse out.
// Lane 0 solves on or-tools-wasm's own worker; it takes one job at a time, so further lanes (skill pair classes solved side
// by side) get dedicated workers with their own solver instance (pool-worker.js), started on first use. Jobs on one lane
// (warm-up included) run one after another: a solver that is still busy refuses the next job.
import { CpSat, setCloudNoticeEnabled } from 'or-tools-wasm/cp-sat';

setCloudNoticeEnabled(false);

const running = new Map(); // job -> cancel()
const lanes = [];
const queues = []; // lane -> its last job, settled
const pending = new Map(); // pool request id -> { resolve, reject }
let nextId = 0;

function lane(index) {
  if (!lanes[index]) {
    const worker = new Worker(new URL('./pool-worker.js', import.meta.url), { type: 'module' });
    worker.onmessage = ({ data: { id, bytes, error, cancelled } }) => {
      const p = pending.get(id);
      if (!p) return;
      pending.delete(id);
      if (cancelled) p.reject(cancelledError());
      else if (error) p.reject(new Error(error));
      else p.resolve(bytes);
    };
    lanes[index] = worker;
  }
  return lanes[index];
}

/** Runs `work` on the lane once the lane's earlier jobs are done. */
function enqueue(laneIndex, work) {
  const run = (queues[laneIndex] ?? Promise.resolve()).then(work);
  queues[laneIndex] = run.catch(() => {});
  return run;
}

const cancelledError = () => new DOMException('The solve was cancelled.', 'AbortError');

/** Sends a message to a pool lane and waits for its answer. */
function post(laneIndex, message, transfer = []) {
  const id = nextId++;
  return { id, done: new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject });
    lane(laneIndex).postMessage({ id, ...message }, transfer);
  }) };
}

/**
 * @param model serialized CpModelProto
 * @param parameters SatParameters fields in camelCase: { numWorkers, maxTimeInSeconds, subsolvers?, linearizationLevel? }
 * @param laneIndex 0 = or-tools-wasm's worker, 1.. = pool workers
 * @param job an id cancel() can stop, also while it waits for its lane
 * @returns serialized CpSolverResponse
 */
export function solve(model, parameters, laneIndex, job) {
  let cancelled = false;
  running.set(job, () => { cancelled = true; });
  return enqueue(laneIndex, async () => {
    if (cancelled) throw cancelledError();
    if (laneIndex === 0) {
      const controller = new AbortController();
      running.set(job, () => controller.abort());
      const { bytes } = await CpSat.solveProto(model, { ...parameters, signal: controller.signal });
      return bytes;
    }
    const { id, done } = post(laneIndex, { model, parameters }, [model.buffer]);
    running.set(job, () => lane(laneIndex).postMessage({ cancel: id }));
    return await done;
  }).finally(() => running.delete(job));
}

export function cancel(job) {
  running.get(job)?.();
}

/** Loads the WebAssembly runtime and its worker ahead of the first solve. */
export function warmUp() {
  return enqueue(0, () => CpSat.validate(new Uint8Array(0)));
}

/** Starts lanes 1..count-1 ahead of an optimize-mode run (each loads its own runtime, a few seconds). */
export function warmUpLanes(count) {
  return Promise.all(Array.from({ length: Math.max(0, count - 1) }, (_, i) =>
    enqueue(i + 1, () => post(i + 1, { warm: true }).done.catch(() => {}))));
}

export async function environment() {
  return {
    crossOriginIsolated: !!globalThis.crossOriginIsolated,
    cores: navigator.hardwareConcurrency || 4,
    jspi: typeof WebAssembly.promising === 'function',
    nestedWorkers: await nestedWorkers(),
  };
}

/** Whether a worker can start workers (probe-worker.js); false after 10 s without an answer. */
function nestedWorkers() {
  return new Promise((resolve) => {
    let worker;
    const done = (ok) => { clearTimeout(timer); worker?.terminate(); resolve(ok); };
    const timer = setTimeout(() => done(false), 10_000);
    try {
      worker = new Worker(new URL('./probe-worker.js', import.meta.url), { type: 'module' });
      worker.onmessage = ({ data }) => done(data === true);
      worker.onerror = () => done(false);
      worker.postMessage(null);
    } catch {
      done(false);
    }
  });
}
