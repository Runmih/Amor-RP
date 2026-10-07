# Versions and compatibility baseline

Reviewed 2026-10-06. M0 now builds against this baseline with pinned dependencies.
The old scaffold has been replaced. M0 game testing was reported successful by
the maintainer. M1 passed per maintainer report on 2026-10-07. M2 live renewal
and new plugin workflows await acceptance; hosted CI/container evidence is
separate. See the [M2 guide](../delivery/m2-install-test.md).
Other product features remain planned. No SDK 11 compatibility or legacy profile
migration is required for the new product.

## Dependency matrix

| Component | Recommended baseline / observation | Where the implementation records it | Proof gate |
| --- | --- | --- | --- |
| FFXIV game client | Current game build supported by stable Dalamud; release feed currently reports `2026.09.15.0000.0000` | Release test record: actual game build and region | M1 live tests; repeat after game updates |
| Dalamud host | Stable API 15; observed host `15.0.3.6` | Release test record and CI reference artifact version/hash | M0 clean build/load, M1 game adapters |
| Plugin SDK | `Dalamud.NET.Sdk/15.0.0` | Plugin `.csproj` | M0 restore/build/package |
| Plugin target | SDK-selected `net10.0-windows`, x64, C# 14 | Plugin project plus evaluated SDK properties | M0 Windows x64 baseline; M1 live load |
| Plugin game/UI libraries | Dalamud-provided ImGui bindings, FFXIVClientStructs, Lumina and associated assemblies | SDK references; release records resolved assembly versions | M0 package inspection; M1 UI/context/chat tests |
| DalamudPackager | API 15-compatible version supplied by SDK; v15 documentation identifies `15.0.0` | SDK dependency resolution and NuGet lock | M0 manifest and ZIP validation |
| .NET build SDK | `10.0.401` checked baseline; recheck supported stable patch at M0 | Tracked root `global.json`, Docker builder and CI setup | M0 same SDK in clean local/CI builds |
| Server / Core / Contracts | `net10.0`, C# 14; ASP.NET Core 10 for server | Project target frameworks and shared build properties | M0 Linux server and Windows plugin can use Contracts |
| Server runtime | .NET / ASP.NET Core `10.0.12` checked baseline; current serviced .NET 10 patch at deployment | Docker runtime image tag/digest and release record | M0 container; each deploy smoke test |
| Entity Framework Core | Microsoft EF Core packages and `dotnet-ef` all `10.0.12` checked baseline | Server package pins, lock, local tool manifest | M0 migrations; M2 transaction tests |
| PostgreSQL EF provider | `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3` | Server package pins and lock | M0 restore; M2 PostgreSQL tests |
| PostgreSQL driver | `Npgsql 10.0.3` checked candidate | Server package pins and lock | M0 connection; M2 concurrency and TLS checks |
| PostgreSQL server | Major 18, available on Render; upstream current minor `18.6` | Explicit Render major version, local/test image pin; deployed server version in operations record | M0 local DB; M1 hosted connection; M5 restore |
| PostgreSQL backup tools | PostgreSQL 18 `pg_dump` / `pg_restore` family | Backup image/tool pin and restore record | M5 backup/restore drill |
| XIVAuth | Hosted `xivauth.net`; upstream source contains API v1 and OAuth 2 flows | Adapter options, reviewed source reference, fixtures and integration record | M1 confirms hosted routes/scopes/claims/renewal |
| OAuth / attestation transport | M1 uses .NET HttpClient and ASP.NET WebUtilities with S256 PKCE, fixed TLS endpoints and verified-character lookup; no JWT/attestation verification in the spike | Feasibility adapter; upstream XIVAuth `4bc2440684989cf8e56bc1169afcf5bd3a200172` and test fixtures | Hosted flow and durable auth library selection remain gates |
| HTTP specification | OpenAPI `3.0.3`; product API `/api/v1` | `openapi.yaml` and server contract comparison | M0 tooling; endpoint contract tests as implemented |
| Live events | Product protocol v1, HTTPS/WSS using runtime networking | Contracts, capabilities and event fixtures | M4 reconnect and compatibility tests |
| Domain / adapter tests | xunit.v3 3.2.2, runner 3.1.5, Microsoft.NET.Test.Sdk 18.10.1; disposable PostgreSQL 18 | Test projects, package pins/locks and CI | M0 22 cases passed; M2/M4 asset invariant tests remain |
| Containers | Docker with Compose v2; Linux server images compatible with .NET 10 and Render | Dockerfile/compose; exact base image tags and digests | M0 build/start; M1 Render deployment |
| Documentation checks | Python 3.12 baseline; dependencies require Python >=3.10; PyYAML `6.0.3`, openapi-spec-validator `0.9.0` | `scripts/requirements-docs.txt`; CI Python pin | Checker runnable now; reproducible CI at M0 |
| CI runners / actions | ubuntu-24.04 server tests, windows-2025 plugin build; action commit SHAs pinned | `.github/workflows/` and release logs | Hosted executions pending |

