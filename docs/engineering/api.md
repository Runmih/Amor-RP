# API contract and endpoint catalog

**Status: proposed 1.0 contract; M0 implements three public GETs:** `/health/live`,
`/health/ready`, and `/api/v1/capabilities`. Other routes are planned and return 404
in M0. Capabilities advertises empty type/permission/chat lists until those features
exist. [OpenAPI](openapi.yaml) is the
machine-readable route/schema catalog. Every operation has explicit authorization
metadata; framework `security` authentication alone is not enough to implement it.
Paths below are on the configured backend authority. No provider URLs are invented.

## Conventions

- HTTPS, UTF-8 JSON, `/api/v1`. Health and browser OAuth callback are explicitly
  outside that prefix. API version is independent of package semantic version.
- IDs are opaque UUIDs; current character derives from session, never an arbitrary
  body actor ID. Monetary amounts are bounded integer decimal strings, not floats.
- Normal session auth: `Authorization: Bearer <access credential>`. Login attempts
  use `X-Login-Attempt-Credential`. Refresh uses body credential. Tokens never go
  in URLs or normal logs. Browser callback returns status HTML, not access tokens.
- Login start requires a fresh client-generated 256-bit random
  `X-Login-Request-Credential`. It binds start/retry responses so an operation key
  alone cannot retrieve an attempt secret. It proves request possession, not
  character identity. Reuse only for that login start; redact it from logs.
- Every mutation requires `Idempotency-Key` (UUIDv7). Scope is authenticated
  character, or secret-bound bootstrap/attempt/refresh family for auth commands, plus
  method/path and canonical payload. Same key/different payload is rejected.
- New keys must have a timestamp within the last 24 hours (bounded future skew).
  Asset-command records replay for seven days; auth replay is additionally bounded
  by attempt/credential validity and revocation. Once response replay expires, reject
  stale key with `operation_replay_expired`; never execute it as a new command.
  Persist key-to-operation identity in retained history and recover by operation ID.
  Replaying an operation still requires current access authorization; revoked users
  may get a receipt via recovery rather than a stale private resource payload.
- Successful mutations return `{operationId, result}` (typed result) or
  `{operationId}`. Response header `Idempotency-Replayed: true` identifies replay;
  clients must not automatically send game chat after replay/outcome recovery.
- Versioned GETs expose ETag; member roster rows also carry their own `etag`
  for member-management preconditions. Conditional edits require `If-Match`; production,
  use/discard/removal and trades submit explicit expected resource revisions in
  their bodies. Missing precondition -> 428; failed ETag -> 412; stale business
  revision -> 409. Creation cannot accept client-selected prices/remaining points.
- Cursor pagination: default 50, maximum 100, stable tie-break by ID. Filters and
  page cursors are group/character-bound. SnapshotVersion allows UI to notice list
  changes; event cursor allows reconciliation. Large export is streamed separately.
- List route matches are more specific than resource-ID routes (`join`, `removals`,
  `potions`, `letters`). Configure routing/UUID constraints deliberately.
- Schema numeric/text bounds reflect the proposed baseline. Effective service
  limits are advertised in capabilities; deployment-specific OpenAPI exports must
  match validated options. Reject unknown request fields for privilege-bearing
  requests; clients tolerate unknown response fields. Type IDs are extensible strings.

## Errors

Use `application/problem+json` with safe `code`, title/status and request ID. Never
send stack traces, SQL, secrets or private letter data. Nonmember resource probes
return 404 without confirming existence; 403 is for a known accessible resource
with an unauthorized action. Common status codes are documented across operations;
not every endpoint will emit every listed error.

| Status | Meaning / representative code |
| --- | --- |
| 400 | Malformed JSON/identifier, `invalid_request` |
| 401 | Missing/expired/revoked credential, `authentication_required` |
| 403 | Action denied, `capability_required`, `trading_restricted` |
| 404 | Unknown or inaccessible resource, `not_found` |
| 409 | `quota_exhausted`, `insufficient_available_quantity`, `group_limit_reached`, `definition_changed`, `trade_revision_changed`, `trade_expired`, `idempotency_mismatch`, `operation_replay_expired`, `inventory_full` |
| 412 / 428 | `version_mismatch` / `precondition_required` |
| 413 / 422 | Body too large / semantically invalid fields, `invalid_quantity`, `invalid_message` |
| 429 | `rate_limited`; include Retry-After |
| 500 | `internal_error`; safe request ID, recover operation outcome before retry |
| 503 | `maintenance`, `identity_provider_unavailable`, `temporarily_unavailable` |

