# Product rules

Rules below combine agreed scope and recommendations from the
[decision register](../decisions.md). They define the proposed 1.0 behavior.

## Identity and isolation

Inventory ownership, capability grants, quota usage, and trade restrictions belong
to a verified **character within a group**. Different characters can receive
independent budgets, even when controlled by the same person. Do not correlate
alts or silently share their quotas.

Display name plus home world identifies a visible character for context-menu
matching. Authentication uses provider-verified identity, never a client assertion
alone. Store a verified stable character key; names/worlds are mutable display
fields. Provider identity migration must be explicit. No inventory merging by name.

Rename and home-world transfer preserve the same internal Character ID, inventory,
currency, memberships, permissions, restrictions and quota history. Verified
provider profile updates change display fields only. Existing assets remain intact
while a stale provider profile or fresh verification is resolved; never create a
new inventory merely because the old name/world no longer matches.

Every item, definition, category, currency record, reservation, and operation has
a group ID. A command specifies its group; the server checks each referenced
resource belongs to it. Switching the display does not migrate assets.

## Groups and membership

- Limits: three owned and six active memberships, owned included. Pending invitations
  and dormant memberships do not consume a slot. Recommended scope: per character.
- Owner creation atomically creates group, owner membership, currency, categories,
  and policies. Admission and ownership-transfer limits use database locking.
- Only owner issues revocable, expiring invitations. Joining requires explicit
  acceptance of group visibility/data rules; possession of a code is not membership.
- Owner removal deactivates and blocks the member; restore returns eligibility to
  join, and rejoin still requires a valid invitation and available group slot.
- Leave/removal cancels open trades, revokes effective grants, and makes inventory
  inaccessible. Rejoin uses the same character/group identity and quota history.
  Owner must grant creation/currency/removal permissions again after rejoin.
- Dormant assets cannot be traded, consumed, or administratively adjusted; the owner
  can inspect dormant holding summaries for recovery, without reading letter bodies.
- Ownership transfer: current owner proposes an active member; recipient accepts;
  transaction checks limits and switches authority. Former owner remains an ordinary
  member. No owner can remove themselves or leave without transfer/deletion.
- Delete group: owner confirms its exact name; cancel trades and invitations; make
  it read-only/inaccessible to members. Purge only through the documented retention
  workflow. API retries cannot create a second deletion.
  Personal operation receipts and terminal trade outcomes remain available for
  reconciliation within retention; this does not restore inventory/group access.

## Capabilities

| Action | Owner | Authorized member | Ordinary member |
| --- | --- | --- | --- |
| Membership, policies, ownership, categories, potion definitions | Yes | No | No |
| Issue/remove currency | Yes | `currency.manage` | No |
| Create potion copies within own allowance | Yes | `items.potion.create` | No |
| Remove another active member's items | Yes | `inventory.remove` | No |
| Create letters within quota | Yes | Yes | Yes |
| Read owned letters, use potions, discard own holdings | Yes | Yes | Yes |
| Trade | Unless restricted | Unless restricted | Unless restricted |

Owner authority is derived from current ownership, not a role grant. Revocation is
effective on the next command; never trust long-lived token permission claims.
No capability holder may grant permissions or change group policy. Own-item
discard is separate from removing another member's holdings. Other inventory
inspection is limited to summaries required for authorized removal; letter text
is visible only to its current holder.

## Currency

One group-owned currency definition in 1.0: name, symbol, stable currency ID.
Balances and amounts are nonnegative signed-64-bit whole units internally, checked
for overflow. Wire amounts are decimal strings to preserve precision in future
clients. No float arithmetic. Negative adjustments require sufficient *available*
balance; reserved money cannot be removed. An administrative adjustment requires
a nonempty reason and records actor, recipient, signed delta, before/after balance.
Authorized minting is deliberately unrestricted by weekly points in the proposed
1.0; owner oversight and history manage abuse. Trade transfers conserve supply.

## Types, categories, and revisions

Types implement behavior. Categories label inventory and have allowed type IDs;
changing a category never changes item behavior or capability checks. Stable type
IDs are `potion` and `letter`. Owners cannot install types or executable scripts.
Initially seed Consumables (`potion`) and Correspondence (`letter`). Retired
categories remain readable on old holdings, and cannot be selected for new content.

Owner potion definition fields: name, category, description, plain-text use message,
positive integer creation cost, enabled state. Update creates an immutable revision.
Copies reference that revision. Same group/character/definition revision stacks;
different revisions remain distinguishable. Definition retirement blocks production,
but does not delete existing copies or prevent their consumption/trading.

Potion production submits definition ID, expected definition revision, and quantity.
Server verifies availability, grant, limits, budget, and current revision; cost is
`quantity * creationCost` with checked arithmetic. Debit usage, credit holdings, and
write operation/audit/idempotency records in a single transaction. Owner also uses
their own allowance. There is no bypass or arbitrary item-grant endpoint in 1.0.

