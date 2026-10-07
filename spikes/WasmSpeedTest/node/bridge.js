// The browser side of CP-SAT for the Blazor spike: serialized CpModelProto in, serialized CpSolverResponse out.
import { CpSat, setCloudNoticeEnabled } from 'or-tools-wasm/cp-sat';

setCloudNoticeEnabled(false);

const running = new Map();

/**
 * @param parameters SatParameters fields in camelCase, e.g. { numWorkers: 8, maxTimeInSeconds: 120, subsolvers: ['max_lp'] }
 * @param job an id that cancel() can stop
 */
export async function solveProto(model, parameters, job) {
  const controller = new AbortController();
  running.set(job, controller);
  try {
    const { bytes } = await CpSat.solveProto(model, { ...parameters, signal: controller.signal });
    return bytes;
  } finally {
    running.delete(job);
  }
}

export function cancel(job) {
  running.get(job)?.abort();
}

// lanes: dedicated workers with their own solver instance, for solving several models at once (no cancellation here)
const lanes = [];
const pending = new Map();
let nextId = 0;

function lane(index) {
  if (!lanes[index]) {
    const worker = new Worker(new URL('./pool-worker.js', import.meta.url), { type: 'module' });
    worker.onmessage = ({ data: { id, bytes, error } }) => {
      const { resolve, reject } = pending.get(id);
      pending.delete(id);
      error ? reject(new Error(error)) : resolve(bytes);
    };
    lanes[index] = worker;
  }
  return lanes[index];
}

export function solveOnLane(model, parameters, index) {
  const id = nextId++;
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject });
    lane(index).postMessage({ id, model, parameters });
  });
}

// loads the WebAssembly runtime and its worker, so the first solve's time is the solve alone
export async function warmUp() {
  await CpSat.validate(new Uint8Array(0));
}

export function environment() {
  return {
    crossOriginIsolated: globalThis.crossOriginIsolated,
    cores: navigator.hardwareConcurrency,
    jspi: typeof WebAssembly.promising === 'function',
  };
}
