# Roadmap to 1.0

Milestones are ordered by dependency and evidence. Dates require team availability,
integration results and tester feedback; none are promised by this specification.
Do not mark a milestone complete because its code exists without its exit evidence.

| Milestone | Deliverable | Exit evidence |
| --- | --- | --- |
| M0: build and boundaries | Replace old scaffold with clean four-project layout, verify version matrix and lock dependencies, clean ignores, local PostgreSQL and CI | Fresh checkout builds server/core/contracts; plugin builds/packages/loads on supported platform; toolchain, host references and exact commands recorded |
| M1: feasibility spikes | Real XIVAuth character login on Render; live context-menu and consented chat spike | Valid login, wrong identity rejection, all selected channels tested, reconnect shown; no account-ID collection |
| M2: group foundation | Groups, invites, membership/owner lifecycle, capabilities, policies, currency, isolation | Two characters in two groups; caps and all cross-group denials tested with real PostgreSQL |
| M3: playable inventory alpha | Potion definitions/copies/points, letters/quota/editing, extended and compact inventory, consented use | Owner plus two members complete creation/read/use session; restart preserves data; duplicate requests verified |
| M4: trading beta | Context-menu offers, reservations, currency+items, revisions/confirmations, restrictions, history | Concurrent acceptance matrix passes; two real players trade successfully; interruption recovery demonstrated |
| M5: 1.0 candidate | Installation/updates, privacy/support, export/deletion, backup/restore, capacity, UI polish | Entire release checklist passes; no unresolved critical defect or integration gate; human release review |

M0 in-game testing was reported successful by the maintainer on 2026-10-06.
Hosted CI and a complete server container build still need their own recorded
evidence. M1's isolated identity/context/chat probes are implemented; follow the
[M1 setup and exit checklist](m1-install-test.md). Neither an XIVAuth application
nor Render service was available during implementation. M1 is not accepted until
live provider, deployment and channel/menu tests pass. Its five-minute probe
sessions do not implement the durable product authentication contract.

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
- Resolve recommended cap scope before M2; quota/week/content/lifecycle rules before
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
