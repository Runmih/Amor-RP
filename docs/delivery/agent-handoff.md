# Agent handoff

Use branch `m3-inventory` of [Runmih/Amor-RP](https://github.com/Runmih/Amor-RP).
It contains the M0/M1 baseline, M2 foundation, M3 inventory alpha and planned 1.0
specification. Earlier milestone branches remain available. Credentials, databases
and generated artifacts are excluded from Git.

```sh
git clone --branch m3-inventory https://github.com/Runmih/Amor-RP.git
```

Read the [documentation index](../README.md), [M3 installation/test guide](m3-install-test.md),
[decisions](../decisions.md), [roadmap](roadmap.md), [layout](../engineering/repository-layout.md),
[versions](../engineering/versions.md) and [contributing](../../CONTRIBUTING.md).

M0 passed per maintainer on 2026-10-06; M1 on 2026-10-07. M2 testing found a kick
failure; its dedicated-reason/error-persistence fix ships in M3 rather than M2.1.
M3 is built for live acceptance, not reported passed. Require the real plugin
kick/access-loss/history check and owner-plus-two-members inventory/use session.
Local verification passed 76 tests and published Linux native HTTP checks passed.
Docker build is blocked by this environment's Microsoft CDN policy; hosted/game/
CI/container evidence is separate.

M3 implements durable character sessions, groups/member/owner/invitation lifecycle,
individual grants, weekly policies and usage, currency/icons, catalog revisions,
potion/letter holdings, private letter reading/editing, consume/discard/removal,
history and retry recovery. Limits apply per verified character, including alts.
Compact and full windows share the selected group state; refresh preserves drafts.
Trading, event streams and account export/deletion remain M4/M5. OpenAPI status
is checked against source routes; planned endpoints are not runtime promises.

Never authorize product operations with an M1 probe token. Product authentication
requests `character refresh`, retrieves a returning character by saved Lodestone ID
and checks its ownership key. Assets reference immutable internal CharacterId;
names/worlds are display/context. Test providers are DI-only, with no release bypass.
Honor once-per-plugin-startup chat consent; replay/recovery never triggers automatic
chat. Paid rename/transfer tests and separate simulated-rename gates are waived.
Tests must check meaningful accounting, authorization or recovery behavior.

Use the M3 guide for locked restore, real PostgreSQL tests, migrations/model checks,
package validation and artifact generation. Currency media uses SkiaSharp 4.153.1
with upstream notices, decoded static PNG/JPEG/WebP bounded to 128x128; uploads are
authenticated and PostgreSQL-persisted. Publish server with an explicit runtime ID
for its target (`linux-x64` for current Render), avoiding unrelated native assets.

Preserve M2 data and vault keys. The additive PlayableInventory migration seeds old
categories and keeps currency/identity/membership stable. Shared group DB locks
serialize quotas, holdings, catalog and membership changes. Future types require
explicit behavior/contracts/renderers and schema changes rather than arbitrary JSON.

Next: complete M3 live acceptance and fix observed defects, then build M4 trading
using persistent reservations, reviewed offer revisions, two-party confirmation
and existing transaction/idempotency/access boundaries. Do not declare 1.0 ready
before M5 operations, privacy, capacity, recovery and human release checks.