On timeout the client preserves the same key, checks key/operation/trade status,
and retries only under the original key when appropriate. A 404 lookup may race an
in-flight first request and is not permission to generate a second key. Cleanup,
expiry and restrictions must be enforced transactionally at command time.

## Endpoint catalog

Authorization policies apply in addition to valid session authentication. All group
commands check same-group identity for every referenced resource. Administrative
holdings show summaries only; owner permission does not expose private letter text.
Mutations are marked with an asterisk and require an idempotency key.

### Service

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/health/live` | Process liveness | Public |
| GET | `/health/ready` | Database and startup readiness; failure uses 503 | Public |
| GET | `/api/v1/capabilities` | Versions, maintenance, types, permissions and public limits | Public |

### Authentication

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| POST * | `/api/v1/auth/login-attempts` | Start browser character login | Public bootstrap; secret retry credential; rate limited |
| GET | `/api/v1/auth/login-attempts/{attemptId}` | Poll login status without session secrets | LoginAttempt |
| DELETE * | `/api/v1/auth/login-attempts/{attemptId}` | Cancel own pending login attempt | LoginAttempt |
| POST * | `/api/v1/auth/login-attempts/{attemptId}/exchange` | Redeem verified attempt once for session | LoginAttempt |
| GET | `/auth/xivauth/callback` | Registered browser OAuth callback | Exact callback; one-use state; provider exchange |
| POST * | `/api/v1/auth/sessions/refresh` | Rotate refresh credentials and revalidate identity | Valid refresh credential; replay key protected |
| DELETE * | `/api/v1/auth/sessions/current` | Revoke current session and close its event connection | Session |

### Character

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/me` | Current verified character | Session |
| GET | `/api/v1/me/sessions` | Own character sessions | Session |
| DELETE * | `/api/v1/me/sessions/{sessionId}` | Revoke own character session | Session owner |

### Groups

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/groups` | Current character active groups | Session |
| POST * | `/api/v1/groups` | Create isolated group and defaults within caps | Session |
| POST * | `/api/v1/groups/join` | Accept invitation within membership caps | Session |
| GET | `/api/v1/groups/{groupId}` | Group and own permissions/balance | Active member |
| PATCH * | `/api/v1/groups/{groupId}` | Edit group name/description | Owner |
| POST * | `/api/v1/groups/{groupId}/leave` | Leave; cancel trades and make holdings dormant | Active member except owner |
| POST * | `/api/v1/groups/{groupId}/deletion` | Soft delete group with exact-name confirmation | Owner |
| GET | `/api/v1/groups/{groupId}/policies` | Current and next weekly policies | Active member |
| PUT * | `/api/v1/groups/{groupId}/policies` | Schedule next-week limits | Owner |

### Membership

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/groups/{groupId}/members` | Consented own-group roster; owner may include dormant members | Active member |
| PUT * | `/api/v1/groups/{groupId}/members/{characterId}/capabilities` | Replace individually granted actions | Owner; target active nonowner |
| PUT * | `/api/v1/groups/{groupId}/members/{characterId}/trade-restriction` | Restrict/restore both trade directions and cancel affected trades | Owner; active nonowner |
| POST * | `/api/v1/groups/{groupId}/members/{characterId}/removal` | Remove/block member and cancel affected trades | Owner; active nonowner |
| POST * | `/api/v1/groups/{groupId}/members/{characterId}/restoration` | Allow blocked character to rejoin; does not grant a slot or permissions | Owner; blocked member |
| GET | `/api/v1/groups/{groupId}/members/{characterId}/holdings` | Minimal holding summaries for authorized removal; no letter bodies | Owner or inventory.remove; dormant summaries owner only |
| GET | `/api/v1/groups/{groupId}/members/{characterId}/balances` | Balances for authorized currency adjustment | Owner or currency.manage; target active |