## Weekly limits

- Recommended boundary: Monday 00:00 UTC through the following Monday, exclusive.
  Server time determines period; interface shows reset in local time.
- Owner sets a group-wide potion points limit and letter count limit. Each character
  has independent `(group, character, quotaKind, period)` usage.
- Group creation explicitly sets the first period's limits. The UI warns before
  creating a group with zero points because later changes start next period.
- Defaults: zero potion points until configured; five letters/week. Zero disables
  that creation path. Unused allowance does not roll over.
- Weekly policy edits apply next period; show current and scheduled limits. On
  first creation in a period, materialize its immutable policy snapshot.
- Potion cost edits affect future production immediately, without recalculating
  past spending. Stale preview requests fail with `definition_changed`.
- Creating one letter spends one count. Edits, trades, reads, or receiving letters
  do not spend it. No refunds for discard, use, administrative removal, or trading.
- Grant toggles, leave/rejoin, restarts, and device changes do not reset usage.
- No scheduled reset job is needed: derive the period and accumulate spending.

Defaults and hard ceilings are discoverable from the server and configurable as
documented in [architecture](../engineering/architecture.md). Weekly quotas limit
creation, not accumulated storage; inventory ceilings handle total growth.

## Letters

Each letter is a quantity-one instance with title, body, category, immutable author
identity, content revision, and first-trade timestamp. Recommended limits: title
80 characters, body 5,000; service ceiling may be raised through configuration.
No attachments, formatting commands, remote content, or cloned-letter endpoint.

Only the author while holding the letter may edit it, before first completed trade.
Offering it reserves it and blocks edits. Completed trade permanently locks contents;
returning it to the author does not unlock it. Readers see author, group, title,
body, and locked state. Drafts are local; creating the persistent item spends quota.

## Consumption and chat

Ask for chat consent once at plugin startup and explain that posting is required
for potion use. The answer applies to that entire startup: sending a message,
changing the message/destination, switching characters/groups/backends or signing
out does not reset it or trigger another permission question. Plugin reload or a
new game start begins a new startup. The player can change the permission in
settings; declining disables potion use/chat posting while other features remain
available. Do not store this grant as permanent permission across restarts.

One explicit player Use action consumes exactly one available potion. Require
the existing startup chat grant and a valid chosen Emote/Say/Party/specific Linkshell
destination before requesting consumption; do not ask for consent again.
Count the final message as Unicode text elements,
maximum 50 by default; reject line breaks, control characters, command injection,
and unsupported game payloads. 1.0 has literal text, with no template substitution.
Channel prefix and game-supplied character prefix are outside the authored limit.
Test byte-length/game encoding separately; the character limit alone is insufficient.

Show exact message, channel, quantity cost, group, and character before the Use action.
The server commits consumption and returns operation ID plus immutable message.
Only that user action may attempt chat; server events never trigger chat.

Database commit and FFXIV delivery cannot be atomic. After an ambiguous request,
recover operation outcome before doing anything else. Replayed consumption responses
must not automatically send chat. If chat fails after commit, report the consumed
item and failed message; offer explicit manual resend with a warning about possible
prior delivery. Never auto-refund or auto-resend. Recheck channel after the response;
if unavailable, show failure without silently switching channel.

## Trading

Both parties must be active, unrestricted members of the same group. Initiation is
through the character context menu or group-member inventory flow; recipient accepts
before assets may be offered. No global lookup or plugin-user presence probing.
Recommended: one open trade/character/group and ten-minute expiry after initiation,
with the actual expiry shown. Offline recipient gets a pending invitation on next
sync if it has not expired. Assets are not offered until acceptance.

State machine: `invited -> open -> completed`, with `cancelled` or `expired` terminal
alternatives. Each offer edit increments a trade revision and clears both confirmations.
Server reserves offered quantities and currency; available = owned minus reserved.
Trade responses provide server-derived potion definition/message previews and
letter title/author summaries. They do not expose private letter bodies or the
sender's full inventory. Confirmation is based on those exact offered revisions.
Cannot consume, discard, remove, edit, or offer those reserved assets elsewhere.

Each confirmation names the reviewed trade revision. Second confirmation triggers
one transaction: lock participants/resources in deterministic order; recheck membership,
restriction, expiry, revision, holdings and capacity; exchange both sides; lock letters;
write ledger/history; release reservations; mark completed. A failed check rolls back
the entire exchange. Empty trades are rejected; one-sided gifts are allowed.

Cancellation/expiry releases reservations transactionally. Owner restriction/removal
must serialize with completion: either completion occurred first or cancellation
prevents it. Client disconnect alone is not cancellation; recover by GET trade.
Switching groups closes that client's unfinished trade via cancellation before the
UI switch; ambiguous cancellation must be reconciled. Completed trades cannot be
undone through cancel. Reservations and confirmations persist across server restart.
