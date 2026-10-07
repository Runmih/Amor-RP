# Interface and player flows

## Shared display

Every window shows active **group name** and **character name @ home world**.
Top bar above all feature tabs: persistent group dropdown, currency icon/balance,
Refresh data button, last successful update, connection state, and settings. Use
labels as well as color for errors, restrictions, and offline state. No network
or database work runs synchronously in the ImGui drawing callback.

```text
Amor RP    [Group: Lantern Company v]    Character @ Home World
Currency: 125 Crowns                  Connected
[Inventory] [Letters] [Create] [History] [Group settings]
```

Group settings are visible to owner; other capability holders see only their
authorized creation/removal/adjustment actions. Disabled actions explain why.
Use stable ImGui IDs independent of displayed names; render user text with safe,
unformatted helpers. Use Dalamud WindowSystem for all regular windows.

## First use and authentication

1. Show purpose, fictional nature of assets, backend URL, data sent, and group
   visibility. User explicitly connects and opens XIVAuth in their browser.
2. Verify the chosen character. Show status and expiration during login; retry
   never skips verification. Never ask for Square Enix credentials.
3. Offer Create group or Join with invitation. Display remaining membership slots.
4. Ask for potion/chat permission once when the plugin starts and explain that
   chat posting is required for potion use. Allow or Decline applies to this entire
   startup; other features remain available after declining. Consent is adjustable
   in settings. Do not repeat the question after posts, message/channel edits,
   character/group/backend switches or login/logout. Ask again only on plugin restart.

Local character switch immediately clears prior character views, stops event
subscriptions, and cancels pending UI actions. Authenticate the new character;
never display the former character's private letter contents under the new identity.

## Groups

The dropdown stays outside the tab contents and is available from every tab.
Selector lists only current character's active memberships and highlights owner
status. Display pending trade before switching; reconcile cancellation first.
On group removal or deletion show an explanation, clear sensitive caches and
select another available group. Empty membership list has actionable Create/Join.

Create: group name, description, currency name, default coin icon, weekly potion
points and weekly letter count. Default points zero is explained: "Set an allowance before creating
potions." Join: invitation code, group summary, membership disclosure, confirmation.
Invitation codes are copyable only when created, not shown in logs. After a
confirmed join, clear the entered code and reset visibility acceptance. After
confirmed creation, reset its draft to server defaults and select the new group;
keep creation and owner-edit drafts separate. Retain inputs on failed/unknown outcomes.
Owner currency settings upload/preview PNG, JPEG/JPG or static WebP up to 128x128,
with a bundled fallback icon and a clear oversize/invalid-file message.

## Compact consumables

Small movable window with group, currency, and locally pinned potions. Rows show
name, available/owned quantity, details tooltip, and Use. Pinned item revisions are
identified consistently; obsolete pins can be removed. Empty state links to extended
inventory. Reserved count is visible. Offline state disables Use.

Use dialog shows group, potion revision, exact message, selected destination,
"Consumes 1", and Confirm/Cancel for the item-use action. Use the existing startup
chat grant; no per-use consent checkbox or additional chat permission prompt.
Destination picker includes specific Linkshell slots/names available to the current
character; remember selection per character/group. No automatic fallback to current
chat channel. Show explicit failed/unknown outcome rather than optimistic depletion.

## Extended inventory

Search, category and type filters, stable sorting, pagination, and selected details
pane. Columns: item name, type/category, available quantity, reserved quantity.
Details: description, revision, author for letters, potion use message, applicable
actions. Display letter body only after authorized detail request. Default item
icons are bundled. Custom currency icons load only from the configured backend
under current group authorization; no user-supplied remote URLs. Actions include
pin/unpin, read, use, discard, eligible edit, and trade. Discard confirms quantity and irreversibility.

## Creation

- **Potion:** select enabled definition; view current revision, cost/copy, quantity,
  total cost, remaining weekly points, reset time. Disable production if budget is
  insufficient. A stale-definition conflict refreshes preview and requires review.
- **Letter:** title, plain text body, allowed category, live limits, remaining count,
  and reset time. Save draft locally without quota cost; Create clearly explains
  this produces a persistent inventory item. Local drafts are character/group scoped.
- Capability revoked while dialog is open: show rejection and refresh grants.

## Trade

Context menu entry "Amor RP: Trade" is limited to player characters where the
context supplies a reliably resolved home world. Resolve target within own group's
consented member roster. If several joined groups overlap, default to active group
and allow an explicit group choice before initiation. Missing context identity is
an explanatory disabled entry; do not guess the currently visited world.

