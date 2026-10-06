# 1.0 product scope

## Purpose and successful session

Amor RP gives a character a fictional inventory and currency inside closed RP
groups. A group's owner controls supply and permissions. Two players can enter
the same group, create or obtain a potion and a letter, trade items/currency,
read the letter, and use the potion with a visible, consented in-game message.
Those holdings survive plugin reloads and server deployments.

No item changes FFXIV stats, combat, real inventory, gil, or game permissions.
RP value is fictional; 1.0 has no real-money exchange or gambling functionality.

## Required features

| ID | Feature | Required result |
| --- | --- | --- |
| F01 | Login | XIVAuth verifies selected character; server issues revocable character-bound session. |
| F02 | Groups | Create, invite, join, leave, remove/restore member, transfer ownership, delete group; enforce limits. |
| F03 | Switching | Persistent active-group selector; every screen shows group and character. |
| F04 | Permissions | Owner grants/revokes individual actions and toggles member trading restriction. |
| F05 | Currency | Display balance; authorized issue/remove; trade currency; inspect relevant history. |
| F06 | Categories | Seed Consumables and Correspondence; owner can add, rename, or retire categories. |
| F07 | Potion definitions | Owner sets name, category, description, message, creation cost, availability. |
| F08 | Potion production | Authorized character spends its own weekly points to create quantities. |
| F09 | Letters | Every active member can create within weekly quota, read owned letters, edit eligible letters. |
| F10 | Inventory | Extended searchable/filterable list and type-specific details; compact pinned-potion view. |
| F11 | Potion use | A deliberate Use action consumes one; chat consent asked once at startup; selected channel and exact preview; no per-message permission prompt. |
| F12 | Trading | Character context menu opens group-bound trade; quantities/currency, mutual confirmation, cancellation. |
| F13 | History | Personal operations and owner administrative audit; no letter bodies in logs. |
| F14 | Recovery | Reconnect, recover unknown request outcomes, preserve data and reservations across restarts. |
| F15 | Settings | Character/group-specific pins and channel, startup-scoped chat permission, endpoint selection, display preferences. |
| F16 | Service lifecycle | Version compatibility, maintenance messaging, export/deletion request, backups and support. |

Group CRUD and recovery are necessary supporting features, not additional RP
systems. Owner screens ship in the plugin; a separate administration website is
not required. Browser interaction is limited to authentication and documentation.

## Deferred beyond 1.0

Additional types (weapons, equipment, books, containers), crafting ingredients,
recipes with prerequisites, shops/markets, auctions, group banks, multiple
currencies, currency exchange, character stats, combat automation, shared account
allowances, named roles/presets, public directories, uploaded images, attachments,
full localization, mobile/web inventory editing, multiple server instances, and
offline writes. See [expansion](expansion.md).

## Release meaning

"Working 1.0" means the complete session above, with tested isolation, quota and
trade integrity, clean installation, a documented operating budget, recovery,
and a human able to maintain it. It does not promise unlimited capacity or
official plugin-repository acceptance. A closed alpha may precede that standard.
