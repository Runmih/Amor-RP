# Roadmap to 1.0

Milestones are ordered by dependency and evidence. Dates require team availability,
integration results and tester feedback; none are promised by this specification.
Do not mark a milestone complete because its code exists without its exit evidence.

| Milestone | Deliverable | Exit evidence |
| --- | --- | --- |
| M0: build and boundaries | Replace old scaffold with clean four-project layout, verify version matrix and lock dependencies, clean ignores, local PostgreSQL and CI | Fresh checkout builds server/core/contracts; plugin builds/packages/loads on supported platform; toolchain, host references and exact commands recorded |
| M1: feasibility spikes | Real XIVAuth character login on Render; live context-menu and consented chat spike | Valid login, wrong identity rejection, all selected channels tested, reconnect shown; no account-ID collection |
| M2: group foundation | Groups, invites, membership/owner lifecycle, capabilities, policies, currency, isolation | Two characters in two groups; caps and all cross-group denials tested with real PostgreSQL |
| M3: playable inventory alpha | Carried M2 removal/UI fixes, currency icons, potion definitions/copies/points, letters/quota/editing, extended and compact inventory, consented use | Plugin kick/access loss/history demonstrated; shared navigation/refresh/form resets and bounded icons verified; owner plus two members complete creation/read/use session; restart preserves data; duplicate requests verified |
| M4: trading beta | Context-menu offers, reservations, currency+items, revisions/confirmations, restrictions, history | Concurrent acceptance matrix passes; two real players trade successfully; interruption recovery demonstrated |
| M5: 1.0 candidate | Installation/updates, privacy/support, export/deletion, backup/restore, capacity, UI polish | Entire release checklist passes; no unresolved critical defect or integration gate; human release review |

M0 in-game testing was reported successful on 2026-10-06. The maintainer reported
**M1 passed on 2026-10-07**. Paid rename/world-transfer checks remain waived;
there is no separate simulated-rename test gate. Exact external test logs and
credentials were not supplied to this workspace and are not inferred.

M2 is implemented for testing; see the [upgrade and exit checklist](m2-install-test.md).
Durable login is included as a prerequisite: the memory-only M1 credentials cannot
authorize product APIs. M2 adds PostgreSQL-backed sessions and `character refresh`
provider scope. Hosted renewal and the new plugin workflows require M2 acceptance.
Hosted CI/container execution needs separately recorded evidence.

## M2 feedback carried into M3

The maintainer reported one functional bug on 2026-10-07: kicking a member fails
without visible feedback/history. M2 acceptance remains open for that path.
Proceed with one M3 build containing its fix; no separate M2.1 build is scheduled.
Removal and persistent diagnostics come first, before inventory work relies on
membership authority. See [feedback and implementation order](m2-feedback-m3-plan.md).

M3 also includes a shared dropdown above tabs, global/manual and 60-second visible
window refresh, active-only default roster with owner recovery view, success-only
join/creation form resets, and owner-uploaded PNG/JPEG/WebP currency icons up to
128x128. Icon upload is backend functionality and an explicit expansion of 1.0
scope. Cosmetic polish stays M5; usability improvements start now.

M3 is implemented as plugin 0.0.4.0/server 0.0.4 for acceptance; see the
[M3 upgrade/test guide](m3-install-test.md). The removal source fix and inventory
paths have automated PostgreSQL coverage. Live kick/rendering/chat evidence remains
required; no M2.1 release was created.

## Dependency priorities

- M1 precedes large UI investment: identity, home-world context extraction and
  channel posting are externally constrained integration risks.
- Existing project code can be replaced completely. M0 does not need a legacy
  sample-profile migration. Follow the [version matrix](../engineering/versions.md)
  and validate supported combinations again before M5 release.
- Accounting and isolation precede trading; reservations reuse proven holdings
  and currency transactions rather than creating a second accounting path.
- Owner administration is part of M2/M3, not a manual database-edit workaround.
- Letters and potions finish before adding more item types.
- Beta data has a stated persistence policy and paid database before testers rely
  on it. Test and production databases are separate.
- M2 uses per-verified-character group caps, including independent alts; confirm this during M2 acceptance. Resolve quota/week/content/lifecycle rules before
  M3; distribution/capacity/privacy policy before M5.

## Product feedback gates

At M3: can unfamiliar players find their group, remaining allowance and potion
message without help? Does five letters/week feel appropriate? Does compact view
stay useful during RP? Ask owner whether currency history is sufficient oversight.

At M4: can each player understand the final offer and why confirmation resets?
Does restriction behavior match owner expectations? Can the group recover from
disconnect without a maintainer editing data? Record observed defects and update scope.

At M5: a clean install by a new tester, an upgrade with old settings, and a server
restart during a trade must work. Human maintainer can navigate the tree, explain
transactions/authentication, restore data and publish the package.

No public 1.0 before [acceptance](acceptance.md) and external dependencies in the
[feasibility review](../feasibility.md) are resolved. Scope may be reduced through
an explicit product decision; safety/integrity cannot be replaced by UI-only checks.
