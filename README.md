# System Operations

## Overview

The System Operations module provides a centralized developer tools page for platform maintenance and diagnostic actions. It consolidates commonly used operations — previously scattered across Settings, Platform Info, and widgets — into a single, well-documented interface within the Developer Tools section.

The module registers as a Developer Tool tab and renders a self-contained web application inside an iframe, with zero dependency on the platform's AngularJS frontend.

## Compatibility

The module targets platform **`3.1095.0`** and runs on that and any later release. The operations toolset for AI agents also needs the [MCP module](https://github.com/VirtoCommerce/vc-module-mcp); without it, every other operation works as before.

**Modularity (plugin extensions via Module Federation) requires platform `3.1027` or later**, which is where the app-extensions manifest endpoint (`GET /api/apps/{appId}/manifest`) was introduced. On older platforms that endpoint returns 404; the module detects this, logs a single console warning suggesting an upgrade, and continues to work normally without extensions.

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
- **Export Migration Scripts** — Download a ZIP of the EF Core database migration SQL that startup would apply — for the platform, security and every installed module — grouped per target database, with a `_databases.md`/`_databases.json` mapping of which context lives in which database. **Idempotent** by default (safe to run on another environment at any migration state; needs no DB connection); a **pending-only** delta mode is also available. Nothing is applied. This lets you review schema changes with a DBA and run them manually on another environment before it starts — the approach Microsoft [recommends for production](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying). See [Migration export](#migration-export) below.
- **Plugin Extensibility (Module Federation)** — Other modules can ship UI cards that render alongside the built-in ones, discovered automatically via the platform's modularity framework. See [Plugin extensibility](#plugin-extensibility-module-federation) below.
- **Operations toolset for AI agents (MCP)** — With the [MCP module](https://github.com/VirtoCommerce/vc-module-mcp) installed, an administrator asks Claude, ChatGPT or another MCP client "is the platform healthy", "are the resources enough", "is this error our code or Virto's" or "prepare a support case", and the agent answers from the platform's own data. See [AI agents (MCP)](#ai-agents-mcp) below.

Each operation includes a clear description of what it does, when to use it, and appropriate confirmation dialogs for destructive actions.

## Screenshots

### Light Theme

<img width="1912" height="1224" alt="image" src="https://github.com/user-attachments/assets/fbc5180e-5225-4feb-8329-d22945816b95" />

### Dart Theme

<img width="1910" height="1717" alt="image" src="https://github.com/user-attachments/assets/6f4a5210-84b3-49e2-8f27-063b3952c795" />


## AI agents (MCP)

The module adds an operations toolset to the back-office MCP endpoint of the [MCP module](https://github.com/VirtoCommerce/vc-module-mcp), `/admin/mcp`. It is for the people who keep a Virto Commerce solution running: partners, the client's IT and Virto support. They ask an AI agent (Claude, ChatGPT, VS Code or any MCP client) in plain words, and the agent answers from data the platform keeps itself. No telemetry backend is needed.

### Features

- **Daily health check.** The platform's health checks, new errors, failed background jobs, capacity and configuration in one answer, with a short action list.
- **Are the resources enough.** CPU, memory against the container limit, garbage collection, thread pool starvation, database latency and the background job backlog, with one recommendation: scale up, scale out, fix a hotspot, or investigate.
- **Is it our code or Virto's.** An error's stack is mapped to the platform, Virto's modules and the solution's own modules, with versions, and classified as data, configuration, customization, platform code, infrastructure or an external service, with a confidence and the next checks.
- **Incident timeline.** Restarts and deployments, the first occurrence of each error and failed jobs, in time order.
- **Production readiness.** Rules such as the development signing certificate, local file storage or Lucene with several instances, missing Redis, in-memory jobs, debug logs and an expiring license, plus failed modules and pending database migrations.
- **Support case for Virto.** A Markdown report with the environment, own modules, findings, errors with trace ids and stack traces, failed jobs and the timeline, with secrets and personal data removed. Nothing is sent: the person reviews and sends it.
- **Safe actions.** Reset the cache after the user confirms in the chat; restart the platform only after a second person approves.

### Quick setup

For the administrator, once:

1. **Install the modules.** System Operations and the [MCP module](https://github.com/VirtoCommerce/vc-module-mcp), on platform `3.1095.0` or later. The MCP dependency is optional: without the MCP module, System Operations works as before and the toolset is not served.
2. **Open the endpoint for agents.** Add `https://<platform>/admin/mcp` to `Authorization:Resources` and register the agent in **Security → OAuth applications**, as the MCP module's [Set up the platform for agents](https://github.com/VirtoCommerce/vc-module-mcp#set-up-the-platform-for-agents) describes. Skip this step if agents already connect to `/admin/mcp`.
3. **Grant the permissions.** Create a role, for example *Platform operator*, and assign it to the people who run the platform:

   | Permission | Gives |
   | --- | --- |
   | `systemoperations:diagnostics:read` | Every read tool and prompt below |
   | `cache:reset` | `reset_platform_cache` |
   | `platform:module:manage` | `restart_platform`, as a request for approval |
   | `mcp:approve` | Approving a restart in **Agent approvals**; give it to someone else than the requester |

For each user:

1. Connect the agent to `https://<platform>/admin/mcp` with the client id from the administrator. For Claude Code:

   ```bash
   claude mcp add --transport http --client-id claude-code --callback-port 65111 virto https://<platform>/admin/mcp
   ```

   Then run `/mcp`, select `virto` and choose **Authenticate**. Claude Desktop, claude.ai, ChatGPT and VS Code are in the MCP module's [Connect an AI agent](https://github.com/VirtoCommerce/vc-module-mcp#connect-an-ai-agent).
2. Sign in with your back-office account. The agent works as you and sees only the tools your role allows.

### Use

Ask in your own words, for example:

* *Is the platform healthy today? What needs my attention?*
* *Checkout was slow this morning. Are the resources enough, or is something blocking?*
* *Restoring a backup in the back office fails with an error. Is it our code or Virto's?*
* *Is this environment ready for production?*
* *Prepare a support case for Virto about the failing import.*
* *We changed the connection string in the database. Reset the cache.*

Or pick a routine: MCP clients show the prompts as slash commands, for example `/mcp__virto__daily_health_check` in Claude Code.

| Prompt | What it does |
| --- | --- |
| `daily_health_check` | Health, new errors, failed jobs, capacity and configuration, with a short action list |
| `production_readiness` | Go or no-go for production: blockers, risks and a checklist for the deployment |
| `capacity_review` | Are the resources enough; scale up, scale out, or fix a hotspot first |
| `incident_rca` | Root cause of an incident: timeline, errors, whose code, cause class, mitigation and fix |
| `is_it_us_or_virto` | For one error: the solution's own code, Virto's code, data, configuration, infrastructure or an external service |
| `prepare_support_case` | Collects the evidence and prepares a report for Virto support, for you to review and send |

A typical exchange:

> **You:** Restoring a backup in the back office fails with an error. Is it our code or Virto's?
>
> **Agent** (calls `search_errors`, then `explain_error`): The error `33eace179ad0` occurred 3 times in the last hour on instance web-1, last trace id `f799…648d`. It is thrown in `VirtoCommerce.BackupRestore` 3.1008.0, `BackupBlobUrl.GetSafe`, and no module of yours is on the stack. Virto's code refused its input: *Invalid path /assets/no-such-backup.zip*. Likely cause: data (the file path the user chose), medium confidence. Check that the backup file exists in the assets; if it does, I can prepare a support case for Virto.

Every answer names the instance it was measured on, because a call reaches one instance of the cluster. A restart asked for in the chat is not executed at once: the agent returns the change id, and an approver applies or rejects it in **Agent approvals**.

### Tools

| Layer | Tool | Answers |
| --- | --- | --- |
| Orientation | `get_environment_profile` | Versions, environment, providers, Redis, the instance, known instances, license, modules split into Virto's and the solution's own |
| Orientation | `get_platform_health` | The platform's health checks (database, cache, Redis, modules, search and the checks modules add), with findings |
| Orientation | `get_incident_timeline` | Instance starts (restarts, deployments), first occurrences of errors, failed jobs, in time order |
| Finders | `search_errors` | Errors grouped by fingerprint, with counts in a window, trace ids and sources |
| Finders | `list_modules` | Modules with origin (`virto` or `own`); `failed` with their errors |
| Finders | `list_failed_jobs` | Job counts, job servers, queues and the latest failed jobs |
| Analysis | `explain_error` | The stack mapped to the platform, Virto modules and the solution's own modules; the likely cause class with confidence and the next checks |
| Analysis | `check_configuration` | Production-readiness rules with evidence and the fix |
| Analysis | `check_capacity` | CPU, memory, garbage collection, thread pool, database latency and the job backlog against limits, with one recommendation |
| Analysis | `check_pending_migrations` | Applied and pending EF Core migrations of every database context |
| Hand-off | `prepare_support_case` | A Markdown report for Virto support with secrets and personal data removed; nothing is sent |
| Actions | `reset_platform_cache` | The back office's Reset cache; the agent's client asks the user to confirm |
| Actions | `restart_platform` | The back office's Restart; submitted for approval by a second person, with a preview |

Every read tool needs `systemoperations:diagnostics:read`, so a user without it does not see them. The actions need the permissions of the back-office operations they repeat. Tool calls are audited by the MCP module, and tool switches in **Agent tools** turn any of them off.

### Where the data comes from

| Data | Source | Kept |
| --- | --- | --- |
| Errors | A Serilog sink added through `ILoggerConfigurationService` records errors, and warnings with an exception, of every logger | In memory, per instance, since its start: up to 500 fingerprints, the last 200 times of each |
| Health | The platform's `HealthCheckService` | Read on each call |
| Instances and jobs | The Hangfire monitoring API: each platform process that processes jobs is a server with a heartbeat | Read on each call |
| Capacity | .NET runtime counters of the process: CPU over a sample, working set against the GC memory limit (the container limit when there is one), GC pauses, thread pool | Read on each call |
| Modules | `IModuleService` and the module folders; a module is Virto's when its id starts with `VirtoCommerce.` and Virto Commerce owns or authors it | Read on each call |
| License | The platform's license provider: type and expiration date only | Read on each call |

**What it does not do yet.** The error journal is per instance and starts empty on every start, so on a cluster `search_errors` shows the instance the call reached; each answer names it. The platform does not log GraphQL resolver errors, so they do not reach the journal. Request latency per endpoint, longer history and telemetry backends (Application Insights, Seq) are not read.

**Privacy.** Every message, stack trace and value a tool returns passes a redactor: passwords and keys in connection strings, bearer tokens, JWTs, credentials in URLs, emails and card numbers are replaced with `***`. The configuration rules read connection strings and certificate settings but never return their values.

## Architecture

This module is primarily a frontend that calls existing platform APIs directly from the browser. It also adds **one backend endpoint of its own** — the migration export (see [Migration export](#migration-export)) — implemented as a `Core` / `Data` / `Web` vertical slice. The platform-API calls it makes:

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
| Export Migrations | `GET /api/system-operations/migrations/export` (**this module**) |

Long-running operations (Export, Import, Sample Data Install) use Hangfire background jobs on the platform side. Progress is tracked by polling the push notification search endpoint (`POST /api/platform/pushnotifications` with `{ ids: [notificationId] }`) every 2 seconds until the notification's `finished` field is set.

The UI is a Vue.js 3 + TypeScript + Vite application, served via the platform's `<apps>` mechanism and registered as a Developer Tool via `IDeveloperToolRegistrar`. The app supports 13 languages matching the platform's localization.

It is also a **Module Federation host** — see the next section.

## Migration export

The **Export Migration Scripts** card downloads the EF Core migration SQL that platform startup would apply, so you can review it and run it manually on another environment before that environment starts.

- **Endpoint:** `GET /api/system-operations/migrations/export?mode=idempotent|pending` → `migration-scripts.zip` (permission `systemoperations:migrations:export`).
- **How it works:** the running platform already has every `DbContext` (platform + security + all installed modules) registered in DI. The exporter enumerates them and scripts each with EF Core's own `IMigrator.GenerateScript` — using each context's live provider, so the SQL is correct for **SQL Server, PostgreSQL and MySQL**. It never calls `Migrate()` and never opens the database in idempotent mode.
- **Multi-database aware:** Virto supports per-module connection strings (a context resolves `ConnectionStrings:<ModuleId>` with a `VirtoCommerce` fallback; Security uses `Auth:ConnectionString`). The export reads each context's target from the live connection (`DbConnection.DataSource` + `.Database` — no credentials) and groups output per database.
- **ZIP contents:** `<Context>.sql` per context; `_combined.<database>.sql` per target database (apply each to its database); `_databases.md` / `_databases.json` mapping context → provider → server → database → files; and `vc-package.json` (platform version + installed modules) capturing the scope that produced the scripts.
- **Modes:** `idempotent` (default) — full self-guarding script, safe at any target state, the Microsoft-recommended production artifact; `pending` — only migrations not yet applied to the connected database (falls back to idempotent if the DB is unreachable).
- **Note:** MySQL idempotent scripts contain `DELIMITER` directives — run them with a MySQL client that honors them (`mysql` CLI, Workbench).

Backend layout: `VirtoCommerce.SystemOperations.Core` (`IMigrationScriptExporter`, `MigrationExportMode`, permissions), `VirtoCommerce.SystemOperations.Data` (`MigrationScriptExporter`), `VirtoCommerce.SystemOperations.Web` (module + controller). The diagnostics services of the [AI agents toolset](#ai-agents-mcp) are in `Core` (`Diagnostics`) and `Data` (`Diagnostics`), and its tools and prompts in `VirtoCommerce.SystemOperations.MCP`, the only project that references the MCP SDK. This is the module's first backend assembly (its `assemblyFile`/`moduleType` are now enabled in `module.manifest`).

For a plain-language, non-technical summary, see [docs/migration-export-business-overview.md](docs/migration-export-business-overview.md). For full technical details — modes, providers, multi-database mapping, API and usage — see [docs/migration-export.md](docs/migration-export.md).

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

