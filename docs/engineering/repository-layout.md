# Repository layout and navigation

## Current versus target

M0 replaced the sample/SDK 11 source, goat asset, obsolete relay/backend scaffolds
and IDE artifacts with four application projects under `src/`. Contracts currently
has `Common/` DTOs; Core has `Groups/` capacity policy. Server has `Health/` and
`Capabilities/` endpoints, persistence/migrations and options. Plugin has
`Diagnostics/` and pure HTTP helpers, with complete lifecycle unsubscription.
Three test projects exist: Contracts, Plugin and Server.IntegrationTests.

M1 adds `Contracts/Feasibility/`, `Server/Features/Feasibility/` (provider options,
adapter, bounded temporary store and six probe routes), `Plugin/Services/Game/`
(context-menu capture and chat submission), and `Diagnostics/FeasibilityPanel`.
These spike sessions are deliberately separate from future durable authentication
and persistence. See [M1 setup](../delivery/m1-install-test.md).

M2 adds implemented feature folders:

```text
src/AmorRP.Contracts/
  Auth/ Groups/ Currency/ History/ Common/      # wire records, no EF/game dependencies
src/AmorRP.Server/
  Features/Authentication/                      # persistent login/session/provider adapter
  Features/Groups/                             # access + GroupService, membership/invite/owner partials
  Features/Currency/                           # balance queries and transactional adjustment
  Features/History/                            # personal/owner logs and own receipt recovery
  Infrastructure/Persistence/Entities/         # one file per stored entity
  Infrastructure/Persistence/CommandStore.cs   # DB locks, idempotency, version preconditions
  Infrastructure/Persistence/PageCursor.cs     # actor/list-bound opaque pagination
  Infrastructure/Security/                    # safe faults and versioned secret vault
  Options/SessionPolicyOptions.cs             # validated lifecycle limits
src/AmorRP.Plugin/
  Features/Groups/GroupsPanel.cs                # async login/state orchestration
  Features/Groups/GroupScreens.cs               # group/member/owner/history screens
  Services/Api/FoundationClient.cs              # product HTTP, same-key retries, safe errors
  Services/Identity/SavedSession.cs             # DPAPI credentials and unresolved command save
```

The provider's durable adapter remains next to its authentication feature in M2;
M1's adapter stays in Feasibility. M3 adds Inventory/type folders when implemented.
GroupService's partial files keep the shared transaction/access boundary while
separating memberships, invitations and ownership for navigation. Persistence
entities never leave Server; only Contracts cross into Plugin.

Dependency notices are in `licenses/` and travel in the plugin ZIP.

The solution, dependency/tool pins, package locks, deployment recipes, source/package
scripts and two CI workflows exist. Required JSON is no longer blanket-ignored.
Git history, current documentation and license are preserved. No extra backend
implementation or empty future feature folders remain. See [M0 setup](../delivery/m0-install-test.md).

## Target layout

