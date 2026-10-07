# Security and privacy model

This is a design requirement, not an assertion that the prototype is secure.
Threats include modified clients, stolen sessions, cross-group ID substitution,
concurrent duplication, stale trade confirmations, resource exhaustion, and malicious
item text. Group owners intentionally control their fictional economy and alt use.

## Trust boundaries

- Plugin/UI requests are untrusted. Names, world IDs, balance displays, permission
  claims, quota estimates and item IDs are hints until server validation.
- XIVAuth provides authenticated, verified character identity. The integration
  must validate credentials/claims; receiving an identifier alone is insufficient.
- Group ownership grants explicit administrative power, not unrestricted backend
  execution or automatic access to private letter bodies.
- PostgreSQL and server secret configuration are privileged. Game chat is an
  external side effect with no distributed transaction or reliable receipt API.
- No collection of other players' Square Enix account IDs, alt correlation, global
  plugin-user directory, or remote-code execution. Per-character quota is intentional.

## Required controls

| Concern | Required control | Verification |
| --- | --- | --- |
| Spoofed character | Server-controlled XIVAuth exchange; verified stable key bound to session | Tampered/unverified/wrong-character credentials rejected |
| OAuth login interception | Exact registered redirects, one-use expiring state, PKCE where supported, attempt secret bound to plugin | Wrong state, reused callback, login-attempt guessing fail |
| Secret exposure | Provider secret only server-side; redact headers, callbacks, token bodies; OS-protected plugin credential storage | Scan logs/config/package; no credentials in docs/source |
| Session theft | Short-lived opaque access tokens; rotating refresh family; hashes at rest; revoke/logout | Expiry/reuse/revocation tests; sockets terminated |
| Cross-group access | Membership checks plus composite group-scoped constraints for every nested ID | Substitute every resource ID from another group |
| Escalation | Owner-only grants/policies/definitions; per-command current authority | Capability holders cannot grant or define potions |
| Duplication | Transactional accounting, locking, checked arithmetic, durable idempotency | Concurrent creates/consumes/confirms preserve totals |
| Trade race | Revision-specific confirmations; persistent reservations; restrictions serialize with commit | Stale confirmation and restriction/completion races |
| Invalid quantities | Positive bounded quantities, whole monetary strings, overflow checks | Negative/zero/overflow/malformed values rejected |
| Text/command abuse | Plain text; safe unformatted rendering; reject control/line-break/chat command payloads | Unicode/percent/slash/payload/overlong test corpus |
| Discovery/privacy | Only consented own-group rosters; no arbitrary character existence lookup | Nonmember and global lookup probes fail |
| Abuse/exhaustion | Bounded request size, pagination, search length, inventory ceilings, quotas, auth/rate limits | Flood/malformed payload tests with safe failures |
| Unauthorized events | Auth header; group+recipient filtering; periodic session/membership revalidation | Unsubscribe/close on revoke/remove; no private letter payloads |
| Data loss | Paid persistent database, off-provider export, tested restore, migration review | Recovery drill and restart-in-mid-trade tests |
| Dependency compromise | Pinned toolchain/packages, reviewed updates, CI scanning, immutable release artifacts | Reproducible build and source provenance |

Login-start replay also requires the client-generated 256-bit bootstrap request
credential, stored hashed and redacted; an operation key is never authentication.
Encrypted response replay material is access-controlled and purged on expiry or
revocation, including login, refresh and invitation creation secrets.

Opaque random session credentials must have at least 256 bits of entropy; store
cryptographic hashes, not plaintext. Use OS-protected storage on the supported
plugin platform. Access default 30 minutes; refresh default 30 days with provider
revalidation at renewal subject to integration proof. A replayed refresh response
must use its original operation key; unexpected reuse revokes the refresh family.

TLS with trusted certificates and DNS hostnames is mandatory in release builds.
Allow HTTP only in explicitly flagged loopback development. Changing service URL
clears credentials before any request to the new authority. Reject URL credentials,
untrusted redirects and mismatched provider endpoints. Enable browser CSP, secure
cookie settings where used, no permissive CORS, and callback query-log redaction.
Native plugin auth uses bearer headers; browser callback uses OAuth state checks.

## Resource limits (provisional defaults)