The SDK number, host patch, Microsoft build SDK, runtime patch and our product
version are different numbers. Matching every dependency's patch number would
be incorrect. Npgsql's EF provider 10.0.3 declares EF Core >=10.0.4 and <11.0.0,
and Npgsql >=10.0.3; EF Core 10.0.12 fits that declared range. Restore and
transaction tests must still validate the selected combination.

The .NET 10 LTS support end is 2028-11-14. PostgreSQL 18 support ends
2030-11-14. These dates provide maintenance runway; security patching is still
required. Render manages PostgreSQL minor updates, so record the actual hosted
minor and align test coverage rather than claiming Render runs upstream 18.6.
PostgreSQL 19 and .NET 11 previews are not initial production targets.

Primary references and dates are recorded in [sources](../sources.md). The
current documentation check ran under Python 3.12.14; the .NET solution build and
native server tests ran here. A live game load has not been performed.

## Game integration compatibility

Use the SDK's Dalamud assembly references, including
`Dalamud.Bindings.ImGui`; do not independently upgrade or bundle replacements for
host-provided ImGui, Lumina or FFXIVClientStructs. The SDK package does not freeze
all DLLs supplied by an installed Dalamud host. CI must select an identified API
15 reference artifact and record its version/hash; release tests run against the
current stable host. Keep host-provided DLLs out of the distributable ZIP.

Prefer public services for character/context-menu access. API 15 removed
`IClientState.LocalPlayer` and `LocalContentId` in favor of `IPlayerState`, and
changed chat event arguments. Build the adapters against the API 15 services
instead of copying SDK 11 examples. Game object access belongs on the framework
thread; network completions must marshal needed game work to that thread. If
posting requires native client structures, isolate that dependency in the chat
adapter and repeat its live tests after patches.

Choose `IDalamudPlugin` as the initial lifecycle interface; keep network work
asynchronous and cancellable. API 15's newer asynchronous lifecycle interface
is an optional later choice after its load/dispose behavior is tested.

Use explicit `<AssemblyName>AmorRP</AssemblyName>` even when the project file is
`src/AmorRP.Plugin/AmorRP.Plugin.csproj`. Keep the manifest/internal name `AmorRP`,
DLL and config identity consistent. Folder names do not determine product identity.
Choose this once before distribution. Under API 15 the ZIP manifest must itself
be accurate; it is not corrected by replacing it with repository metadata at install.

Initial release support is Windows x64 with the stable global-client Dalamud
track. Other launcher/OS combinations and regional game clients require their
own live compatibility evidence before being listed as supported. Record actual
XIVLauncher version, OS build, Dalamud track, game build and plugin version in
manual test reports. A separate application runtime is controlled by XIVLauncher;
the plugin must not try to replace the host's bundled runtime.

## Product and persisted version boundaries

Keep these independent of package versions:

- Plugin and server build versions: record independently, with source commit and
  release notes; versioned alpha/beta artifacts precede product 1.0.
