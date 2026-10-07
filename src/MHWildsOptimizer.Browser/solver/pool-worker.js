// One lane of the solver pool (see bridge.js): its own or-tools-wasm instance, solving directly in this worker; CP-SAT's
// threads are nested workers.
import { CpSat, setCloudNoticeEnabled } from 'or-tools-wasm/cp-sat';

setCloudNoticeEnabled(false);

const controllers = new Map();

self.onmessage = async ({ data }) => {
  if (data.cancel !== undefined) {
    controllers.get(data.cancel)?.abort();
    return;
  }
  const { id, model, parameters } = data;
  const controller = new AbortController();
  controllers.set(id, controller);
  try {
    const { bytes } = await CpSat.solveProto(model, { ...parameters, executor: 'direct', signal: controller.signal });
    self.postMessage({ id, bytes }, [bytes.buffer]);
  } catch (e) {
    if (controller.signal.aborted) self.postMessage({ id, cancelled: true });
    else self.postMessage({ id, error: String(e?.stack ?? e) });
  } finally {
    controllers.delete(id);
  }
};
