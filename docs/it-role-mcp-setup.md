# IT role and System Operations agent: setup and test run

How we set up an IT operator to run a Virto Commerce platform with an AI agent over the back-office MCP endpoint `/admin/mcp`, with the [System Operations](../README.md#ai-agents-mcp) toolset, and how we tested it. Every step was run on the `vcst-qa` environment on 2026-11-07 and 2026-11-08; repeat it on another environment by replacing the hosts, or use the timings and results below as the baseline for optimizing the setup.

| | vcst-qa |
| --- | --- |
| Back office | `https://vcst-qa.govirto.com` |
| MCP endpoint | `https://vcst-qa.govirto.com/admin/mcp` |
| Platform | 3.1096.0, two instances |
| Modules | `VirtoCommerce.MCP` 3.1000.0-alpha.51-vcst-6155-core-mcp-module, `VirtoCommerce.SystemOperations` 3.1003.0-pr-5-9ea0, `VirtoCommerce.UCP` 3.1011.0-pr-13-68e1 |

Nothing here needs a secret in a document: passwords and tokens stay in the person's password manager or in a local file that is never committed.

## 1. Check the prerequisites

| Check | How | vcst-qa |
| --- | --- | --- |
| Modules installed, no errors | `GET /api/platform/modules`: `VirtoCommerce.MCP` and `VirtoCommerce.SystemOperations` with `isInstalled: true` and no `validationErrors` | OK |
| Permissions registered | `GET /api/platform/security/permissions` lists `systemoperations:diagnostics:read`, `cache:reset`, `platform:module:manage`, `mcp:approve`, `mcp:audit:read` | OK |
| The endpoint is an OAuth resource | `Authorization:Resources` contains `https://<platform>/admin/mcp`. Check: a token request with `resource=https://<platform>/admin/mcp` must not fail with `invalid_target` | OK after [vc-deploy-dev#7057](https://github.com/VirtoCommerce/vc-deploy-dev/pull/7057); before it, `invalid_target` |
| The endpoint knows its public origin | `MCP:PublicOrigin` (or `MCP:Scopes:admin:PublicOrigin`) is `https://<platform>`. Outside Development the endpoint refuses every token without it: `401 invalid_token`, "This MCP server has no public origin configured (MCP:PublicOrigin)", because it cannot check the token's audience. Check: an MCP request with a valid agent token answers `200`, not that `401` | OK after [vc-deploy-dev#7060](https://github.com/VirtoCommerce/vc-deploy-dev/pull/7060); before it, that `401` |
| The platform host signs the user in on its own login page | Only when `Authorization:OAuthLoginPath` is set, which a storefront that hosts the sign-in page needs: list the platform host in `Authorization:OAuthLoginPaths` without a path. The setting is read from platform 3.1096.0 ([vc-platform#3121](https://github.com/VirtoCommerce/vc-platform/pull/3121)). Otherwise the agent's sign-in ends on a 404 at `https://<platform>/oauth/authorize`. Check: `GET https://<platform>/connect/authorize?...` without a session redirects to `https://<platform>/?ReturnUrl=/connect/authorize...`, the back-office login page, and `GET https://<platform>/connect/session` answers `404` | OK after [vc-deploy-dev#7065](https://github.com/VirtoCommerce/vc-deploy-dev/pull/7065) and platform 3.1096.0; before, the 404 |
| An OAuth client for the agent | **Security → OAuth applications**: a *Public* client with PKCE, *Explicit* consent, grant types *Authorization code* and *Refresh token*, the agent's redirect URI, and the resource `https://<platform>/admin/mcp` ticked | OK: `claude-admin-qa` ("Claude — Back Office (QA)"), redirect `http://localhost:65111/callback` for Claude Code |

On a Virto Cloud environment, the resource is a line in the environment's `infra/environments.yml` of the deployment repository:

```yaml
      Authorization__Resources__2: https://vcst-qa.govirto.com/admin/mcp
      MCP__PublicOrigin: https://vcst-qa.govirto.com
      Authorization__OAuthLoginPaths__0__Host: vcst-qa.govirto.com   # only with Authorization__OAuthLoginPath
```

Use the next free index of `Authorization__Resources` and `Authorization__OAuthLoginPaths` if the lists already have entries, and deploy the environment. The first line lets the platform issue a token for the endpoint, the second lets the endpoint accept it, and the third lets the operator sign in on the platform host while the storefront keeps its own sign-in page. The host goes in the value, not in the variable's name: Virto Cloud drops a variable whose name has the host's dots in it, which is why `Authorization__OAuthLoginPaths__vcst-qa.govirto.com` never reached the platform ([#7062](https://github.com/VirtoCommerce/vc-deploy-dev/pull/7062), [#7063](https://github.com/VirtoCommerce/vc-deploy-dev/pull/7063)).

## 2. Create the role

**Security → Roles → Add**, or `PUT /api/platform/security/roles`:

| Field | Value |
| --- | --- |
| Name | `IT Platform Operator` |
| Description | Runs the platform with an AI agent over /admin/mcp: System Operations diagnostics, cache reset, a restart as a request for approval, and the MCP audit log. |
| Permissions | `systemoperations:diagnostics:read`, `cache:reset`, `platform:module:manage`, `mcp:audit:read` |

```json
{
  "name": "IT Platform Operator",
  "description": "Runs the platform with an AI agent over /admin/mcp: System Operations diagnostics, cache reset, a restart as a request for approval, and the MCP audit log.",
  "permissions": [
    { "name": "systemoperations:diagnostics:read" },
    { "name": "cache:reset" },
    { "name": "platform:module:manage" },
    { "name": "mcp:audit:read" }
  ]
}
```

Why each permission:

- `systemoperations:diagnostics:read`: every read tool and prompt of System Operations. Without it the user does not see them.
- `cache:reset`: `reset_platform_cache`, the back office's **Reset cache**.
- `platform:module:manage`: `restart_platform`. The agent does not restart: it submits the restart for approval.
- `mcp:audit:read`: the operator can follow the agent's calls in **Agent audit log**.
- Not given: `mcp:approve`. A restart is approved by **another** person (self-approval is off by default, `MCP.Approval.AllowSelfApproval`), so the approver has a role of their own. Not given either: `mcp:manage` (switching tools off, everyone's agents), which stays with the administrator.

Check: `GET /api/platform/security/roles/IT%20Platform%20Operator` returns the four permissions. The role search (`POST .../roles/search`) does not return permissions, so read the role by name.

## 3. Create the user

**Security → Users → Add**, or `POST /api/platform/security/users/create`:

| Field | Value |
| --- | --- |
| User name | `it-operator-qa` |
| Email | `it-operator-qa@example.com` (a test address) |
| Type | `Manager` (a back-office user, not an administrator) |
| Roles | `IT Platform Operator` |
| Password | generated, 25 characters, kept in a password manager (here: a local file outside every repository) |

```json
{
  "userName": "it-operator-qa",
  "email": "it-operator-qa@example.com",
  "password": "<generated>",
  "userType": "Manager",
  "isAdministrator": false,
  "emailConfirmed": true,
  "roles": [ { "id": "<role id>", "name": "IT Platform Operator" } ]
}
```

Check: `GET /api/platform/security/users/it-operator-qa` shows type `Manager`, not an administrator, the role, not locked; and the user can sign in to the back office.

## 4. Connect the agent

The IT operator connects their agent to the endpoint with the client id from step 1. For Claude Code:

```bash
claude mcp add --transport http --client-id claude-admin-qa --callback-port 65111 virto-qa https://vcst-qa.govirto.com/admin/mcp
```

Then `/mcp`, select `virto-qa`, **Authenticate**: the back-office sign-in opens, the operator signs in as `it-operator-qa`, and the consent page asks to allow "Claude — Back Office (QA)" once. Other agents (Claude Desktop, claude.ai, ChatGPT, VS Code) are in the MCP module's [Connect an AI agent](https://github.com/VirtoCommerce/vc-module-mcp#connect-an-ai-agent).

**The same sign-in without a browser**, for repeatable tests: it is what the back-office login page and the agent do, step by step, on a platform host that uses the built-in login page (step 1).

| # | Request | Result |
| --- | --- | --- |
| 1 | `GET /connect/authorize?response_type=code&client_id=claude-admin-qa&redirect_uri=...&code_challenge=<S256>&code_challenge_method=S256&resource=https://<platform>/admin/mcp&scope=openid profile offline_access` without a session | `302` to `https://<platform>/?ReturnUrl=/connect/authorize?...`: the back-office login page |
| 2 | `POST /api/platform/security/login` with `userName` and `password`, as the login page does | `200` and the sign-in cookie |
| 3 | The request of step 1 again, with the cookie | The consent page (explicit consent), or `302` to the redirect URI with `code` when the user consented before |
| 4 | `POST /connect/authorize` with the page's hidden fields and `submit.Accept=Allow` | `302` to the redirect URI with `code` and `iss` |
| 5 | `POST /connect/token` with `grant_type=authorization_code`, the code, the PKCE verifier and the resource | The agent's token for `/admin/mcp`, 30 minutes, with a refresh token |

Two details:

- The consent page renders the antiforgery field as `<input name="__RequestVerificationToken" type="hidden" value="...">`: read hidden inputs whatever the order of their attributes. A form whose antiforgery token has expired answers `302` to the same authorization request and shows the page again, not a `400`.
- A host that keeps the storefront's sign-in page (`OAuthLoginPath`) signs in through `/connect/session` instead, and a back-office cookie does not help there. The contract of that page is in the platform's [OAuth sign-in and consent](https://github.com/VirtoCommerce/vc-platform/blob/dev/docs/techniques/oauth-sign-in-and-consent.md#host-the-sign-in-page-in-a-front-end).

On vcst-qa:

- Before [#7057](https://github.com/VirtoCommerce/vc-deploy-dev/pull/7057), the authorization request failed with `invalid_target`.
- After it, a token was issued for `https://vcst-qa.govirto.com/admin/mcp`, client `claude-admin-qa`, scopes `openid profile offline_access`, with `iss` in the authorization response (RFC 9207). **Connected agents** listed the agent at once, but its MCP calls failed with `401 invalid_token` until `MCP:PublicOrigin` was set ([#7060](https://github.com/VirtoCommerce/vc-deploy-dev/pull/7060)).
- Claude Code's **Authenticate** ended on `404` at `https://vcst-qa.govirto.com/oauth/authorize?returnUrl=/connect/authorize?...`: with one `OAuthLoginPath` for the whole platform, the platform host sent the operator to the storefront's sign-in page, which only the storefront host serves. Fixed by platform 3.1096.0 with the platform host in `OAuthLoginPaths` ([#7065](https://github.com/VirtoCommerce/vc-deploy-dev/pull/7065)).
- On 3.1096.0 the five steps pass, 3 s in all: the redirect to the back-office login page, `/connect/session` answering `404` on the platform host, then the code with `iss` and the token.

## 5. Scenarios

As the IT operator, with the agent's token from step 4. The administrator's part (rejecting the restart, reading the audit) uses the administrator's own token on the REST API.

| # | Scenario | Tools or prompt | Expected |
| --- | --- | --- | --- |
| 1 | What can I do here | `tools/list`, `prompts/list` | The 11 read tools, `reset_platform_cache`, `restart_platform` and the 6 prompts of System Operations; nothing the role does not allow |
| 2 | Daily health check | prompt `daily_health_check` | Health, new errors, failed jobs, capacity and configuration, with an action list and the instance |
| 3 | Is it our code or Virto's | `search_errors`, `explain_error` | An error mapped to the platform, Virto's modules or the solution's own, with a cause class and confidence |
| 4 | Are the resources enough | `check_capacity` | CPU, memory, GC, thread pool, database latency, job backlog, one recommendation |
| 5 | Production readiness | `check_configuration`, `check_pending_migrations`, `list_modules` | Rules with evidence and fixes |
| 6 | Incident timeline | `get_incident_timeline` | Restarts, first occurrences of errors, failed jobs, in time order |
| 7 | Support case | `prepare_support_case` | A Markdown report with secrets and personal data removed; nothing sent |
| 8 | Reset the cache | `reset_platform_cache` | Runs; audited |
| 9 | Restart | `restart_platform` | Not executed: a pending change id; another person rejects it in **Agent approvals** (on a shared environment we reject, not approve) |
| 10 | Audit and agents | **Agent audit log**, **Connected agents** | Every call with `it-operator-qa` and `claude-admin-qa`; the agent listed |

## 6. Results

Times are the client's, from the request to the answer, over the internet; the audit times are the server's.

- Scenarios 1–8: 2026-11-08 at 13:50 UTC, with the platform and module versions of the header.
- Scenarios 9–10: the earlier run of 2026-11-08 at about 08:20 UTC, on platform 3.1095.0 with `VirtoCommerce.MCP` 3.1000.0-pr-1-b150 and `VirtoCommerce.SystemOperations` 3.1003.0-pr-5-c351. They need the administrator's token to reject the restart, and were not repeated: a restart left pending on a shared environment can be approved by someone else.

| # | Result | Time | What we saw |
| --- | --- | --- | --- |
| 1 | Pass | 1.4 s, 0.9 s | 18 tools: the 11 read tools, `reset_platform_cache`, `restart_platform`, and the MCP module's `list_pending_changes`, `get_pending_change`, `reject_pending_change`, `get_job_status`, `cancel_job`. No `approve_pending_change`: the role has no `mcp:approve`. The 6 prompts |
| 2 | Pass | 1.6 s, 1.0 s, 1.5 s | `get_environment_profile` (2 known instances), `get_platform_health`, `list_failed_jobs`: no failed and 235,734 succeeded jobs, two Hangfire servers with 10 workers in all. On the earlier run, `list_failed_jobs` took 35 s on the server on its first call after the instances started, 1 s on the next ones. Prompt `daily_health_check` renders |
| 3 | Pass | 0.9 s, 2.1 s | `search_errors` over 24 hours: no errors in the journal of the instance reached. `explain_error` with a pasted stack trace: thrown in `VirtoCommerce.Catalog` 3.1051.0, `own_code_on_stack: false`, cause class `platform_code`, confidence medium, with the next checks. Prompt `is_it_us_or_virto` renders |
| 4 | Pass | 3.0 s | `check_capacity`: CPU, memory, GC, thread pool, database and job figures and a recommendation. Prompt `capacity_review` renders |
| 5 | Pass | 0.9 s, 6.6 s, 0.9 s | `check_configuration`, `check_pending_migrations` (it reads every DbContext), `list_modules`: 91 modules, read in two pages of 50 and 41 with `skip`. Prompt `production_readiness` renders |
| 6 | Pass | 1.1 s | `get_incident_timeline` over 24 hours. Prompt `incident_rca` renders |
| 7 | Pass | 1.0 s | `prepare_support_case`: a Markdown report and a note that nothing was sent; no password in it. Prompt `prepare_support_case` renders |
| 8 | Pass | 0.9 s | `reset_platform_cache` ran and returned the instance and the reason. The test client declares no elicitation, and the tool's confirmation falls back to *Allow* (`McpConfirmationFallback.Allow`), so the client's own tool approval is the only confirmation. Claude Code asks it before every call of a tool not allowed in advance |
| 9 | Pass | 1.5 s | `restart_platform`: `pending_approval` with a change id, expiring in 3 days; nothing restarted. The administrator rejected it: `Rejected` |
| 10 | Pass | | **Agent audit log**: 12 entries of `it-operator-qa`, every one with client `claude-admin-qa`; `restart_platform` as `PendingApproval` with the change id, the others `Succeeded`. **Connected agents**: "Claude — Back Office (QA)", `it-operator-qa`, resource `https://vcst-qa.govirto.com/admin/mcp`, last used at the run |

Every prompt renders in 0.8–1.2 s when its required arguments are passed.

Found on the way:

- `list_modules` returned only the first 50 of the 91 modules and had no way to read the rest, so an agent could not see UCP or System Operations. Fixed in System Operations 3.1003.0-pr-5-9ea0: the tool takes `skip`.
- `prompts/get` without a required argument (`symptom` of `incident_rca`, `error` of `is_it_us_or_virto`) answers `-32603 An error occurred.` instead of an invalid-parameters error; still so on 3.1096.0. `prompts/list` marks the arguments as required, so an agent passes them; the answer comes from the MCP SDK.
- An expired token is not an error on `GET /api/platform/security/currentuser`: it answers `200` as an anonymous user. Check a token on an endpoint that requires authorization, such as `POST /api/mcp/audit/search`, which answers `401` with "The specified token is no longer valid".

To optimize next: the setup took six deployments of the environment (#7057, #7060 to #7063, #7065), because the settings were found one at a time and the first form of the per-host login path did not survive Virto Cloud. Set the three lines of step 1 in one change, on platform 3.1096.0 or later; then the run is role, user, sign-in, scenarios: about 2 minutes with the scripts.
