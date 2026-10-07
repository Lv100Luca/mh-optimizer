// CP-SAT for the Blazor app (CpSatBridge.cs): a serialized CpModelProto in, a serialized CpSolverResponse out.
// Lane 0 solves on or-tools-wasm's own worker; it takes one job at a time, so further lanes (skill pair classes solved side
// by side) get dedicated workers with their own solver instance (pool-worker.js), started on first use.
import { CpSat, setCloudNoticeEnabled } from 'or-tools-wasm/cp-sat';

setCloudNoticeEnabled(false);

const running = new Map(); // job -> cancel()
const lanes = [];
const pending = new Map(); // pool request id -> { resolve, reject }
let nextId = 0;

function lane(index) {
  if (!lanes[index]) {
    const worker = new Worker(new URL('./pool-worker.js', import.meta.url), { type: 'module' });
    worker.onmessage = ({ data: { id, bytes, error, cancelled } }) => {
      const p = pending.get(id);
      if (!p) return;
      pending.delete(id);
      if (cancelled) p.reject(new DOMException('The solve was cancelled.', 'AbortError'));
      else if (error) p.reject(new Error(error));
      else p.resolve(bytes);
    };
    lanes[index] = worker;
  }
  return lanes[index];
}

/**
 * @param model serialized CpModelProto
 * @param parameters SatParameters fields in camelCase: { numWorkers, maxTimeInSeconds, subsolvers?, linearizationLevel? }
 * @param laneIndex 0 = or-tools-wasm's worker, 1.. = pool workers
 * @param job an id cancel() can stop
 * @returns serialized CpSolverResponse
 */
export async function solve(model, parameters, laneIndex, job) {
  if (laneIndex === 0) {
    const controller = new AbortController();
    running.set(job, () => controller.abort());
    try {
      const { bytes } = await CpSat.solveProto(model, { ...parameters, signal: controller.signal });
      return bytes;
    } finally {
      running.delete(job);
    }
  }
  const id = nextId++;
  const worker = lane(laneIndex);
  running.set(job, () => worker.postMessage({ cancel: id }));
  try {
    return await new Promise((resolve, reject) => {
      pending.set(id, { resolve, reject });
      worker.postMessage({ id, model, parameters }, [model.buffer]);
    });
  } finally {
    running.delete(job);
  }
}

export function cancel(job) {
  running.get(job)?.();
}

/** Loads the WebAssembly runtime and its worker ahead of the first solve. */
export async function warmUp() {
  await CpSat.validate(new Uint8Array(0));
}

export function environment() {
  return {
    crossOriginIsolated: !!globalThis.crossOriginIsolated,
    cores: navigator.hardwareConcurrency || 4,
    jspi: typeof WebAssembly.promising === 'function',
  };
}