| Limit | Initial recommendation |
| --- | --- |
| JSON body | 64 KiB; export responses streamed separately |
| Page size | Default 50; maximum 100 |
| Search term | 100 text elements |
| Group/category/item names | 80 text elements |
| Descriptions/reasons | 2,000 / 500 text elements |
| Letter title/body | 80 / 5,000 text elements |
| Potion use message | 50 text elements plus tested game byte cap |
| Holdings / group definitions / categories | 10,000 per character-group / 1,000 per group / 100 per group |
| Potion quantity per request | 1,000; also bounded by budget/holding ceiling |
| Trade lines | 50 per side |
| Login starts | 5 per 10 minutes per IP, with abuse-aware IPv6 handling |
| Character requests | 120 reads + 30 mutations/minute; bounded event connections |

These are named server options with discoverable public limits. Owner limits
cannot exceed service ceilings. Mutation quotas and group accounting are enforced
durably even if a single-instance ingress rate limiter restarts. Group admission
limits and creation quotas are separate from HTTP abuse limits. Increasing ceilings
requires capacity verification, not an unreviewed owner setting.

## Data policy

Collect verified identity fields needed for group matching, own-group membership,
assets, and necessary operation metadata. No nonessential analytics in 1.0. Explain
character visibility to group members during join; owner sees required administrative
summaries. Letter bodies are private to holder; author attribution remains visible
on the item. Plaintext on-screen privacy is not end-to-end encryption: database
operators can access stored content and the privacy notice must say so.

Provisional retention: login attempts 10 minutes; events 24 hours; response replay
7 days; personal/administrative audit 365 days; deleted-group private content purged
after 30 days; backups follow operator's defined recovery schedule. Rate-limit
metadata must have a short bounded lifetime. Redact item bodies, invitation secrets,
tokens and authentication identifiers from routine logs. Keep correlation/request IDs.

Exports/deletion requests are character-authenticated, with operator resolution in
1.0 rather than a sprawling self-service privacy system. Resolve group ownership
before deletion. Audit identity can be tombstoned; deletion must also address
backup expiry and other holders' received letters. Confirm exact policy before beta.

## Security limits that must be disclosed accurately

Authentication proves authority over a verified character, not that a client is
unmodified or that its user actually posted potion chat. Consent/chat checks in
the legitimate plugin improve behavior, but backend enforcement of actual FFXIV
posting is unavailable. Owner oversight governs RP conduct and alt grants.

No hardcoded production passwords, admin character lists, provider secrets or bypass
headers. Development identity fixtures must be unavailable in release deployments.
Disabling TLS verification or granting all capabilities for debugging is prohibited.

## Implemented M2 controls

Durable product sessions are separate from M1 probe credentials. Current owner,
active membership and individual grants are checked from PostgreSQL on each
command. Nonmember group/foreign member/foreign invitation requests return 404;
a known member without the action gets 403. Character/group DB advisory locks
serialize caps, membership/ownership changes, invite use and currency writes
across server instances. Checked Int64 changes and ledger records are atomic.

All product mutation keys are UUIDv7, request-bound including ETag, age-bounded
on initial submission and replayed from a durable unique scope/key record.
Responses that carry codes or grants are encrypted. Login start replay is bound
to the secret request credential; polling/exchange require the attempt credential.
One-use callback state is committed consumed before a provider call. Refresh
rotation revalidates the exact stored Lodestone ID/key; unexpected reuse revokes
the family. Ordinary credentials are 256-bit random values stored as hashes.

The platform AES-GCM vault uses purpose-bound/versioned envelopes and separate
operator keys with IDs; client saves use Windows DPAPI CurrentUser. Fixed provider
TLS origin, redirect refusal, minimal character+refresh scopes, request/body bounds,
validated text/amounts, required JSON fields and rejected unknown fields constrain
the attack surface. HTTP/provider logging excludes callback/grant data, errors are
safe ProblemDetails, and authenticated/callback responses are no-store. M2 has
server-wide API/login rate budgets; refine actor/network partitioning at M5.

Export/deletion, operator cleanup, detailed abuse/load testing, backup/restore and
release distribution are future gates. OAuth token rotation and DB commit cannot
be atomic; lost provider responses can require reauthentication with assets intact.
See [M2 checks](../delivery/m2-install-test.md) for the hosted renewal gate.
