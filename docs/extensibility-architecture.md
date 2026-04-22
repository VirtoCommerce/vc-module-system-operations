# System Operations Extensibility Architecture

## Problem

The System Operations module provides a centralized developer tools page, but all operations are hardcoded in the Vue app. Other VirtoCommerce modules cannot contribute their own operation cards or sections without modifying this module directly.

## Goal

Allow any VirtoCommerce module to extend the System Operations page by:
- Adding new operation cards to existing sections
- Adding entirely new sections
- Providing either simple JSON-defined cards (no build step) or rich Vue plugin-based cards (full custom UI)

## Design Principles

- **Convention over configuration** — extensions are discovered by well-known file paths, no registration API needed
- **Progressive complexity** — simple JSON cards for common cases, Vue plugins only when custom UI is required
- **Zero platform changes** — relies entirely on existing module static file serving (`/modules/$(ModuleName)/*`)
- **Runtime discovery** — extensions are found and loaded at page load, not at build time

## How It Works

### Extension Discovery Flow

```
┌──────────────────────────────────────────────────────────────┐
│  System Operations App (Vue SPA in iframe)                   │
│                                                              │
│  1. GET /api/platform/modules                                │
│     → [{ id, dependencies: [{ id, version }], ... }, ...]    │
│                                                              │
│  2. Filter: keep only modules where dependencies             │
│     includes "VirtoCommerce.SystemOperations"                │
│     → ["VirtoCommerce.Foo", "VirtoCommerce.Bar"]             │
│                                                              │
│  3. For each dependent module, probe:                        │
│     GET /modules/$(module)/Content/                          │
│         Plugins/system-operations/manifest.json                  │
│     → 404 = no extension, skip                               │
│     → 200 = parse manifest                                   │
│                                                              │
│  4a. If manifest.plugin == false (or absent):                │
│      Render cards from JSON descriptors                      │
│                                                              │
│  4b. If manifest.plugin == true:                             │
│      Dynamic import(pluginUrl)                               │
│      Call plugin.install(app, extensionApi)                  │
│                                                              │
│  5. Merge extension sections/cards with built-in ones        │
│     Sort by sortOrder, render                                │
└──────────────────────────────────────────────────────────────┘
```

### File Convention

Any module that depends on `VirtoCommerce.SystemOperations` places files at:

```
modules/VirtoCommerce.MyModule/
├── module.manifest              ← must declare dependency on VirtoCommerce.SystemOperations
└── Content/
    └── Plugins/system-operations/
        ├── manifest.json        ← required: extension descriptor
        └── plugin.mjs           ← optional: Vue plugin bundle (only if manifest.plugin == true)
```

The platform's existing static file middleware serves these at:
```
GET /modules/$(VirtoCommerce.MyModule)/Content/Plugins/system-operations/manifest.json
GET /modules/$(VirtoCommerce.MyModule)/Content/Plugins/system-operations/plugin.mjs
```

No backend changes or new API endpoints are needed.

---

## Option A: JSON Extension Manifest

For modules that need simple operation cards (button → API call → result).

### manifest.json Schema

```json
{
  "$schema": "https://virtocommerce.com/schemas/system-operations-plugin/v1.json",
  "version": "1.0",
  "plugin": false,
  "sections": [
    {
      "id": "my-section",
      "title": {
        "en": "My Section",
        "ru": "Мой раздел"
      },
      "sortOrder": 50,
      "cards": [
        {
          "id": "my-operation",
          "icon": "fas fa-heartbeat",
          "iconColor": "red",
          "title": {
            "en": "Health Check",
            "ru": "Проверка состояния"
          },
          "description": {
            "en": "Tests connectivity to all infrastructure services."
          },
          "scenario": {
            "en": "Use after deployment to verify everything is connected."
          },
          "permission": "my-module:health:check",
          "variant": null,
          "actions": [
            {
              "type": "api-call",
              "label": { "en": "Run Check", "ru": "Запустить" },
              "method": "POST",
              "url": "/api/my-module/health-check",
              "confirm": {
                "title": { "en": "Run Health Check?" },
                "message": { "en": "This will test all service connections." }
              },
              "successMessage": { "en": "All services are healthy." }
            }
          ]
        }
      ]
    }
  ]
}
```