```text
Amor-RP/
├── README.md
├── LICENSE.md
├── CONTRIBUTING.md                    # implemented; verified setup guide
├── SECURITY.md                        # reporting route at release preparation
├── AmorRP.sln
├── global.json                        # verified/pinned toolchain, tracked explicitly
├── Directory.Build.props              # common warnings/style; platform-specific overrides
├── Directory.Packages.props           # exact application/test package pins; SDK-aware scope
├── .config/dotnet-tools.json           # local migration/tool versions
├── .editorconfig
├── .gitignore                         # targeted build/cache/secrets rules
├── .github/workflows/
│   ├── server-ci.yml
│   ├── plugin-ci.yml
│   └── release.yml
├── src/
│   ├── AmorRP.Contracts/
│   │   ├── Auth/ Groups/ Inventory/ Currency/ Trades/ Events/
│   │   └── Common/                    # IDs, pagination, ProblemDetails
│   ├── AmorRP.Core/
│   │   ├── Groups/                    # policies and membership use cases
│   │   ├── Inventory/
│   │   │   ├── Types/Potions/
│   │   │   ├── Types/Letters/
│   │   │   └── Quotas/
│   │   ├── Currency/
│   │   ├── Trades/
│   │   └── Common/                    # clock and transaction interfaces
│   ├── AmorRP.Server/
│   │   ├── Program.cs                 # composition only
│   │   ├── Features/
│   │   │   ├── Auth/ Groups/ Categories/ Definitions/
│   │   │   ├── Inventory/ Currency/ Trades/ History/
│   │   │   └── Account/ Events/ Health/ Capabilities/
│   │   ├── Infrastructure/
│   │   │   ├── Persistence/           # DbContext, mappings, migrations, transactions
│   │   │   ├── Identity/XIVAuth/      # provider adapter; no provider DTO leakage
│   │   │   ├── Sessions/ Idempotency/ Notifications/
│   │   │   └── Observability/
│   │   ├── Options/                   # typed options and startup validation
│   │   └── appsettings.json           # nonsecret defaults only
│   └── AmorRP.Plugin/
│       ├── Plugin.cs                  # services/lifecycle registration only
│       ├── Features/
│       │   ├── Auth/ Groups/ Inventory/ Consumables/ Letters/
│       │   ├── Creation/ Trading/ Administration/ History/
│       │   └── Settings/ Diagnostics/
│       ├── Services/
│       │   ├── Api/                   # HTTP client, outcome recovery, event subscription
│       │   ├── Game/                  # character/context-menu/chat adapters
│       │   └── LocalStorage/          # versioned settings, drafts, secure credentials
│       ├── UI/                        # shared widgets/type renderers
│       └── Assets/                    # curated icons; replace template goat
├── tests/
│   ├── AmorRP.Core.Tests/
│   ├── AmorRP.Server.IntegrationTests/ # real disposable PostgreSQL
│   ├── AmorRP.Contracts.Tests/
│   └── AmorRP.Plugin.Tests/           # pure presenters/adapters, not ImGui clones
├── deploy/
│   ├── Dockerfile                     # repo-root context for Core + Contracts + Server
│   ├── render.yaml                    # secrets by reference only
│   ├── compose.yaml                   # local server + PostgreSQL
│   └── .env.example                   # names and placeholders only
├── scripts/
│   ├── check-docs.py                  # link/contract checks, implemented now
│   ├── requirements-docs.txt           # pinned documentation-check dependencies
│   └── export-openapi.*               # contract export/compare in implementation
└── docs/
    ├── README.md
    ├── decisions.md
    ├── feasibility.md
    ├── sources.md
    ├── product/                       # scope, rules, interface, expansion
    ├── engineering/                   # layout, architecture, API, auth, security, data
    └── delivery/                      # development, roadmap, acceptance, operations
```

This tree describes the complete target, including future features. M0's projects,
deployment/CI files and contributor guide exist; later feature folders,
Core.Tests, SECURITY.md and release publication workflow are still planned.
Use these four application projects initially; split another assembly only when
it enforces a useful dependency boundary. Avoid empty future-feature folders.
Track per-project package locks. Keep the plugin assembly/internal name explicitly
`AmorRP` despite its `AmorRP.Plugin` project filename; see [versions](versions.md)
for framework, SDK, host-library and manifest requirements.
Within a feature use concrete names such as `CreatePotionHandler.cs`,
`CreatePotionEndpoint.cs`, `PotionDefinition.cs`, `PotionDetailsView.cs`; avoid
miscellaneous `Helpers`, `Managers`, or oversized all-feature services.

## Finding and changing a feature

| Need | Location in target tree |
| --- | --- |
| Change a player rule | `Core/<feature>/` plus product rules and acceptance scenario |
| Change HTTP shape | `Contracts/<feature>/`, endpoint, OpenAPI |
| Fix database mapping or migration | `Server/Infrastructure/Persistence/` |
| Fix XIVAuth | `Server/Infrastructure/Identity/XIVAuth/`, auth feature |
| Fix an inventory window | `Plugin/Features/Inventory/` or type renderer |
| Fix game chat or context menu | `Plugin/Services/Game/` |
| Add a type | Core type handler, contract schema, renderer, migration, type-specific tests |
| Change hosting | `deploy/`, operations runbook; no domain changes |

Do not share server domain entities with the plugin. Keep code adjacent to its
feature rather than creating a global folder for every syntactic class category.

## Replacement hygiene

M0 replaced `*.json` ignore with explicit ignores for `bin/`, `obj/`, `.vs/`,
`node_modules/`, generated output, local secret files, and developer overrides.
Track SDK pins, package manifests/locks, plugin manifests, and nonsecret config.
Previously ignored configuration was reviewed/replaced with nonsecret defaults;
future local overrides and credentials must remain ignored.
Separate plugin manifest generation from runtime configuration. Recreate the
solution and metadata with real product values. New lifecycle code must unregister
commands/UI/context subscriptions on disposal; no sample behavior needs preserving.
