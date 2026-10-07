// One lane of the solver pool: its own or-tools-wasm instance, solving directly in this worker (CP-SAT's threads are nested
// workers), so several lanes can solve at the same time; or-tools-wasm's shared executor takes one job at a time.
import { CpSat, setCloudNoticeEnabled } from 'or-tools-wasm/cp-sat';

setCloudNoticeEnabled(false);

self.onmessage = async ({ data: { id, model, parameters } }) => {
  try {
    const { bytes } = await CpSat.solveProto(model, { ...parameters, executor: 'direct' });
    self.postMessage({ id, bytes }, [bytes.buffer]);
  } catch (e) {
    self.postMessage({ id, error: String(e?.stack ?? e) });
  }
};
