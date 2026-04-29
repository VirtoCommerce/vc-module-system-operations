// Plugin loader for System Operations.
//
// Calls the platform's manifest endpoint, registers each plugin's MF remote
// with @module-federation/runtime, and invokes the plugin's `install` with a
// scoped host facade. Failures of individual plugins are isolated — a bad
// plugin logs and is skipped; the rest of the page continues to load.

import * as Vue from 'vue';
import { createInstance } from '@module-federation/runtime';
import type { PluginRegistry } from './registry';
import type { SystemOperationsPlugin, PluginContext } from './types';

/** Shape of one plugin entry in the manifest response. Mirrors PluginEntry.cs. */
interface PluginEntry {
  id: string;
  version: string;
  entry: { type: 'script' | 'style'; path: string; hash?: string };
  contentFiles: Array<{ type: 'script' | 'style'; path: string; hash?: string }>;
  remote?: { name: string; exposed: string };
}

interface ManifestResponse {
  appId: string;
  version: string;
  title: string;
  plugins: PluginEntry[];
}

const ENDPOINT = '/api/apps/system-operations/manifest';

export async function loadPlugins(registry: PluginRegistry, isDev: boolean): Promise<void> {
  let manifest: ManifestResponse;
  try {
    const response = await fetch(ENDPOINT, {
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
    });
    if (!response.ok) {
      // 401/403/404 are not fatal — just means no plugins to load.
      console.warn(
        `[system-operations] manifest endpoint ${ENDPOINT} returned HTTP ${response.status}; skipping plugin discovery.`,
      );
      return;
    }
    manifest = (await response.json()) as ManifestResponse;
  } catch (err) {
    console.error('[system-operations] failed to fetch plugin manifest:', err);
    return;
  }

  const plugins = (manifest.plugins ?? []).filter((p) => p.remote && p.entry?.path);
  if (plugins.length === 0) {
    return;
  }

  // Inject any plugin-declared style files up front so they apply to the
  // components the plugin will register (parallel-safe — order of stylesheets
  // doesn't matter for correctness; specificity does).
  for (const plugin of plugins) {
    for (const cf of plugin.contentFiles ?? []) {
      if (cf.type === 'style') injectStyle(cf.path, cf.hash);
    }
  }

  // Build the MF runtime instance with all remotes pre-declared. We share
  // Vue with plugins so they don't ship their own copy and so reactivity
  // works across the boundary.
  const mf = createInstance({
    name: 'system-operations-host',
    remotes: plugins.map((p) => ({
      name: p.remote!.name,
      entry: withCacheBust(p.entry.path, p.entry.hash),
      type: 'module' as const,
    })),
    shared: {
      vue: {
        version: Vue.version,
        scope: 'default',
        lib: () => Vue,
        shareConfig: { singleton: true, requiredVersion: '^3.5.0' },
      },
    },
  });

  // Load + install plugins. Each plugin runs in its own try/catch so one
  // bad apple doesn't break the page.
  for (const plugin of plugins) {
    const exposedKey = (plugin.remote!.exposed ?? './Module').replace(/^\.\//, '');
    const loadKey = `${plugin.remote!.name}/${exposedKey}`;

    try {
      const mod = await mf.loadRemote<{ default?: SystemOperationsPlugin } | SystemOperationsPlugin>(loadKey);
      const impl: SystemOperationsPlugin | undefined =
        (mod as { default?: SystemOperationsPlugin })?.default ?? (mod as SystemOperationsPlugin);

      if (!impl || typeof impl.install !== 'function') {
        console.error(
          `[system-operations] plugin "${plugin.id}" does not export a default { install(host, ctx) }; skipping.`,
        );
        continue;
      }

      const ctx: PluginContext = {
        pluginId: plugin.id,
        pluginVersion: plugin.version,
        isDev,
      };
      await impl.install(registry.forPlugin(plugin.id), ctx);
    } catch (err) {
      console.error(`[system-operations] plugin "${plugin.id}" failed to install:`, err);
    }
  }
}

function injectStyle(href: string, hash?: string): void {
  const link = document.createElement('link');
  link.rel = 'stylesheet';
  link.href = withCacheBust(href, hash);
  document.head.appendChild(link);
}

function withCacheBust(path: string, hash?: string): string {
  return hash ? `${path}?v=${encodeURIComponent(hash)}` : path;
}