### Card Placement

Cards can be added to existing built-in sections or to new sections:

| `section.id` | Behavior |
|--------------|----------|
| `maintenance` | Adds cards to the existing Maintenance section |
| `data` | Adds cards to the existing Data section |
| `diagnostics` | Adds cards to the existing Diagnostics & Export section |
| Any other ID | Creates a new section |

### Action Types

| Type | Description | Properties |
|------|-------------|------------|
| `api-call` | POST/GET to an API endpoint with optional confirm dialog | `method`, `url`, `confirm?`, `successMessage?`, `errorMessage?` |
| `link` | Navigate to a URL | `url`, `target` (`_self` or `_blank`) |
| `download` | Download a file from URL | `url`, `filename?` |

### Localized Strings

All user-facing strings use a locale map: `{ "en": "English", "ru": "Русский", ... }`. The app resolves the current locale and falls back to `en`.

### JSON Card Rendering

JSON-defined cards are rendered by a generic `ExtensionCard` component that:
- Displays icon, title, description, scenario from the manifest
- Renders action buttons per the `actions` array
- Handles `api-call` actions: shows confirm dialog → calls API → shows success/error
- Handles `link` actions: renders as `<a>` tags
- Handles `download` actions: triggers file download

---

## Option B: Vue Plugin Extension

For modules that need rich, interactive UI (progress bars, file uploads, multi-step wizards, custom forms).

### manifest.json for Plugin Mode

```json
{
  "version": "1.0",
  "plugin": true,
  "pluginUrl": "plugin.mjs"
}
```

### Plugin Contract

The plugin must export a default object with an `install` method:

```typescript
// plugin.mjs — ES module built with Vite/Rollup
import { defineComponent, ref } from 'vue';

export default {
  install(app, ctx) {
    // ctx provides:
    // - ctx.registerSection(section)    — add a new section
    // - ctx.registerCard(sectionId, card) — add a card to a section
    // - ctx.useApi()      — { get, post } API composable
    // - ctx.useDialog()   — { warning, success, error } dialog composable
    // - ctx.t(key, params?) — i18n translation function
    // - ctx.resolveLocale(localeMap) — resolve { en: '...', ru: '...' } to current locale

    ctx.registerCard('maintenance', {
      id: 'my-custom-widget',
      icon: 'fas fa-stethoscope',
      iconColor: 'green',
      title: ctx.resolveLocale({
        en: 'Service Diagnostics',
        ru: 'Диагностика сервисов',
      }),
      sortOrder: 20,
      // Vue component rendered inside the OperationCard slot
      component: defineComponent({
        setup() {
          const { post } = ctx.useApi();
          const result = ref('');
          const isLoading = ref(false);

          async function run() {
            isLoading.value = true;
            try {
              const data = await post('/api/my-module/diagnostics');
              result.value = JSON.stringify(data, null, 2);
            } finally {
              isLoading.value = false;
            }
          }

          return { result, isLoading, run };
        },
        template: `
          <div class="op-card__actions">
            <button class="btn btn--primary" :disabled="isLoading" @click="run">
              <i :class="isLoading ? 'fas fa-spinner fa-spin' : 'fas fa-play'"></i>
              Run Diagnostics
            </button>
          </div>
          <div v-if="result" class="expand-section visible">
            <pre style="font-size: 12px; overflow: auto; max-height: 300px;">{{ result }}</pre>
          </div>
        `,
      }),
    });
  },
};
```

### Extension API (`ctx`) Reference

| Method | Signature | Description |
|--------|-----------|-------------|
| `registerSection` | `(section: ExtSection) => void` | Add a new section to the page |
| `registerCard` | `(sectionId: string, card: ExtCard) => void` | Add a card to an existing or new section |
| `useApi` | `() => { get, post }` | API composable (same as built-in) |
| `useDialog` | `() => { warning, success, error }` | Dialog composable (same as built-in) |
| `t` | `(key: string, params?) => string` | Translation function (for built-in keys) |
| `resolveLocale` | `(map: Record<string, string>) => string` | Resolve locale map to current language |

