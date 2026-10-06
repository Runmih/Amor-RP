# M0 agent handoff

This checkout contains the M0 foundation and the full planned 1.0 specification.
Use the `m0-foundation` branch of [Runmih/Amor-RP](https://github.com/Runmih/Amor-RP).
Separate agent environments should clone that branch; local build artifacts,
credentials and databases are deliberately excluded from Git.

```sh
git clone --branch m0-foundation https://github.com/Runmih/Amor-RP.git
```

## Start here

Read the [documentation index](../README.md), [decision register](../decisions.md),
[roadmap](roadmap.md), [M0 installation and test guide](m0-install-test.md),
[repository layout](../engineering/repository-layout.md) and
[version matrix](../engineering/versions.md). Follow [contributing](../../CONTRIBUTING.md).
User requirements and recommended defaults are distinguished in the decision register.

M0 implements the diagnostic Dalamud plugin and exactly three public server GETs:
`/health/live`, `/health/ready`, and `/api/v1/capabilities`. The remaining OpenAPI
operations are planned. Authentication, groups, coins, potions, letters and trading
are not implemented yet.

## Verification and next work

The complete seven-project Release build and all 22 automated tests passed,
including tests against PostgreSQL 18.6. Locked restores, migrations, schema
readiness, database-outage recovery, plugin packaging and documentation checks
also passed. The M0 guide records exact commands and prerequisites.

Pending gates: real Windows/FFXIV load and unload, hosted CI execution, and a
complete server Docker image build. Microsoft's image CDN was blocked by this
environment's network policy; the native published server was tested. Do not
describe those pending gates as passed.

Verify M0's remaining gates before progressing to M1's XIVAuth, character
identity, context-menu and consented chat feasibility experiments. No Render
deployment or XIVAuth application has been provisioned. No deployment or
authentication credentials are committed. Recreate ZIPs from source using the
installation guide, or obtain the plugin artifact from a successful Windows CI run.

Preserve the agreed boundaries: character-specific allowances including alts,
individual action permissions, isolated groups and server-authoritative mutations.
Update the relevant rules, API contracts and acceptance criteria alongside features.
