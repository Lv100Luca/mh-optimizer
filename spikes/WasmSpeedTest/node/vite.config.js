// Bundles bridge.js with or-tools-wasm (worker + WebAssembly runtime) into the Blazor app's wwwroot/ortools.
import { defineConfig } from 'vite';

// or-tools-wasm references every solver's runtime; only CP-SAT is used, so the others are dropped from the output
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
    outDir: '../Browser/wwwroot/ortools',
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
