import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import { resolve } from 'path';

export default defineConfig({
  root: resolve(__dirname, 'app'),
  plugins: [vue()],
  base: '/apps/system-operations/',
  build: {
    outDir: resolve(__dirname, 'Content/system-operations'),
    emptyOutDir: true,
  },
  server: {
    proxy: {
      '/api': 'http://localhost:5001',
      '/images': 'http://localhost:5001',
    },
  },
});
