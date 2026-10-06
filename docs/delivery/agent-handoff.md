# Agent handoff

Use branch `m1-feasibility` of [Runmih/Amor-RP](https://github.com/Runmih/Amor-RP).
It includes M0 plus M1's implementation and the full planned 1.0 specification.
`m0-foundation` retains the earlier M0 build. Build artifacts, credentials and
local databases are excluded from Git.

```sh
git clone --branch m1-feasibility https://github.com/Runmih/Amor-RP.git
```

Read the [documentation index](../README.md), [decision register](../decisions.md),
[roadmap](roadmap.md), [M1 installation/test guide](m1-install-test.md),
[repository layout](../engineering/repository-layout.md),
[version matrix](../engineering/versions.md) and [contributing](../../CONTRIBUTING.md).

M0 in-game testing was reported successful by the maintainer on 2026-10-06.
M1 adds temporary XIVAuth/PKCE probes, player context-menu inspection and chat submission with
once-per-startup permission. Six implemented feasibility routes are separate from
three existing health/capabilities GETs. Durable authentication and all group,
coin, potion, letter and trade operations remain planned in OpenAPI.

M1's identity adapter requests only `character` scope, requires exact selected
name/home-world and a verified ownership binding, and never collects ContentId or
the complete alt list. Attempts and five-minute probe sessions are bounded and
memory-only. They cannot authorize future product operations. Do not turn these
spike credentials into production sessions without the durable auth/revocation
work and its acceptance tests.

The seven-project Release build and automated tests pass; the M1 guide records
counts, versions and commands. No runtime fake provider exists; test fixtures
are injected only by tests. Native server, PostgreSQL and package checks have
local evidence. Windows CI, complete server image build, real M1 game behavior
and live hosted XIVAuth/Render login need separately recorded evidence.
Neither a Render service nor XIVAuth application has been set up by the maintainer.
The M1 guide supplies the exact callback and configuration sequence.

Finish M1's live exit checklist before calling it accepted. Any follow-up should
preserve individual action permissions, independent character allowances including
alts, isolated groups and server-authoritative mutations. Update rules, contracts,
interface behavior and acceptance criteria alongside product implementation.
