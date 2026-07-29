# Migration Export

## Problem

Virto Commerce applies EF Core database migrations *only* at web startup, programmatically, via `Database.Migrate()` — for the platform, security, and every installed module. There is no built-in way to **see the SQL that would run before it runs**. Teams preparing a production or regulated environment need to review, approve, and often hand-apply schema changes with a DBA before the application starts. `dotnet ef migrations script` cannot help here: it needs buildable `.csproj` projects and cannot see the installed modules in a deployed binaries folder.

## Goal

Give operators a one-click way, from the **System Operations → Developer Tools** page, to download the exact migration SQL that startup would apply — for **all installed modules** — so they can:

- review the schema changes,
- run them manually on **another environment before it starts**, and
- keep an auditable, per-database record of what was applied.

This matches the approach Microsoft [recommends for production](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying): deploy migrations as reviewable SQL scripts rather than calling `Database.Migrate()` at runtime, and use **idempotent** scripts when the target database may be at any migration state.

## What it produces

A `migration-scripts.zip` containing:

| Entry | Contents |
|---|---|
| `<Context>.sql` | One script per DbContext — `Platform`, `Security`, and one per installed module (`Catalog`, `Order`, …). |
| `_combined.<database>.sql` | All contexts that target the **same database**, concatenated in order — apply one file per database. |
| `_databases.md` / `_databases.json` | Mapping of **context → provider → server → database → script files**. The "which module lives in which database" report. |
| `vc-package.json` | The **platform version and installed modules (with versions)** — the scope of the platform that produced these scripts, so the SQL and its origin travel together. |

Every script is prefixed with a header noting the context, provider, target server/database, and mode.

## Modes

- **Idempotent** (default) — a full, self-guarding script per context: each migration is wrapped in an `__EFMigrationsHistory` existence check, so it is safe to run against a database at **any** migration state. This is the artifact to hand to another environment. Generated offline (no database connection needed) for SQL Server and PostgreSQL.
- **Pending** — only the migrations not yet applied to the *connected* database (a delta computed from its history). Anchored on the last applied migration that exists in the deployed assembly, so a database that is ahead of the binaries does not fail. Falls back to an idempotent script if the database is unreachable. Available on the API (`?mode=pending`); not surfaced in the UI.

## Database providers

The export scripts each context through its **live provider**, so the SQL is automatically correct for the target:

- **SQL Server** — `IF NOT EXISTS (SELECT … __EFMigrationsHistory …)` guards.
- **PostgreSQL** — `DO $$ … $$` blocks.
- **MySQL** — stored-procedure + `DELIMITER` wrapper. Run these with a client that honours `DELIMITER` (`mysql` CLI, MySQL Workbench). Note: the MySQL provider connects when its options are built, so the MySQL server must be reachable for the export even in idempotent mode; SQL Server / PostgreSQL are fully offline.

## Multi-database awareness

Virto Commerce supports per-module connection strings: a module resolves `ConnectionStrings:<ModuleId>` and falls back to `ConnectionStrings:VirtoCommerce`; security uses `Auth:ConnectionString`. The export reads each context's **actual** target from its live connection (`DbConnection.DataSource` + `.Database`) — provider-agnostic and without reading the secret-bearing connection string — and groups the combined scripts per database. No credentials appear anywhere in the output.

## API

```
GET /api/system-operations/migrations/export?mode=idempotent|pending
→ 200 application/zip  (migration-scripts.zip)
```

- Permission: `systemoperations:migrations:export`.
- Read-only: it enumerates the already-registered DbContexts, scripts them, and returns the ZIP. It **never** calls `Migrate()` and never starts anything.

## How it works

Because the module runs **inside the live platform host**, every `DbContext` is already registered in DI. The exporter enumerates the registered contexts, obtains each one's `IMigrator`, and calls `GenerateScript`. There is no module re-loading or assembly juggling — the running host is the source of truth. A failure scripting any single context is logged and skipped; the rest of the archive is still produced.

## Backend layout

The feature follows the standard Core / Data / Web vertical slice — this is the module's first backend assembly (its `assemblyFile` / `moduleType` are enabled in `module.manifest`):

- **`VirtoCommerce.SystemOperations.Core`** — `IMigrationScriptExporter`, `MigrationExportMode`, `ModuleConstants` (permissions).
- **`VirtoCommerce.SystemOperations.Data`** — `MigrationScriptExporter` (the engine; references `Microsoft.EntityFrameworkCore.Relational`).
- **`VirtoCommerce.SystemOperations.Web`** — `Module` (registers the service + permission) and `SystemOperationsMigrationsController` (the endpoint).

## Frontend

An **Export Migration Scripts** operation card in the *Diagnostics & Export* section triggers the download (`ExportMigrations.vue` + a binary-aware `downloadFile` helper in `useApi.ts`). The card is gated by the `systemoperations:migrations:export` permission.

## Usage

1. Open **Developer Tools → System Operations**.
2. In *Diagnostics & Export*, click **Export** on the *Export Migration Scripts* card.
3. Review `_databases.md` to see which script applies to which database, then review the individual `.sql` files.
4. Apply each `_combined.<database>.sql` to its database on the target environment **before** starting the platform there.