- HTTP and event protocol: negotiate through capabilities before mutations.
  Support current and previous compatible plugin release while advertised by
  the server minimum-version policy. A breaking wire change needs a new protocol,
  compatible deployment transition and explicit update guidance.
- Database migrations: version-controlled identifiers and tested schema range
  per server build. A newer schema does not justify blindly rolling back binaries.
- Item snapshots: `typeDataVersion` per type, initially 1; retain readers for
  existing revisions when adding newer representations.
- Local settings: explicit schema version, initially 1 for the replacement
  product. Test migration between new-product releases; legacy scaffold settings
  are not imported as authoritative identity, permissions or assets.
- Provider contract: XIVAuth API v1 in upstream source is evidence for a candidate
  adapter, not proof of hosted behavior or a guarantee of OpenID Connect support.
  Record verified URLs, scopes, attestation format/algorithms and renewal semantics
  at M1; isolate them from our API version and Core types.

Unknown types/versions show an update-required state and fail actions safely.
They must not be interpreted as a potion or letter based on a numeric fallback.

## Reproducible implementation and update policy

M0 has created the following files; resolved pins are their source of truth:

- `global.json`: tested SDK baseline, explicit roll-forward policy, no previews.
  Recommend `latestPatch` within the selected feature band and record the resolved
  SDK in CI. Builder images use explicit patch tags and digests.
- `Directory.Packages.props`: central exact pins for compatible application/test
  projects. Verify interaction with SDK-generated plugin package references;
  if central management conflicts, opt the plugin out and pin its extra packages
  in its project. Do not override the SDK's own packager version to force alignment.
- `packages.lock.json` for each package graph and locked-mode CI restore.
- `.config/dotnet-tools.json`: local migration/tool versions; no reliance on
  whatever global `dotnet-ef` happens to be installed.
- `deploy/Dockerfile`, `deploy/compose.yaml`, `deploy/render.yaml`: matching runtime,
  database major, concrete images/digests and architecture. Avoid `latest` images.
- CI files: versioned Python/test tools and action SHAs. Documentation transitive
  dependencies are pinned in `scripts/requirements-docs.lock.txt`; hosted execution
  and release provenance remain to be demonstrated.

Review dependency/security updates monthly and before every release. Apply urgent
security fixes promptly with relevant regression checks. Minor/patch updates are
reviewed changes; automated proposals do not auto-deploy. Major updates need a
compatibility review and migration/test plan. Never freeze a security patch forever
because the initial planning document names it.

Game/Dalamud updates require checking load, character/home-world extraction,
context menu, selected chat channels and inventory UI. Backend updates require
authentication, transactions, retry/recovery and old supported client checks.
Database major updates require migration and restore drills. Refresh this dated
matrix when a baseline changes; exact deployed pins live in manifests and release
records rather than scattered constants throughout feature code.

## Milestone evidence

M0 records the resolved toolchain and dependency graph, creates the clean source
tree, proves clean CI build/tests and package integrity, and confirms the plugin
loads. M1 proves real provider and game integration using the recorded versions.
M4 exercises supported client/server combinations during deployment/reconnect.
M5 checks installation, new-product settings upgrade, schema compatibility and
backup restoration on the exact release artifacts. Test/image pins are now selected;
the M1 source-reviewed adapter is isolated from durable sessions. M0 game testing
and M1 were reported successful; M2 hosted renewal/game workflows and CI/container
evidence are recorded separately.

## M2 additions

Plugin `0.0.3.0`, server `0.0.3`, API v1, minimum product client `0.0.3`.
Host/SDK/.NET/PostgreSQL pins remain those above; M1 acceptance does not invent
new version evidence. `System.Security.Cryptography.ProtectedData` is pinned to
`10.0.12` for Windows CurrentUser DPAPI and included in the plugin package.
Server envelope encryption uses the .NET 10 AES-GCM implementation; OAuth/PKCE
uses .NET HTTP and WebUtilities against the same reviewed provider source.
Durable flow adds minimal `refresh` scope; hosted behavior is an M2 check.