### Invitations

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/groups/{groupId}/invitations` | Invitation metadata without secret codes | Owner |
| POST * | `/api/v1/groups/{groupId}/invitations` | Create bounded expiring invitation; return code once | Owner |
| DELETE * | `/api/v1/groups/{groupId}/invitations/{invitationId}` | Revoke invitation | Owner |

### Ownership

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| POST * | `/api/v1/groups/{groupId}/ownership-transfers` | Propose transfer to active member | Owner |
| GET | `/api/v1/groups/{groupId}/ownership-transfers/{transferId}` | Read transfer status | Current owner or proposed recipient |
| POST * | `/api/v1/groups/{groupId}/ownership-transfers/{transferId}/accept` | Accept pending transfer within owner caps | Proposed recipient; active member |
| POST * | `/api/v1/groups/{groupId}/ownership-transfers/{transferId}/cancel` | Cancel pending transfer | Owner or proposed recipient |

### Currency

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/groups/{groupId}/currency` | Group currency definition | Active member |
| PATCH * | `/api/v1/groups/{groupId}/currency` | Rename currency without replacing identity | Owner |
| GET | `/api/v1/groups/{groupId}/balances` | Own available/reserved currency | Active member |
| POST * | `/api/v1/groups/{groupId}/currency/adjustments` | Issue/remove whole-unit currency with reason | Owner or currency.manage; target active |

### Categories

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/groups/{groupId}/categories` | Group categories including retired labels | Active member |
| POST * | `/api/v1/groups/{groupId}/categories` | Create bounded organizational category | Owner |
| GET | `/api/v1/groups/{groupId}/categories/{categoryId}` | Category details and concurrency token | Active member |
| PATCH * | `/api/v1/groups/{groupId}/categories/{categoryId}` | Edit category without changing item behavior | Owner |
| POST * | `/api/v1/groups/{groupId}/categories/{categoryId}/retirement` | Retire category for new content; preserve old holdings | Owner |

### Definitions

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/groups/{groupId}/item-definitions` | Potion definitions with latest revisions | Active member |
| POST * | `/api/v1/groups/{groupId}/item-definitions` | Define potion without issuing copies | Owner |
| GET | `/api/v1/groups/{groupId}/item-definitions/{definitionId}` | Definition revision; latest by default | Active member |
| PATCH * | `/api/v1/groups/{groupId}/item-definitions/{definitionId}` | Create immutable definition revision | Owner |
| POST * | `/api/v1/groups/{groupId}/item-definitions/{definitionId}/retirement` | Retire production without deleting existing copies | Owner |

### Inventory

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/groups/{groupId}/quotas` | Own current usage, limits, reset time | Active member |
| GET | `/api/v1/groups/{groupId}/inventory` | Own holding summaries with filters | Active member |
| GET | `/api/v1/groups/{groupId}/inventory/{holdingId}` | Own holding and type-specific details | Current holder; active member |
| POST * | `/api/v1/groups/{groupId}/inventory/potions` | Spend own potion points to produce copies | Owner or items.potion.create |
| POST * | `/api/v1/groups/{groupId}/inventory/letters` | Spend own letter allowance to create individual letter | Active member |
| GET | `/api/v1/groups/{groupId}/letters/{letterId}` | Read currently owned letter and edit concurrency token | Current holder; active member |
| PATCH * | `/api/v1/groups/{groupId}/letters/{letterId}` | Edit untraded, unreserved letter held by its author | Author and current holder |
| POST * | `/api/v1/groups/{groupId}/inventory/{holdingId}/use` | Consume one available potion and return literal chat text | Current holder; active member; potion type |
| POST * | `/api/v1/groups/{groupId}/inventory/{holdingId}/discard` | Discard own available quantity; no quota refund | Current holder; active member |
| POST * | `/api/v1/groups/{groupId}/inventory/removals` | Remove another active member holding with reason | Owner or inventory.remove; target active |

### Trading

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/groups/{groupId}/trades` | Own trades, pending invitations and recent outcomes | Participant; active member |
| POST * | `/api/v1/groups/{groupId}/trades` | Invite same-group unrestricted character to trade | Active unrestricted member; same for target |
| GET | `/api/v1/groups/{groupId}/trades/{tradeId}` | Read own trade and recover outcome | Participant; terminal status remains available after removal |
| POST * | `/api/v1/groups/{groupId}/trades/{tradeId}/accept` | Recipient accepts unexpired invitation | Recipient; both active/unrestricted |
| PUT * | `/api/v1/groups/{groupId}/trades/{tradeId}/offers/mine` | Replace own offer; reserve resources; reset confirmations | Participant; both active/unrestricted |
| POST * | `/api/v1/groups/{groupId}/trades/{tradeId}/confirm` | Confirm exact revision; second confirmation atomically completes | Participant; both active/unrestricted |
| POST * | `/api/v1/groups/{groupId}/trades/{tradeId}/cancel` | Cancel/decline own trade and release reservations | Participant; never undo completed trade |

