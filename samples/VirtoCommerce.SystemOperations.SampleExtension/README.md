# System Operations — Sample Extension

A reference plugin that demonstrates the modularity framework. It contributes a single Browser Info card to the Diagnostics section of the System Operations app,
showing the running browser, OS, screen size, and time zone of the current admin user.

## Screenshot

<img width="906" height="561" alt="image" src="https://github.com/user-attachments/assets/3837255c-0675-429c-bfa7-dafe05bdf6e1" />

## What it does

When the System Operations app loads, the platform's manifest endpoint
(`GET /api/apps/system-operations/manifest`) returns this module as a
plugin. The host's MF runtime fetches the remote, calls
`install(host, ctx)`, and the plugin registers an `OperationCard` via
`host.registerCard(...)`.


## Anatomy

```
samples/VirtoCommerce.SystemOperations.SampleExtension/
├── _module.manifest                 .NET module manifest. Declares
│                                   <dependency id="VirtoCommerce.SystemOperations">,
│                                   which is what makes the platform's
│                                   AppManifestService treat this module
│                                   as a system-operations plugin.
├── package.json                    npm metadata; deps: vue, MF Vite plugin.
├── vite.config.mts                 MF remote build. outDir =
│                                   plugins/system-operations (the
│                                   discovery folder the host probes).
├── tsconfig.json                   minimal TS config.
├── module.ignore                   excluded from the .nupkg / module zip.
├── src/
│   ├── index.ts                    Plugin entry. default-exports
│   │                               { install(host, ctx) }.
│   ├── BrowserInfoCard.vue         Card body.
│   └── host-types.ts               Inlined copy of the host plugin
│                                   contract — keeps the sample free of
│                                   any runtime dependency on the host.
└── plugins/system-operations/      Build output. Generated; not committed.
    ├── remoteEntry.js
    ├── *.js                        chunks
    └── *.css
```

## Build

```bash
npm install
npm run build
```

The build emits `plugins/system-operations/remoteEntry.js` (and chunks) directly into the discovery folder.

## Install on a running platform

1. Build (above).
2. Rename `_module.manifest` to `module.manifest`.
3. Zip the module folder so the archive contains, at minimum:
   ```
   module.manifest
   plugins/system-operations/
   ```
4. Drop the zip into the platform's `modules/` folder, or upload it via
   **Configuration → Modules → Advanced → Upload module**.
5. Restart the platform. 
6. Open **Configuration → Developer Tools → System Operations**. The
   "Browser Info" card appears at the bottom of **Diagnostics & Export**.
   No browser refresh is required after the platform restart.

## How it works end-to-end

```
1. Browser loads /apps/system-operations/index.html
2. main.ts boots Vue, mounts the shell, then calls loadPlugins()
3. loadPlugins() ─→ GET /api/apps/system-operations/manifest
4. Platform walks installed modules in dependency order, finds this
   sample under {moduleRoot}/plugins/system-operations/, returns:
     {
       id: "VirtoCommerce.SystemOperations.SampleExtension",
       version: "3.1000.0",
       entry: { type: "script",
                path: "/modules/$(...)/plugins/system-operations/remoteEntry.js",
                hash: "..." },
       remote: { name: "VirtoCommerce.SystemOperations.SampleExtension",
                 exposed: "./Module" }
     }
5. Host's @module-federation/runtime registers the remote, fetches
   remoteEntry.js, negotiates Vue as a singleton.
6. mf.loadRemote('VirtoCommerce.SystemOperations.SampleExtension/Module')
   resolves to this module's `src/index.ts`.
7. Host calls plugin.install(scopedHost, ctx).
8. install() calls host.registerCard({ section: 'diagnostics', ... }).
9. The card appears in the page (reactive — no second fetch or rerender).
```

## Customising the sample

- **Where the card lands** — change `section` in `src/index.ts` to
  `'maintenance'`, `'data'`, `'diagnostics'`, or `'plugins'`. The
  `'plugins'` section only renders when at least one plugin contributes
  to it.
- **Multiple cards** — call `host.registerCard(...)` once per card.
- **Permission gating** — pass `permission: "..."` in the card props to
  show a permission badge. Server-side filtering (the `permission`
  field on `plugin.json`) gates the *whole* plugin from being shipped
  to unauthorized users; per-card UI gating is purely cosmetic.
- **Custom CSS** — write `<style scoped>` inside the card component.
  Module Federation isolates non-scoped styles to the chunk that loads
  them, so unscoped global selectors are usually safe but `:scoped`
  is the recommended pattern.

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
