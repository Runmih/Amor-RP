# Agent handoff

Use branch `m2-foundation` of [Runmih/Amor-RP](https://github.com/Runmih/Amor-RP).
It includes the M0/M1 baseline, M2 source and full planned 1.0 specification.
Earlier milestone branches remain available. Credentials, databases and artifacts
are excluded from Git.

```sh
git clone --branch m2-foundation https://github.com/Runmih/Amor-RP.git
```

Read the [documentation index](../README.md), [M2 installation/test guide](m2-install-test.md),
[decision register](../decisions.md), [roadmap](roadmap.md),
[repository layout](../engineering/repository-layout.md),
[version matrix](../engineering/versions.md) and [contributing](../../CONTRIBUTING.md).

M0 testing was reported successful on 2026-10-06; M1 passed per the maintainer on
2026-10-07. M2 is built for acceptance, not reported accepted. Its live hosted
refresh grant and new in-game workflows still need the M2 exit checklist. Exact
external versions/logs and server credentials are not present in this workspace.

M2 implements persistent character bindings, encrypted provider refresh grants,
rotating server sessions, groups/invitations/members/ownership, individual action
grants, weekly policies, currency and ledger/history/recovery. Group limits are
per verified character (3 owned / 6 active joined, owned included). Alts receive
independent memberships and allowances. Inventory/types/categories, trading,
events and account export/deletion remain later milestones. OpenAPI marks each
route implemented or planned and a checker compares it with source.

Never authorize product operations with an M1 probe token. Product auth requests
`character refresh` and fetches a returning character by saved Lodestone ID,
checking its ownership key. Assets reference immutable internal CharacterId.
Names/worlds remain display/context data. No ContentId or full alt list is read.
Test fakes are DI-only; there is no runtime bypass or fake-provider setting.

Honor the agreed once-per-plugin-startup chat permission, direct action grants
instead of roles, group isolation and independently allowed alts. Paid rename/
world-transfer tests and a separate simulated-rename gate are waived (A16).
Only add tests that cover real accounting, authorization or recovery failures.

Follow the M2 guide for locked restores, PostgreSQL tests, migration/model checks,
package validation and artifact generation. Update product rules, API schemas,
UI behavior and acceptance documentation alongside implementation. M3 should
reuse the transaction/idempotency/access boundaries for potion and letter creation;
seed categories with that inventory migration. Do not implement trades prematurely.

## Next work: M3 with carried feedback

Read the [M2 feedback/M3 plan](m2-feedback-m3-plan.md) before implementing inventory.
The maintainer reports kicking fails and feedback disappears; M2 acceptance stays
open for that path. Source shows an empty/shared reason can cause 422, followed by
a read refresh that overwrites the error; exact live response is not recorded.
Fix dedicated removal validation and persistent safe diagnostics first, then prove
plugin removal/access loss/history. No M2.1 artifact is requested.

M3 also adds shared dropdown above tabs, global and 60-second visible-window read
refresh preserving drafts, active-only default roster/owner inactive toggle,
success-only join/create resets, and owner-uploaded static currency icons in
PNG/JPEG/WebP up to 128x128. Icons add bounded authenticated backend media storage
and a texture cache; they are no longer deferred beyond 1.0. Planned icon routes
are in OpenAPI. Do not interpret this plan as implemented/accepted M3 code.