### History

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/groups/{groupId}/history` | Personal group operation history | Character participant; own history after leaving |
| GET | `/api/v1/groups/{groupId}/audit` | Administrative group history without private content | Owner |

### Recovery

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/operations/{operationId}` | Own operation receipt after ambiguous request | Actor or involved trade participant |
| GET | `/api/v1/operation-keys/{idempotencyKey}` | Own key status; unknown key returns 404 | Authenticated actor only; excludes login/refresh keys |

### Data lifecycle

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| POST * | `/api/v1/me/export-requests` | Request own-character export for operator workflow | Character; rate limited |
| POST * | `/api/v1/me/deletion-requests` | Request deletion; resolve group ownership first | Character; recent reauthentication |
| GET | `/api/v1/me/data-requests/{requestId}` | Read own export/deletion request status | Request owner |
| GET | `/api/v1/me/data-requests/{requestId}/download` | Stream approved unexpired own-character JSON export | Request owner; ready export only |

### Events

| Method | Endpoint | Purpose | Authorization |
| --- | --- | --- | --- |
| GET | `/api/v1/events` | Upgrade authenticated connection to recipient-filtered WSS events | Session; active group member |

## Representative requests

Headers below omit real credentials. Values are examples, not built-in item IDs.

```http
POST /api/v1/groups/{groupId}/inventory/potions
Authorization: Bearer <character session>
Idempotency-Key: <UUIDv7 generated for this action>
Content-Type: application/json

{"definitionId":"<UUID>","expectedDefinitionRevision":3,"quantity":2}
```

The server computes cost from revision 3 and the current definition; it does not
accept `cost`, `characterId`, or `remainingPoints` from the caller.

```json
{
  "expectedTradeRevision": 7,
  "offer": {
    "items": [{"holdingId": "<UUID>", "quantity": 2}],
    "currency": [{"currencyId": "<UUID>", "amount": "25"}]
  }
}
```

Trade responses enrich item lines with server-derived previews: potion name, exact
revision, description and use message; letter title/author, without its private body.
Recipients never need permission to read another character's entire inventory.
Replace own offer atomically, reserve units and reset both confirmations. Confirmation
submits only `expectedTradeRevision`; second confirmation commits all transfers.
There is no endpoint to write arbitrary balances, upload executable type behavior,
move items between groups, bypass quota, or directly declare a trade completed.

## WebSocket protocol

`GET /api/v1/events?groupId=<UUID>&afterSequence=<cursor>` upgrades to WSS with the
same bearer header (native client). No query token. One selected-group subscription
per client session. Reject invalid auth/group access before upgrade. 426 without
upgrade; 101 on success. OpenAPI describes the handshake, not a general WebSocket SDK.

Messages are UTF-8 JSON `ChangeEvent` values. Types: `inventory.changed`,
`currency.changed`, `quota.changed`, `trade.changed`, `membership.changed`,
`permissions.changed`, `definitions.changed`, `categories.changed`, `policies.changed`,
`maintenance.changed`, `resync.required`, `session.revoked`. Data carries only
sequence, group, resource ID/version, operation ID and time, not private content.
Recipient filter restricts inventory/currency/quota/trade events; group-wide metadata
is visible only to entitled members. Authoritative data is obtained via HTTP.

Default replay window is 24 hours with a bounded backlog. After a gap/expired cursor,
send `resync.required` and fetch group, holdings, balances, quotas and own open trades.
Use jittered exponential reconnect with cancellation and heartbeat detection.
Duplicate events are harmless; compare sequence/resource version before refresh.
On session expiry/revocation or membership removal close after notification; do not
leave a stale-authorized socket alive. Slow consumers get resync/close, not unbounded
memory buffering. Event delivery never executes a chat action or locally commits a trade.

## Operational actions outside public API

Database migrations, retention cleanup, backup/restore and operator resolution of
data requests use controlled operator workflows, not unauthenticated admin routes.
1.0 has no web-admin interface. Operator export readiness is recorded with an audit
entry; download streams the approved snapshot under owner authentication. Secrets
are never included. Document the verified operator CLI/runbook during implementation.

## Contract maintenance

Implement and export server OpenAPI, compare routes/schemas with this draft, and
keep the catalog synchronized. Integration tests assert authorization/invariants;
OpenAPI validation alone cannot prove those semantics. Add endpoints only alongside
product rules, schemas, failure handling and acceptance coverage.
