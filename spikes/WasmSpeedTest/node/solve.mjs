// Solves the .pb models saved by `Native dump` with or-tools-wasm, to compare against the native solver on the same bytes.
// node solve.mjs <dir> [workers=16] [timeLimitSeconds=120] [filter]
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { CpSat, CpSolverStatus, setCloudNoticeEnabled } from 'or-tools-wasm/cp-sat';

setCloudNoticeEnabled(false);
const [dir, workers = '16', limit = '120', filter = ''] = process.argv.slice(2);
const files = readdirSync(dir).filter(f => f.endsWith('.pb') && f.includes(filter)).sort();

// first call loads and compiles the WebAssembly runtime; time it separately
let t = performance.now();
await CpSat.validate(readFileSync(join(dir, files[0])));
console.log(`runtime startup ${((performance.now() - t) / 1000).toFixed(2)}s`);

for (const file of files) {
  const bytes = readFileSync(join(dir, file));
  t = performance.now();
  const { response } = await CpSat.solveProto(bytes, { numWorkers: Number(workers), maxTimeInSeconds: Number(limit) });
  const seconds = (performance.now() - t) / 1000;
  console.log(`${file}  wasm: ${seconds.toFixed(2).padStart(6)}s  ${CpSolverStatus[response?.status] ?? response?.status}  objective ${response?.objectiveValue}  (solver wall ${response?.wallTime?.toFixed(2)}s)`);
}
