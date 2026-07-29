# Export Migration Scripts — Feature Overview

*A plain-language overview for business and operations stakeholders. For the technical reference, see [migration-export.md](migration-export.md).*

## In one sentence

From the platform's **Developer Tools** page, one click downloads the exact database changes the platform would make on startup — so your team can **review and approve them, and apply them on another environment before go-live**, instead of letting them happen automatically and unseen.

## The problem it solves

Every Virto Commerce release can include **database schema changes** ("migrations") — new tables, columns, or indexes needed by the platform and its modules. Normally these are applied **automatically the moment the application starts**.

That's convenient for development, but for **production and regulated environments** it creates a control gap:

- No one gets to **see or approve** the change before it happens.
- Applying schema changes to a live database is a **high-risk, hard-to-reverse** operation.
- Database administrators (DBAs) and change-control processes are **bypassed**.

## What the feature does

It hands you the database changes **as reviewable SQL files, up front** — without touching any database:

- Covers the **whole platform**: the core platform, security, and **every installed module**.
- Produces a downloadable **ZIP package** you can read, share, archive, and hand to a DBA.
- Changes **nothing** — it only reads and writes files. Safe to run against production.

## Business value

| Benefit | What it means for you |
|---|---|
| **Control & approval** | Review and sign off on schema changes *before* they reach production, through your normal change process. |
| **Lower risk go-lives** | Apply pre-reviewed, tested scripts in a controlled window — no surprise changes at startup. |
| **Compliance & audit** | A concrete, archivable record of exactly what changed and where — useful for audits and regulated industries. |
| **Portability across environments** | Export from one environment and apply to another (e.g. staging → production) before it starts. |
| **Clarity on scope** | The package shows **which module's data lives in which database**, plus the platform version and installed modules, so nothing is ambiguous. |
| **Industry best practice** | This is the approach **Microsoft recommends** for production databases — deploy migrations as reviewable scripts rather than automatically at startup. |

## Who it's for

- **Solution Architects & DevOps** preparing a new environment or an upgrade.
- **Database Administrators** who need to review and apply changes on their terms.
- **Delivery & Compliance teams** who need an auditable record of database changes.

## How it's used (three steps)

1. Open **Developer Tools → System Operations** and click **Export** on the *Export Migration Scripts* card.
2. Review the downloaded package — a summary shows which changes apply to which database.
3. Hand the scripts to your DBA (or run them yourself) on the target environment **before** starting the platform there.

## What's in the package

- One SQL file per area (platform, security, and each module).
- One combined script **per database**, ready to apply to that database.
- A **database map** — which module's data goes to which database/server.
- A **scope file** — the platform version and the list of installed modules that produced these scripts.

## Access & safety

- Available only to users with the appropriate **Developer Tools** permission.
- Read-only: it never changes the database and never starts the application.
- No passwords or secrets are included in the package — only server and database names for identification.

## What it does *not* do

- It does **not** apply changes for you — applying remains a deliberate, controlled step.
- It is **not** a data backup or migration of *records*; it covers **schema** (structure) only.

## Availability

Works on the platform's lowest supported release (**Stable 14**) and later. The optional plug-in extensions on this page require a newer platform, but the migration export itself works everywhere.
