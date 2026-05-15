import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import { federation } from '@module-federation/vite';
import { resolve } from 'node:path';

// MF remote config for the sample extension.
//
// `outDir` writes the build output directly to the discovery folder the
// platform serves: `{moduleRoot}/plugins/system-operations/`. With this
// in place no copy step is needed — `npm run build` produces a publishable
// `plugins/system-operations/` ready for the platform to discover.
//
// The `name` field MUST match what the host loader uses when calling
// `mf.loadRemote('<name>/Module')`. The platform's manifest endpoint
// defaults `remote.name` to the .NET module id, so we set the same here.
//
// The exposed module — './Module' — must default-export an object
// implementing { install(host, ctx) }. See src/index.ts.

export default defineConfig({
  plugins: [
    vue(),
    federation({
      name: 'VirtoCommerce.SystemOperations.SampleExtension',
      filename: 'remoteEntry.js',
      exposes: {
        './Module': './src/index.ts',
      },
      shared: {
        vue: { singleton: true, requiredVersion: '^3.5.0' },
      },
      // Disable .d.ts emission. The host inlines its own copy of the plugin
      // contract (`src/host-types.ts`) so consumers never import federated
      // types from this remote.
      dts: false,
    }),
  ],
  build: {
    target: 'esnext',
    outDir: resolve(__dirname, 'plugins/system-operations'),
    emptyOutDir: true,
    minify: 'esbuild',
    rollupOptions: {
      // Pure MF remote: no HTML entry. Pointing Rollup directly at the
      // TypeScript source bypasses Vite's default `index.html` lookup
      // (which is the cause of "Could not resolve entry module 'index.html'"
      // when there is no app shell). The `@module-federation/vite` plugin
      // still injects its own virtual entries on top, producing the
      // canonical `remoteEntry.js`.
      input: resolve(__dirname, 'src/index.ts'),
      output: {
        // MF remotes must produce ES modules so the host can `import()` them.
        format: 'esm',
        // Keep entry filenames literal — in particular, the federation
        // plugin's `filename: 'remoteEntry.js'` must land as `remoteEntry.js`
        // (no hash) because the platform's manifest endpoint synthesizes
        // that exact path. Chunks and assets stay hashed for cache busting.
        entryFileNames: '[name].js',
        chunkFileNames: 'assets/[name]-[hash].js',
        assetFileNames: 'assets/[name]-[hash].[ext]',
      },
    },
  },
});
