# Acceptance and release checks

These are required future checks, not existing automated test results. Use pure
unit tests for domain rules and real disposable PostgreSQL for transactional/race
tests. In-game behavior requires human tests in FFXIV.

## Feature traceability

| Features | Required evidence |
| --- | --- |
| F01 login | Verified ID/key resolves the existing character; wrong ownership, callback replay, expiry, refresh/logout; paid rename/transfer checks waived and no separate simulated-rename gate (A16) |
| F02 groups | 3/6 caps under concurrency; invites expire/revoke; leave/remove/restore; accepted transfer; deletion |
| F03 switching | Correct group/character always shown; pending trade reconciled before switching |
| F04 permissions | Each grant controls only its action; revoke in open dialog; owner transfer updates authority |
| F05 currency | Issue/remove history; insufficient/reserved/overflow denied; exchanges conserve supply |
| F06 categories | Seed/add/rename/retire; old item remains readable; category never grants creation privilege |
| F07 definitions | Owner-only revisions; cost changes invalidate old preview; retirement preserves copies |
| F08 production | Correct quantity*cost; independent alt quotas; grant/rejoin does not refill; no concurrent overspend |
| F09 letters | Five/week default; limit changes scheduled; editable/locked author rules; bounded body |
| F10 inventory | Both views; search/pagination; details privacy; accurate available/reserved quantities |
| F11 use/chat | One startup consent question; posts/message/channel/character/group/backend changes retain it; next plugin startup asks again; decline/settings disable posting; exact preview/all channels; correct one-item commit; duplicate and chat-failure UX |
| F12 trades | Safe potion/letter previews, invite/accept, offer resets, stale revision rejection, reservations, completion/cancel/expiry races |
| F13 history | Personal visibility/owner scope; reasons and actor; no letter body or secret leakage |
| F14 recovery | Request ambiguity, reconnect gaps, restart during trade, durable idempotency |
| F15 settings | Character/backend scoping; credential clear on endpoint change; upgraded config; logout cleanup |
| F16 lifecycle | Compatible versions, maintenance, export/deletion, backup restoration, install/update/support |

## Essential invariant scenarios

Normal durable authentication tests must verify that a provider-verified ID/key
resolves the existing internal Character record and authorizes that character's
data. Client-supplied IDs alone and another ownership binding must not authorize
it. Cover returning login/session renewal and explicit binding recovery within
the authentication suite. Name/world is never an ownership lookup or fallback.
Live paid rename/transfer checks are waived by A16 and recorded as skipped for
cost; no separate simulated-rename/transfer gate is required. Other live
login/provider checks remain required.

1. Substitute a group B holding/definition/member/currency/trade/category ID into
   every applicable group A endpoint; deny without mutation or private-data leak.
2. Send 20 parallel production requests with only enough budget for five copies;
   exactly allowed spending succeeds, no negative quota, no partial output.
3. Repeat a command with the same key/payload; original operation/result returned.
   Change payload with same key; reject. Try an expired key after replay purge;
   reject rather than treating it as new. Recover by operation ID.
4. Concurrent consume, discard, administrative removal and offer of last potion;
   at most one valid debit/reservation succeeds.
5. Two users confirm revision N while a third request edits the offer; no exchange
   using stale approval. Both review the new revision.
6. Race completion against owner restriction/removal and cancellation/expiry;
   exactly one serial outcome, no stranded reservations or partial transfer.
7. Restart server between confirmation requests and after commit before response;
   reconcile persisted state and do not duplicate exchanges or chat.
8. Leave/rejoin and revoke/regrant after spending allowance; same week's usage
   remains. Two authorized alt characters deliberately get independent allowances.
9. Create at exact weekly boundary and concurrent policy edit; server period and
   scheduled revision decide consistently. Test daylight-saving display separately.
10. Trade letter away and back; author still cannot edit it. Former holders cannot
    read current body through API or cache after access removal.
11. Recipient inventory full; entire trade fails without either side losing assets.
12. Authorized currency delta overflows or spends reserved units; reject atomically.

## In-game and usability checks

Use at least two real characters. Test home-world extraction during travel, same
name on different worlds, undefined context data, party/no-party, each Linkshell
slot, Emote formatting, byte/Unicode/percent/control text, and rapid repeated clicks.
No silent destination changes. Simulate FFXIV chat failure after confirmed consumption.

Measure plugin frame impact during large inventory search and server timeout.
Asynchronous requests never block drawing; cancel/dispose unsubscribes all events.
Verify settings at UI scales, keyboard navigation, once-per-startup chat consent, restrictive
permissions, offline mode, and readable failure messages with a request ID.

## Capacity and operating checks

Provisional R14 target: 50 connected clients across five groups, ten simultaneous
trades, inventories up to 10,000 holdings/group. Initial acceptance target: p95
ordinary API responses under 500 ms in same-region synthetic load (excluding
external authentication); no unbounded memory growth or invariant violation during
a one-hour mixed workload. Real player latency is separately observed; synthetic
measurements do not guarantee global latency. Adjust tier/target with measured evidence.

Exercise backup export and restore into isolated database; verify ledger/holdings,
quota snapshots, reservations and migrations. Record achieved RPO/RTO. Restart
service while trades are open. Alerts/reporting and budget settings must be exercised.

## 1.0 release gate

- All features above pass; no critical duplication, cross-group, auth or data-loss bug.
- XIVAuth and current Dalamud/channel/context integrations demonstrated end to end.
- Fresh clone builds with documented toolchain; clean install and update by new tester.
- [Version matrix](../engineering/versions.md) gates passed: exact tool/package/image
  pins and locked CI restore, host-provided DLLs excluded from plugin ZIP, matching
  `AmorRP` internal name/manifest/API level, actual game/host/OS test versions recorded.
- Current and previous advertised compatible client releases tested against server;
  incompatible protocol/type/settings versions fail clearly; migrations and restore
  tested on the selected PostgreSQL major and actual release artifacts.
- Target file structure implemented; no template text/assets, ignored required
  manifests, scattered authoritative state, or secrets in repository/package.
- OpenAPI matches server routes and contract tests; old supported client behavior tested.
- Paid persistence/backups, restore drill, operator/support contact, privacy/retention
  notice and source-license links ready; testers agree to persistence expectations.
- Distribution path selected. Official submission requires actual human review,
  personal testing, AI-use disclosure and human-written submission text under current policy.
- Human maintainer signs off scope/usability, deployment and maintenance responsibility.

## Recorded milestone status

M0: testing reported successful 2026-10-06. M1: passed per maintainer report
2026-10-07. M2: implemented for testing; local verification and the new live exit
checklist are documented in the [M2 guide](m2-install-test.md). Earlier acceptance
does not claim a new refresh scope was demonstrated. Existing 1.0 feature rows
remain release criteria, not all completed features.