### ExtSection Shape

```typescript
interface ExtSection {
  id: string;
  title: string;           // Already resolved to current locale
  sortOrder: number;       // Relative to built-in sections (maintenance=10, data=20, diagnostics=30)
}
```

### ExtCard Shape

```typescript
interface ExtCard {
  id: string;
  icon: string;            // Font Awesome class
  iconColor: 'blue' | 'red' | 'orange' | 'green' | 'purple';
  title: string;           // Already resolved
  description?: string;
  scenario?: string;
  permission?: string;
  variant?: 'danger' | 'warning';
  sortOrder?: number;      // Within the section
  component: Component;    // Vue component rendered in the card's slot
}
```

### Building a Plugin

Extending modules should use Vite with `build.lib` to produce an ES module:

```typescript
// vite.config.ts in the extending module
import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';

export default defineConfig({
  plugins: [vue()],
  build: {
    lib: {
      entry: 'src/system-operations-plugin/plugin.ts',
      formats: ['es'],
      fileName: 'plugin',
    },
    outDir: 'Content/Plugins/system-operations',
    rollupOptions: {
      external: ['vue'],  // Vue is provided by the host app
    },
  },
});
```

**Important:** The plugin must treat `vue` as external — it will use the Vue instance from the host System Operations app via the global scope or import map.

---

## Implementation Plan

### Phase 1: Extension Infrastructure

**New files to create:**

#### 1. `app/composables/useExtensions.ts`

Core discovery and loading logic.

```typescript
const SYSTEM_OPS_MODULE_ID = 'VirtoCommerce.SystemOperations';

interface InstalledModule {
  id: string;
  version: string;
  isInstalled: boolean;
  dependencies: { id: string; version: string; optional: boolean }[];
}

export interface ExtensionManifest {
  version: string;
  plugin: boolean;
  pluginUrl?: string;
  sections?: ExtManifestSection[];
}

export function useExtensions() {
  const { get } = useApi();
  const extensions = ref<LoadedExtension[]>([]);
  const isLoading = ref(true);

  async function discover(): Promise<void> {
    // 1. Get all installed modules with their dependencies
    const allModules = await get<InstalledModule[]>('/api/platform/modules');

    // 2. Filter to only modules that depend on VirtoCommerce.SystemOperations
    const dependentModules = allModules.filter(
      (m) => m.isInstalled && m.dependencies?.some((d) => d.id === SYSTEM_OPS_MODULE_ID)
    );

    // 3. Probe each dependent module for manifest.json (parallel, ignore 404s)
    const results = await Promise.allSettled(
      dependentModules.map(async (module) => {
        const url = `/modules/$(${module.id})/Content/Plugins/system-operations/manifest.json`;
        const manifest = await get<ExtensionManifest>(url);
        return { moduleName: module.id, manifest };
      })
    );

    // 4. Process successful probes
    for (const result of results) {
      if (result.status === 'fulfilled') {
        const { moduleName, manifest } = result.value;
        if (manifest.plugin) {
          await loadPlugin(moduleName, manifest);
        } else {
          loadJsonExtension(moduleName, manifest);
        }
      }
    }

    isLoading.value = false;
  }

  return { extensions, isLoading, discover };
}
```

#### 2. `app/components/ExtensionCard.vue`

Generic renderer for JSON-defined cards.

- Renders `OperationCard` with props from the JSON descriptor
- Maps `actions` to buttons with click handlers
- Handles `api-call` type: confirm dialog → fetch → success/error display
- Handles `link` type: renders `<a>` tag
- Handles `download` type: triggers blob download

#### 3. `app/types/extensions.ts`

TypeScript interfaces for the manifest schema, section, card, action shapes.

#### 4. Updates to `App.vue`

- Import and call `useExtensions()` on mount
- Merge `extensions.sections` with built-in sections
- Render extension sections after built-in ones (sorted by `sortOrder`)
- For plugin-based cards, use Vue's `<component :is="card.component" />` dynamic rendering

### Phase 2: JSON Extension Support (Option A)

