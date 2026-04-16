# VirtoCommerce System Operations Module

## Overview

The System Operations module provides a centralized developer tools page for platform maintenance and diagnostic actions. It consolidates commonly used operations — previously scattered across Settings, Platform Info, and widgets — into a single, well-documented interface within the Developer Tools section.

The module registers as a Developer Tool tab and renders a self-contained web application inside an iframe, with zero dependency on the platform's AngularJS frontend.

## Key Features

- **Reset Cache** — Clear all platform in-memory caches. Useful after direct database changes or configuration updates.
- **Restart Platform** — Gracefully restart the application with automatic polling until the platform is back online.
- **Install Sample Data** — Discover and install sample data packages for demos, development, or evaluation environments. Includes pre-flight state check, automatic state reset for repeat installs, inline progress bar with live status from the Hangfire job, and cancel support.
- **Export Platform Data** — Create a full platform backup with module/entry selection (Select All / Unselect All), real-time progress streaming, cancel support, and a download link on completion.
- **Import Platform Data** — Restore platform data from a previously exported ZIP backup with module/entry selection, real-time progress streaming, and cancel support.
- **Download Manifest** — Export complete platform diagnostic information (version, license, modules, OS, .NET runtime) as `vc-platform-info.json`.
- **Download Package JSON** — Export `vc-package.json` with all installed modules and versions for environment replication.
- **Module Load Sequence** — View the dependency-resolved loading order of all installed modules.

Each operation includes a clear description of what it does, when to use it, and appropriate confirmation dialogs for destructive actions.

## Architecture

This module has no backend services, no database, and no custom API endpoints. It calls existing platform APIs directly from the browser:

| Operation | API Endpoint |
|-----------|-------------|
| Reset Cache | `POST /api/platform-cache/reset` |
| Restart Platform | `POST /api/platform/modules/restart` |
| Discover Sample Data | `GET /api/platform/sampledata/discover` |
| Sample Data State | `GET /api/platform/sampledata/state` |
| Import Sample Data | `POST /api/platform/sampledata/import` |
| Reset Sample Data State | `POST /api/platform/settings` |
| Export Platform Data | `POST /api/platform/export` |
| Import Platform Data | `POST /api/platform/import` |
| Export Manifest | `GET /api/platform/export/manifest/new` |
| Load Import Manifest | `GET /api/platform/export/manifest/load` |
| Upload Backup File | `POST /api/assets/localstorage` |
| Cancel Job | `POST /api/platform/exortimport/tasks/{jobId}/cancel` |
| Poll Progress | `POST /api/platform/pushnotifications` |
| Download Manifest | `GET /api/platform/diagnostics/systeminfo` |
| Module Load Sequence | `GET /api/platform/modules/loading-order` |

Long-running operations (Export, Import, Sample Data Install) use Hangfire background jobs on the platform side. Progress is tracked by polling the push notification search endpoint (`POST /api/platform/pushnotifications` with `{ ids: [notificationId] }`) every 2 seconds until the notification's `finished` field is set.

The UI is a Vue.js 3 + TypeScript + Vite application, served via the platform's `<apps>` mechanism and registered as a Developer Tool via `IDeveloperToolRegistrar`. The app supports 13 languages matching the platform's localization.

## Installation

