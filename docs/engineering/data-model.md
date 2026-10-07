# Data model and transaction boundaries

Proposed product schema. M0 has an infrastructure bootstrap migration; M1 identity
probes are memory-only. Durable product entities below are not implemented yet. UUIDs are
opaque server IDs. All timestamps are UTC `timestamptz`. Revision counters are
monotonic integers. Monetary amounts use checked signed `bigint`; positive deltas
create supply, negative deltas destroy supply. Text is bounded by validated service
options, with plain-text storage.

## Entity catalog

| Entity | Key fields and purpose |
| --- | --- |
| Character | Immutable internal ID, current display name, home-world identity, verification timestamp, status; asset ownership remains attached to this ID after rename/transfer |
| IdentityBinding | Character ID, provider namespace, verified Lodestone ID and ownership key/attestation binding; minimum data needed for login, no alt roster |
| LoginAttempt | ID, hashed bootstrap/attempt credential/state, selected character hint, expiry, encrypted short-lived exchange material, status |
| Session | ID, character ID, hashed access/refresh credentials, expiry, refresh family, revoked timestamp |
| Group | ID, owner character ID, name, description, version, deletion timestamp |
| Membership | Group/character unique pair, active/dormant/blocked state, joined timestamps; identity reused on rejoin |
| CapabilityGrant | Group/character/capability unique tuple, granted-by and timestamp; effective only for active member |
| TradeRestriction | Group/character unique tuple, blocked flag, reason, version |
| GroupPolicyRevision | Group, effective period start, potion points, letter count, immutable policy revision |
| Category | Group/ID, name, allowed type IDs, retired timestamp, version |
| ItemDefinition | Group/ID, stable type ID, latest revision, enabled/retired state |
| ItemDefinitionRevision | Group/definition/revision, category ID, name, description, typeDataVersion and typed potion payload |
| Holding | Group/character/ID, type ID, definition revision or instance ID, owned quantity, reserved quantity, version |
| LetterInstance | Group/ID, author ID, title/body, category ID, content version, first-traded timestamp |
| Currency | Group/ID, name, symbol, version; one/group uniqueness in 1.0 |
| CurrencyBalance | Group/character/currency unique tuple, owned amount, reserved amount, version |
| CurrencyLedger | Operation/group/currency, affected character, signed delta, reason/transfer counterpart, before/after, actor |
| QuotaUsage | Group/character/kind/period unique tuple, limit snapshot, used, policy revision |
| Invitation | Group/ID, hashed secret, expiry, maximum uses/used, creator, revoked timestamp |
| OwnershipTransfer | Group/ID, current owner, recipient, expiry, proposed/accepted/cancelled status |
| Trade | Group/ID, parties, invited/open/completed/cancelled/expired, revision, expires, confirmed revision per party |
| TradeOfferLine | Trade/party/resource unique line, holding/currency reference and quantity, server-derived safe item preview; own offer revision |
| Reservation | Trade/group/character/resource, quantity/amount, expiry; exact reservation source |
| Operation | ID, group, actor, kind, result resource IDs/versions, timestamp; durable outcome recovery; actor may be null only for anonymous login bootstrap |
| IdempotencyRecord | Actor or login-attempt scope, key, method/path/request hash, operation/result status, replay expiry |
| AuditEntry | Group, actor, action, subject, reason, metadata without letter body/secrets, operation ID |
| ChangeEvent | Group/sequence, recipient visibility, type, resource ID/version, operation ID, timestamp |
| AccountRequest | Character, export/delete request, status, expiry/result location, operator resolution |

Prefer relational fields for ownership, quantities, permissions and cost. A bounded,
schema-validated type payload can hold type-specific description/message fields;
never use unvalidated JSON for core accounting. Type schema versions are explicit.
Asset/currency/trade operations always require an authenticated character actor;
anonymous bootstrap receipts are restricted to their secret-bound login context
and short-lived auth cleanup, never the personal financial history.

## Required constraints and indexes

- Unique verified identity key within provider namespace. Provider values are not
  interchangeable with client-supplied IDs. Separate display names from ownership.
- All character-owned product records reference the internal Character ID. A
  verified rename/world transfer updates display metadata in place without new
  balances, holdings, memberships or quota records. A changed ownership binding
  requires explicit recovery; do not automatically reassign assets by public
  Lodestone ID or a matching name/world.
- Composite uniqueness/FKs enforce group equality between definitions, revisions,
  holdings, letters, currencies, trades and reservations. Application checks alone
  are insufficient to prevent accidental cross-group references.
- `owned >= 0`, `reserved >= 0`, `reserved <= owned`; potion cost > 0; letter quantity
  exactly one; quota `0 <= used <= limit`; invitation uses <= maximum.
- One active membership/group/character; one currency/group; no active trade with
  the same character in another open trade in that group. Model the latter with
  unique active participant slots, not a race-prone pre-insert query.
- Stable stacking key `(group, character, definition, revision)` for potions;
  unique current holding for each letter instance. Dormant holdings still own assets.
- Index membership by character/status, inventory by group/character/type/category,
  history by group/character/time, pending trade participants/expiry, events by
  recipient/group/sequence, quotas by period, cleanup data by expiry.
- Deletion does not cascade blindly through financial history. Tombstone display
  identity and retain bounded non-content audit according to retention policy.

## Atomic operations

| Operation | Records changed in one commit |
| --- | --- |
| Group create/join | Character cap lock, group/membership, defaults/invitation usage, history |
| Ownership accept | Locks on both characters + group; caps, owner, transfer status, history |
| Potion production | Definition/current policy lock, quota debit, holding credit, operation/audit/idempotency/event |
| Letter creation | Quota debit, instance + holding, operation/audit/idempotency/event |
| Consumption/discard/removal | Available quantity decrement, operation/audit/idempotency/event |
| Currency adjustment | Available balance, supply ledger, operation/audit/idempotency/event |
| Offer edit | Trade revision, own lines, old/new reservations, confirmations cleared, event |
| Second confirmation | Both inventories/balances, balanced trade ledger, letter locks, release reservations, terminal trade, history |
| Restrict/remove/leave/delete | Membership/restriction/group state, cancellations and reservation release, events |

All implementations authorize inside the command transaction or serialize the
authorization state with it; a preflight permission check outside the transaction
is not enough. Handle serialization/deadlock errors with bounded retries under the
same idempotency key. Recheck expiry on each action. Lock ordering is documented
in code and exercised by concurrent integration tests.

## History and privacy

Personal history shows operations involving that character. Owner history exposes
administrative issuance/removal/grant/policy changes and trade summaries, never letter
bodies. `inventory.remove` exposes only the minimal holding summary needed to act.
Ownership is not automatic letter-reading authority. Trading parties see offered
letter titles/author and may open full contents only after receiving ownership.

Account export includes own identity, memberships, grants, current holdings and
owned letter content, quota usage, and relevant history. It excludes other members'
private content and auth secrets. Deletion requests cancel trades, resolve ownership,
remove credentials/private content, and tombstone necessary shared records; operator
must check exports/backups/retention. Leaving a group is not a data-erasure request.

## Persistence across expansion

Keep currency as an entity despite one/group in 1.0; remove the uniqueness constraint
only with a deliberate multi-currency migration. Definition revisions and letter
instances are distinct so books/equipment can gain their own instance payloads later.
Quota kind is an extensible string; weekly potion/letter mechanics stay separate.
Archived categories and retired definitions remain resolvable for existing holdings.
Do not make group-name or character-name changes rewrite asset identity.