1. Implement `ExtensionCard.vue` with all action types
2. Implement locale resolution for JSON string maps
3. Add error handling (malformed manifests, failed API calls)
4. Write documentation with example `manifest.json`

### Phase 3: Plugin Extension Support (Option B)

1. Implement dynamic `import()` loading with error boundaries
2. Create the extension API context (`registerSection`, `registerCard`, `useApi`, etc.)
3. Implement Vue shared instance (ensure plugins use the same Vue runtime)
4. Add sandbox error handling (plugin errors should not crash the host app)
5. Write documentation with example plugin + Vite config

### Phase 4: Example Extension Module

Create a minimal example module `VirtoCommerce.SystemOperationsExample` that:
- Declares dependency on `VirtoCommerce.SystemOperations` in `module.manifest`
- Contains a JSON extension with one simple card (API call)
- Contains a plugin extension with one custom Vue component card
- Serves as both a test and a reference implementation

---

## Built-in Section Sort Order

Extensions use `sortOrder` to position their sections relative to built-in ones:

| Section | sortOrder | Notes |
|---------|-----------|-------|
| Developer Tools | 0 | Dynamic, from platform API |
| Maintenance | 10 | Platform Info, Reset Cache, Restart |
| Data | 20 | Sample Data, Export, Import |
| Diagnostics & Export | 30 | Package JSON, Module Sequence |
| *Extension sections* | 40+ | Added by other modules |

Cards within a section default to `sortOrder: 100` if not specified. Built-in cards use values 10, 20, 30, etc.

---

## Security Considerations

- **Module trust boundary** — VirtoCommerce modules are admin-installed and trusted. Loading JS from a module's static content is equivalent to loading the module's DLL. No additional sandboxing is needed.
- **Permission filtering** — JSON cards with `permission` field are only rendered if the user has that permission (checked via a new `GET /api/platform/security/permissions` call or passed from the parent frame).
- **CORS** — All requests are same-origin (module content is served by the same platform). No CORS issues.
- **Plugin isolation** — Plugin errors are caught in a `try/catch` around `install()` and logged to console. A failing plugin does not prevent other extensions or built-in cards from rendering.

---

## Migration Path

Existing built-in cards are not affected. The extension system is purely additive:

1. **v1.0 (current)** — All cards hardcoded in `App.vue`
2. **v2.0 (after this plan)** — Built-in cards remain hardcoded; extension cards loaded dynamically alongside them
3. **v3.0 (future, optional)** — Built-in cards could themselves be extracted into the extension manifest format for consistency, making the entire page data-driven

---

## Example: Adding a Card from Another Module

### Simple (JSON only, no build step)

In `VirtoCommerce.Webhooks` module, create:

**`Content/Plugins/system-operations/manifest.json`:**
```json
{
  "version": "1.0",
  "plugin": false,
  "sections": [
    {
      "id": "maintenance",
      "cards": [
        {
          "id": "webhooks-retry-failed",
          "icon": "fas fa-redo",
          "iconColor": "orange",
          "title": { "en": "Retry Failed Webhooks", "ru": "Повторить неудачные вебхуки" },
          "description": { "en": "Retries all webhook deliveries that failed in the last 24 hours." },
          "scenario": { "en": "Use when webhook endpoint was temporarily down." },
          "permission": "webhooks:manage",
          "actions": [
            {
              "type": "api-call",
              "label": { "en": "Retry All", "ru": "Повторить все" },
              "method": "POST",
              "url": "/api/webhooks/retry-failed",
              "confirm": {
                "title": { "en": "Retry Failed Webhooks?" },
                "message": { "en": "This will re-send all failed webhook deliveries from the last 24 hours." }
              },
              "successMessage": { "en": "Retry initiated." }
            }
          ]
        }
      ]
    }
  ]
}
```

**`module.manifest` (dependency section):**
```xml
<dependencies>
  <dependency id="VirtoCommerce.SystemOperations" version="3.1000.0" />
</dependencies>
```

That's it. No code, no build step, no Vue. The card appears in the Maintenance section.

### Rich (Vue Plugin)

See the Plugin Contract section above for a full example with a custom Vue component.