1. Download the latest release from [GitHub Releases](https://github.com/VirtoCommerce/vc-module-system-operations/releases).
2. Extract the ZIP into the platform's `modules` directory.
3. Restart the platform.

The module will appear under **Configuration > Developer Tools > System Operations**.

## Development

### Prerequisites

- .NET 10.0 SDK
- Node.js 18+

### Build

```bash
# Restore and compile
dotnet restore
dotnet build

# Build Vue app (type-check + Vite production build)
cd src/VirtoCommerce.SystemOperations.Web
npm install
npm run build
cd ../..

# Or use vc-build (runs npm ci + npm run webpack:build automatically)
vc-build Compress
```

### Local Development

```bash
cd src/VirtoCommerce.SystemOperations.Web
npm install
npm run dev
```

Vite dev server starts with HMR. API calls are proxied to `http://localhost:5001` (configure in `vite.config.ts`).

### Project Structure

```
src/VirtoCommerce.SystemOperations.Web/
    Module.cs                       — Registers developer tool (minimal)
    module.manifest                 — Module metadata with <apps> section
    vite.config.ts                  — Vite build config (outputs to Content/system-operations/)
    tsconfig.json                   — TypeScript configuration
    package.json                    — Vue/Vite dependencies
    app/                            — Vue.js application source
        index.html                  — Vite entry HTML
        main.ts                     — App entry point
        App.vue                     — Root component (sections + cards)
        components/
            OperationCard.vue       — Reusable card (icon, title, desc, scenario, action slot)
            VcDialog.vue            — Platform-styled modal dialog
            SectionGroup.vue        — Section with title + cards grid
            ModuleLoadSequence.vue  — Expandable module load order list
            SampleDataPackages.vue  — Sample data discovery + install with progress
            PlatformBackup.vue      — Export with manifest config + progress polling
            PlatformRestore.vue     — Import with file upload, manifest config + progress
        composables/
            useApi.ts               — fetch() wrapper with error handling
            useDialog.ts            — Promise-based dialog state (provide/inject)
            useI18n.ts              — Locale detection + translation loading
            useOperations.ts        — Reset cache + restart logic
            useSystemInfo.ts        — Download manifest/package logic
        locales/                    — Translation files (13 languages)
            en.json, de.json, es.json, fi.json, fr.json, it.json,
            ja.json, no.json, pl.json, pt.json, ru.json, sv.json, zh.json
        types/index.ts              — TypeScript interfaces
        styles/
            variables.css           — VirtoCommerce design tokens
            dialog.css              — Modal dialog styles
            main.css                — Cards, buttons, layout
    Content/system-operations/      — Vite build output (served at /apps/system-operations/)
```

## Extending the Page

The page is built from two building blocks: **sections** (grouping headers with a card grid) and **operation cards** (individual action widgets). Both are localized across 13 languages.

### Page anatomy

```
┌─ page-header ──────────────────────────────────────────────┐
│  System Operations                                         │
│  Platform maintenance and diagnostic tools...              │
└────────────────────────────────────────────────────────────┘

┌─ SectionGroup (title="Maintenance") ───────────────────────┐
│  ┌─ OperationCard  ──┐  ┌─ OperationCard  ──┐              │
│  │  Reset Cache      │  │  Restart Platform │              │
│  │  [slot: button]   │  │  [slot: button]   │              │
│  └───────────────────┘  └───────────────────┘              │
└────────────────────────────────────────────────────────────┘

┌─ SectionGroup (title="Data") ───────────────────────────────┐
│  ┌─ OperationCard ──┐  ┌─ OperationCard ──┐  ┌─ Card ──┐  │
│  │  Sample Data     │  │  Export Data     │  │  Import │  │
│  │  [SampleData…]   │  │  [Backup…]      │  │  [Rest…]│  │
│  └──────────────────┘  └─────────────────┘  └─────────┘  │
└─────────────────────────────────────────────────────────────┘

┌─ SectionGroup (title="Diagnostics & Export") ───────────────┐
│  ┌─ OperationCard ──┐  ┌─ OperationCard ──┐  ┌─ Card ──┐  │
│  │  Manifest        │  │  Package JSON   │  │  Module │  │
│  │  [download]      │  │  [download]     │  │  [list] │  │
│  └──────────────────┘  └─────────────────┘  └─────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

### Adding a new section

A section is a titled group that contains one or more operation cards.

**Step 1.** Add locale key in every `app/locales/*.json` file:

```json
{
  "sections": {
    "monitoring": "Monitoring"
  }
}
```

**Step 2.** Add a `<SectionGroup>` in `app/App.vue` at the desired position:

```vue
<SectionGroup :title="t('sections.monitoring')">
  <!-- operation cards go here -->
</SectionGroup>
```

That's it — the section renders an uppercase label and a responsive card grid.

---

### Adding a new operation card (widget)

An operation card is a self-contained widget with icon, title, description, usage scenario, and an action slot for buttons or custom content.

#### 1. Add locale strings

Add a new block to every file in `app/locales/*.json` (start with `en.json`, then translate for each language):

```json
{
  "healthCheck": {
    "title": "Connection Health Check",
    "description": "Tests connectivity to database, Redis, blob storage, and search engine.",
    "scenario": "Use after deployment to verify infrastructure health.",
    "action": "Run Check",
    "loading": "Checking...",
    "error": "Health check failed."
  }
}
```

#### 2. Create a component (optional — for custom UI)

Simple operations that only need a button can inline the action in `App.vue`. For operations with expandable content, loading states, or complex UI, create a dedicated component:

```vue
<!-- app/components/HealthCheck.vue -->
<script setup lang="ts">
import { ref, inject } from 'vue';
import { useApi, ApiError } from '../composables/useApi';
import { I18nKey } from '../composables/useI18n';

const { t } = inject(I18nKey)!;
const { get } = useApi();
const isLoading = ref(false);
const error = ref('');
const result = ref('');

async function execute() {
  error.value = '';
  result.value = '';
  isLoading.value = true;
  try {
    const data = await get<{ status: string }>('/health');
    result.value = data.status;
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : t('healthCheck.error');
  } finally {
    isLoading.value = false;
  }
}
</script>

<template>
  <div class="op-card__actions">
    <button class="btn btn--primary" :disabled="isLoading" @click="execute">
      <i :class="isLoading ? 'fas fa-spinner fa-spin' : 'fas fa-heartbeat'"></i>
      {{ isLoading ? t('healthCheck.loading') : t('healthCheck.action') }}
    </button>
  </div>
  <div v-if="result" class="expand-section">
    <pre>{{ result }}</pre>
  </div>
  <div v-if="error" class="op-card__error">{{ error }}</div>
</template>
```

#### 3. Add the card to `App.vue`

Import the component and place an `<OperationCard>` inside a `<SectionGroup>`:

```vue
<script setup lang="ts">
import HealthCheck from './components/HealthCheck.vue';
// ... existing imports
</script>

<template>
  <!-- Add to an existing section... -->
  <SectionGroup :title="t('sections.monitoring')">
    <OperationCard
      icon="fas fa-heartbeat"
      icon-color="green"
      :title="t('healthCheck.title')"
      :description="t('healthCheck.description')"
      :scenario="t('healthCheck.scenario')"
    >
      <HealthCheck />
    </OperationCard>
  </SectionGroup>
</template>
```

#### 4. Build and test

```bash
npm run dev    # Development with HMR
npm run build  # Production build
```

---

### Component reference

#### `<OperationCard>` props

| Prop | Type | Required | Description |
|------|------|----------|-------------|
| `icon` | `string` | yes | Font Awesome class (e.g. `fas fa-bolt`) |
| `icon-color` | `'blue' \| 'red' \| 'orange' \| 'green' \| 'purple'` | yes | Icon background color |
| `title` | `string` | yes | Card heading |
| `description` | `string` | yes | What the operation does |
| `scenario` | `string` | yes | When/why to use it |
| `permission` | `string` | no | Permission badge shown under title |
| `variant` | `'danger' \| 'warning'` | no | Card hover color for destructive actions |

The default `<slot>` is rendered below the scenario box — use it for action buttons, expandable content, or custom components.

#### `<SectionGroup>` props

| Prop | Type | Required | Description |
|------|------|----------|-------------|
| `title` | `string` | yes | Uppercase section label |

The default `<slot>` renders inside a responsive CSS grid (`repeat(auto-fill, minmax(380px, 1fr))`).

#### Composables available via `inject()`

| Key | Composable | Usage |
|-----|-----------|-------|
| `I18nKey` | `useI18n()` | `const { t } = inject(I18nKey)!` — translate strings |
| `DialogKey` | `useDialog()` | `const dialog = inject(DialogKey)!` — show modal dialogs |

#### Dialog API

```typescript
const dialog = inject(DialogKey)!;

// Warning (Yes/No) — returns true if user clicks Yes
const confirmed = await dialog.warning('Title', 'Message');

// Success (OK only)
await dialog.success('Title', 'Done.');

// Error (OK only)
await dialog.error('Title', 'Something went wrong.');
```

#### API composable

```typescript
import { useApi, ApiError } from '../composables/useApi';

const { get, post } = useApi();

// GET with typed response
const data = await get<MyType>('/api/endpoint');

// POST (no body — platform APIs use query params)
await post('/api/endpoint?param=value');

// Error handling
try {
  await post('/api/protected-action');
} catch (err) {
  if (err instanceof ApiError && err.isPermissionDenied) {
    // 401/403
  }
}
```

---

### Localization

All user-visible strings must be in `app/locales/*.json` and accessed via `t('key')`. The app auto-detects the platform locale from the parent window and falls back to English.

To add or update translations:
1. Edit `app/locales/en.json` first
2. Add the same keys to all 12 other locale files (de, es, fi, fr, it, ja, no, pl, pt, ru, sv, zh)
3. Use `{param}` placeholders for dynamic values: `t('key', { param: value })`

## License

Copyright (c) Virto Solutions LTD. All rights reserved.

Licensed under the Virto Commerce Open Software License (the "License"); you
may not use this file except in compliance with the License. You may
obtain a copy of the License at

http://virtocommerce.com/opensourcelicense

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
implied.

