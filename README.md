# Amor RP

Amor RP is a planned FFXIV Dalamud interface for fictional RP currency, potions,
letters, and trading inside isolated, owner-managed groups.

**Status:** M0 foundation built and locally tested. The plugin shows local character
information and checks server connectivity; the server has PostgreSQL migrations,
liveness/readiness and capabilities. Authentication, groups, inventory and trading
are later milestones. Live Windows/game loading and hosted CI still need verification.

Start testing with the [M0 installation guide](docs/delivery/m0-install-test.md).
For another agent or environment, use the [M0 handoff](docs/delivery/agent-handoff.md).

Start with the [documentation index](docs/README.md), then read the
[1.0 scope](docs/product/scope.md), [roadmap](docs/delivery/roadmap.md), and
[feasibility review](docs/feasibility.md).

## Where to look

| Task | Document |
| --- | --- |
| Understand the player experience | [Interface and flows](docs/product/interface.md) |
| Understand permissions, quotas, and trading | [Product rules](docs/product/rules.md) |
| Find every proposed endpoint | [API contract](docs/engineering/api.md) and [OpenAPI](docs/engineering/openapi.yaml) |
| Understand trust and authentication | [Security](docs/engineering/security.md) and [XIVAuth](docs/engineering/authentication.md) |
| Find the intended source layout | [Repository layout](docs/engineering/repository-layout.md) |
| Find supported version targets and upgrade policy | [Version matrix](docs/engineering/versions.md) |
| Understand persistence and transactions | [Data model](docs/engineering/data-model.md) |
| Build, test, and release | [Development](docs/delivery/development.md), [acceptance](docs/delivery/acceptance.md), [operations](docs/delivery/operations.md) |
| See unresolved choices | [Decision register](docs/decisions.md) |
| Plan beyond 1.0 | [Expansion](docs/product/expansion.md) |

The SDK 11/sample scaffold was replaced with four application projects under `src/`,
three focused test projects, deployment recipes and CI. Future feature directories
are created when their implementation begins. See [contributing](CONTRIBUTING.md).

Repository: [Runmih/Amor-RP](https://github.com/Runmih/Amor-RP).
Existing license: [AGPL](LICENSE.md). Preserve it and provide the deployed server's
corresponding source link when implementing distribution.
