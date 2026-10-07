// Bundles bridge.js with or-tools-wasm (its worker and WebAssembly runtime) into ../wwwroot/solver.
import { defineConfig } from 'vite';

// or-tools-wasm references the runtime of every solver it offers; only CP-SAT is used
const onlyCpSat = {
  name: 'only-cp-sat',
  generateBundle(_, bundle) {
    for (const name of Object.keys(bundle))
      if (/_runtime/.test(name) && !/cp_sat_runtime/.test(name)) delete bundle[name];
  },
};

export default defineConfig({
  base: './',
  plugins: [onlyCpSat],
  worker: { format: 'es' },
  build: {
    outDir: '../wwwroot/solver',
    emptyOutDir: true,
    target: 'esnext',
    assetsInlineLimit: 0,
    rollupOptions: {
      input: 'bridge.js',
      preserveEntrySignatures: 'strict',
      output: { entryFileNames: 'bridge.js' },
    },
  },
});