Recipient sees sender, group, expiry, Accept/Decline. Open screen has two columns:
each side's offered items, quantities, currency, and confirmation status. Show
the reviewed revision. Offered potions show their exact revision, description and
use message; letters show title and author, with contents sealed until received.
These previews come from the trade response, without exposing another inventory.
Offer edits reset both confirmation labels immediately on
authoritative update. Confirm stays disabled while updates are pending. Cancellation,
restriction, expiry, completion and reconnect all have distinct messages. Unknown
completion shows "Checking trade result" until authoritative reconciliation.

## Owner and delegated tools

- Members: character/home world, individual action checkboxes, trading restriction,
  remove/restore, invite. Default to active members, including for owners.
  Owner-only Show inactive members exposes dormant/blocked recovery rows.
  Member removal has its own mandatory reason and confirmation, with rejection
  visible until acknowledged or superseded by another action. Do not show
  online/offline plugin usage.
- Potions: definition list, revision editor, enable/retire, current creation cost,
  warning that edits affect new copies. Group owner alone defines recipes.
- Policies: current weekly limits and next-week scheduled values with effective date.
- Categories: names, allowed types, retire; existing holdings remain readable.
- Currency: authorized recipient, signed adjustment, mandatory reason, preview balance.
- Removal: authorized holding summaries, quantity, mandatory reason; no letter-body
  read privilege. Reserved assets show why removal is unavailable.
- History: owner administrative events; members see personal operations and their
  own completed trades. Letter bodies and authentication secrets never appear.
- Ownership/deletion: clear consequences, recipient consent, typed group-name confirmation.

## Settings and failure handling

Service URL, login/logout/session management, startup-scoped chat permission,
destination, compact
view, pins, scale, and support/export/deletion instructions. Changing backend
requires confirmation, drops sessions and scopes caches to that backend. Do not send
the previous backend's credentials to the new URL. Release builds require HTTPS.

| State | Display/behavior |
| --- | --- |
| Loading/empty | Skeleton/status then useful empty-state action; not a blank window. |
| Offline | Last sync time; cached own summaries read-only; no queued mutations. |
| Expired login | Sign-in required; preserve nonsecret drafts; stop privileged actions. |
| Quota exhausted | Remaining amount, cost, exact local reset date/time. |
| Restricted trading | Explicit restriction; other allowed inventory features remain. |
| Request timeout | Keep operation key; query outcome; no new-key retry or assumed success. |
| Stale revision | Refresh, explain change, require renewed review. |
| Maintenance/update required | Server message and compatible-version instructions. |
| Server error | Safe message and request ID for support; no raw stack traces. |

Before 1.0, validate keyboard navigation, UI scaling, long names/letters, percent
signs, Unicode, two potion revisions with identical names, and unfamiliar-user flows
with testers. Product text must explain actions, not internal API mechanisms.

## M2 test-build navigation

`/amorrp` opens Character login plus four tabs: Groups, Members & currency,
Owner settings, History & devices. Groups shows the group selector, your currency,
join visibility acceptance and creation with first-week limits. Members selects
stable roster IDs for direct grants, restrictions, removal, coin changes and
ownership proposals. Owner settings exposes current/scheduled policies, renaming,
invitations, ownership cancellation and exact-name deletion. History/devices shows
personal or owner history and session revocation. Save/renewal work is asynchronous
and disables changes while pending. Unresolved commands retain their retry key and
require retry/receipt reconciliation before another mutation.

The selected group is saved per backend/verified character. Inventory, compact
consumables and trading tabs arrive at their milestones. Diagnostics stays under
an explicit M1 section; its chat test is not a currency/item use action.

## Refresh and action feedback (M3)

Keep Refresh data beside the group dropdown above the tabs. Display the last
successful update and an explicit stale/offline state. Refresh after committed
mutations and every 60 seconds while an authenticated product window is visible;
skip overlapping/in-flight actions and back off on connection failures.
Refresh read models without replacing local drafts, clearing selection unnecessarily
or resetting startup chat consent. Group/character switching clears private views
and cannot redirect a pending command into the newly selected context.

Separate synchronization status from action results. Loading/refreshed messages
must never overwrite a removal or other command error. Explain required reasons,
authority denials and stale versions; expandable troubleshooting shows sanitized
HTTP status, error code, request ID and operation key without credentials/content.
Only a committed removal appears as a successful history event.
See the [M2 feedback plan](../delivery/m2-feedback-m3-plan.md) for implementation order.
