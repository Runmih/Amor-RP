# Deployment, operations and data recovery

Host: Render web service plus managed PostgreSQL, one region, one initial API
instance. The maintainer reported M1 deployment/login testing passed; this
workspace has no hosted credentials. M2 upgrade configuration is in the [guide](m2-install-test.md). Application and database
remain portable through Docker and standard PostgreSQL.

Follow the [version matrix](../engineering/versions.md). Explicitly provision
PostgreSQL major 18, record its actual hosted minor version, and align local/test
databases with that major. Pin the server's .NET 10 build/runtime images and
digests; rebuild for serviced patches. Record source commit, image digest, SDK,
runtime, driver/provider, database and schema versions for each deployment.
Use compatible PostgreSQL 18 backup tools and repeat restore drills for upgrades.

## Render setup

- Paid web compute for persistent beta/1.0; paid PostgreSQL before users depend on
  holdings. Free server sleeps after 15 idle minutes and free database expires
  after 30 days. Filesystem is ephemeral; do not persist assets or SQLite locally.
- Docker build context must be repository root so Server can reference Core and
  Contracts. Build filters include all three projects and deployment/toolchain files;
  a Server-only root directory would exclude shared files.
- Listen on Render's assigned port, trust only configured reverse proxies, expose
  health routes, terminate public TLS using trusted hosting certificates. Use DNS URL.
- Keep database in same region/private network. Restrict public database exposure,
  use dedicated credentials and controlled migration authority.
- Configure exact XIVAuth callback URL for each environment. Separate test and
  production provider app/session keys/data; no copied production user database.
- Secrets: database connection, provider client credentials, encryption/signing keys.
  Nonsecrets: issuer, client ID, callback/base URL, policy ceilings, maintenance state.
- Production root URL may use `onrender.com` initially; custom URL is optional.
  Plugin backend is configurable and credentials scoped to exact backend authority.

Current rough minimum compute budget is around US$13/month for smallest paid API
and Postgres, excluding storage/usage/tax. Larger workloads and a second staging
environment cost more. Recheck current plans before provisioning; no capacity
guarantee follows from that price. Monitor usage, configure available billing
alerts/build limits, and assign a human owner for charges. See [sources](../sources.md).

## Deployment sequence

1. Build/test immutable artifact from reviewed commit; attach matching source/version.
2. Backup/export before destructive migration. Apply reviewed migration in controlled
   step, never as a race between multiple app instances.
3. Additive schema before code requiring it; preserve compatibility with deployed
   clients and previous server binary. Defer destructive cleanup to later release.
4. Deploy, verify readiness/database/version endpoint and authentication callback.
5. Smoke-test login, production, use and a two-character trade in test group.
6. Watch error rate, latency, active reservations, memory, storage and auth failures.
   Roll back code only if schema remains compatible; otherwise restore/forward-fix.

`/health/live` reports process liveness; `/health/ready` verifies required database
and startup migration compatibility. Neither exposes infrastructure/secret details.
XIVAuth temporary outage degrades login/renewal, not process liveness. Maintenance
mode rejects new mutations with a clear message; reads/outcome recovery remain
available where possible. Close/reconnect sockets gracefully during rollout.

## Backups and incident recovery

Paid Render Postgres supports point-in-time recovery; current documented Hobby
window is three days. Maintain an encrypted daily independent logical export with
bounded retention (recommended 30 days) and access controls. Test restoration into
an isolated database before beta, then regularly and after schema changes.

Initial targets: independent-export RPO <=24 hours; restoration RTO <=4 hours.
Measured drills must confirm or revise these targets. Restoring to an older point
can undo completed RP trades; communicate the recovery point and invalidate sessions
to prevent stale command replay. Never silently claim that every later trade survived.

Incident steps: enter maintenance, preserve relevant redacted diagnostics, revoke
compromised sessions/keys as appropriate, determine affected groups, backup current
state, restore/repair in isolation, verify accounting/reservations, publish clear
status to affected users, then reopen mutations. No ad-hoc balance edits without
an audited correction operation.

Monitor: HTTP error/latency, login failure rates, database usage/connections, trade
expiry/reservation age, quota rejection, operation replay mismatch, socket reconnect
rate and storage growth. No letter-body logging. Use configurable cleanup jobs for
retention, stale attempts/exports/events and soft-delete purge. Runtime checks must
enforce expiry even if cleanup is delayed.

## Distribution and support

Closed beta recommendation: versioned ZIP and custom Dalamud repository manifest,
with changelog, API compatibility range, source links and rollback instructions.
Pin supported SDK/toolchain in tracked files. Fresh installation and update must be
tested by a human. Source/license material follows existing AGPL obligations.

An official repository submission is a separate gate. Current Dalamud policies
require human understanding/testing and AI-use disclosure; AI disclosures/submission
text must be human-written. A working custom-distributed 1.0 is not automatically
an approved official plugin. No external maintainer contact is sent by this task.

Before testers: publish operator/support contact, data/privacy notice, retention,
export/deletion route, known limits, outage and maintenance notices. Groups are
closed but do not constitute encrypted private messaging. Owner moderation governs
alt usage and member trading. Assign responsibility for service bills and updates.

## M2 secrets and schema upgrade

Keep the encryption key ring separate from DB backups and include it in secure
recovery instructions. Add keys by ID; never overwrite an old key still referenced
by envelopes/backups. The M2 migration is additive to the M0 bootstrap. Deploy the
reviewed migration through the existing pre-deploy command before the new API.
Probe callbacks and product callbacks have separate options; disable the old
probe surface when it is no longer needed. CLI local native execution can use
`deploy/.env.example` for variable names without storing real values in source.

## M3 inventory/media upgrade

Use [M3 setup](m3-install-test.md) and branch `m3-inventory`. Preserve the database
and existing vault/XIVAuth secrets. Back up before the additive PlayableInventory
migration; deploy migration before serving the new binary. Currency icons use
small normalized PostgreSQL assets and are included in database backups. Old
media bytes are deleted atomically after a successful replacement; receipts
retain identifiers, not historical image content. Restore a matching DB/binary
pair if rollback is needed. Existing Render resources suffice; no new service
or storage account is provisioned by M3.
