import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import { federation } from '@module-federation/vite';
import { resolve } from 'path';

// System Operations is a Module Federation HOST. It declares which deps it
// will share with plugin remotes (vue today; add more here as plugin authors
// need them) but exposes nothing itself — remotes are registered dynamically
// at runtime via @module-federation/runtime once the manifest endpoint
// (/api/apps/system-operations/manifest) tells us which plugins to load.
export default defineConfig({
  root: resolve(__dirname, 'app'),
  plugins: [
    vue(),
    federation({
      name: 'system-operations-host',
      // No `exposes`: this is a host, not a remote.
      shared: {
        vue: { singleton: true, requiredVersion: '^3.5.0' },
      },
      // Disable .d.ts generation. The plugin's DTS step looks for
      // `tsconfig.json` relative to Vite's `root` (`app/`) and fails when
      // tsconfig lives one level up. We don't ship federated types — the
      // sample plugin's host-types.ts is inlined — so DTS is pure overhead.
      dts: false,
    }),
  ],
  base: '/apps/system-operations/',
  build: {
    target: 'esnext',
    outDir: resolve(__dirname, 'Content/system-operations'),
    emptyOutDir: true,
  },
  server: {
    proxy: {
      '/api': 'http://localhost:5001',
      '/images': 'http://localhost:5001',
      '/modules': 'http://localhost:5001',
    },
  },
});
