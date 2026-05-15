# System Operations

## Overview

The System Operations module provides a centralized developer tools page for platform maintenance and diagnostic actions. It consolidates commonly used operations — previously scattered across Settings, Platform Info, and widgets — into a single, well-documented interface within the Developer Tools section.

The module registers as a Developer Tool tab and renders a self-contained web application inside an iframe, with zero dependency on the platform's AngularJS frontend.

## Key Features

- **Developer Tools Navigation** — Dynamic section listing all registered developer tools from the platform, with automatic icon/color assignment per tool type (Health, Hangfire, Swagger, GraphQL, etc.). Click opens in the same iframe, Ctrl+click opens in a new tab. External tools open in a new tab automatically.
- **Platform Information** — Auto-loaded on page open. Displays platform version, environment mode, database provider, runtime, and license details. Includes Copy to Clipboard and Download as `vc-platform-info.json`.
- **Reset Cache** — Clear all platform in-memory caches. Useful after direct database changes or configuration updates.
- **Restart Platform** — Gracefully restart the application with automatic polling until the platform is back online.
- **Install Sample Data** — Discover and install sample data packages for demos, development, or evaluation environments. Includes pre-flight state check, automatic state reset for repeat installs, inline progress bar with live status from the Hangfire job, and cancel support.
- **Export Platform Data** — Create a full platform backup with module/entry selection (Select All / Unselect All), real-time progress streaming, cancel support, and a download link on completion.
- **Import Platform Data** — Restore platform data from a previously exported ZIP backup with module/entry selection, real-time progress streaming, and cancel support.
- **Download Package JSON** — Export `vc-package.json` with all installed modules and versions for environment replication. Includes a ready-to-copy `vc-build Install` command for restoring modules from the downloaded file.
- **Module Load Sequence** — View the dependency-resolved loading order of all installed modules with Copy to Clipboard support.
- **Plugin Extensibility (Module Federation)** — Other modules can ship UI cards that render alongside the built-in ones, discovered automatically via the platform's modularity framework. See [Plugin extensibility](#plugin-extensibility-module-federation) below.

Each operation includes a clear description of what it does, when to use it, and appropriate confirmation dialogs for destructive actions.

## Screenshots

### Light Theme

<img width="1910" height="1717" alt="image" src="https://github.com/user-attachments/assets/75caab31-251f-4e6f-98bb-b3c80abccf62" />

### Dart Theme

<img width="1910" height="1717" alt="image" src="https://github.com/user-attachments/assets/6f4a5210-84b3-49e2-8f27-063b3952c795" />


## Architecture

This module has no backend services, no database, and no custom API endpoints. It calls existing platform APIs directly from the browser:

| Operation | API Endpoint |
|-----------|-------------|
| Developer Tools | `GET /api/platform/developer-tools` |
| Platform Information | `GET /api/platform/diagnostics/systeminfo` |
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
| Module Load Sequence | `GET /api/platform/modules/loading-order` |

Long-running operations (Export, Import, Sample Data Install) use Hangfire background jobs on the platform side. Progress is tracked by polling the push notification search endpoint (`POST /api/platform/pushnotifications` with `{ ids: [notificationId] }`) every 2 seconds until the notification's `finished` field is set.

The UI is a Vue.js 3 + TypeScript + Vite application, served via the platform's `<apps>` mechanism and registered as a Developer Tool via `IDeveloperToolRegistrar`. The app supports 13 languages matching the platform's localization.

It is also a **Module Federation host** — see the next section.

## Plugin extensibility (Module Federation)

System Operations participates in the Backoffice Modularity Framework.

### How it works

```
1. Browser loads /apps/system-operations/index.html
2. main.ts boots Vue, mounts the shell immediately
3. loadPlugins() ─→ GET /api/apps/system-operations/manifest
4. Platform walks installed modules in dependency order, finds every
   {moduleRoot}/plugins/system-operations/remoteEntry.js, returns one
   PluginEntry per discovered remote.
5. @module-federation/runtime registers the remotes, negotiates Vue
   as a singleton, fetches each remoteEntry.js.
6. For each plugin, host calls `plugin.install(host, ctx)` — the
   plugin uses `host.registerCard({...})` to add OperationCards.
7. The reactive registry surfaces new cards into the shell without a
   re-fetch.
```

### Sample
See [Sample Extension](samples/VirtoCommerce.SystemOperations.SampleExtension/README.md) for a reference implementation of a System Operations plugin.

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

