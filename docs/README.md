# Documentation index

Specification date: 2026-10-06. Target: Amor RP 1.0.

This documentation specifies the intended product. M0 implements three public
server GETs and the diagnostic plugin; M1 adds six isolated feasibility operations.
M2 implements saved authentication and the group/currency foundation; inventory and trading remain planned.
Use the [M2 installation/test guide](delivery/m2-install-test.md) for actual commands
and verification evidence, and the [agent handoff](delivery/agent-handoff.md) when
continuing in another environment. Numeric operational limits and
unconfirmed product choices are recommendations, identified in the decision
register. Acceptance tests describe work to implement, not passing tests.

## Reading order

1. [Scope](product/scope.md): what ships and what is deferred.
2. [Rules](product/rules.md): identity, group boundaries, permissions, item behavior.
3. [Interface](product/interface.md): what players and owners see.
4. [Architecture](engineering/architecture.md), [layout](engineering/repository-layout.md),
   and [version compatibility](engineering/versions.md).
5. [API](engineering/api.md), [OpenAPI](engineering/openapi.yaml), and [data model](engineering/data-model.md).
6. [Security](engineering/security.md) and [authentication](engineering/authentication.md).
7. [Roadmap](delivery/roadmap.md), [acceptance](delivery/acceptance.md),
   [development](delivery/development.md), and [operations](delivery/operations.md).
8. [Decisions](decisions.md), [feasibility](feasibility.md), and [expansion](product/expansion.md).

## Document ownership

| Area | Source of truth |
| --- | --- |
| Agreed requirements versus recommendations | [Decision register](decisions.md) |
| Product behavior | [Rules](product/rules.md) |
| HTTP paths, wire schemas, authorization metadata | [OpenAPI](engineering/openapi.yaml) |
| HTTP conventions and event protocol | [API guide](engineering/api.md) |
| Persistence invariants | [Data model](engineering/data-model.md) |
| Toolchain targets, dependency compatibility and update gates | [Version matrix](engineering/versions.md) |
| External claims and integration references | [Sources](sources.md) |

When changing a feature, update its rules, contract, interface behavior, and
acceptance criteria in the same change. Keep decisions short and explain why a
boundary exists. Generated endpoint tables must match OpenAPI.

## Vocabulary

- **Character:** verified FFXIV character; the unit of inventory, permissions, and quotas.
- **Group:** isolated RP environment. Nothing transfers between groups.
- **Owner:** the character responsible for group administration; not a grantable role.
- **Capability:** an individually grantable action permission.
- **Type:** behavior implemented in code, initially `potion` or `letter`.
- **Category:** group-owned organization label; it never grants a capability.
- **Definition:** owner-authored potion design. **Revision:** immutable version of it.
- **Holding:** quantity or individual item owned by a character in one group.
- **Period:** server-defined weekly interval used to track creation usage.
- **Reservation:** items/currency held for an open trade, still owned by the offerer.
- **Operation ID:** durable identifier for a completed inventory/currency mutation.

Current feedback and next implementation order: [M2 feedback/M3 plan](delivery/m2-feedback-m3-plan.md). M2 has a reported removal issue; M3 carries its fix and the requested display/currency-icon changes.
