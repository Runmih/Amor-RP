# Decision register

**Agreed** means explicitly established in the conversation. **Recommended** is a
concrete baseline for implementation planning, subject to product review.
**Unverified** requires an integration experiment or external setup.

| ID | Status | Decision |
| --- | --- | --- |
| A01 | Agreed | FFXIV Dalamud interface for fictional RP items and currency. |
| A02 | Agreed | Closed groups isolate all items and currency; easy active-group switching. |
| A03 | Agreed | Up to three created groups and six joined groups; owned groups count toward joined. |
| A04 | Agreed | Initial item types are potions and letters. Other types wait until the foundation works. |
| A05 | Agreed | Group owner defines potions; authorized characters create copies. |
| A06 | Agreed | Individually authorized actions instead of named roles. |
| A07 | Agreed | Owner sets a group-wide weekly potion allowance; each authorized character has its own usage. Potion cost determines points per copy. |
| A08 | Agreed | Alt characters can have independent allowances. Alt management is the owner's responsibility. No account-level sharing or alt detection. |
| A09 | Agreed | Owner-configurable letter creation limits; five letters/week is the initial default. |
| A10 | Agreed | Weekly allowance reset. |
| A11 | Agreed | Potion use consumes one and posts its visible use message; initially 50 characters. Ask for chat consent once at plugin startup. Consent stays active for that startup across posts, message/channel changes and character/group/backend switches; no per-message permission prompt. Player selects Emote, Say, Party, or Linkshell and may change consent in settings. |
| A12 | Agreed | Context-menu trading; members can trade by default; owner can restrict a character. |
| A13 | Agreed | Evaluate XIVAuth for authentication and verified character identity. |
| A14 | Agreed | Existing project files may be completely replaced. The new product does not need to preserve the SDK 11 scaffold or its sample profile behavior. |
| A15 | Agreed | Character rename or home-world transfer must preserve inventory, currency, group memberships, permissions and quota usage. Ownership uses an internal character ID linked to provider-verified identifiers; names/worlds are mutable display/context fields. |
| A16 | Agreed | Skip live paid character rename/home-world-transfer tests because they require real-money services. No separate simulated-rename/transfer test gate is required. Verified ID/key mapping and authorization are covered by normal authentication tests; provider identifier derivation is documented. Tests must check meaningful behavior/security boundaries rather than hypothetical alternate implementations. |
| R01 | Recommended | One repo; C# server, PostgreSQL, Render hosting. Plugin remains a separate deployment artifact. |
| R02 | Recommended | Apply three-owned/six-joined limits per verified character. The user's original wording did not settle account versus character for group caps. Confirm before finalizing group admission. |
| R03 | Recommended | Monday 00:00 UTC weekly boundary; no rollover; changes to weekly limits start next period. Potion allowance initially zero until owner configures it. |
| R04 | Recommended | Three grantable capabilities: `currency.manage`, `items.potion.create`, `inventory.remove`. Owner authority is implicit and cannot be delegated by a capability holder. |
| R05 | Recommended | One whole-unit currency per group. No per-week currency issuance quota in 1.0; record every authorized adjustment. |
| R06 | Recommended | Owner can enable/retire potion definitions. Initially all enabled potions are available to characters with creation permission. Per-recipe access waits beyond 1.0. |
| R07 | Recommended | Immutable potion revisions; existing copies retain description/message. Creation uses latest enabled revision and current cost. Cost edits invalidate stale creation requests. |
| R08 | Recommended | Letters editable only by author while held, permanently locked on first completed trade. Title 80 characters, body 5,000 maximum by default. |
| R09 | Recommended | Trade restriction blocks sending and receiving. One open trade per character per group; owner restrictions/removal cancel affected open trades. |
| R10 | Recommended | Leaving/removal makes holdings dormant; rejoining restores them and existing weekly usage. Owner removal bans rejoining until explicitly restored. |
| R11 | Recommended | Ownership transfer requires recipient acceptance and checks group limits. Group deletion is soft deletion followed by a documented purge process. |
| R12 | Recommended | Invitation-only admission; no global player/plugin-user search, public group directory, or presence discovery. |
| R13 | Recommended | Closed beta through a custom plugin repository first. Official Dalamud submission is a separate review path. |
| R14 | Recommended | Provisional capacity target: 50 connected clients, five groups of ten testers, ten simultaneous trades, 10,000 holdings/group. Validate before public 1.0. |
| R15 | Recommended | Initial cleanup policy: login attempts 10 minutes, replayable events 24 hours, idempotent response replay 7 days, financial audit 365 days, deleted-group purge after 30 days. Confirm privacy notice and operator workflow before inviting testers. |
| R16 | Recommended / M0 build verified | Plugin baseline: stable Dalamud API 15, Dalamud.NET.Sdk 15.0.0, .NET 10. M0 compiles against checksum-pinned Dalamud 15.0.3.6 references; live game loading is pending. Recheck stable support at each release; API 16 is currently a development preview. |
| R17 | Recommended | Use the dated engineering version matrix: .NET 10 LTS, PostgreSQL 18 on Render, EF Core/Npgsql 10, SDK-provided game/UI assemblies; prove and pin exact dependencies at M0/M1. Initial live support is Windows x64 on the stable global-client Dalamud track. |
| U01 | Unverified | Register XIVAuth application; verify actual scopes/endpoints, character identifiers, provider renewal/revocation, and deployment callback. |
| U02 | Partially verified | Replacement scaffold builds and local M0 tests pass on R16/R17 baselines. Live plugin loading, hosted Windows CI, complete Docker build, context menu and chat sending remain gates. |
| U03 | Unverified | Render account, billing, region, domain, credentials, database restore, and measured capacity. No resources provisioned. |
| U04 | Unverified | Identify a human release/operator maintainer, testers, support route, and distribution route. |

The recommended choices provide a complete proposed behavior, rather than leaving
endpoint implementations to guess. Resolve R02, R03, R05, R08, R10, R11, R13,
R14, and R15 at their milestone gates. They do not block documentation or the
initial identity/chat feasibility spikes.
